using Microsoft.AspNetCore.SignalR;

namespace Backend.WebSockets;

public class NotificationHub : Hub
{
    public async Task SendOrderNotification(string orderId, string message)
    {
        await Clients.All.SendAsync("ReceiveOrderNotification", orderId, message);
    }
}

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
