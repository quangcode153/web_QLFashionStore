using System.Collections.Generic;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Auth;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class AccountManagementBranchScopingComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public AccountManagementBranchScopingComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        _mockAdminApi.Setup(api => api.GetBranchesAsync())
            .ReturnsAsync(new List<BranchDto>
            {
                new BranchDto { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)", IsOnlineWarehouse = false },
                new BranchDto { MaChiNhanh = 2, TenChiNhanh = "QF Quang Trung (Q. Gò Vấp, TP.HCM)", IsOnlineWarehouse = false },
                new BranchDto { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình (TP.HCM)", IsOnlineWarehouse = true }
            });

        _mockAdminApi.Setup(api => api.GetAccountsAsync())
            .ReturnsAsync(new List<AccountSummaryDto>
            {
                new AccountSummaryDto
                {
                    MaTaiKhoan = 1,
                    TenDangNhap = "admin",
                    TenNhanVien = "Nguyễn Quản Trị",
                    VaiTro = RoleConstants.AdminChuoi,
                    ChiNhanh = "Toàn hệ thống",
                    DangHoatDong = true
                },
                new AccountSummaryDto
                {
                    MaTaiKhoan = 2,
                    TenDangNhap = "thukho_online",
                    TenNhanVien = "Phạm Đình Kho Online",
                    VaiTro = RoleConstants.ThuKho,
                    LoaiThuKho = "ONLINE",
                    MaChiNhanh = 9,
                    ChiNhanh = "Kho Tổng QF Tân Bình (TP.HCM)",
                    IsOnlineWarehouse = true,
                    DangHoatDong = true
                },
                new AccountSummaryDto
                {
                    MaTaiKhoan = 3,
                    TenDangNhap = "thukho_q10",
                    TenNhanVien = "Lê Kho Chi Nhánh",
                    VaiTro = RoleConstants.ThuKho,
                    LoaiThuKho = "CHI_NHANH",
                    MaChiNhanh = 1,
                    ChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                    IsOnlineWarehouse = false,
                    DangHoatDong = true
                },
                new AccountSummaryDto
                {
                    MaTaiKhoan = 4,
                    TenDangNhap = "coordinator01",
                    TenNhanVien = "Nguyễn Điều Phối",
                    VaiTro = RoleConstants.OrderCoordinator,
                    ChiNhanh = "QF Sư Vạn Hạnh (Q.10, TP.HCM)",
                    MaChiNhanh = 1,
                    DangHoatDong = true
                }
            });
    }

    [Fact]
    public void AccountManagement_WhenAdminLoads_ShowsWarehouseKeeperClassificationTags()
    {
        // Arrange: Đăng nhập với quyền Admin Chuỗi
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(null);

        // Act
        var cut = Render<AccountManagement>();

        // Assert: Bảng phải hiển thị rõ 2 phân loại Thủ kho Online và Thủ kho Chi nhánh
        cut.Markup.Should().Contain("Thủ kho Online (Kho Tổng)");
        cut.Markup.Should().Contain("Thủ kho Chi nhánh");
        cut.Markup.Should().Contain("Kho Tổng Online QF Tân Bình");
    }

    [Fact]
    public void AccountManagement_FilterTabs_CanFilterWarehouseKeepers()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(null);

        var cut = Render<AccountManagement>();

        // Act: Nhấn vào tab "Thủ kho"
        var warehouseTab = System.Linq.Enumerable.FirstOrDefault(cut.FindAll(".tab-btn"), b => b.TextContent.Trim() == "Thủ kho");
        warehouseTab.Should().NotBeNull();
        warehouseTab!.Click();

        // Assert: Danh sách lọc chỉ chứa các tài khoản thủ kho
        cut.Markup.Should().Contain("thukho_online");
        cut.Markup.Should().Contain("thukho_q10");
        cut.Find("tbody").InnerHtml.Should().NotContain("Nguyễn Quản Trị");
    }

    [Fact]
    public void AccountManagement_WhenBranchManagerLoads_HidesBranchManagerFilter()
    {
        // Arrange: Quản lý chi nhánh đăng nhập
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh (Q.10, TP.HCM)");

        // Act
        var cut = Render<AccountManagement>();

        // Assert: Quản lý chi nhánh không thấy tab "Quản lý chi nhánh"
        cut.Markup.Should().NotContain("Quản lý chi nhánh</button>");
        cut.Markup.Should().Contain("Thêm nhân viên chi nhánh");
    }

    [Fact]
    public void AccountManagement_WhenCreateModalOpens_DropdownContainsOrderCoordinator()
    {
        // Arrange: Quản lý chi nhánh mở modal
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh (Q.10, TP.HCM)");

        var cut = Render<AccountManagement>();

        // Act: Click nút mở modal
        var addBtn = cut.Find(".btn-primary-action");
        addBtn.Click();

        // Assert: Dropdown vai trò phải chứa Order Coordinator
        var select = cut.Find("select.form-control");
        select.InnerHtml.Should().Contain(RoleConstants.OrderCoordinator);
    }

    [Fact]
    public void AccountManagement_FilterTabs_CanFilterOrderCoordinator()
    {
        // Arrange: Admin chuỗi tải trang
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(null);

        var cut = Render<AccountManagement>();

        // Act: Click tab Order Coordinator
        var coordinatorTab = System.Linq.Enumerable.FirstOrDefault(cut.FindAll(".tab-btn"), b => b.TextContent.Trim() == "Order Coordinator");
        coordinatorTab.Should().NotBeNull();
        coordinatorTab!.Click();

        // Assert: Phải hiển thị coordinator01 và ẩn thukho_online
        cut.Markup.Should().Contain("coordinator01");
        cut.Find("tbody").InnerHtml.Should().NotContain("thukho_online");
    }

    [Fact]
    public void AccountManagement_RoleDropdown_UnifiesStaffAndCashier_NoDuplicateOption()
    {
        // Arrange: Quản lý chi nhánh mở modal
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh (Q.10, TP.HCM)");

        var cut = Render<AccountManagement>();

        // Act: Click nút mở modal
        var addBtn = cut.Find(".btn-primary-action");
        addBtn.Click();

        // Assert: Dropdown vai trò phải hợp nhất Nhân viên & Thu ngân thành 1 option duy nhất
        var select = cut.Find("select.form-control");
        var options = select.QuerySelectorAll("option");

        // Không được phép tồn tại option riêng biệt cho Thu ngân
        options.Should().NotContain(o => o.GetAttribute("value") == RoleConstants.ThuNgan);
        options.Should().NotContain(o => o.TextContent.Trim() == "Thu ngân");

        // Bắt buộc có option hợp nhất đại diện cho nhân viên bán hàng kiêm thu ngân
        options.Should().Contain(o => o.GetAttribute("value") == RoleConstants.NhanVien && o.TextContent.Contains("Nhân viên bán hàng (Thu ngân)"));
    }

    [Fact]
    public void AccountManagement_Header_DoesNotContainCustomerPageShortcut()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(null);

        // Act
        var cut = Render<AccountManagement>();

        // Assert: Không được phép có nút shortcut nhảy sang trang khách hàng & VIP
        cut.FindAll(".btn-nav-customers").Should().BeEmpty();
        cut.Markup.Should().NotContain("Tài khoản khách hàng & VIP");
    }

    [Fact(DisplayName = "VÒNG 3: Khi chọn vai trò Nhân viên bán hàng (Thu ngân), Chi nhánh Online bị ẩn khỏi danh sách lựa chọn")]
    public void AccountManagement_WhenStaffRoleSelected_HidesOnlineBranchFromDropdown()
    {
        // Arrange: Admin chuỗi mở modal thêm nhân sự
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);

        var cut = Render<AccountManagement>();
        var addBtn = cut.Find(".btn-primary-action");
        addBtn.Click();

        // Act: Chọn vai trò "Nhân viên bán hàng (Thu ngân)"
        var roleSelect = cut.Find("select.form-control");
        roleSelect.Change(RoleConstants.NhanVien);

        // Assert: Dropdown chi nhánh không chứa Chi nhánh Online #9
        var branchSelect = cut.Find("select.select-branch-control");
        var branchOptions = branchSelect.QuerySelectorAll("option");
        branchOptions.Should().NotContain(o => o.GetAttribute("value") == "9");
        branchOptions.Should().NotContain(o => o.TextContent.Contains("Kho Tổng Online"));
    }

    [Theory(DisplayName = "VÒNG 3: Quản lý chi nhánh, Thủ kho, Điều phối viên đều có thể chọn Chi nhánh Online như một chi nhánh bình thường")]
    [InlineData(RoleConstants.QuanLyChiNhanh)]
    [InlineData(RoleConstants.ThuKho)]
    [InlineData(RoleConstants.OrderCoordinator)]
    public void AccountManagement_WhenManagerOrKeeperOrCoordinatorSelected_CanSelectOnlineBranch(string role)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);

        var cut = Render<AccountManagement>();
        var addBtn = cut.Find(".btn-primary-action");
        addBtn.Click();

        // Act: Chọn vai trò
        var roleSelect = cut.Find("select.form-control");
        roleSelect.Change(role);

        // Assert: Dropdown chi nhánh chứa Chi nhánh Online #9
        var branchSelect = cut.Find("select.select-branch-control");
        var branchOptions = branchSelect.QuerySelectorAll("option");
        branchOptions.Should().Contain(o => o.GetAttribute("value") == "9");
    }
}

