using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Announcement;
using Shared.DTOs.Audit;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Warehouse;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class WarehousePortalRoleComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public WarehousePortalRoleComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();
        _mockAuthState = new Mock<IAuthStateService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;

        _mockAdminApi.Setup(api => api.GetLowStockAlertsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(api => api.GetSuppliersAsync())
            .ReturnsAsync(new List<SupplierDto>());
        _mockAdminApi.Setup(api => api.GetAnnouncementsAsync(It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<InternalAnnouncementDto>());
        _mockAdminApi.Setup(api => api.GetBranchesAsync())
            .ReturnsAsync(new List<BranchDto>
            {
                new BranchDto { MaChiNhanh = 1, TenChiNhanh = "Cơ sở QF Quận 1", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false },
                new BranchDto { MaChiNhanh = 2, TenChiNhanh = "Cơ sở QF Quận 10", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false },
                new BranchDto { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", LoaiChiNhanh = "Kho tổng online", IsOnlineWarehouse = true }
            });
        _mockAdminApi.Setup(api => api.GetBranchInventoryAsync(It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<BranchInventoryDto>());
    }

    #region Vòng 1 & 3: Role-based RBAC Isolation Matrix (TS-WH-ROLE-01 & TS-WH-ROLE-02)

    [Theory]
    [InlineData("Thủ kho")]
    [InlineData("thủ kho")]
    [InlineData("Kho")]
    [InlineData("warehouse_keeper")]
    public void WarehousePortal_WhenRoleIsWarehouseKeeper_ShouldNotShowAdminHubOrPosShortcut(string roleName)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(roleName);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Lê Văn Cường");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("3");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert: Không được xuất hiện nút có title "Về trang Quản trị chuỗi" hoặc link tới /admin/dashboard
        cut.Markup.Should().NotContain("Về trang Quản trị chuỗi", "Thủ kho không được có nút dẫn về Quản trị chuỗi");
        cut.Markup.Should().NotContain("href=\"/admin/dashboard\"", "Thủ kho không được có liên kết tới trang admin dashboard");
        cut.Markup.Should().NotContain("Quầy POS", "Thủ kho không bán lẻ quầy POS");
    }

    [Theory]
    [InlineData("Quản lý đa chi nhánh")]
    [InlineData("Admin")]
    [InlineData("Admin chuỗi")]
    public void WarehousePortal_WhenRoleIsChainAdmin_ShouldShowAdminHubShortcut(string roleName)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(roleName);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Quản Trị");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("1");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert: Quản trị chuỗi vào kiểm tra kho thì có nút quay lại Admin Hub
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Về trang Quản trị chuỗi");
            cut.Markup.Should().Contain("/admin/dashboard");
        });
    }

    [Theory]
    [InlineData("Quản lý chi nhánh")]
    [InlineData("Cửa hàng trưởng")]
    public void WarehousePortal_WhenRoleIsBranchManager_ShouldNotShowAdminHub_ButCanAccessPos(string roleName)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(roleName);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Nguyễn Quản Lý");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("2");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert: Quản lý chi nhánh không có quyền Quản trị chuỗi
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().NotContain("Về trang Quản trị chuỗi");
            cut.Markup.Should().NotContain("href=\"/admin/dashboard\"");
            cut.Markup.Should().Contain("QUẢN LÝ CHI NHÁNH");
        });
    }

    [Fact]
    public void WarehousePortal_WhenRoleIsWarehouseKeeper_ShouldShowCorrectRoleBadgeAndProfile()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("Thủ kho");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Lê Văn Cường");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("3");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("THỦ KHO");
            cut.Markup.Should().Contain("Lê Văn Cường");
        });
    }

    [Fact]
    public void WarehousePortal_WhenRoleIsChainAdmin_ShouldShowActiveScopeSelectorWithAllBranches()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("Admin");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Trần Quản Trị");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("1");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var select = cut.Find("select[data-testid='warehouse-scope-selector']");
            select.Should().NotBeNull();
            select.HasAttribute("disabled").Should().BeFalse("Admin toàn chuỗi có quyền tự do chuyển đổi kho");
            
            var options = cut.FindAll("select[data-testid='warehouse-scope-selector'] option");
            options.Should().Contain(o => o.TextContent.Contains("Toàn chuỗi"));
            options.Should().Contain(o => o.TextContent.Contains("Kho Tổng Online"));
            options.Should().Contain(o => o.TextContent.Contains("Cơ sở QF Quận 10"));
        });
    }

    [Fact]
    public void WarehousePortal_WhenRoleIsOnlineWarehouseKeeper_ShouldLockScopeToOnlineWarehouse()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("Thủ kho");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Nguyễn Kho Online");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("5");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchId").SetResult("9");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchName").SetResult("Kho Tổng QF Tân Bình");
        JSInterop.Setup<string?>("localStorage.getItem", "loaiThuKho").SetResult("Online");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert: Quản lý/Thủ kho Online bị khóa cứng chỉ xem Kho Online, không xem được chi nhánh khác hoặc toàn chuỗi
        cut.WaitForAssertion(() =>
        {
            // Selector bị khóa (disabled) hoặc chỉ có 1 option duy nhất của kho online
            var select = cut.Find("select[data-testid='warehouse-scope-selector']");
            select.HasAttribute("disabled").Should().BeTrue("Thủ kho online bị khóa cứng chỉ xem kho online");
            
            var options = cut.FindAll("select[data-testid='warehouse-scope-selector'] option");
            options.Should().NotContain(o => o.TextContent.Contains("Toàn chuỗi"), "Thủ kho online không được xem toàn chuỗi");
            options.Should().NotContain(o => o.TextContent.Contains("Cơ sở QF Quận 10"), "Thủ kho online không được xem kho chi nhánh offline");
            options.Should().Contain(o => o.TextContent.Contains("Kho Tổng") || o.TextContent.Contains("Online"));
        });
    }

    [Fact]
    public void WarehousePortal_WhenRoleIsBranchWarehouseKeeper_ShouldLockScopeToBranchOnly()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("Thủ kho");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Phạm Thủ Kho CN2");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("6");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchId").SetResult("2");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchName").SetResult("Cơ sở QF Quận 10");
        JSInterop.Setup<string?>("localStorage.getItem", "loaiThuKho").SetResult("Offline");

        // Act
        var cut = Render<WarehousePortal>();

        // Assert: Thủ kho chi nhánh bị khóa cứng chỉ xem kho của chi nhánh mình
        cut.WaitForAssertion(() =>
        {
            var select = cut.Find("select[data-testid='warehouse-scope-selector']");
            select.HasAttribute("disabled").Should().BeTrue("Thủ kho chi nhánh bị khóa cứng chỉ xem kho của chi nhánh mình");

            var options = cut.FindAll("select[data-testid='warehouse-scope-selector'] option");
            options.Should().NotContain(o => o.TextContent.Contains("Toàn chuỗi"), "Thủ kho chi nhánh không được xem toàn chuỗi");
            options.Should().NotContain(o => o.TextContent.Contains("Kho Tổng QF Tân Bình"), "Thủ kho chi nhánh không được xem kho tổng online");
            options.Should().Contain(o => o.TextContent.Contains("Cơ sở QF Quận 10"));
        });
    }

    [Fact(DisplayName = "TS-WH-LOG-01: Thủ kho chi nhánh chỉ được xem nhật ký kho của chi nhánh mình")]
    public void WarehousePortal_WhenBranchWarehouseKeeperViewsHistory_ShouldQueryLogsOnlyForOwnBranch()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("Thủ kho");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Phạm Thủ Kho CN2");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("6");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchId").SetResult("2");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchName").SetResult("Cơ sở QF Quận 10");
        JSInterop.Setup<string?>("localStorage.getItem", "loaiThuKho").SetResult("Offline");

        _mockAdminApi.Setup(api => api.GetAuditLogsAsync("NHAP_HANG", 2, 50))
            .ReturnsAsync(new List<AuditLogDto>
            {
                new AuditLogDto
                {
                    MaNhatKy = 101,
                    LoaiThaoTac = "NHAP_HANG",
                    DoiTuong = "Biến thể #5",
                    NoiDungChiTiet = "Nhập 50 sản phẩm vào kho chi nhánh #2",
                    MaNhanVien = 6,
                    TenNhanVien = "Phạm Thủ Kho CN2",
                    MaChiNhanh = 2,
                    TenChiNhanh = "Cơ sở QF Quận 10"
                }
            });

        // Act
        var cut = Render<WarehousePortal>();

        // Tìm nút Tab Lịch Sử
        cut.Find("button[data-testid='tab-history-btn']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _mockAdminApi.Verify(api => api.GetAuditLogsAsync("NHAP_HANG", 2, 50), Times.AtLeastOnce);
            _mockAdminApi.Verify(api => api.GetAuditLogsAsync("NHAP_HANG", null, 50), Times.Never);
            cut.Markup.Should().Contain("Nhập 50 sản phẩm vào kho chi nhánh #2");
        });
    }

    [Fact(DisplayName = "TS-WH-LOG-02: Quản lý chuỗi (Admin) xem toàn bộ nhật ký nhập kho toàn hệ thống khi ở phạm vi toàn chuỗi")]
    public void WarehousePortal_WhenChainAdminViewsHistoryInChainScope_ShouldQueryLogsForWholeChain()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult("AdminChuoi");
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Admin Chuỗi Toàn Quyền");
        JSInterop.Setup<string?>("localStorage.getItem", "staffId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "staffBranchId").SetResult("1");

        _mockAdminApi.Setup(api => api.GetAuditLogsAsync("NHAP_HANG", null, 50))
            .ReturnsAsync(new List<AuditLogDto>
            {
                new AuditLogDto
                {
                    MaNhatKy = 201,
                    LoaiThaoTac = "NHAP_HANG",
                    DoiTuong = "Biến thể #12",
                    NoiDungChiTiet = "Nhập 200 sản phẩm vào kho chi nhánh #1",
                    MaNhanVien = 1,
                    TenNhanVien = "Admin Chuỗi",
                    MaChiNhanh = 1,
                    TenChiNhanh = "Cơ sở QF Quận 1"
                },
                new AuditLogDto
                {
                    MaNhatKy = 202,
                    LoaiThaoTac = "NHAP_HANG",
                    DoiTuong = "Biến thể #15",
                    NoiDungChiTiet = "Nhập 80 sản phẩm vào kho chi nhánh #2",
                    MaNhanVien = 6,
                    TenNhanVien = "Phạm Thủ Kho CN2",
                    MaChiNhanh = 2,
                    TenChiNhanh = "Cơ sở QF Quận 10"
                }
            });

        // Act
        var cut = Render<WarehousePortal>();

        // Tìm nút Tab Lịch Sử
        cut.Find("button[data-testid='tab-history-btn']").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            _mockAdminApi.Verify(api => api.GetAuditLogsAsync("NHAP_HANG", null, 50), Times.AtLeastOnce);
            cut.Markup.Should().Contain("Nhập 200 sản phẩm vào kho chi nhánh #1");
            cut.Markup.Should().Contain("Nhập 80 sản phẩm vào kho chi nhánh #2");
            cut.Markup.Should().Contain("TOÀN HỆ THỐNG");
        });
    }

    #endregion
}

