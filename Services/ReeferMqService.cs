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
                using IDestination destination = await session.GetQueueAsync(_queueName);
                using IMessageConsumer consumer = await session.CreateConsumerAsync(destination);

                _logger.LogInformation("ReeferMqService: Connected successfully to ActiveMQ queue '{Queue}'. Consuming live messages...", _queueName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = await consumer.ReceiveAsync();
                    if (message is ITextMessage textMessage)
                    {
                        var data = textMessage.Text.Split('|');
                        if (data.Length >= 3)
                        {
                            string id = data[0];
                            string temp = data[1];
                            string status = data[2];

                            await _hubContext.Clients.All.SendAsync("UpdateContainer", id, temp, status, cancellationToken: stoppingToken);
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