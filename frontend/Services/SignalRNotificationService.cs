using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Frontend.Services;

public class SignalRNotificationService : ISignalRNotificationService, IAsyncDisposable
{
    private HubConnection? _notificationHub;
    private HubConnection? _inventoryHub;
    private readonly List<RealtimeNotificationItem> _notifications = new();
    private readonly string _backendBaseUrl;
    private readonly Microsoft.JSInterop.IJSRuntime? _jsRuntime;

    public bool IsConnected => 
        (_notificationHub?.State == HubConnectionState.Connected) || 
        (_inventoryHub?.State == HubConnectionState.Connected);

    public int UnreadCount => _notifications.Count(n => !n.IsRead);
    public IReadOnlyList<RealtimeNotificationItem> Notifications => _notifications.AsReadOnly();

    public event Action? OnChange;
    public event Action<int, string>? OnOrderPlaced;
    event Action<int, int, string>? ISignalRNotificationService.OnInventoryUpdated
    {
        add => OnInventoryUpdated += value;
        remove => OnInventoryUpdated -= value;
    }
    public event Action<int, int, string>? OnInventoryUpdated;
    public event Action<int, string, int>? OnLowStockAlert;
    public event Action<int, string, string, string>? OnCustomerFeedbackReceived;
    public event Action<int, string, string, string, string>? OnAnnouncementReceived;

    public SignalRNotificationService(string backendBaseUrl = "http://localhost:5000/", Microsoft.JSInterop.IJSRuntime? jsRuntime = null)
    {
        _backendBaseUrl = backendBaseUrl.TrimEnd('/');
        _jsRuntime = jsRuntime;
    }

    public async Task StartAsync()
    {
        try
        {
            await LoadRecentAnnouncementsAsync();

            if (_notificationHub == null)
            {
                _notificationHub = new HubConnectionBuilder()
                    .WithUrl($"{_backendBaseUrl}/hubs/notifications")
                    .WithAutomaticReconnect()
                    .Build();

                // Sự kiện đơn hàng: Kích hoạt event cho màn hình theo dõi đơn hàng (không đẩy vào chuông thông báo cấp trên)
                _notificationHub.On<int, string>("NewOrderPlaced", (orderId, message) =>
                {
                    OnOrderPlaced?.Invoke(orderId, message);
                });

                // Sự kiện khiếu nại khách hàng
                _notificationHub.On<int, string, string, string>("CustomerFeedbackReceived", (id, hoTen, loai, tieuDe) =>
                {
                    OnCustomerFeedbackReceived?.Invoke(id, hoTen, loai, tieuDe);
                });

                // Sự kiện THÔNG BÁO CỦA CẤP TRÊN (Hiển thị trực tiếp vào chuông thông báo)
                _notificationHub.On<int, string, string, string, int?, string>("InternalAnnouncementReceived", (id, tieuDe, mucDo, phamVi, branchId, noiDung) =>
                {
                    AddNotification(new RealtimeNotificationItem
                    {
                        Id = id.ToString(),
                        Title = tieuDe,
                        Message = string.IsNullOrWhiteSpace(noiDung) ? $"Phạm vi gửi: {phamVi}" : noiDung,
                        Type = "ANNOUNCEMENT",
                        Priority = mucDo,
                        Scope = phamVi,
                        Timestamp = DateTime.Now,
                        IsRead = false
                    });

                    OnAnnouncementReceived?.Invoke(id, tieuDe, mucDo, phamVi, noiDung);
                });

                _notificationHub.On<int, string, string, string, int?>("InternalAnnouncementReceivedLegacy", (id, tieuDe, mucDo, phamVi, branchId) =>
                {
                    AddNotification(new RealtimeNotificationItem
                    {
                        Id = id.ToString(),
                        Title = tieuDe,
                        Message = $"Mức độ: {mucDo} | Phạm vi: {phamVi}",
                        Type = "ANNOUNCEMENT",
                        Priority = mucDo,
                        Scope = phamVi,
                        Timestamp = DateTime.Now,
                        IsRead = false
                    });

                    OnAnnouncementReceived?.Invoke(id, tieuDe, mucDo, phamVi, string.Empty);
                });
            }

            if (_inventoryHub == null)
            {
                _inventoryHub = new HubConnectionBuilder()
                    .WithUrl($"{_backendBaseUrl}/hubs/inventory")
                    .WithAutomaticReconnect()
                    .Build();

                // Sự kiện biến động kho: Kích hoạt event để trang kho cập nhật tức thì (không làm rác chuông)
                _inventoryHub.On<int, int, string>("InventoryUpdated", (variantId, qty, action) =>
                {
                    OnInventoryUpdated?.Invoke(variantId, qty, action);
                });

                _inventoryHub.On<int, string, int>("ReceiveLowStockAlert", (variantId, sku, currentStock) =>
                {
                    OnLowStockAlert?.Invoke(variantId, sku, currentStock);
                });
            }

            if (_notificationHub.State == HubConnectionState.Disconnected)
            {
                await _notificationHub.StartAsync();
            }

            if (_inventoryHub.State == HubConnectionState.Disconnected)
            {
                await _inventoryHub.StartAsync();
                try
                {
                    await _inventoryHub.InvokeAsync("JoinStaffGroup");
                }
                catch { }
            }
        }
        catch
        {
            // Fail-safe: Lỗi kết nối SignalR không chặn đứng hoạt động ứng dụng
        }
    }

