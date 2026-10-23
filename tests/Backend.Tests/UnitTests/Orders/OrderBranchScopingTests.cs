using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.Constants;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.UnitTests.Orders;

/// <summary>
/// Unit Tests kiểm thử phân quyền và phạm vi hiển thị đơn hàng theo chi nhánh:
/// 1. Quản lý chi nhánh chỉ xem đơn thuộc chi nhánh của mình (Branch Isolation).
/// 2. Đơn khách vãng lai tại quầy xác định theo chi nhánh của nhân viên hỗ trợ thanh toán.
/// 3. Admin khi không chỉ định chi nhánh mặc định vào khu vực chi nhánh 1.
/// Tuân thủ Multi-Round Testing Standard (Rule 6) và Red-First Gate (Rule 8).
/// </summary>
public class OrderBranchScopingTests
{
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly OrderService _service;

    public OrderBranchScopingTests()
    {
        _mockRepo = new Mock<IOrderRepository>();
        _service = new OrderService(_mockRepo.Object);
    }

    private List<OrderSummaryDto> GetSampleOrders()
    {
        return new List<OrderSummaryDto>
        {
            // Đơn 101: Khách vãng lai, NV thu ngân chi nhánh 1 (Sư Vạn Hạnh)
            new OrderSummaryDto
            {
                MaHoaDon = 101,
                TenKhachHang = "Khách vãng lai",
                NguoiLapDon = "Trần Thị Bích",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                TongThanhToan = 350000,
                TrangThai = "Đã hoàn thành"
            },
            // Đơn 102: Khách vãng lai, NV thu ngân chi nhánh 2 (Quang Trung)
            new OrderSummaryDto
            {
                MaHoaDon = 102,
                TenKhachHang = "Khách vãng lai",
                NguoiLapDon = "Vũ Ngọc Lan",
                MaChiNhanh = 2,
                TenChiNhanh = "QF Quang Trung (Q. Gò Vấp, TP.HCM)",
                TongThanhToan = 720000,
                TrangThai = "Đã hoàn thành"
            },
            // Đơn 103: Khách online (không có nhân viên), mặc định chi nhánh 1 tiếp nhận
            new OrderSummaryDto
            {
                MaHoaDon = 103,
                TenKhachHang = "Lê Hoàng Nam",
                NguoiLapDon = "Đặt online qua Web",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                TongThanhToan = 500000,
                TrangThai = "Chờ xử lý"
            },
            // Đơn 104: Khách thành viên tại chi nhánh 2, NV thu ngân chi nhánh 2
            new OrderSummaryDto
            {
                MaHoaDon = 104,
                TenKhachHang = "Nguyễn Văn Tuấn",
                NguoiLapDon = "Ngô Thị Hạnh",
                MaChiNhanh = 2,
                TenChiNhanh = "QF Quang Trung (Q. Gò Vấp, TP.HCM)",
                TongThanhToan = 1200000,
                TrangThai = "Đã hoàn thành"
            }
        };
    }

    #region VÒNG 1: THAM SỐ BIÊN VÀ ĐỊNH DANH CHI NHÁNH (BOUNDARY & PARAMETERIZED MATRIX)

