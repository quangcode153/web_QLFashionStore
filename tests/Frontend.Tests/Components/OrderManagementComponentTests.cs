using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Order;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

using Shared.DTOs.Feedback;
using Shared.DTOs.Common;

namespace Frontend.Tests.Components;

public class OrderManagementComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public OrderManagementComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        // Mock mặc định trả về danh sách đơn mẫu
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync(It.IsAny<int?>()))
            .ReturnsAsync(CreateSampleOrders());

        _mockAdminApi.Setup(api => api.GetFeedbacksAsync())
            .ReturnsAsync(CreateSampleFeedbacks());
    }

    private List<OrderSummaryDto> CreateSampleOrders()
    {
        return new List<OrderSummaryDto>
        {
            new OrderSummaryDto
            {
                MaHoaDon = 101,
                NgayLapHoaDon = DateTime.Now,
                TenKhachHang = "Khách vãng lai",
                SdtKhachHang = "N/A",
                NguoiLapDon = "Trần Thị Bích",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                TienHang = 300000,
                TongThanhToan = 300000,
                TrangThai = "Đã hoàn thành"
            },
            new OrderSummaryDto
            {
                MaHoaDon = 102,
                NgayLapHoaDon = DateTime.Now,
                TenKhachHang = "Nguyễn Văn Tuấn",
                SdtKhachHang = "0901234567",
                NguoiLapDon = "Đặt online qua Web",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                TienHang = 500000,
                TongThanhToan = 500000,
                TrangThai = "Chờ xử lý"
            }
        };
    }

    [Fact(DisplayName = "UI-ORD-01: Admin khi vào mục Quản lý đơn hàng mặc định hiển thị khu vực Chi nhánh 1")]
    public void AdminAccess_DefaultsToBranch1()
    {
        // Arrange: JS mock trả về role Admin
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);

        // Act
        var cut = Render<OrderManagement>();

        // Assert
        cut.Find(".page-title").TextContent.Should().Contain("QUẢN LÝ ĐƠN HÀNG");
        var select = cut.Find("select.branch-select-box");
        select.Should().NotBeNull();
        // Option đầu tiên được chọn mặc định là Chi nhánh 1
        select.Attributes["value"]?.Value.Should().Be("1");
    }

    [Fact(DisplayName = "UI-ORD-02: Quản lý chi nhánh truy cập thì khóa cố định chi nhánh của mình, không hiện dropdown")]
    public void BranchManagerAccess_DisplaysLockedBranchBadge()
    {
        // Arrange: JS mock trả về role Quản lý chi nhánh và branchId = 2
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("2");

        // Act
        var cut = Render<OrderManagement>();

        // Assert: Không hiển thị dropdown chọn chi nhánh
        cut.FindAll("select.branch-select-box").Should().BeEmpty();
        // Hiển thị badge khóa khu vực quản lý
        var badge = cut.Find(".branch-badge-locked");
        badge.Should().NotBeNull();
        badge.TextContent.Should().Contain("Khu vực quản lý");
        badge.TextContent.Should().Contain("QF Quang Trung");
    }

    [Fact(DisplayName = "UI-ORD-03: Đơn hàng khách vãng lai hiển thị nhãn và chi nhánh phụ trách")]
    public void GuestOrder_DisplaysGuestPillAndBranchInfo()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);

        // Act
        var cut = Render<OrderManagement>();

        // Assert: Tìm hàng đơn #101 của khách vãng lai
        cut.Markup.Should().Contain("Khách vãng lai quầy");
        cut.Markup.Should().Contain("QF Sư Vạn Hạnh");
    }

    [Fact(DisplayName = "UI-ORD-04: Admin chuyển đổi dropdown chi nhánh thì gọi API tải lại đơn cho đúng chi nhánh mới")]
    public void AdminSwitchesBranch_CallsApiWithNewBranch()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        var cut = Render<OrderManagement>();

        // Act: Chọn Chi nhánh 2
        var select = cut.Find("select.branch-select-box");
        select.Change("2");

        // Assert
        _mockAdminApi.Verify(api => api.GetAllOrdersAsync(2), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "UI-ORD-05: Quản lý đơn hàng sau khi gộp không còn nút shortcut rời rạc sang page cũ (sử dụng 2 Tab tích hợp)")]
    public void OrderManagement_WhenRendered_DoesNotContainOldReturnOrdersShortcutButton()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh");

        // Act
        var cut = Render<OrderManagement>();

        // Assert
        cut.FindAll("a.btn-order-support-shortcut").Should().BeEmpty();
    }

    [Fact(DisplayName = "UI-ORD-06: Đơn hàng 'Chờ xử lý' có nút 'Bàn giao Shipper' chuyển trạng thái 'Đang giao hàng'")]
    public async Task OrderManagement_WhenPendingOrder_DisplaysHandoverShipperButton()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);
        _mockAdminApi.Setup(x => x.UpdateOrderStatusAsync(102, "Đang giao hàng")).ReturnsAsync(true);

        // Act
        var cut = Render<OrderManagement>();

        // Assert
        var btnShipping = cut.Find("button.btn-action-shipping[data-action-shipping='102']");
        btnShipping.Should().NotBeNull();
        btnShipping.TextContent.Should().Contain("Bàn giao Shipper");

        // Act click
        await cut.InvokeAsync(() => btnShipping.Click());

        // Assert
        _mockAdminApi.Verify(x => x.UpdateOrderStatusAsync(102, "Đang giao hàng"), Times.Once);
        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(msg => msg.Contains("Đang giao hàng"))), Times.Once);
    }

    [Fact(DisplayName = "UI-ORD-07: Tab Quản lý Đổi trả tích hợp trực tiếp, chuyển tab hiển thị danh sách phiếu đổi trả")]
    public async Task OrderManagement_MainTabs_AllowsSwitchingToReturnsManagement()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);

        // Act
        var cut = Render<OrderManagement>();

        // Click Tab Đổi trả
        var tabReturns = cut.Find("button.tab-nav-returns");
        await cut.InvokeAsync(() => tabReturns.Click());

        // Assert
        cut.Markup.Should().Contain("#PH-501");
        cut.Markup.Should().Contain("Yêu cầu hoàn trả hàng");
        cut.Find("button.btn-approve-return").Should().NotBeNull();
    }

    private List<FeedbackDto> CreateSampleFeedbacks()
    {
        return new List<FeedbackDto>
        {
            new()
            {
                MaPhanHoi = 501,
                MaHoaDon = 101,
                HoTen = "Lê Văn Hùng",
                DienThoai = "0901112233",
                LoaiPhanHoi = "Yêu cầu hoàn trả hàng",
                TieuDe = "Yêu cầu hoàn trả hàng cho đơn #101",
                NoiDung = "Áo bị rách đường may",
                TrangThai = "Cần duyệt",
                HinhThucHoan = "Thu hồi tại nhà",
                DiaChiThuHoi = "123 Cách Mạng Tháng 8, Q.3, TP.HCM",
                SoLuongTra = 1,
                NgayGui = DateTime.Now.AddHours(-1)
            }
        };
    }
}
