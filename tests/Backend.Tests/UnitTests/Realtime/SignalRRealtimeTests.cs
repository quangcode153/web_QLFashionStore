using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using Backend.WebSockets;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;
using Xunit;

namespace Backend.Tests.UnitTests.Realtime;

/// <summary>
/// Unit Tests kiểm thử phân hệ Đồng bộ Thời gian thực SignalR (P1-7 - TDD Test-First)
/// Kiểm thử các sự kiện NewOrderPlaced, InventoryUpdated, LowStockAlert và tính năng Exception Resilience
/// </summary>
public class SignalRRealtimeTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<IWarehouseRepository> _mockWarehouseRepo;

    private readonly Mock<IHubContext<NotificationHub>> _mockNotificationHub;
    private readonly Mock<IHubClients> _mockNotificationClients;
    private readonly Mock<IClientProxy> _mockNotificationAllProxy;

    private readonly Mock<IHubContext<InventoryHub>> _mockInventoryHub;
    private readonly Mock<IHubClients> _mockInventoryClients;
    private readonly Mock<IClientProxy> _mockInventoryAllProxy;
    private readonly Mock<IClientProxy> _mockInventoryGroupProxy;

    public SignalRRealtimeTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockWarehouseRepo = new Mock<IWarehouseRepository>();

        // Setup NotificationHub Mocks
        _mockNotificationHub = new Mock<IHubContext<NotificationHub>>();
        _mockNotificationClients = new Mock<IHubClients>();
        _mockNotificationAllProxy = new Mock<IClientProxy>();
        _mockNotificationHub.Setup(h => h.Clients).Returns(_mockNotificationClients.Object);
        _mockNotificationClients.Setup(c => c.All).Returns(_mockNotificationAllProxy.Object);

        // Setup InventoryHub Mocks
        _mockInventoryHub = new Mock<IHubContext<InventoryHub>>();
        _mockInventoryClients = new Mock<IHubClients>();
        _mockInventoryAllProxy = new Mock<IClientProxy>();
        _mockInventoryGroupProxy = new Mock<IClientProxy>();
        _mockInventoryHub.Setup(h => h.Clients).Returns(_mockInventoryClients.Object);
        _mockInventoryClients.Setup(c => c.All).Returns(_mockInventoryAllProxy.Object);
        _mockInventoryClients.Setup(c => c.Group("Staff")).Returns(_mockInventoryGroupProxy.Object);
    }

    [Fact(DisplayName = "TS-RT-01: Tạo đơn hàng thành công phát sự kiện NewOrderPlaced qua NotificationHub")]
    public async Task CreateOrderAsync_WhenSuccess_BroadcastsNewOrderPlaced()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            MaNhanVien = 2,
            Items = new List<CreateOrderItemDto>
            {
                new() { MaBienThe = 1, SoLuong = 2 }
            }
        };

        _mockOrderRepo.Setup(r => r.CreateOrderAsync(1, 2, null))
            .ReturnsAsync(99);

        var service = new OrderService(
            _mockOrderRepo.Object,
            _mockNotificationHub.Object,
            _mockInventoryHub.Object);

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);

        // Xác thực phát sự kiện NewOrderPlaced tới Clients.All
        _mockNotificationAllProxy.Verify(
            p => p.SendCoreAsync(
                "NewOrderPlaced",
                It.Is<object[]>(args => args.Length > 0 && Convert.ToInt32(args[0]) == 99),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "TS-RT-02: Đổi trả hàng thành công phát sự kiện InventoryUpdated qua InventoryHub")]
    public async Task ProcessReturnAsync_WhenSuccess_BroadcastsInventoryUpdated()
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 4,
            SoLuongTra = 2,
            LyDoTra = "Đổi kích cỡ"
        };

        var returnResult = new ReturnOrderResultDto
        {
            MaDonTra = 1,
            MaHoaDon = 10,
            MaBienThe = 4,
            SoLuongTra = 2,
            LyDoTra = "Đổi kích cỡ",
            TrangThai = "Đã xử lý"
        };

        _mockOrderRepo.Setup(r => r.ProcessOrderReturnAsync(10, 4, 2, "Đổi kích cỡ", 5))
            .ReturnsAsync((true, "Đổi trả thành công", returnResult));

        var service = new OrderService(
            _mockOrderRepo.Object,
            _mockNotificationHub.Object,
            _mockInventoryHub.Object);

        // Act
        var result = await service.ProcessReturnAsync(10, request, 5);

        // Assert
        result.Success.Should().BeTrue();

        // Xác thực phát sự kiện InventoryUpdated tới Clients.All
        _mockInventoryAllProxy.Verify(
            p => p.SendCoreAsync(
                "InventoryUpdated",
                It.Is<object[]>(args => args.Length >= 2 && Convert.ToInt32(args[0]) == 4 && Convert.ToInt32(args[1]) == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "TS-RT-03: Nhập kho thành công phát sự kiện InventoryUpdated qua InventoryHub")]
    public async Task ImportStockAsync_WhenSuccess_BroadcastsInventoryUpdated()
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 5,
            SoLuong = 30,
            DonGiaNhap = 150000
        };

        _mockWarehouseRepo.Setup(r => r.ImportStockAsync(request))
            .Returns(Task.CompletedTask);

        var service = new WarehouseService(
            _mockWarehouseRepo.Object,
            _mockInventoryHub.Object);

        // Act
        var result = await service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        // Xác thực phát sự kiện InventoryUpdated tới Clients.All
        _mockInventoryAllProxy.Verify(
            p => p.SendCoreAsync(
                "InventoryUpdated",
                It.Is<object[]>(args => args.Length >= 2 && Convert.ToInt32(args[0]) == 5 && Convert.ToInt32(args[1]) == 30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "TS-RT-04: Khả năng chống chịu lỗi SignalR - Lỗi broadcast không làm sập nghiệp vụ tạo đơn")]
    public async Task CreateOrderAsync_WhenSignalRThrows_StillSucceedsSafely()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            MaNhanVien = 2,
            Items = new List<CreateOrderItemDto>
            {
                new() { MaBienThe = 1, SoLuong = 1 }
            }
        };

        _mockOrderRepo.Setup(r => r.CreateOrderAsync(1, 2, null))
            .ReturnsAsync(101);

        // Giả lập SignalR bị đứt kết nối mạng / timeout
        _mockNotificationAllProxy.Setup(p => p.SendCoreAsync(
                "NewOrderPlaced",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR Client Disconnected"));

        var service = new OrderService(
            _mockOrderRepo.Object,
            _mockNotificationHub.Object,
            _mockInventoryHub.Object);

        // Act
        var result = await service.CreateOrderAsync(request);

        // Assert: Nghiệp vụ tạo đơn hàng vẫn thành công hoàn hảo
        result.Success.Should().BeTrue();
        result.Data.Should().Be(101);
    }

    [Fact(DisplayName = "TS-RT-05: Khả năng chống chịu lỗi SignalR - Lỗi broadcast không làm sập nghiệp vụ nhập kho")]
    public async Task ImportStockAsync_WhenSignalRThrows_StillSucceedsSafely()
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 5,
            SoLuong = 20,
            DonGiaNhap = 100000
        };

        _mockWarehouseRepo.Setup(r => r.ImportStockAsync(request))
            .Returns(Task.CompletedTask);

        // Giả lập SignalR bị lỗi
        _mockInventoryAllProxy.Setup(p => p.SendCoreAsync(
                "InventoryUpdated",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("SignalR timeout"));

        var service = new WarehouseService(
            _mockWarehouseRepo.Object,
            _mockInventoryHub.Object);

        // Act
        var result = await service.ImportStockAsync(request);

        // Assert: Nghiệp vụ nhập kho vẫn thành công hoàn hảo
        result.Success.Should().BeTrue();
    }
}
