using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Announcement;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Pages.Warehouse;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class BrandManagementAndSupplierTabComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi = new();
    private readonly Mock<IToastService> _mockToastService = new();
    private readonly Mock<ISignalRNotificationService> _mockSignalR = new();
    private readonly Mock<IAuthStateService> _mockAuthState = new();

    public BrandManagementAndSupplierTabComponentTests()
    {
        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact(DisplayName = "TS-BRAND-MGMT-01: ProductManagement hiển thị nút Quản lý thương hiệu và mở modal xóa brand không còn hợp tác")]
    public void ProductManagement_ShouldRenderBrandManagementButtonAndOpenModal()
    {
        // Arrange
        var mockBrands = new List<BrandDto>
        {
            new BrandDto { MaThuongHieu = 1, TenThuongHieu = "QF NOIR", XuatXu = "Việt Nam", SoLuongSanPham = 3 },
            new BrandDto { MaThuongHieu = 2, TenThuongHieu = "Zara Old", XuatXu = "Tây Ban Nha", SoLuongSanPham = 0 }
        };

        var mockProducts = new List<ProductListItemDto>
        {
            new ProductListItemDto { MaSanPham = 1, TenSanPham = "Áo Thun Noir", TenThuongHieu = "QF NOIR", TongTonKho = 20 }
        };

        _mockAdminApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(mockBrands);
        _mockAdminApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockAdminApi.Setup(x => x.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = mockProducts,
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10
            });

        // Act
        var cut = Render<ProductManagement>();

        // Chờ render xong và kiểm tra có nút Quản lý thương hiệu
        cut.WaitForAssertion(() =>
        {
            cut.Find(".btn-brand-mgmt").Should().NotBeNull();
        });

        // Click nút Quản lý thương hiệu để mở modal
        cut.Find(".btn-brand-mgmt").Click();

        // Chờ modal xuất hiện
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Quản Lý Thương Hiệu Thời Trang");
            cut.Markup.Should().Contain("QF NOIR");
            cut.Markup.Should().Contain("Zara Old");
            cut.Markup.Should().Contain("Xóa đối tác"); // Zara có 0 SP nên có nút xóa
            cut.Markup.Should().Contain("Có 1 SP"); // QF NOIR có 1 SP trong mock nên bị khóa
        });
    }

    [Fact(DisplayName = "TS-SUPPLIER-TAB-02: WarehousePortal với tài khoản Admin hiển thị Tab Quản lý & Bổ sung nguồn hàng")]
    public void WarehousePortal_Admin_ShouldRenderSuppliersManagementTab()
    {
        // Arrange: Cấu hình tài khoản Admin chuỗi
        _mockAuthState.SetupGet(x => x.IsAuthenticated).Returns(true);
        _mockAuthState.SetupGet(x => x.UserRole).Returns("Admin chuỗi");
        _mockAuthState.SetupGet(x => x.StaffName).Returns("Nguyễn Văn Quản Trị");

        var suppliers = new List<SupplierDto>
        {
            new SupplierDto { MaNhaCungCap = 1, TenNhaCungCap = "Tổng Kho May Mặc QF", NguoiLienHe = "Trần An", SoDienThoai = "0901234567" },
            new SupplierDto { MaNhaCungCap = 2, TenNhaCungCap = "Công Ty TNHH Dệt May Việt Nam", NguoiLienHe = "Lê Bình", SoDienThoai = "0987654321" }
        };

        _mockAdminApi.Setup(x => x.GetSuppliersAsync()).ReturnsAsync(suppliers);
        _mockAdminApi.Setup(x => x.GetBranchesAsync()).ReturnsAsync(new List<BranchDto>
        {
            new BranchDto { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", IsOnlineWarehouse = true }
        });
        _mockAdminApi.Setup(x => x.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(x => x.GetAnnouncementsAsync(It.IsAny<string?>(), It.IsAny<int?>())).ReturnsAsync(new List<InternalAnnouncementDto>());

        // Act
        var cut = Render<WarehousePortal>();

        // Chờ render topbar & tabs
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("QUẢN LÝ & BỔ SUNG NGUỒN HÀNG");
        });

        // Click chuyển sang Tab Quản lý nguồn hàng
        var tabButtons = cut.FindAll("button.wh-tab-btn");
        var supplierTabBtn = tabButtons.FirstOrDefault(b => b.TextContent.Contains("QUẢN LÝ & BỔ SUNG NGUỒN HÀNG"));
        supplierTabBtn.Should().NotBeNull();
        supplierTabBtn!.Click();

        // Chờ nội dung Tab Suppliers xuất hiện
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("BỔ SUNG NGUỒN HÀNG (NHÀ CUNG CẤP) MỚI");
            cut.Markup.Should().Contain("DANH SÁCH NGUỒN HÀNG HỆ THỐNG");
            cut.Markup.Should().Contain("Tổng Kho May Mặc QF");
            cut.Markup.Should().Contain("Công Ty TNHH Dệt May Việt Nam");
            cut.Markup.Should().Contain("Sẵn sàng chọn nhập hàng");
        });
    }

    [Fact(DisplayName = "TS-SUPPLIER-TAB-03: WarehousePortal với tài khoản Thủ kho chi nhánh KHÔNG hiển thị Tab Quản lý nguồn hàng")]
    public void WarehousePortal_BranchWarehouseKeeper_ShouldNotRenderSuppliersManagementTab()
    {
        // Arrange: Cấu hình tài khoản Thủ kho chi nhánh
        _mockAuthState.SetupGet(x => x.IsAuthenticated).Returns(true);
        _mockAuthState.SetupGet(x => x.UserRole).Returns("Thủ kho");
        _mockAuthState.SetupGet(x => x.StaffName).Returns("Lê Văn Cường");

        _mockAdminApi.Setup(x => x.GetSuppliersAsync()).ReturnsAsync(new List<SupplierDto>());
        _mockAdminApi.Setup(x => x.GetBranchesAsync()).ReturnsAsync(new List<BranchDto>
        {
            new BranchDto { MaChiNhanh = 1, TenChiNhanh = "Cơ sở QF Quận 1", IsOnlineWarehouse = false }
        });
        _mockAdminApi.Setup(x => x.GetLowStockAlertsAsync(It.IsAny<int>())).ReturnsAsync(new List<LowStockAlertDto>());
        _mockAdminApi.Setup(x => x.GetAnnouncementsAsync(It.IsAny<string?>(), It.IsAny<int?>())).ReturnsAsync(new List<InternalAnnouncementDto>());

        // Act
        var cut = Render<WarehousePortal>();

        // Chờ render
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("QF WAREHOUSE");
        });

        // Tab bổ sung nguồn hàng của Admin không được xuất hiện
        cut.Markup.Should().NotContain("QUẢN LÝ & BỔ SUNG NGUỒN HÀNG");
    }
}
