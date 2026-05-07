using Apache.NMS;
using Apache.NMS.ActiveMQ;
using ISession = Apache.NMS.ISession;

public class MqTestProducer : BackgroundService
{
    private readonly IConnectionFactory _factory;
    public MqTestProducer() { _factory = new ConnectionFactory("tcp://localhost:61616"); }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IConnection conn = await _factory.CreateConnectionAsync();
        await conn.StartAsync();

        using ISession session = await conn.CreateSessionAsync();

        IDestination dest = await session.GetQueueAsync("ReeferDataQueue");

        using IMessageProducer producer = await session.CreateProducerAsync(dest);

        string[] statuses = { "CONN", "ALARM", "ALERT" };
        Random rnd = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            string id = $"CONT-{rnd.Next(1, 21)}{rnd.Next(1, 6)}{rnd.Next(1, 7)}";
            string temp = (rnd.NextDouble() * (-15 - -25) + -25).ToString("0.0");
            string status = statuses[rnd.Next(statuses.Length)];

            var msg = session.CreateTextMessage($"{id}|{temp}|{status}");
            await producer.SendAsync(msg);

            await Task.Delay(2000, stoppingToken); 
        }

    }
}