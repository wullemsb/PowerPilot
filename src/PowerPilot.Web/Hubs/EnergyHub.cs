using Microsoft.AspNetCore.SignalR;

namespace PowerPilot.Web.Hubs;

public class EnergyHub : Hub
{
    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
    }
}
