using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Backend.WebSockets;

public class InventoryHub : Hub
{
    public async Task SendLowStockAlert(int variantId, string sku, int currentStock)
    {
        await Clients.Group("Staff").SendAsync("ReceiveLowStockAlert", variantId, sku, currentStock);
    }

    public async Task JoinStaffGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
    }
}
