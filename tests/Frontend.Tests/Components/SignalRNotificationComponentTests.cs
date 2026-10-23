using System;
using System.Collections.Generic;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Frontend.Components;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class SignalRNotificationComponentTests : BunitContext
{
    private readonly Mock<ISignalRNotificationService> _mockSignalRService;

    public SignalRNotificationComponentTests()
    {
        _mockSignalRService = new Mock<ISignalRNotificationService>();
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalRService.Object);
    }

    private List<RealtimeNotificationItem> CreateSampleNotifications()
    {
        return new List<RealtimeNotificationItem>
        {
            new()
            {
                Id = "notif-1",
                Title = "Đơn hàng mới #8001",
                Message = "Khách hàng vừa đặt đơn mới tại hệ thống.",
                Type = "ORDER",
                Timestamp = DateTime.Now,
                IsRead = false
            },
            new()
            {
                Id = "notif-2",
                Title = "Cảnh báo tồn kho",
                Message = "Biến thể AT-01 chỉ còn 3 sản phẩm trong kho.",
                Type = "LOW_STOCK",
                Timestamp = DateTime.Now.AddMinutes(-5),
                IsRead = false
            }
        };
    }

    #region 1. RealtimeNotificationBell Component Tests (RT-01)

    [Fact]
    public void SignalRNotificationBell_InitialRender_ShowsBellIconAndZeroBadge()
    {
        _mockSignalRService.SetupGet(s => s.UnreadCount).Returns(0);
        _mockSignalRService.SetupGet(s => s.Notifications).Returns(new List<RealtimeNotificationItem>());

        var cut = Render<RealtimeNotificationBell>();

        cut.Find("button.notification-bell-btn").Should().NotBeNull();
        cut.FindAll(".notification-badge").Should().BeEmpty(); // Không hiện badge khi = 0
    }

    [Fact]
    public void SignalRNotificationBell_WhenNotificationReceived_ShowsBadgeAndDropdown()
    {
        var list = CreateSampleNotifications();
        _mockSignalRService.SetupGet(s => s.UnreadCount).Returns(2);
        _mockSignalRService.SetupGet(s => s.Notifications).Returns(list);

        var cut = Render<RealtimeNotificationBell>();

        // Kiểm tra badge hiển thị số 2
        var badge = cut.Find(".notification-badge");
        badge.TextContent.Should().Contain("2");

        // Bấm mở chuông
        cut.Find("button.notification-bell-btn").Click();

        // Kiểm tra popup dropdown mở ra
        cut.Find(".notification-dropdown").Should().NotBeNull();
        cut.Markup.Should().Contain("Đơn hàng mới #8001");
        cut.Markup.Should().Contain("Cảnh báo tồn kho");
        cut.Markup.Should().Contain("AT-01");
    }

    [Fact]
    public void SignalRNotificationBell_MarkAllAsRead_CallsServiceMethod()
    {
        var list = CreateSampleNotifications();
        _mockSignalRService.SetupGet(s => s.UnreadCount).Returns(2);
        _mockSignalRService.SetupGet(s => s.Notifications).Returns(list);

        var cut = Render<RealtimeNotificationBell>();

        // Mở dropdown
        cut.Find("button.notification-bell-btn").Click();

        // Bấm "Đánh dấu đã đọc"
        var btnMarkRead = cut.Find("button.btn-mark-read");
        btnMarkRead.Click();

        _mockSignalRService.Verify(s => s.MarkAllAsReadAsync(), Times.Once);
    }

    #endregion

    #region 2. SignalRNotificationService Unit Logic Tests

    [Fact]
    public void SignalRNotificationService_AddNotification_IncrementsUnreadAndTriggersOnChange()
    {
        var service = new SignalRNotificationService();
        var changeTriggered = false;
        service.OnChange += () => { changeTriggered = true; };

        service.AddNotification(new RealtimeNotificationItem
        {
            Title = "Đơn mới #9001",
            Message = "Test order placed",
            Type = "ORDER"
        });

        service.UnreadCount.Should().Be(1);
        service.Notifications.Should().HaveCount(1);
        changeTriggered.Should().BeTrue();
    }

    [Fact]
    public void SignalRNotificationService_MarkAllAsRead_ClearsUnreadCount()
    {
        var service = new SignalRNotificationService();
        service.AddNotification(new RealtimeNotificationItem { Title = "1", Message = "Msg 1" });
        service.AddNotification(new RealtimeNotificationItem { Title = "2", Message = "Msg 2" });

        service.UnreadCount.Should().Be(2);

        service.MarkAllAsRead();

        service.UnreadCount.Should().Be(0);
        service.Notifications.Should().OnlyContain(n => n.IsRead);
    }

    #endregion
}
