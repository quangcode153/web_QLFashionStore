using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Backend.WebSockets;

public class NotificationHub : Hub
{
    public async Task SendOrderNotification(string orderId, string message)
    {
        await Clients.All.SendAsync("ReceiveOrderNotification", orderId, message);
    }
}
