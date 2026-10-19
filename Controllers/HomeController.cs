using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ReeferSystem.Hubs;
using ReeferSystem.Models;
using ReeferSystem.Services;
using System.Diagnostics;

namespace ReeferSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly IOracleYardService _oracleService;
        private readonly IHubContext<YardHub> _hubContext;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IOracleYardService oracleService, IHubContext<YardHub> hubContext, ILogger<HomeController> logger)
        {
            _oracleService = oracleService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Dedicated screen to view, insert, update and delete yard slots (yard_slots table)
        /// </summary>
        public async Task<IActionResult> YardConfiguration()
        {
            var slots = await _oracleService.GetYardSlotsAsync();
            return View(slots);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // =========================================================================
        // ORACLE API ENDPOINTS (Calling Pkg_Front_End)
        // =========================================================================

        /// <summary>
        /// Calls Pkg_Front_End.Get_All_Containers
        /// </summary>
        [HttpGet("api/yard/containers")]
        public async Task<IActionResult> GetContainers([FromQuery] string userId = "ADMIN", [FromQuery] bool refresh = false)
        {
            var (status, containers) = await _oracleService.GetAllContainersAsync(userId, refresh);
            return Ok(new { status, count = containers.Count, data = containers });
        }

        /// <summary>
        /// Calls Pkg_Front_End.Get_Cv_Details
        /// </summary>
        [HttpGet("api/yard/cv-details/{ctrNbr}")]
        public async Task<IActionResult> GetCvDetails(string ctrNbr, [FromQuery] string userId = "ADMIN")
        {
            var (status, details) = await _oracleService.GetCvDetailsAsync(ctrNbr, userId);
            return Ok(new { status, data = details });
        }

        /// <summary>
        /// Gets all configured yard slots
        /// </summary>
        [HttpGet("api/yard/slots")]
        public async Task<IActionResult> GetSlots()
        {
            var slots = await _oracleService.GetYardSlotsAsync();
            return Ok(new { status = "OK", count = slots.Count, data = slots });
        }

        /// <summary>
        /// Calls Pkg_Front_End.Insert_Yard_slot
        /// </summary>
        [HttpPost("api/yard/slots/insert")]
        public async Task<IActionResult> InsertSlot([FromBody] YardSlotModel slot)
        {
            if (string.IsNullOrWhiteSpace(slot.YardSlotName))
            {
                return BadRequest(new { status = "ERROR", message = "Yard Slot Name is required." });
            }

            var (status, useq, error) = await _oracleService.InsertYardSlotAsync(slot);

            // Broadcast via SignalR to all connected users
            await _hubContext.Clients.All.SendAsync("YardSlotCreated", slot);

            return Ok(new { status, useq, message = error ?? "Yard slot created successfully." });
        }

        /// <summary>
        /// Calls Pkg_Front_End.update_Yard_slot
        /// </summary>
        [HttpPost("api/yard/slots/update")]
        public async Task<IActionResult> UpdateSlot([FromBody] YardSlotModel slot)
        {
            if (slot.Useq <= 0)
            {
                return BadRequest(new { status = "ERROR", message = "Valid Useq is required for update." });
            }

            var (status, error) = await _oracleService.UpdateYardSlotAsync(slot);

            // Broadcast via SignalR
            await _hubContext.Clients.All.SendAsync("YardSlotUpdated", slot);

            return Ok(new { status, useq = slot.Useq, message = error ?? "Yard slot updated successfully." });
        }

        /// <summary>
        /// Calls Pkg_Front_End.Delete_yard_solt
        /// </summary>
        [HttpPost("api/yard/slots/delete")]
        public async Task<IActionResult> DeleteSlot([FromBody] DeleteSlotRequest request)
        {
            if (request.Useq <= 0)
            {
                return BadRequest(new { status = "ERROR", message = "Valid Useq is required for deletion." });
            }

            var (status, error) = await _oracleService.DeleteYardSlotAsync(request.Useq, request.UserId ?? "1");

            // Broadcast via SignalR
            await _hubContext.Clients.All.SendAsync("YardSlotDeleted", request.Useq);

            return Ok(new { status, useq = request.Useq, message = error ?? "Yard slot deleted successfully." });
        }

        /// <summary>
        /// Updates container position in Oracle Ctr2 and broadcasts via SignalR
        /// </summary>
        [HttpPost("api/yard/containers/relocate")]
        public async Task<IActionResult> RelocateContainer([FromBody] RelocateContainerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CtrNbr) || string.IsNullOrWhiteSpace(request.NewPosition))
            {
                return BadRequest(new { status = "ERROR", message = "CtrNbr and NewPosition are required." });
            }

            var (status, error) = await _oracleService.UpdateContainerPositionAsync(request.CtrNbr, request.NewPosition);
            await _hubContext.Clients.All.SendAsync("ContainerRelocated", request.CtrNbr, request.FromSlot, request.ToSlot);

            return Ok(new { status, message = error ?? "Container position updated successfully." });
        }

        /// <summary>
        /// Sends a test telemetry message to ActiveMQ queue 'YardDataQueue' at tcp://localhost:61616
        /// </summary>
        [HttpPost("api/yard/test-mq")]
        public async Task<IActionResult> SendTestActiveMq([FromQuery] string ctrNbr = "MEDU5128574", [FromQuery] string status = "HAZMAT", [FromQuery] string type = "40RF Reefer")
        {
            try
            {
                var factory = new Apache.NMS.ActiveMQ.ConnectionFactory("tcp://localhost:61616");
                using var conn = await factory.CreateConnectionAsync();
                await conn.StartAsync();
                using var session = await conn.CreateSessionAsync();
                var dest = await session.GetQueueAsync("YardDataQueue");
                using var producer = await session.CreateProducerAsync(dest);
                var text = $"{ctrNbr}|{type}|{status}|MAERSK|28,500 KG|Chemicals IMO-3";
                var msg = await producer.CreateTextMessageAsync(text);
                await producer.SendAsync(msg);
                _logger.LogInformation("TestActiveMQ: Dispatched test message to YardDataQueue: '{Text}'", text);
                return Ok(new { status = "OK", message = "Message sent to ActiveMQ broker on YardDataQueue", payload = text });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestActiveMQ failed: {Error}", ex.Message);
                return StatusCode(500, new { status = "ERROR", error = ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class DeleteSlotRequest
    {
        public long Useq { get; set; }
        public string? UserId { get; set; } = "1";
    }

    public class RelocateContainerRequest
    {
        public string CtrNbr { get; set; } = string.Empty;
        public string NewPosition { get; set; } = string.Empty;
        public string FromSlot { get; set; } = string.Empty;
        public string ToSlot { get; set; } = string.Empty;
    }
}
