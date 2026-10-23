using Bunit;
using FluentAssertions;
using Frontend.Layout;
using Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Xunit;

namespace Frontend.Tests.Components;

public class AdminLayoutRoleNavigationComponentTests : BunitContext
{
    private readonly Mock<IAuthStateService> _mockAuthState;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;
    private readonly Mock<IAdminApiClient> _mockAdminApi;

    public AdminLayoutRoleNavigationComponentTests()
    {
        _mockAuthState = new Mock<IAuthStateService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockAdminApi.Setup(x => x.GetBranchesAsync()).ReturnsAsync(new List<global::Shared.DTOs.Warehouse.BranchDto>());

        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact(DisplayName = "NAV-01: OrderCoordinator cùng cấp thủ kho/thu ngân, không có quyền thông báo nội bộ (không có menu /admin/announcements)")]
    public void AdminLayout_WhenOrderCoordinator_DoesNotContainAnnouncementsMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Lê Thị Ánh");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/announcements']").Should().BeEmpty();
        cut.Markup.Should().NotContain("href=\"/admin/announcements\"");
    }

    [Fact(DisplayName = "NAV-02: WarehouseKeeper (Thủ kho) cùng cấp nhân viên, không có quyền thông báo nội bộ")]
    public void AdminLayout_WhenWarehouseKeeper_DoesNotContainAnnouncementsMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.ThuKho);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Văn Kho");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/announcements']").Should().BeEmpty();
        cut.Markup.Should().NotContain("href=\"/admin/announcements\"");
    }

    [Fact(DisplayName = "NAV-03: ChainAdmin và BranchManager có quyền quản lý và phát thông báo nội bộ")]
    public void AdminLayout_WhenAdminOrBranchManager_ContainsAnnouncementsMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Admin Chuỗi");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/announcements']").Should().NotBeEmpty();
    }

    [Fact(DisplayName = "NAV-04: BranchManager có menu Quản lý Đơn hàng và menu Quản lý Đổi trả & Hoàn kho lỗi")]
    public void AdminLayout_WhenBranchManager_ContainsOrderAndOrderSupportMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Thị Bích");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/orders']").Should().NotBeEmpty();
        cut.FindAll("a[href='/staff/order-support']").Should().NotBeEmpty();
    }

    [Fact(DisplayName = "NAV-05: OrderCoordinator có menu Đổi trả & Hoàn kho lỗi (/staff/order-support) và menu Quản lý đơn hàng")]
    public void AdminLayout_WhenOrderCoordinator_ContainsOrderSupportMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Lê Thị Ánh");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/staff/order-support']").Should().NotBeEmpty();
        cut.FindAll("a[href='/admin/orders']").Should().NotBeEmpty();
    }

    [Fact(DisplayName = "NAV-06: OrderCoordinator KHÔNG có menu Khiếu nại & Góp ý (/admin/feedback)")]
    public void AdminLayout_WhenOrderCoordinator_DoesNotContainFeedbackMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Lê Thị Ánh");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/feedback']").Should().BeEmpty();
        cut.Markup.Should().NotContain("href=\"/admin/feedback\"");
    }

    [Fact(DisplayName = "NAV-07: WarehouseKeeper (Thủ kho) KHÔNG có menu Khiếu nại & Góp ý (/admin/feedback)")]
    public void AdminLayout_WhenWarehouseKeeper_DoesNotContainFeedbackMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.ThuKho);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Văn Kho");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/feedback']").Should().BeEmpty();
        cut.Markup.Should().NotContain("href=\"/admin/feedback\"");
    }

    [Theory(DisplayName = "NAV-08: Chỉ có ChainAdmin và BranchManager mới có menu Khiếu nại & Góp ý (/admin/feedback)")]
    [InlineData(RoleConstants.AdminChuoi)]
    [InlineData(RoleConstants.QuanLyChiNhanh)]
    public void AdminLayout_WhenAdminOrBranchManager_ContainsFeedbackMenu(string role)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(role);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Người quản lý");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/feedback']").Should().NotBeEmpty();
    }

    [Fact(DisplayName = "NAV-09: BranchManager (Quản lý chi nhánh) KHÔNG có menu Quản lý Sản phẩm & Biến thể (/admin/products)")]
    public void AdminLayout_WhenBranchManager_DoesNotContainProductsMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Thị Bích");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/products']").Should().BeEmpty();
        cut.Markup.Should().NotContain("href=\"/admin/products\"");
    }

    [Fact(DisplayName = "NAV-10: ChainAdmin (Quản trị viên toàn chuỗi) CÓ menu Quản lý Sản phẩm & Biến thể (/admin/products)")]
    public void AdminLayout_WhenChainAdmin_ContainsProductsMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Admin Chuỗi");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/products']").Should().NotBeEmpty();
    }
}

