using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.Enums;
using Xunit;

namespace Backend.Tests.UnitTests.Orders;

/// <summary>
/// Unit Test kiểm thử State Machine luồng trạng thái "Đang giao hàng" và phân quyền vai trò Order Coordinator (Bước 2).
/// Tuân thủ quy chuẩn Multi-Round Testing Standard (Rule 6):
/// - Vòng 1: Parameterized Boundary Matrix (orderId <= 0, newStatus rỗng/null/lạ)
/// - Vòng 2: State Machine Pipeline (Chờ xử lý -> Đang giao hàng -> Đã hoàn thành, Chặn đổi trạng thái sau Hoàn tất/Hủy)
/// - Vòng 3: Multi-Role Isolation & Scoping Matrix (Order Coordinator xem toàn bộ đơn và điều phối không bị bó hẹp chi nhánh)
/// </summary>
public class OrderCoordinatorStatusTests
{
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly OrderService _service;

    public OrderCoordinatorStatusTests()
    {
        _mockRepo = new Mock<IOrderRepository>();
        _service = new OrderService(_mockRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory(DisplayName = "VÒNG 1: Chặn cập nhật trạng thái khi mã đơn hàng không hợp lệ (<= 0)")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task UpdateOrderStatusAsync_InvalidOrderId_ReturnsFail(int invalidOrderId)
    {
        // Act
        var result = await _service.UpdateOrderStatusAsync(invalidOrderId, "Đang giao hàng");

        // Assert
        result.Success.Should().BeFalse("Mã đơn hàng <= 0 phải bị chặn");
        result.Message.Should().Contain("Mã đơn hàng không hợp lệ");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn cập nhật trạng thái khi chuỗi trạng thái mới rỗng hoặc null")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateOrderStatusAsync_EmptyOrNullStatus_ReturnsFail(string? emptyStatus)
    {
        // Act
        var result = await _service.UpdateOrderStatusAsync(10, emptyStatus!);

        // Assert
        result.Success.Should().BeFalse("Trạng thái mới không được để trống");
        result.Message.Should().Contain("Trạng thái không được để trống");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn cập nhật trạng thái khi trạng thái mới không nằm trong Whitelist")]
    [InlineData("DangGiao123")]
    [InlineData("TrangThaiHack")]
    [InlineData("PendingShipment")]
    [InlineData("<script>alert(1)</script>")]
    public async Task UpdateOrderStatusAsync_InvalidStatusNotInWhitelist_ReturnsFail(string invalidStatus)
    {
        // Act
        var result = await _service.UpdateOrderStatusAsync(10, invalidStatus);

        // Assert
        result.Success.Should().BeFalse("Trạng thái lạ phải bị từ chối");
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI (STATE MACHINE PIPELINE)

    [Theory(DisplayName = "VÒNG 2: Cho phép các bước chuyển trạng thái nghiệp vụ hợp lệ")]
    [InlineData("Chờ xử lý", "Đang giao hàng")]
    [InlineData("Chờ xử lý", "Đã hoàn thành")] // Mua tại quầy POS hoàn tất ngay
    [InlineData("Chờ xử lý", "Đã hủy")]
    [InlineData("Đang giao hàng", "Đã hoàn thành")] // Shipper giao xong, Order Coordinator chốt đơn
    [InlineData("Đang giao hàng", "Chờ xử lý")] // Giao thất bại, hẹn giao lại chuyển về Chờ xử lý
    [InlineData("Đang giao hàng", "Đã hủy")] // Giao thất bại, khách từ chối nhận
    public async Task UpdateOrderStatusAsync_ValidStateTransitions_ReturnsSuccess(string currentStatus, string nextStatus)
    {
        // Arrange
        int orderId = 100;
        _mockRepo.Setup(r => r.GetOrderStatusAsync(orderId))
            .ReturnsAsync(currentStatus);

        _mockRepo.Setup(r => r.UpdateOrderStatusAsync(orderId, nextStatus))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, nextStatus);

        // Assert
        result.Success.Should().BeTrue($"Chuyển từ '{currentStatus}' sang '{nextStatus}' là bước hợp lệ");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(orderId, nextStatus), Times.Once);
    }

    [Theory(DisplayName = "VÒNG 2: CHẶN TUYỆT ĐỐI việc thay đổi trạng thái khi đơn hàng đã 'Đã hoàn thành'")]
    [InlineData("Chờ xử lý")]
    [InlineData("Đang giao hàng")]
    [InlineData("Đã hủy")]
    public async Task UpdateOrderStatusAsync_FromCompletedOrder_ReturnsFail(string attemptNextStatus)
    {
        // Arrange
        int orderId = 101;
        _mockRepo.Setup(r => r.GetOrderStatusAsync(orderId))
            .ReturnsAsync("Đã hoàn thành");

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, attemptNextStatus);

        // Assert
        result.Success.Should().BeFalse("Đơn hàng đã hoàn thành thì không được phép thay đổi trạng thái (chỉ xử lý qua quy trình đổi trả)");
        result.Message.Should().Contain("đã hoàn thành");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 2: CHẶN TUYỆT ĐỐI việc thay đổi trạng thái khi đơn hàng đã 'Đã hủy'")]
    [InlineData("Chờ xử lý")]
    [InlineData("Đang giao hàng")]
    [InlineData("Đã hoàn thành")]
    public async Task UpdateOrderStatusAsync_FromCancelledOrder_ReturnsFail(string attemptNextStatus)
    {
        // Arrange
        int orderId = 102;
        _mockRepo.Setup(r => r.GetOrderStatusAsync(orderId))
            .ReturnsAsync("Đã hủy");

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, attemptNextStatus);

        // Assert
        result.Success.Should().BeFalse("Đơn hàng đã hủy là trạng thái kết thúc, không thể kích hoạt lại");
        result.Message.Should().Contain("đã hủy");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 2: CHẶN TUYỆT ĐỐI việc thay đổi trạng thái khi đơn hàng đã 'Đã hoàn trả'")]
    [InlineData("Chờ xử lý")]
    [InlineData("Đang giao hàng")]
    [InlineData("Đã hoàn thành")]
    [InlineData("Đã hủy")]
    public async Task UpdateOrderStatusAsync_FromReturnedOrder_ReturnsFail(string attemptNextStatus)
    {
        // Arrange
        int orderId = 103;
        _mockRepo.Setup(r => r.GetOrderStatusAsync(orderId))
            .ReturnsAsync("Đã hoàn trả");

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, attemptNextStatus);

        // Assert
        result.Success.Should().BeFalse("Đơn hàng đã hoàn trả là trạng thái kết thúc, không thể thay đổi");
        result.Message.Should().Contain("đã hoàn trả");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "VÒNG 2: Báo lỗi khi đơn hàng không tồn tại trên hệ thống")]
    public async Task UpdateOrderStatusAsync_OrderNotFound_ReturnsFail()
    {
        // Arrange
        int orderId = 999;
        _mockRepo.Setup(r => r.GetOrderStatusAsync(orderId))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, "Đang giao hàng");

        // Assert
        result.Success.Should().BeFalse("Đơn hàng không tồn tại phải trả về Fail");
        result.Message.Should().Contain("Không tìm thấy đơn hàng");
        _mockRepo.Verify(r => r.UpdateOrderStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & CÔ LẬP CHI NHÁNH ĐIỀU PHỐI (BRANCH ISOLATION MATRIX)

    [Theory(DisplayName = "VÒNG 3: Điều phối viên tại bất kỳ chi nhánh nào (Online hay Offline) đều bị cô lập chặt chẽ theo chi nhánh quản lý")]
    [InlineData(9, null, 9)] // Điều phối kho Online (9), không truyền branchId -> ép buộc 9
    [InlineData(9, 1, 9)]    // Điều phối kho Online (9), cố tình truyền branchId 1 -> ép buộc 9
    [InlineData(1, 2, 1)]    // Điều phối chi nhánh 1, cố tình truyền branchId 2 -> ép buộc 1
    [InlineData(2, null, 2)] // Điều phối chi nhánh 2, không truyền branchId -> ép buộc 2
    [InlineData(3, 9, 3)]    // Điều phối chi nhánh 3, cố tình truyền branchId 9 -> ép buộc 3
    public async Task GetAllOrdersAsync_OrderCoordinator_StrictlyEnforcesAssignedBranch(int assignedBranchId, int? queryBranchId, int expectedRepoBranchId)
    {
        // Arrange
        var mockOrders = new List<OrderSummaryDto>
        {
            new() { MaHoaDon = 1, MaChiNhanh = expectedRepoBranchId, TongThanhToan = 500000, TrangThai = "Chờ xử lý" }
        };

        _mockRepo.Setup(r => r.GetAllOrdersAsync(expectedRepoBranchId))
            .ReturnsAsync(mockOrders);

        // Act
        var result = await _service.GetAllOrdersAsync(
            branchId: queryBranchId, 
            userRole: RoleConstants.OrderCoordinator, 
            userBranchId: assignedBranchId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.All(x => x.MaChiNhanh == expectedRepoBranchId).Should().BeTrue();
        _mockRepo.Verify(r => r.GetAllOrdersAsync(expectedRepoBranchId), Times.Once);
        if (queryBranchId.HasValue && queryBranchId.Value != expectedRepoBranchId)
        {
            _mockRepo.Verify(r => r.GetAllOrdersAsync(queryBranchId.Value), Times.Never);
        }
    }

    [Fact(DisplayName = "VÒNG 3: Điều phối viên chưa gán userBranchId hợp lệ mặc định về chi nhánh 1 an toàn")]
    public async Task GetAllOrdersAsync_OrderCoordinatorWithoutBranch_DefaultsToBranch1Safe()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(1))
            .ReturnsAsync(new List<OrderSummaryDto>());

        // Act
        var result = await _service.GetAllOrdersAsync(
            branchId: null, 
            userRole: RoleConstants.OrderCoordinator, 
            userBranchId: null);

        // Assert
        result.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.GetAllOrdersAsync(1), Times.Once);
        _mockRepo.Verify(r => r.GetAllOrdersAsync(null), Times.Never);
    }

    #endregion
}
