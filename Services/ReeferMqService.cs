using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.AspNetCore.SignalR;
using ReeferSystem.Hubs;
using ISession = Apache.NMS.ISession;

public class ReeferMqService : BackgroundService
{
    private readonly IHubContext<YardHub> _hubContext;
    private readonly ILogger<ReeferMqService> _logger;
    private readonly IConnectionFactory _factory;
    private readonly string _queueName = "ReeferDataQueue"; 

    public ReeferMqService(IHubContext<YardHub> hubContext, ILogger<ReeferMqService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _factory = new ConnectionFactory("tcp://localhost:61616");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("ReeferMqService: Attempting connection to ActiveMQ queue '{Queue}' at tcp://localhost:61616...", _queueName);
                using IConnection connection = await _factory.CreateConnectionAsync();
                await connection.StartAsync();
                using ISession session = await connection.CreateSessionAsync();
                IDestination destination;
                try
                {
                    destination = await session.GetQueueAsync("YardDataQueue");
                }
                catch
                {
                    destination = await session.GetQueueAsync(_queueName);
                }
                using IMessageConsumer consumer = await session.CreateConsumerAsync(destination);

                _logger.LogInformation("YardMqService: Connected successfully to ActiveMQ queue. Consuming live messages...");

                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = await consumer.ReceiveAsync();
                    if (message is ITextMessage textMessage)
                    {
                        var data = textMessage.Text.Split('|');
                        if (data.Length >= 6)
                        {
                            string id = data[0];
                            string type = data[1];
                            string status = data[2];
                            string line = data[3];
                            string weight = data[4];
                            string category = data[5];

                            await _hubContext.Clients.All.SendAsync("UpdateContainer", id, type, status, line, weight, category, cancellationToken: stoppingToken);
                        }
                        else if (data.Length >= 3)
                        {
                            string id = data[0];
                            string type = data[1];
                            string status = data[2];

                            await _hubContext.Clients.All.SendAsync("UpdateContainer", id, type, status, "MAERSK", "22,400 KG", "General Cargo", cancellationToken: stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("ReeferMqService: ActiveMQ connection attempt failed ({Error}). Will retry in 5 seconds...", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}