    public async Task LoadRecentAnnouncementsAsync()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_backendBaseUrl) };
            if (_jsRuntime != null)
            {
                try
                {
                    var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }
                catch { }
            }

            var res = await client.GetFromJsonAsync<global::Shared.DTOs.Common.ApiResponse<List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>>>("api/announcements");
            if (res?.Data != null)
            {
                foreach (var a in res.Data.Where(x => !x.TieuDe.StartsWith("[Đổi trả hàng]", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.NgayTao).Take(20))
                {
                    var existing = _notifications.FirstOrDefault(n => n.Id == a.MaThongBao.ToString());
                    if (existing == null)
                    {
                        _notifications.Add(new RealtimeNotificationItem
                        {
                            Id = a.MaThongBao.ToString(),
                            Title = a.TieuDe,
                            Message = a.NoiDung,
                            Type = "ANNOUNCEMENT",
                            Priority = a.MucDoUuTien,
                            Scope = a.PhamViGui,
                            Timestamp = a.NgayTao,
                            IsRead = a.DaDoc
                        });
                    }
                    else
                    {
                        if (a.DaDoc) existing.IsRead = true;
                    }
                }
                OnChange?.Invoke();
            }
        }
        catch { }
    }

    public async Task StopAsync()
    {
        if (_notificationHub != null)
        {
            await _notificationHub.StopAsync();
        }
        if (_inventoryHub != null)
        {
            await _inventoryHub.StopAsync();
        }
    }

    public void AddNotification(RealtimeNotificationItem item)
    {
        _notifications.Insert(0, item);
        if (_notifications.Count > 50)
        {
            _notifications.RemoveAt(_notifications.Count - 1);
        }
        OnChange?.Invoke();
    }

    public void MarkAllAsRead()
    {
        foreach (var n in _notifications)
        {
            n.IsRead = true;
        }
        OnChange?.Invoke();
        _ = MarkAllAsReadAsync();
    }

    public async Task MarkAsReadAsync(string announcementId)
    {
        var item = _notifications.FirstOrDefault(n => n.Id == announcementId);
        if (item != null)
        {
            item.IsRead = true;
            OnChange?.Invoke();
        }

        if (int.TryParse(announcementId, out var id))
        {
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(_backendBaseUrl) };
                if (_jsRuntime != null)
                {
                    var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                }
                await client.PostAsync($"api/announcements/{id}/read", null);
            }
            catch { }
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        foreach (var n in _notifications)
        {
            n.IsRead = true;
        }
        OnChange?.Invoke();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(_backendBaseUrl) };
            if (_jsRuntime != null)
            {
                var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }
            await client.PostAsync("api/announcements/read-all", null);
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_notificationHub != null)
        {
            await _notificationHub.DisposeAsync();
        }
        if (_inventoryHub != null)
        {
            await _inventoryHub.DisposeAsync();
        }
    }
}
