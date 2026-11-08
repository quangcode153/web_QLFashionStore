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
using Shared.DTOs.Warehouse;
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

    [Fact(DisplayName = "UI-ORD-08: Bộ lọc trạng thái đa chọn cho phép lọc các đơn hàng thỏa mãn 1 hoặc nhiều tiêu chí cùng lúc")]
    public void OrderManagement_MultiSelectStatusFilter_FiltersOrdersMatchingAnySelectedStatus()
    {
        // Arrange: Tạo đơn gồm nhiều trạng thái
        var customOrders = new List<OrderSummaryDto>
        {
            new() { MaHoaDon = 201, TenKhachHang = "A", TrangThai = "Chờ xử lý", TienHang = 100000, TongThanhToan = 100000 },
            new() { MaHoaDon = 202, TenKhachHang = "B", TrangThai = "Đang giao hàng", TienHang = 200000, TongThanhToan = 200000 },
            new() { MaHoaDon = 203, TenKhachHang = "C", TrangThai = "Đã hoàn thành", TienHang = 300000, TongThanhToan = 300000 },
            new() { MaHoaDon = 204, TenKhachHang = "D", TrangThai = "Đã hủy", TienHang = 400000, TongThanhToan = 400000 }
        };
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync(It.IsAny<int?>())).ReturnsAsync(customOrders);
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);

        var cut = Render<OrderManagement>();

        // Phải có bộ lọc trạng thái đa chọn (Dropdown / Multi-select)
        var triggerBtn = cut.Find(".status-multiselect-trigger");
        triggerBtn.Should().NotBeNull();
        triggerBtn.Click();

        // Chọn "Đã hoàn thành" và "Đã hủy"
        var chkCompleted = cut.Find("input[data-status-checkbox='Đã hoàn thành']");
        var chkCancelled = cut.Find("input[data-status-checkbox='Đã hủy']");
        chkCompleted.Change(true);
        chkCancelled.Change(true);

        // Assert: Danh sách lọc phải chứa #203 và #204, không chứa #201, #202
        cut.Markup.Should().Contain("#203");
        cut.Markup.Should().Contain("#204");
        cut.Markup.Should().NotContain("#201");
        cut.Markup.Should().NotContain("#202");
    }

    [Fact(DisplayName = "UI-ORD-09: Bộ chọn chi nhánh có ô search gõ từ khóa lọc nhanh danh sách chi nhánh")]
    public void OrderManagement_SearchableBranchSelector_FiltersBranchListBySearchTerm()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        _mockAdminApi.Setup(api => api.GetBranchesAsync()).ReturnsAsync(new List<BranchDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)" },
            new() { MaChiNhanh = 2, TenChiNhanh = "QF Quang Trung (Q. Gò Vấp, TP.HCM)" },
            new() { MaChiNhanh = 3, TenChiNhanh = "QF Cầu Giấy (Hà Nội)" }
        });

        var cut = Render<OrderManagement>();

        // Phải có custom searchable dropdown chi nhánh
        var branchCombobox = cut.Find(".branch-searchable-combobox");
        branchCombobox.Should().NotBeNull();

        // Mở popup
        var btnOpen = cut.Find(".branch-combobox-trigger");
        btnOpen.Click();

        // Gõ từ khóa tìm kiếm "Hà Nội"
        var searchInput = cut.Find(".branch-search-input");
        searchInput.Input("Hà Nội");

        // Assert: Trong danh sách popup chỉ còn gợi ý Cầu Giấy Hà Nội, không còn Quang Trung hay Sư Vạn Hạnh
        var items = cut.FindAll(".branch-combobox-item");
        items.Should().Contain(i => i.TextContent.Contains("Cầu Giấy"));
        items.Should().NotContain(i => i.TextContent.Contains("Quang Trung"));
    }

    [Fact(DisplayName = "UI-ORD-10: Quản lý đơn hàng hiển thị chuẩn xác badge Tiền mặt / VietQR / MoMo cho đơn tại quầy POS")]
    public void OrderManagement_RenderPosOrders_DisplaysAccuratePaymentMethodBadges()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        var orders = new List<OrderSummaryDto>
        {
            new()
            {
                MaHoaDon = 201,
                NguoiLapDon = "Thu Ngân 01",
                LoaiDonHang = "TAI_QUAY",
                DiaChiGiaoHang = "[POS-TIEN-MAT] Thanh toán tiền mặt tại quầy",
                TongThanhToan = 150000,
                TrangThai = "Đã hoàn thành",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh"
            },
            new()
            {
                MaHoaDon = 202,
                NguoiLapDon = "Thu Ngân 02",
                LoaiDonHang = "TAI_QUAY",
                DiaChiGiaoHang = "[POS-VIETQR] Chuyển khoản VietQR tại quầy",
                TongThanhToan = 350000,
                TrangThai = "Đã hoàn thành",
                MaChiNhanh = 1,
                TenChiNhanh = "QF Sư Vạn Hạnh"
            }
        };
        _mockAdminApi.Setup(api => api.GetAllOrdersAsync(It.IsAny<int?>())).ReturnsAsync(orders);

        // Act
        var cut = Render<OrderManagement>();

        // Assert: Hiển thị badge Tiền mặt và VietQR
        var content = cut.Markup;
        content.Should().Contain("Tiền mặt");
        content.Should().Contain("VietQR (Chuyển khoản)");
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
