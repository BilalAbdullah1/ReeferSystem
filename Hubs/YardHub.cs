using Microsoft.AspNetCore.SignalR;

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
    }
}