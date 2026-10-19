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

                _logger.LogInformation("YardMqService: Connected successfully to ActiveMQ queue '{Dest}'. Consuming live messages...", destination);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = await consumer.ReceiveAsync();
                    if (message is ITextMessage textMessage)
                    {
                        var raw = textMessage.Text?.Trim() ?? string.Empty;
                        _logger.LogInformation("ActiveMQ: Received message on YardDataQueue: '{Raw}'", raw);

                        string id = "";
                        string type = "40RF Reefer";
                        string status = "HAZMAT";
                        string line = "MAERSK";
                        string weight = "28,500 KG";
                        string category = "ActiveMQ Telemetry";

                        if (raw.StartsWith("{") && raw.EndsWith("}"))
                        {
                            try
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("ctrNbr", out var c) || root.TryGetProperty("id", out c) || root.TryGetProperty("container", out c))
                                    id = c.GetString() ?? "";
                                if (root.TryGetProperty("type", out var t) || root.TryGetProperty("ctrType", out t))
                                    type = t.GetString() ?? type;
                                if (root.TryGetProperty("status", out var s))
                                    status = s.GetString() ?? status;
                                if (root.TryGetProperty("line", out var l) || root.TryGetProperty("vesselCode", out l))
                                    line = l.GetString() ?? line;
                                if (root.TryGetProperty("weight", out var w) || root.TryGetProperty("grossWeight", out w))
                                    weight = w.GetString() ?? weight;
                                if (root.TryGetProperty("category", out var cat))
                                    category = cat.GetString() ?? category;
                            }
                            catch (Exception jex)
                            {
                                _logger.LogWarning("ActiveMQ: JSON parse error: {Msg}", jex.Message);
                            }
                        }
                        else if (raw.Contains('|'))
                        {
                            var parts = raw.Split('|').Select(s => s.Trim()).ToArray();
                            id = parts.Length > 0 ? parts[0] : "";
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) type = parts[1];
                            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2])) status = parts[2];
                            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3])) line = parts[3];
                            if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4])) weight = parts[4];
                            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5])) category = parts[5];
                        }
                        else if (raw.Contains(','))
                        {
                            var parts = raw.Split(',').Select(s => s.Trim()).ToArray();
                            id = parts.Length > 0 ? parts[0] : "";
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) type = parts[1];
                            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2])) status = parts[2];
                            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3])) line = parts[3];
                            if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4])) weight = parts[4];
                            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5])) category = parts[5];
                        }
                        else
                        {
                            id = raw.Trim();
                        }

                        if (!string.IsNullOrEmpty(id))
                        {
                            _logger.LogInformation("ActiveMQ -> SignalR Broadcasting: Ctr={Id}, Type={Type}, Status={Status}, Line={Line}, Weight={Weight}", id, type, status, line, weight);
                            await _hubContext.Clients.All.SendAsync("UpdateContainer", id, type, status, line, weight, category, cancellationToken: stoppingToken);
                        }
                        else
                        {
                            _logger.LogWarning("ActiveMQ: Received empty or invalid message content: '{Raw}'", raw);
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