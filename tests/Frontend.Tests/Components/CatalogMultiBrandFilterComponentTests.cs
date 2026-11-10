using System;
using System.Collections.Generic;
using System.Linq;
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

public class CatalogMultiBrandFilterComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi = new();
    private readonly Mock<ICartService> _mockCartService = new();
    private readonly Mock<IToastService> _mockToastService = new();
    private readonly Mock<IAuthStateService> _mockAuthState = new();

    public CatalogMultiBrandFilterComponentTests()
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

    [Fact(DisplayName = "TS-CATALOG-MULTIBRAND-01: Cho phép chọn đồng thời nhiều thương hiệu qua checkbox và gửi BrandIds tới API")]
    public async Task Catalog_MultiBrandFilter_AllowsSelectingMultipleBrandsAndQueriesApi()
    {
        // Arrange
        var brands = new List<BrandDto>
        {
            new BrandDto { MaThuongHieu = 1, TenThuongHieu = "Canifa", SoLuongSanPham = 10 },
            new BrandDto { MaThuongHieu = 2, TenThuongHieu = "Coolmate", SoLuongSanPham = 15 },
            new BrandDto { MaThuongHieu = 3, TenThuongHieu = "Gucci", SoLuongSanPham = 5 }
        };

        ProductQueryParameters? capturedQuery = null;
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(brands);
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .Callback<ProductQueryParameters>(q => capturedQuery = q)
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = new List<ProductListItemDto>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 12
            });

        // Act: Điều hướng tới /catalog?category=ao-nam để kích hoạt dedicated horizontal filter toolbar
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("/catalog?category=ao-nam");
        var cut = Render<Catalog>();

        // Chờ component tải xong toolbar
        cut.WaitForAssertion(() =>
        {
            var brandBtn = cut.FindAll("button.filter-tag-chip").FirstOrDefault(b => b.TextContent.Contains("Thương hiệu"));
            brandBtn.Should().NotBeNull("Nút filter-tag-chip Thương hiệu phải tồn tại");
        });

        // Mở dropdown thương hiệu
        var brandChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Thương hiệu"));
        brandChip.Click();

        // Chờ dropdown mở ra và kiểm tra các checkbox
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".dropdown-options-list input[type='checkbox']").Count.Should().BeGreaterThanOrEqualTo(2, "Bộ lọc thương hiệu phải có các checkbox cho từng thương hiệu");
        });

        var checkboxes = cut.FindAll(".dropdown-options-list input[type='checkbox']");
        // Chọn Canifa (MaThuongHieu = 1)
        checkboxes[0].Change(true);
        // Chọn Coolmate (MaThuongHieu = 2)
        checkboxes = cut.FindAll(".dropdown-options-list input[type='checkbox']");
        checkboxes[1].Change(true);

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery!.BrandIds.Should().NotBeNull();
        capturedQuery.BrandIds.Should().Contain(new[] { 1, 2 });

        // Chip thương hiệu phải hiển thị "2 thương hiệu"
        brandChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("thương hiệu"));
        brandChip.TextContent.Should().Contain("2 thương hiệu");

        // Bấm nút "Đặt lại" trong dropdown để xóa chọn
        var resetBtn = cut.Find(".dropdown-reset-btn");
        resetBtn.Should().NotBeNull();
        resetBtn.Click();

        // Kiểm tra sau khi đặt lại, query xóa danh sách thương hiệu và chip trở về "Thương hiệu"
        capturedQuery.BrandIds.Should().BeEmpty();
        capturedQuery.BrandId.Should().BeNull();
        brandChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Thương hiệu"));
        brandChip.TextContent.Should().Contain("Thương hiệu");
    }
}
