using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.AspNetCore.SignalR;
using ReeferSystem.Hubs;
using ISession = Apache.NMS.ISession;

public class ReeferMqService : BackgroundService
{
    private readonly IHubContext<YardHub> _hubContext;
    private readonly IConnectionFactory _factory;
    private readonly string _queueName = "ReeferDataQueue"; 

    public ReeferMqService(IHubContext<YardHub> hubContext)
    {
        _hubContext = hubContext;
        _factory = new ConnectionFactory("tcp://localhost:61616");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IConnection connection = await _factory.CreateConnectionAsync();
        await connection.StartAsync();
        using ISession session = await connection.CreateSessionAsync();
        using IDestination destination = await session.GetQueueAsync(_queueName);
        using IMessageConsumer consumer = await session.CreateConsumerAsync(destination);

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await consumer.ReceiveAsync();
            if (message is ITextMessage textMessage)
            {
                var data = textMessage.Text.Split('|');
                string id = data[0];
                string temp = data[1];
                string status = data[2];

                await _hubContext.Clients.All.SendAsync("UpdateContainer", id, temp, status);
            }
        }
    }
}