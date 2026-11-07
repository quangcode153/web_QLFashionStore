using System.Collections.Generic;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class ProductManagementBrandFilterComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public ProductManagementBrandFilterComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        var pagedData = new PagedResult<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>
            {
                new()
                {
                    MaSanPham = 1,
                    TenSanPham = "Áo Thun Cotton Compact 220gsm",
                    TenDanhMuc = "Áo Thun",
                    TenThuongHieu = "QF NOIR",
                    GiaThapNhat = 187000m,
                    GiaCaoNhat = 187000m,
                    TongTonKho = 50,
                    DaXoa = false
                },
                new()
                {
                    MaSanPham = 2,
                    TenSanPham = "Quần Jeans Slim-fit Classic",
                    TenDanhMuc = "Quần Jeans",
                    TenThuongHieu = "DENIM LAB",
                    GiaThapNhat = 350000m,
                    GiaCaoNhat = 350000m,
                    TongTonKho = 30,
                    DaXoa = false
                }
            },
            TotalCount = 2,
            PageNumber = 1,
            PageSize = 10
        };

        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(pagedData);
        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pagedData);

        _mockAdminApi.Setup(api => api.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto> { new() { MaDanhMuc = 1, TenDanhMuc = "Áo Thun" } });

        _mockAdminApi.Setup(api => api.GetBrandsAsync())
            .ReturnsAsync(new List<BrandDto>
            {
                new() { MaThuongHieu = 1, TenThuongHieu = "QF NOIR" },
                new() { MaThuongHieu = 2, TenThuongHieu = "DENIM LAB" }
            });

        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
    }

    [Fact(DisplayName = "TS-BRAND-FILTER-01: Trang Quản lý Sản phẩm hiển thị Dropdown lọc Thương hiệu với đầy đủ danh sách")]
    public void ProductManagement_ShouldRenderBrandFilterDropdown()
    {
        // Act
        var cut = Render<ProductManagement>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var select = cut.Find("select.brand-filter-select");
            select.Should().NotBeNull();
            cut.Markup.Should().Contain("QF NOIR");
            cut.Markup.Should().Contain("DENIM LAB");
        });
    }

    [Fact(DisplayName = "TS-BRAND-FILTER-02: Lọc theo thương hiệu và tìm kiếm theo tên thương hiệu hoạt động chính xác")]
    public void ProductManagement_FilterByBrand_ShouldOnlyShowMatchingProducts()
    {
        // Act
        var cut = Render<ProductManagement>();

        // Khi chọn lọc thương hiệu #1 (QF NOIR)
        cut.WaitForAssertion(() =>
        {
            var select = cut.Find("select.brand-filter-select");
            select.Change("1");
        });

        // Bảng chỉ hiển thị SP của QF NOIR, ẩn DENIM LAB
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Áo Thun Cotton Compact 220gsm");
            cut.Markup.Should().NotContain("Quần Jeans Slim-fit Classic");
        });

        // Tìm kiếm theo tên thương hiệu "denim"
        var searchInput = cut.Find("input.search-catalog-input");
        searchInput.Input("denim");

        // Chọn lại Tất cả thương hiệu ("0")
        var selectAll = cut.Find("select.brand-filter-select");
        selectAll.Change("0");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Quần Jeans Slim-fit Classic");
            cut.Markup.Should().NotContain("Áo Thun Cotton Compact 220gsm");
        });
    }
}
