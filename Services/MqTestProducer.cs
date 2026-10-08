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
    private readonly string[] _types = { "40HC Dry", "20GP Standard", "40GP Standard", "40RF Reefer", "20TK Tank ISO", "40OT Open Top" };
    private readonly string[] _lines = { "MAERSK", "MSC", "CMA CGM", "COSCO", "HAPAG-LLOYD", "ONE", "EVERGREEN" };
    private readonly string[] _statuses = { "LOADED", "LOADED", "CLEARED", "INSPECT", "HAZMAT" };
    private readonly string[] _categories = { "Electronics & Tech", "Consumer Goods", "Auto Parts", "Chemicals IMO-3", "Perishable Cold Chain", "Heavy Machinery", "Textiles" };

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
                IDestination dest = await session.GetQueueAsync("YardDataQueue");
                using IMessageProducer producer = await session.CreateProducerAsync(dest);

                _logger.LogInformation("MqTestProducer: Successfully connected to ActiveMQ broker. Publishing telemetry for Yard Stack AB...");

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Generate container coordinate matching Stack AB:
                    // 14 Rows (01 to 14), 6 Tiers (1 to 6), 6 Columns (A to F)
                    int row = rnd.Next(1, 15);
                    int tier = rnd.Next(1, 7);
                    string col = _columns[rnd.Next(_columns.Length)];
                    string id = $"CONT-AB{row:D2}-{tier}{col}";

                    string type = _types[rnd.Next(_types.Length)];
                    string status = _statuses[rnd.Next(_statuses.Length)];
                    string line = _lines[rnd.Next(_lines.Length)];
                    string weight = $"{rnd.Next(16000, 31500):N0} KG";
                    string category = _categories[rnd.Next(_categories.Length)];

                    var msg = session.CreateTextMessage($"{id}|{type}|{status}|{line}|{weight}|{category}");
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

                    string type = _types[rnd.Next(_types.Length)];
                    string status = _statuses[rnd.Next(_statuses.Length)];
                    string line = _lines[rnd.Next(_lines.Length)];
                    string weight = $"{rnd.Next(16000, 31500):N0} KG";
                    string category = _categories[rnd.Next(_categories.Length)];

                    await _hubContext.Clients.All.SendAsync("UpdateContainer", id, type, status, line, weight, category, cancellationToken: stoppingToken);

                    await Task.Delay(1500, stoppingToken);
                    retryAttempts++;
                }
            }
        }
    }
}