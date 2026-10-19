using Microsoft.AspNetCore.SignalR;
using ReeferSystem.Models;

namespace ReeferSystem.Hubs
{
    public class YardHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public async Task RelocateContainer(string containerId, string fromSlot, string toSlot)
        {
            await Clients.Others.SendAsync("ContainerRelocated", containerId, fromSlot, toSlot);
        }

        public async Task UpdateRowOrder(string[] rowIds)
        {
            await Clients.Others.SendAsync("RowOrderChanged", rowIds);
        }

        public async Task NotifyContainerTelemetry(string ctrNbr, string size, string type, string status, string position, string weight)
        {
            await Clients.All.SendAsync("UpdateContainer", ctrNbr, type, status, size, weight, position);
        }

        public async Task BroadcastYardSlotCreated(YardSlotModel slot)
        {
            await Clients.Others.SendAsync("YardSlotCreated", slot);
        }

        public async Task BroadcastYardSlotUpdated(YardSlotModel slot)
        {
            await Clients.Others.SendAsync("YardSlotUpdated", slot);
        }

        public async Task BroadcastYardSlotDeleted(long useq)
        {
            await Clients.Others.SendAsync("YardSlotDeleted", useq);
        }
    }
}