using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;
using Frontend.Pages;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class CatalogBrandOutOfStockComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi = new();
    private readonly Mock<ICartService> _mockCartService = new();
    private readonly Mock<IToastService> _mockToastService = new();
    private readonly Mock<IAuthStateService> _mockAuthState = new();

    public CatalogBrandOutOfStockComponentTests()
    {
        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;

        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync())
            .ReturnsAsync(new List<BannerDto>());
    }

    [Fact(DisplayName = "TS-CATALOG-BRAND-01: Nhãn hàng hết hàng (0 SP) không được hiển thị trong bộ lọc của khách")]
    public void Catalog_OutOfStockBrand_ShouldNotAppearInBrandFilterOptions()
    {
        // Arrange
        var brands = new List<BrandDto>
        {
            new BrandDto { MaThuongHieu = 1, TenThuongHieu = "QF NOIR", SoLuongSanPham = 10 },
            new BrandDto { MaThuongHieu = 2, TenThuongHieu = "Zara Old", SoLuongSanPham = 0 } // Đã hết hàng
        };

        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(brands);
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = new List<ProductListItemDto>
                {
                    new ProductListItemDto { MaSanPham = 1, TenSanPham = "Áo Thun Noir", TongTonKho = 10 }
                },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 12
            });

        // Act
        var cut = Render<Catalog>();

        // Assert: Trong danh sách bộ lọc thương hiệu, có QF NOIR nhưng KHÔNG CÓ Zara Old (để khách không tốn thời gian)
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("QF NOIR");
            cut.Markup.Should().NotContain("Zara Old");
        });
    }

    [Fact(DisplayName = "TS-CATALOG-BRAND-02: Khi lọc nhãn hàng hết hàng, thông báo lịch sự 'Nhãn hàng tạm thời hết hàng' thay vì 'Không tìm thấy'")]
    public void Catalog_WhenFilteringByOutOfStockBrand_ShouldDisplayPoliteBrandOutOfStockNotice()
    {
        // Arrange: Nhãn hàng "Uno" có 0 sản phẩm
        var brands = new List<BrandDto>
        {
            new BrandDto { MaThuongHieu = 5, TenThuongHieu = "Uno", SoLuongSanPham = 0 }
        };

        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(brands);
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = new List<ProductListItemDto>(), // Rỗng vì hết hàng
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 12
            });

        // Act: Điều hướng vào catalog với BrandId = 5
        var cut = Render<Catalog>();

        // Kích hoạt lọc theo brand Uno
        var instance = cut.Instance;
        // Gọi chọn brand Uno
        cut.InvokeAsync(async () =>
        {
            var selectBrandMethod = typeof(Catalog).GetMethod("SelectBrand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (selectBrandMethod != null)
            {
                var task = (Task)selectBrandMethod.Invoke(instance, new object[] { 5 })!;
                await task;
            }
        });

        // Assert: Phải hiển thị thông báo "Nhãn hàng Uno tạm thời hết hàng", KHÔNG hiển thị "Không tìm thấy sản phẩm phù hợp"
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Nhãn hàng Uno tạm thời hết hàng");
            cut.Markup.Should().Contain("Khám phá thương hiệu khác");
            cut.Markup.Should().NotContain("Không tìm thấy sản phẩm phù hợp");
        });
    }
}

