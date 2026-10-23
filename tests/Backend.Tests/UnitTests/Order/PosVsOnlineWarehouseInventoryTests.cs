using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.UnitTests.Order;

public class PosVsOnlineWarehouseInventoryTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly IOrderService _orderService;

    public PosVsOnlineWarehouseInventoryTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _orderService = new OrderService(_orderRepoMock.Object);
    }

    // =========================================================================
    // VÒNG 1: MA TRẬN THAM SỐ GÁN CHI NHÁNH (ONLINE KHO TỔNG VS POS CHI NHÁNH)
    // =========================================================================
    [Theory]
    [InlineData(null, 9)] // Khách mua online không truyền branch -> tự động gán Kho Tổng Online (ID 9)
    [InlineData(0, 9)]    // Truyền 0 -> tự động fallback về Kho Tổng Online (ID 9)
    [InlineData(1, 1)]    // Thu ngân POS chi nhánh 1 -> gán đúng chi nhánh 1
    [InlineData(2, 2)]    // Thu ngân POS chi nhánh 2 -> gán đúng chi nhánh 2
    [InlineData(5, 5)]    // Thu ngân POS chi nhánh 5 -> gán đúng chi nhánh 5
    public async Task CreateOrder_BranchScoping_AssignsCorrectBranchId(int? requestBranchId, int expectedBranchId)
    {
        // Arrange
        var req = new CreateOrderRequestDto
        {
            MaKhachHang = 10,
            MaNhanVien = requestBranchId.HasValue && requestBranchId.Value > 0 ? 2 : 1,
            MaChiNhanh = requestBranchId,
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto { MaBienThe = 101, SoLuong = 2 }
            }
        };

        int? capturedBranchId = null;
        _orderRepoMock.Setup(r => r.CreateOrderAsync(
                It.IsAny<int?>(), 
                It.IsAny<int>(), 
                It.IsAny<string?>(), 
                It.IsAny<int?>()))
            .Callback<int?, int, string?, int?>((cId, eId, coupon, bId) => capturedBranchId = bId)
            .ReturnsAsync(501);

        _orderRepoMock.Setup(r => r.CreateOrderAsync(
                It.IsAny<int?>(), 
                It.IsAny<int>(), 
                It.IsAny<string?>()))
            .Callback<int?, int, string?>((cId, eId, coupon) => capturedBranchId = 9)
            .ReturnsAsync(501);

        _orderRepoMock.Setup(r => r.AddOrderItemAsync(501, 101, 2))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _orderService.CreateOrderAsync(req);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(501);
        capturedBranchId.Should().Be(expectedBranchId);
    }

    // =========================================================================
    // VÒNG 2: XỬ LÝ LỖI TỒN KHO CHI NHÁNH (FAIL-FAST KHI KHO CHI NHÁNH HẾT HÀNG)
    // =========================================================================
    [Fact]
    public async Task CreateOrder_WhenBranchInventoryInsufficient_ReturnsAppropriateErrorMessage()
    {
        // Arrange: Đặt hàng tại chi nhánh 1 nhưng kho chi nhánh 1 không đủ tồn
        var req = new CreateOrderRequestDto
        {
            MaKhachHang = 5,
            MaNhanVien = 2,
            MaChiNhanh = 1,
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto { MaBienThe = 105, SoLuong = 10 }
            }
        };

        _orderRepoMock.Setup(r => r.CreateOrderAsync(
                It.IsAny<int?>(), 
                It.IsAny<int>(), 
                It.IsAny<string?>(), 
                It.IsAny<int?>()))
            .ReturnsAsync(502);

        _orderRepoMock.Setup(r => r.AddOrderItemAsync(502, 105, 10))
            .ThrowsAsync(new System.Exception("Số lượng tồn kho tại chi nhánh không đủ để xuất bán cho mặt hàng này!"));

        // Act
        var result = await _orderService.CreateOrderAsync(req);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("tồn kho tại chi nhánh");
    }
}
