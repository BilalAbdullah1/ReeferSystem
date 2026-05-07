using Microsoft.AspNetCore.SignalR;

namespace ReeferSystem.Hubs
{
    public class YardHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
    }
}