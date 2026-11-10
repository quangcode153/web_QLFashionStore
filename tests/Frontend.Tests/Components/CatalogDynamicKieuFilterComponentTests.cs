using System.Collections.Generic;
using System.Linq;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;
using Frontend.Pages;
using Frontend.Services;
using Microsoft.AspNetCore.Components;

namespace Frontend.Tests.Components;

public class CatalogDynamicKieuFilterComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi = new();
    private readonly Mock<ICartService> _mockCartService = new();
    private readonly Mock<IToastService> _mockToastService = new();
    private readonly Mock<IAuthStateService> _mockAuthState = new();

    public CatalogDynamicKieuFilterComponentTests()
    {
        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;

        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 3, TenDanhMuc = "Phụ kiện" },
                new() { MaDanhMuc = 10, TenDanhMuc = "Thắt Lưng & Ví", MaDanhMucCha = 3 }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync())
            .ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync())
            .ReturnsAsync(new List<BannerDto>());
    }

    [Fact(DisplayName = "TS-CATALOG-KIEU-01: Trong mục Phụ Kiện, Kiểu sản phẩm phải hiển thị phụ kiện (Ví da, Thắt lưng) thay vì quần")]
    public void Catalog_WhenViewingAccessoryCategory_KieuOptionsMustMatchAccessoriesNotPants()
    {
        // ARRANGE: Trả về danh sách sản phẩm phụ kiện
        var accessoryProducts = new List<ProductListItemDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Ví Cầm Tay Clutch Nam Da Bò Thật", TenDanhMuc = "Thắt lưng & Ví", TongTonKho = 5 },
            new() { MaSanPham = 2, TenSanPham = "Thắt Lưng Nam Da Bò Ý Khóa Tự Động", TenDanhMuc = "Thắt lưng & Ví", TongTonKho = 8 }
        };

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = accessoryProducts,
                TotalCount = 2,
                PageNumber = 1,
                PageSize = 12
            });

        // ACT: Điều hướng tới mục phụ kiện
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/catalog?category=phukien");
        var cut = Render<Catalog>();

        // Mở dropdown "Kiểu sản phẩm"
        cut.WaitForAssertion(() =>
        {
            var kieuBtn = cut.FindAll("button.filter-tag-chip").FirstOrDefault(b => b.TextContent.Contains("Kiểu sản phẩm"));
            kieuBtn.Should().NotBeNull();
        });

        var kieuChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Kiểu sản phẩm"));
        kieuChip.Click();

        // ASSERT: Phải có tùy chọn phụ kiện và KHÔNG ĐƯỢC có "Quần Tây"
        cut.WaitForAssertion(() =>
        {
            var optionsText = cut.Find(".dropdown-options-list").TextContent;
            optionsText.Should().NotContain("Quần Tây Loose Fit", "Mục phụ kiện không được hiển thị kiểu dáng quần tây");
            optionsText.Should().NotContain("Quần Tây Slim Fit", "Mục phụ kiện không được hiển thị kiểu dáng quần tây");
            optionsText.Should().Contain("Ví Da", "Mục phụ kiện phải có kiểu dáng ví da");
        });
    }

    [Fact(DisplayName = "TS-CATALOG-KIEU-02: Trong mục Áo, Kiểu sản phẩm phải hiển thị các kiểu áo thay vì phụ kiện hoặc quần")]
    public void Catalog_WhenViewingShirtCategory_KieuOptionsMustMatchShirts()
    {
        var shirtProducts = new List<ProductListItemDto>
        {
            new() { MaSanPham = 10, TenSanPham = "Áo Polo Nam Classic Cotton", TenDanhMuc = "Áo Polo", TongTonKho = 10 },
            new() { MaSanPham = 11, TenSanPham = "Áo Thun Cổ Tròn Oversize Form Rộng", TenDanhMuc = "Áo Thun", TongTonKho = 15 }
        };

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = shirtProducts,
                TotalCount = 2,
                PageNumber = 1,
                PageSize = 12
            });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/catalog?category=ao");
        var cut = Render<Catalog>();

        var kieuChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Kiểu sản phẩm"));
        kieuChip.Click();

        cut.WaitForAssertion(() =>
        {
            var optionsText = cut.Find(".dropdown-options-list").TextContent;
            optionsText.Should().Contain("Áo Polo", "Mục áo phải có kiểu dáng Áo Polo");
            optionsText.Should().Contain("Form Rộng / Oversize", "Mục áo phải có kiểu dáng Oversize");
            optionsText.Should().NotContain("Quần Tây", "Mục áo không được có Quần Tây");
            optionsText.Should().NotContain("Thắt Lưng", "Mục áo không được có Thắt Lưng");
        });
    }

    [Fact(DisplayName = "TS-CATALOG-KIEU-03: Chọn checkbox kiểu sản phẩm lọc danh sách hiển thị và reset khôi phục")]
    public void Catalog_WhenTogglingKieuFilter_FiltersProductsAndResetsProperly()
    {
        var products = new List<ProductListItemDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Ví Cầm Tay Clutch Nam Da Bò Thật", TenDanhMuc = "Thắt lưng & Ví", TongTonKho = 5 },
            new() { MaSanPham = 2, TenSanPham = "Thắt Lưng Nam Da Bò Ý Khóa Tự Động", TenDanhMuc = "Thắt lưng & Ví", TongTonKho = 8 }
        };

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>
            {
                Items = products,
                TotalCount = 2,
                PageNumber = 1,
                PageSize = 12
            });

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/catalog?category=phukien");
        var cut = Render<Catalog>();

        // Mở dropdown kiểu sản phẩm
        var kieuChip = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Kiểu sản phẩm"));
        kieuChip.Click();

        // Tìm checkbox Ví Da
        cut.WaitForAssertion(() =>
        {
            var labels = cut.FindAll(".dropdown-option-item");
            labels.Should().NotBeEmpty();
        });

        var viDaLabel = cut.FindAll(".dropdown-option-item").First(l => l.TextContent.Contains("Ví Da & Bóp Cầm Tay"));
        var checkbox = viDaLabel.QuerySelector("input[type='checkbox']");
        checkbox.Should().NotBeNull();

        // Check checkbox Ví Da
        checkbox!.Change(true);

        // Kiểm tra sản phẩm hiển thị chỉ còn 1 sản phẩm ví
        cut.WaitForAssertion(() =>
        {
            var renderedCards = cut.FindAll(".yame-product-card");
            renderedCards.Count.Should().Be(1);
            renderedCards[0].TextContent.Should().Contain("Ví Cầm Tay Clutch");
            renderedCards[0].TextContent.Should().NotContain("Thắt Lưng Nam");
        });

        // Bấm Đặt lại (reset)
        var resetBtn = cut.Find("button.dropdown-reset-btn");
        resetBtn.Click();

        // Kiểm tra sản phẩm hiển thị quay lại cả 2 sản phẩm
        cut.WaitForAssertion(() =>
        {
            var renderedCards = cut.FindAll(".yame-product-card");
            renderedCards.Count.Should().Be(2);
        });
    }
}
