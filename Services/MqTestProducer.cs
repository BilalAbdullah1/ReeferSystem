using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.AspNetCore.SignalR;
using ReeferSystem.Hubs;
using ISession = Apache.NMS.ISession;

public class MqTestProducer : BackgroundService
{
    private readonly IConnectionFactory _factory;
    private readonly IHubContext<YardHub> _hubContext;
    private readonly ILogger<MqTestProducer> _logger;
    private readonly string[] _columns = { "A", "B", "C", "D", "E", "F" };
    private readonly string[] _statuses = { "CONN", "CONN", "CONN", "ALARM", "ALERT" };

    public MqTestProducer(IHubContext<YardHub> hubContext, ILogger<MqTestProducer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _factory = new ConnectionFactory("tcp://localhost:61616");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rnd = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("MqTestProducer: Connecting to ActiveMQ at tcp://localhost:61616...");
                using IConnection conn = await _factory.CreateConnectionAsync();
                await conn.StartAsync();
                using ISession session = await conn.CreateSessionAsync();
                IDestination dest = await session.GetQueueAsync("ReeferDataQueue");
                using IMessageProducer producer = await session.CreateProducerAsync(dest);

                _logger.LogInformation("MqTestProducer: Successfully connected to ActiveMQ broker. Publishing telemetry for Stack AB...");

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Generate container coordinate matching Stack AB:
                    // 14 Rows (01 to 14), 6 Tiers (1 to 6), 6 Columns (A to F)
                    int row = rnd.Next(1, 15);
                    int tier = rnd.Next(1, 7);
                    string col = _columns[rnd.Next(_columns.Length)];
                    string id = $"CONT-AB{row:D2}-{tier}{col}";

                    // Temperature between -25.0°C and -15.0°C
                    string temp = (rnd.NextDouble() * (-15 - -25) + -25).ToString("0.0");
                    string status = _statuses[rnd.Next(_statuses.Length)];

                    var msg = session.CreateTextMessage($"{id}|{temp}|{status}");
                    await producer.SendAsync(msg);

                    await Task.Delay(1500, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("ActiveMQ is not reachable at tcp://localhost:61616 ({Error}). Generating live telemetry directly via SignalR while broker is offline.", ex.Message);

                // Fallback loop while broker is starting or offline
                int retryAttempts = 0;
                while (!stoppingToken.IsCancellationRequested && retryAttempts < 6)
                {
                    int row = rnd.Next(1, 15);
                    int tier = rnd.Next(1, 7);
                    string col = _columns[rnd.Next(_columns.Length)];
                    string id = $"CONT-AB{row:D2}-{tier}{col}";

                    string temp = (rnd.NextDouble() * (-15 - -25) + -25).ToString("0.0");
                    string status = _statuses[rnd.Next(_statuses.Length)];

                    await _hubContext.Clients.All.SendAsync("UpdateContainer", id, temp, status, cancellationToken: stoppingToken);

                    await Task.Delay(1500, stoppingToken);
                    retryAttempts++;
                }
            }
        }
    }
}