    [Theory(DisplayName = "ROUND-1-01: Lọc đơn theo mã chi nhánh cụ thể trả về đúng tập hợp của chi nhánh đó")]
    [InlineData(1, 2)] // Chi nhánh 1 có 2 đơn (101, 103)
    [InlineData(2, 2)] // Chi nhánh 2 có 2 đơn (102, 104)
    public async Task GetAllOrdersAsync_WithSpecificBranch_ReturnsOnlyThatBranchOrders(int branchId, int expectedCount)
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(branchId))
            .ReturnsAsync(GetSampleOrders().Where(x => x.MaChiNhanh == branchId));

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: branchId, userRole: RoleConstants.AdminChuoi, userBranchId: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(expectedCount);
        result.Data!.All(x => x.MaChiNhanh == branchId).Should().BeTrue();
    }

    [Fact(DisplayName = "ROUND-1-02: Chi nhánh không tồn tại hoặc không có đơn trả về danh sách rỗng")]
    public async Task GetAllOrdersAsync_WithNonExistentBranch_ReturnsEmptyList()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(99))
            .ReturnsAsync(new List<OrderSummaryDto>());

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: 99, userRole: RoleConstants.AdminChuoi, userBranchId: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region VÒNG 2: XÁC ĐỊNH CHI NHÁNH CHO ĐƠN KHÁCH VÃNG LAI (GUEST ORDER BRANCH ATTRIBUTION)

    [Fact(DisplayName = "ROUND-2-01: Khách vãng lai do NV chi nhánh 2 thanh toán được xếp chính xác vào chi nhánh 2")]
    public async Task GetAllOrdersAsync_GuestOrderCashierBranch2_IsAttributedToBranch2()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(2))
            .ReturnsAsync(GetSampleOrders().Where(x => x.MaChiNhanh == 2));

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: 2, userRole: RoleConstants.QuanLyChiNhanh, userBranchId: 2);

        // Assert
        result.Success.Should().BeTrue();
        var guestOrder = result.Data!.FirstOrDefault(x => x.MaHoaDon == 102);
        guestOrder.Should().NotBeNull();
        guestOrder!.TenKhachHang.Should().Be("Khách vãng lai");
        guestOrder.MaChiNhanh.Should().Be(2);
        guestOrder.NguoiLapDon.Should().Be("Vũ Ngọc Lan");
    }

    [Fact(DisplayName = "ROUND-2-02: Khách vãng lai do NV chi nhánh 1 thanh toán không xuất hiện khi lọc chi nhánh 2")]
    public async Task GetAllOrdersAsync_GuestOrderCashierBranch1_DoesNotAppearInBranch2()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(2))
            .ReturnsAsync(GetSampleOrders().Where(x => x.MaChiNhanh == 2));

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: 2, userRole: RoleConstants.QuanLyChiNhanh, userBranchId: 2);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Any(x => x.MaHoaDon == 101).Should().BeFalse();
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & CÔ LẬP CHI NHÁNH (RBAC & BRANCH ISOLATION MATRIX)

    [Fact(DisplayName = "ROUND-3-01: Admin khi chọn Tất cả chi nhánh (null hoặc 0) phải truy vấn toàn chuỗi (branchId = null)")]
    public async Task GetAllOrdersAsync_AdminWithNoBranchSpecified_PassesNullToRepoToFetchAllBranches()
    {
        // Arrange - Admin chọn xem Tất cả chi nhánh (branchId = null), repository phải nhận null để lấy toàn bộ đơn hàng
        _mockRepo.Setup(r => r.GetAllOrdersAsync(null))
            .ReturnsAsync(GetSampleOrders());

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: null, userRole: RoleConstants.AdminChuoi, userBranchId: null);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count().Should().Be(GetSampleOrders().Count);
        _mockRepo.Verify(r => r.GetAllOrdersAsync(null), Times.Once);
        _mockRepo.Verify(r => r.GetAllOrdersAsync(1), Times.Never);
    }

    [Fact(DisplayName = "ROUND-3-02: Quản lý chi nhánh bị cô lập chặt chẽ - chỉ lấy đơn chi nhánh của mình kể cả khi cố tình request branch khác")]
    public async Task GetAllOrdersAsync_BranchManager_EnforcesOwnBranchIsolation()
    {
        // Arrange - Quản lý thuộc chi nhánh 2, nhưng gửi param branchId = 1 (cố tình rà soát chi nhánh khác)
        _mockRepo.Setup(r => r.GetAllOrdersAsync(2))
            .ReturnsAsync(GetSampleOrders().Where(x => x.MaChiNhanh == 2));

        // Act - Hệ thống phải cưỡng chế branchId = 2 theo userBranchId của Quản lý chi nhánh
        var result = await _service.GetAllOrdersAsync(branchId: 1, userRole: RoleConstants.QuanLyChiNhanh, userBranchId: 2);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.All(x => x.MaChiNhanh == 2).Should().BeTrue();
        // Phải gọi Repo với branchId = 2, TUYỆT ĐỐI KHÔNG gọi branchId = 1
        _mockRepo.Verify(r => r.GetAllOrdersAsync(2), Times.Once);
        _mockRepo.Verify(r => r.GetAllOrdersAsync(1), Times.Never);
    }

    [Fact(DisplayName = "ROUND-3-03: Nhân viên bán hàng / Thu ngân cũng bị cô lập chi nhánh theo nơi làm việc")]
    public async Task GetAllOrdersAsync_StaffCashier_EnforcesOwnBranchIsolation()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetAllOrdersAsync(2))
            .ReturnsAsync(GetSampleOrders().Where(x => x.MaChiNhanh == 2));

        // Act
        var result = await _service.GetAllOrdersAsync(branchId: null, userRole: RoleConstants.ThuNgan, userBranchId: 2);

        // Assert
        result.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.GetAllOrdersAsync(2), Times.Once);
    }

    #endregion
}
