using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontend.Services;

public class RealtimeNotificationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "ANNOUNCEMENT"; // ANNOUNCEMENT, INFO, FEEDBACK
    public string Priority { get; set; } = "Bình thường";
    public string Scope { get; set; } = "Toàn hệ thống";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
}

public interface ISignalRNotificationService
{
    bool IsConnected { get; }
    int UnreadCount { get; }
    IReadOnlyList<RealtimeNotificationItem> Notifications { get; }

    event Action? OnChange;
    event Action<int, string>? OnOrderPlaced;
    event Action<int, int, string>? OnInventoryUpdated;
    event Action<int, string, int>? OnLowStockAlert;
    event Action<int, string, string, string>? OnCustomerFeedbackReceived;
    event Action<int, string, string, string, string>? OnAnnouncementReceived;

    Task StartAsync();
    Task StopAsync();
    void MarkAllAsRead();
    Task MarkAsReadAsync(string announcementId);
    Task MarkAllAsReadAsync();
    void AddNotification(RealtimeNotificationItem item);
    Task LoadRecentAnnouncementsAsync();
}
