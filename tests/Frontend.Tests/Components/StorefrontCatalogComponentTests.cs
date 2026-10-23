using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Frontend.Components;
using Frontend.Pages;
using Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Xunit;

namespace Frontend.Tests.Components;

public class StorefrontCatalogComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public StorefrontCatalogComponentTests()
    {
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync()).ReturnsAsync(new List<BannerDto>());
        _mockToastService = new Mock<IToastService>();
        _mockCartService = new Mock<ICartService>();
        _mockAuthState = new Mock<IAuthStateService>();

        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);
    }

    #region VÒNG 1: PRODUCT CARD - THAM SỐ BIÊN & HIỂN THỊ (BOUNDARY & RENDERING)

    [Fact]
    public void ProductCard_RendersCompleteProductInformation()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 101,
            TenSanPham = "Áo Polo Nam YaMe Cool Comfort",
            TenThuongHieu = "YaMe Design",
            TenDanhMuc = "Áo Polo",
            ChatLieu = "95% Cotton, 5% Spandex",
            GiaThapNhat = 199000,
            GiaCaoNhat = 249000,
            TongTonKho = 50,
            AnhDaiDien = "https://yame.vn/images/polo1.jpg"
        };

        // Act
        var cut = Render<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
        );

        // Assert
        cut.Markup.Should().Contain("Áo Polo Nam YaMe Cool Comfort");
        cut.Markup.Should().Contain("YaMe Design");
        cut.Markup.Should().Match(m => m.Contains("199.000") || m.Contains("199,000"));
        cut.Markup.Should().Match(m => m.Contains("249.000") || m.Contains("249,000"));
        cut.Find(".product-img").GetAttribute("src").Should().Be("https://yame.vn/images/polo1.jpg");
    }

    [Fact]
    public void ProductCard_ImageOptimization_HasLazyLoadingAndAsyncDecoding()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 101,
            TenSanPham = "Áo Polo Nam YaMe Cool Comfort",
            GiaThapNhat = 199000,
            GiaCaoNhat = 249000,
            TongTonKho = 50,
            AnhDaiDien = "https://yame.vn/images/polo1.jpg"
        };

        // Act
        var cut = Render<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
        );

        // Assert (Kiểm tra tối ưu hóa tốc độ load ảnh: lazy-load và giải mã bất đồng bộ)
        var img = cut.Find(".product-img");
        img.GetAttribute("loading").Should().Be("lazy");
        img.GetAttribute("decoding").Should().Be("async");
    }

    [Theory]
    [InlineData(0, true)]   // Hết hàng -> Hiển thị badge Hết hàng và disable nút mua
    [InlineData(5, false)]  // Còn 5 món -> Còn hàng
    [InlineData(100, false)]// Còn 100 món -> Còn hàng
    public void ProductCard_StockState_RendersAccurately(int stock, bool isOutOfStock)
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 102,
            TenSanPham = "Quần Tây Nam Basic",
            GiaThapNhat = 350000,
            GiaCaoNhat = 350000,
            TongTonKho = stock
        };

        // Act
        var cut = Render<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
        );

        // Assert
        if (isOutOfStock)
        {
            cut.Markup.Should().Contain("HẾT HÀNG");
            var buyBtn = cut.Find("button.btn-quick-add");
            buyBtn.HasAttribute("disabled").Should().BeTrue();
        }
        else
        {
            cut.Markup.Should().NotContain("HẾT HÀNG");
            var buyBtn = cut.Find("button.btn-quick-add");
            buyBtn.HasAttribute("disabled").Should().BeFalse();
        }
    }

    [Fact]
    public async Task ProductCard_QuickAddClick_FiresAddToCartCallback()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 103,
            TenSanPham = "Áo Thun Cổ Tròn Minimalist",
            GiaThapNhat = 150000,
            GiaCaoNhat = 150000,
            TongTonKho = 20
        };
        ProductListItemDto? selectedProduct = null;

        var cut = Render<ProductCard>(parameters => parameters
            .Add(p => p.Product, product)
            .Add(p => p.OnAddToCart, p => selectedProduct = p)
        );

        // Act
        var button = cut.Find("button.btn-quick-add");
        await cut.InvokeAsync(() => button.Click());

        // Assert
        selectedProduct.Should().NotBeNull();
        selectedProduct!.MaSanPham.Should().Be(103);
    }

    #endregion

    #region VÒNG 2: CATALOG PAGE - TÍCH HỢP BỘ LỌC & PHÂN TRANG (PAGINATION & FILTERS)

    [Fact]
    public void CatalogPage_RendersProductGridAndFilterSummary()
    {
        // Arrange
        var mockItems = new List<ProductListItemDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Áo Polo A", GiaThapNhat = 200000, GiaCaoNhat = 200000, TongTonKho = 10 },
            new() { MaSanPham = 2, TenSanPham = "Quần Jean B", GiaThapNhat = 400000, GiaCaoNhat = 400000, TongTonKho = 15 }
        };
        var pagedResult = new PagedResult<ProductListItemDto>(mockItems, 2, 1, 10);

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(pagedResult);
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto> { new() { MaDanhMuc = 1, TenDanhMuc = "Áo Nam" } });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync())
            .ReturnsAsync(new List<BrandDto> { new() { MaThuongHieu = 1, TenThuongHieu = "YaMe" } });

        // Act
        var cut = Render<Catalog>();

        // Assert
        cut.Markup.Should().Contain("Áo Polo A");
        cut.Markup.Should().Contain("Quần Jean B");
        cut.Markup.Should().Contain("2 sản phẩm");
    }

    [Fact]
    public void CatalogPage_EmptyResults_ShowsFriendlyEmptyMessage()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10);

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(pagedResult);
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());

        // Act
        var cut = Render<Catalog>();

        // Assert
        cut.Markup.Should().Contain("Không tìm thấy sản phẩm phù hợp");
    }

    [Fact]
    public async Task CatalogPage_ChangingPage_CallsApiWithNewPageNumber()
    {
        // Arrange
        var mockItems = new List<ProductListItemDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Sản phẩm Trang 1", GiaThapNhat = 100000, GiaCaoNhat = 100000, TongTonKho = 5 }
        };
        var pagedResult = new PagedResult<ProductListItemDto>(mockItems, 25, 1, 10); // 25 items -> 3 trang

        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(pagedResult);
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());

        var cut = Render<Catalog>();

        // Act: Click nút trang 2
        var page2Btn = cut.FindAll("button.page-btn").FirstOrDefault(b => b.TextContent.Trim() == "2");
        page2Btn.Should().NotBeNull();
        await cut.InvokeAsync(() => page2Btn!.Click());

        // Assert: API đã được gọi với PageNumber = 2
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.PageNumber == 2)), Times.Once);
    }

    #endregion

    #region VÒNG 3: PRODUCT DETAIL - LỰA CHỌN BIẾN THỂ SKU & TỒN KHO TỨC THÌ (STATE MACHINE)

    [Fact]
    public void ProductDetail_InitialState_ShowsPromptToSelectColorAndSize()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-DEN-M", MauSac = "Đen", KichCo = "M", GiaBan = 180000, SoLuongTon = 12, TinhTrangHang = "ConHang" },
            new() { MaBienThe = 2, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-TRANG-L", MauSac = "Trắng", KichCo = "L", GiaBan = 180000, SoLuongTon = 0, TinhTrangHang = "HetHang" }
        };
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(10)).ReturnsAsync(variants);

        // Act
        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 10));

        // Assert: Ban đầu chưa chọn đủ -> Nút Thêm vào giỏ bị vô hiệu hóa
        cut.Markup.Should().Contain("Áo Thun YaMe");
        var addToCartBtn = cut.Find("button.btn-add-to-cart");
        addToCartBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ProductDetail_SelectColorAndSize_UpdatesSkuAndRealtimeStock()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-DEN-M", MauSac = "Đen", KichCo = "M", GiaBan = 180000, SoLuongTon = 12, TinhTrangHang = "ConHang" },
            new() { MaBienThe = 2, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-TRANG-L", MauSac = "Trắng", KichCo = "L", GiaBan = 180000, SoLuongTon = 0, TinhTrangHang = "HetHang" }
        };
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(10)).ReturnsAsync(variants);

        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 10));

        // Act: Chọn màu "Đen" và size "M"
        var colorBtn = cut.FindAll("button.swatch-color-btn").First(b => b.TextContent.Trim() == "Đen");
        await cut.InvokeAsync(() => colorBtn.Click());

        var sizeBtn = cut.FindAll("button.swatch-size-btn").First(b => b.TextContent.Trim() == "M");
        await cut.InvokeAsync(() => sizeBtn.Click());

        // Assert: Hiển thị SKU "AT-DEN-M" và còn lại 12 sản phẩm, nút Mua được kích hoạt
        cut.Markup.Should().Contain("AT-DEN-M");
        cut.Markup.Should().Contain("Còn lại 12 sản phẩm");
        var addToCartBtn = cut.Find("button.btn-add-to-cart");
        addToCartBtn.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task ProductDetail_SelectOutOfStockVariant_DisablesAddToCart()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-DEN-M", MauSac = "Đen", KichCo = "M", GiaBan = 180000, SoLuongTon = 12, TinhTrangHang = "ConHang" },
            new() { MaBienThe = 2, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-TRANG-L", MauSac = "Trắng", KichCo = "L", GiaBan = 180000, SoLuongTon = 0, TinhTrangHang = "HetHang" }
        };
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(10)).ReturnsAsync(variants);

        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 10));

        // Act: Chọn màu "Trắng" và size "L" (Tồn = 0)
        var colorBtn = cut.FindAll("button.swatch-color-btn").First(b => b.TextContent.Trim() == "Trắng");
        await cut.InvokeAsync(() => colorBtn.Click());

        var sizeBtn = cut.FindAll("button.swatch-size-btn").First(b => b.TextContent.Trim() == "L");
        await cut.InvokeAsync(() => sizeBtn.Click());

        // Assert: Báo Hết hàng và Nút Thêm vào giỏ bị vô hiệu hóa
        cut.Markup.Should().Contain("Hết hàng");
        var addToCartBtn = cut.Find("button.btn-add-to-cart");
        addToCartBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ProductDetail_AddToCart_ShowsSuccessToastNotification()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-DEN-M", MauSac = "Đen", KichCo = "M", GiaBan = 180000, SoLuongTon = 12, TinhTrangHang = "ConHang" }
        };
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(10)).ReturnsAsync(variants);

        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 10));

        // Chọn biến thể
        var colorBtn = cut.FindAll("button.swatch-color-btn").First(b => b.TextContent.Trim() == "Đen");
        await cut.InvokeAsync(() => colorBtn.Click());
        var sizeBtn = cut.FindAll("button.swatch-size-btn").First(b => b.TextContent.Trim() == "M");
        await cut.InvokeAsync(() => sizeBtn.Click());

        // Act: Bấm Thêm vào giỏ hàng
        var addToCartBtn = cut.Find("button.btn-add-to-cart");
        await cut.InvokeAsync(() => addToCartBtn.Click());

        // Assert: Toast Service được gọi với thông báo thành công
        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(s => s.Contains("giỏ hàng"))), Times.Once);
    }

    #endregion

    #region VÒNG 4: CATALOG QUERY PARAMETERS & REACTIVE FILTERS (MULTI-ROUND URL QUERY)

    [Theory]
    [InlineData("ao", 1)]
    [InlineData("quan", 3)]
    [InlineData("jean", 3)]
    [InlineData("phukien", 4)]
    [InlineData("unknown_xyz", -999)]
    public void CatalogPage_UrlQueryCategory_MapsToCorrectCategoryIdAndCallsApi(string? categorySlug, int expectedCategoryId)
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 1, TenDanhMuc = "Áo Nam" },
                new() { MaDanhMuc = 2, TenDanhMuc = "Váy Nữ" },
                new() { MaDanhMuc = 3, TenDanhMuc = "Quần Jean" },
                new() { MaDanhMuc = 4, TenDanhMuc = "Phụ Kiện" }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo($"http://localhost/catalog?category={categorySlug}");

        // Act
        var cut = Render<Catalog>();

        // Assert: API phải được gọi với CategoryId tương ứng
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == expectedCategoryId)), Times.Once);
    }

    [Fact]
    public void CatalogPage_DirectCategoryIdQuery_CallsApiWithExactCategoryId()
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("http://localhost/catalog?categoryId=2");

        // Act
        var cut = Render<Catalog>();

        // Assert
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == 2)), Times.Once);
    }

    [Fact]
    public async Task CatalogPage_ReactiveUrlQueryChange_PipelineUpdatesAndRefetchesProducts()
    {
        // Arrange: State Machine Pipeline
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 1, TenDanhMuc = "Áo Nam" },
                new() { MaDanhMuc = 3, TenDanhMuc = "Quần Jean" }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        // Bước 1: Khởi tạo với Tất cả (CategoryQuery = null)
        var cut = Render<Catalog>();
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == null)), Times.Once);

        // Bước 2: Đổi sang ÁO (?category=ao)
        await cut.InvokeAsync(() => navMan.NavigateTo("http://localhost/catalog?category=ao"));
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == 1)), Times.Once);

        // Bước 3: Đổi tiếp sang QUẦN (?category=quan)
        await cut.InvokeAsync(() => navMan.NavigateTo("http://localhost/catalog?category=quan"));
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == 3)), Times.Once);

        // Bước 4: Đổi sang Search Query (?search=polo)
        await cut.InvokeAsync(() => navMan.NavigateTo("http://localhost/catalog?search=polo"));
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.Search == "polo")), Times.Once);
    }

    [Fact]
    public async Task CatalogPage_InvalidPriceRange_MinGreaterThanMax_ShowsWarningAndDoesNotCallApi()
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var cut = Render<Catalog>();
        _mockCatalogApi.Invocations.Clear(); // Xóa lượt gọi lúc khởi tạo

        // Nhập khoảng giá phi lý: Từ 500.000 đến 200.000 (Min > Max)
        await cut.InvokeAsync(() =>
        {
            var inputs = cut.FindAll("input.price-input");
            inputs[0].Change("500000");
        });

        await cut.InvokeAsync(() =>
        {
            var inputs = cut.FindAll("input.price-input");
            inputs[1].Change("200000");
        });

        // Act: Bấm Áp dụng giá
        await cut.InvokeAsync(() => cut.Find("button.btn-apply-filters").Click());

        // Assert (Spec requirement): Phải cảnh báo khoảng giá không hợp lệ và KHÔNG được gọi API với khoảng giá sai
        _mockToastService.Verify(t => t.ShowWarning(It.Is<string>(s => s.Contains("Khoảng giá không hợp lệ"))), Times.Once);
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.MinPrice > q.MaxPrice)), Times.Never);
    }

    [Fact]
    public void ProductDetail_IncreaseQuantityWithoutSelectedVariant_MustBeDisabled()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 10, TenSanPham = "Áo Thun YaMe", MaSku = "AT-DEN-M", MauSac = "Đen", KichCo = "M", GiaBan = 180000, SoLuongTon = 12, TinhTrangHang = "ConHang" }
        };
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(10)).ReturnsAsync(variants);

        // Act: Render ban đầu chưa chọn size và màu
        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 10));

        // Assert (Spec requirement SCR-02): Khi chưa chọn biến thể, nút tăng số lượng [+] PHẢI BỊ VÔ HIỆU HÓA (disabled)
        var plusBtn = cut.FindAll("button.btn-qty").Last();
        plusBtn.HasAttribute("disabled").Should().BeTrue("nút tăng số lượng phải bị vô hiệu hóa khi chưa chọn biến thể SKU");
    }

    #endregion

    #region VÒNG 5: INTERACTIVE FILTER DROPDOWNS & ACTIVE CHIPS (CHUẨN SCREENSHOT YAME)

    [Fact]
    public async Task CatalogPage_DedicatedCategory_ClickingFilterButton_OpensDropdownPopoverWithAccurateOptions()
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 3, TenDanhMuc = "Quần Tây Nam Lưng Thun Ẩn" }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("http://localhost/catalog?category=quan-tay");

        var cut = Render<Catalog>();

        // Act: Click nút "Kiểu sản phẩm"
        var kieuBtn = cut.FindAll("button.filter-tag-chip").FirstOrDefault(b => b.TextContent.Contains("Kiểu sản phẩm"));
        kieuBtn.Should().NotBeNull();
        await cut.InvokeAsync(() => kieuBtn!.Click());

        // Assert: Dropdown popup mở ra hiển thị các phân cấp loài đúng chuẩn screenshot YaMe
        cut.Find(".filter-dropdown-menu").Should().NotBeNull();
        cut.Markup.Should().Contain("Đã chọn 0");
        cut.Markup.Should().Contain("Đặt lại");
        cut.Markup.Should().Contain("Quần Tây Loose Fit (1)");
        cut.Markup.Should().Contain("Quần Tây Regular Fit (4)");
        cut.Markup.Should().Contain("Quần Tây Slim Fit (20)");
    }

    [Fact]
    public async Task CatalogPage_SelectingFilterOption_UpdatesHeaderAndShowsActiveChip()
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 3, TenDanhMuc = "Quần Tây Nam Lưng Thun Ẩn" }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("http://localhost/catalog?category=quan-tay");

        var cut = Render<Catalog>();

        // Act 1: Mở dropdown "Kiểu sản phẩm"
        var kieuBtn = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Kiểu sản phẩm"));
        await cut.InvokeAsync(() => kieuBtn.Click());

        // Act 2: Chọn checkbox "Quần Tây Regular Fit (4)"
        var checkbox = cut.FindAll(".dropdown-option-item input[type='checkbox']")[1];
        await cut.InvokeAsync(() => checkbox.Change(true));

        // Assert: Header đếm 1, xuất hiện chip bên dưới toolbar (chuẩn screenshot YaMe: Kiểu : Quần Tây Regular Fit ✕)
        cut.Markup.Should().Contain("Đã chọn 1");
        cut.Markup.Should().Contain("Kiểu : Quần Tây Regular Fit");

        // Act 3: Xóa chip bằng nút ✕
        var removeChipBtn = cut.Find("button.chip-remove-btn");
        await cut.InvokeAsync(() => removeChipBtn.Click());

        // Assert: Chip bị xóa
        cut.Markup.Should().NotContain("Kiểu : Quần Tây Regular Fit");
    }

    [Fact]
    public async Task CatalogPage_DropdownResetButton_ClearsSelectedOptions()
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 3, TenDanhMuc = "Quần Tây Nam Lưng Thun Ẩn" }
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var navMan = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navMan.NavigateTo("http://localhost/catalog?category=quan-tay");

        var cut = Render<Catalog>();

        // Act 1: Mở dropdown "Kiểu sản phẩm"
        var kieuBtn = cut.FindAll("button.filter-tag-chip").First(b => b.TextContent.Contains("Kiểu sản phẩm"));
        await cut.InvokeAsync(() => kieuBtn.Click());

        // Act 2: Check option 0
        var checkbox = cut.FindAll(".dropdown-option-item input[type='checkbox']")[0];
        await cut.InvokeAsync(() => checkbox.Change(true));

        cut.Markup.Should().Contain("Đã chọn 1");

        // Act 3: Bấm "Đặt lại" trong header dropdown
        var resetBtn = cut.Find("button.dropdown-reset-btn");
        await cut.InvokeAsync(() => resetBtn.Click());

        // Assert: Đã chọn 0
        cut.Markup.Should().Contain("Đã chọn 0");
    }

    [Fact]
    public async Task CatalogPage_QuickAddWithMultipleSizes_OpensQuickSizeSelector()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 201,
            TenSanPham = "Áo Sơ Mi Nam Oxford",
            GiaThapNhat = 320000,
            TongTonKho = 15
        };
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto> { product }, 1, 1, 10));
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(201))
            .ReturnsAsync(new List<ProductVariantDto>
            {
                new() { MaBienThe = 1, MaSanPham = 201, MauSac = "Trắng", KichCo = "M", SoLuongTon = 5, GiaBan = 320000 },
                new() { MaBienThe = 2, MaSanPham = 201, MauSac = "Trắng", KichCo = "L", SoLuongTon = 10, GiaBan = 320000 }
            });

        var cut = Render<Catalog>();

        // Act: Click quick add on product card
        var quickAddBtn = cut.Find("button.btn-quick-add");
        await cut.InvokeAsync(() => quickAddBtn.Click());

        // Assert: Quick Size Selector opens with distinct sizes
        cut.Markup.Should().Contain("CHỌN SIZE NHANH");
        cut.Markup.Should().Contain("Áo Sơ Mi Nam Oxford");
        cut.Markup.Should().Contain("M");
        cut.Markup.Should().Contain("L");
    }

    [Fact]
    public async Task CatalogPage_QuickSelectSize_AddsSpecificVariantToCart()
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 202,
            TenSanPham = "Quần Tây Nam Slimfit",
            GiaThapNhat = 380000,
            TongTonKho = 20
        };
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto> { product }, 1, 1, 10));
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(202))
            .ReturnsAsync(new List<ProductVariantDto>
            {
                new() { MaBienThe = 11, MaSanPham = 202, MauSac = "Đen", KichCo = "29", SoLuongTon = 8, GiaBan = 380000 },
                new() { MaBienThe = 12, MaSanPham = 202, MauSac = "Đen", KichCo = "30", SoLuongTon = 12, GiaBan = 380000 }
            });

        var cut = Render<Catalog>();

        // Act 1: Click quick add to open quick size selector
        var quickAddBtn = cut.Find("button.btn-quick-add");
        await cut.InvokeAsync(() => quickAddBtn.Click());

        // Act 2: Click size "30"
        var sizeBtn = cut.FindAll("button.quick-size-pill").First(b => b.TextContent.Contains("30"));
        await cut.InvokeAsync(() => sizeBtn.Click());

        // Assert: Variant 12 (Size 30) added to cart via CartService
        _mockCartService.Verify(x => x.AddItemAsync(It.Is<CartItemDto>(i => i.MaBienThe == 12 && i.KichCo == "30")), Times.Once);
        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(s => s.Contains("Size 30"))), Times.Once);
    }

    [Theory]
    [InlineData("Áo", 4)]
    [InlineData("Quần", 5)]
    [InlineData("Phụ kiện", 3)]
    public async Task CatalogPage_ClickingQuickPill_SelectsParentCategoryForAllItems(string pillText, int expectedCategoryId)
    {
        // Arrange
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>
            {
                new() { MaDanhMuc = 17, MaDanhMucCha = 4, TenDanhMuc = "Áo Hoodie & Sweater" },
                new() { MaDanhMuc = 4, MaDanhMucCha = 1, TenDanhMuc = "Áo Nam" },
                new() { MaDanhMuc = 13, MaDanhMucCha = 5, TenDanhMuc = "Quần Jean Nam" },
                new() { MaDanhMuc = 5, MaDanhMucCha = 1, TenDanhMuc = "Quần Nam" },
                new() { MaDanhMuc = 3, MaDanhMucCha = null, TenDanhMuc = "Phụ kiện" },
            });
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        var cut = Render<Catalog>();

        // Act: Click quick pill
        var pillBtn = cut.FindAll("button.pill-tab-btn").First(b => b.TextContent.Trim() == pillText);
        await cut.InvokeAsync(() => pillBtn.Click());

        // Assert: Catalog API was queried with the parent CategoryId (not a specific child subcategory)
        _mockCatalogApi.Verify(x => x.GetProductsAsync(It.Is<ProductQueryParameters>(q => q.CategoryId == expectedCategoryId)), Times.AtLeastOnce);
    }

    [Fact]
    public void CatalogPage_RendersDynamicBanner_WhenActiveBannersReturnedFromApi()
    {
        // Arrange
        var customBanner = new BannerDto
        {
            MaBanner = 99,
            TieuDe = "BST THỜI TRANG ĐỈNH CAO 2026\nTỰ TIN TỎA SÁNG",
            PhuDe = "ƯU ĐÃI KHỦNG 50%",
            GiaNoiBat = "350K",
            NhanBadge = "EXCLUSIVE SALE",
            HinhAnh = "https://images.unsplash.com/custom-banner.jpg",
            NutChinhText = "MUA NGAY HOM NAY",
            NutChinhLink = "/catalog?sale=true",
            NutPhuText = "Xem gợi ý size",
            NutPhuLink = "#ai-advisor"
        };
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync())
            .ReturnsAsync(new List<BannerDto> { customBanner });
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        // Act
        var cut = Render<Catalog>();

        // Assert: Dynamic banner data rendered on storefront
        cut.Markup.Should().Contain("BST THỜI TRANG ĐỈNH CAO 2026");
        cut.Markup.Should().Contain("TỰ TIN TỎA SÁNG");
        cut.Markup.Should().Contain("ƯU ĐÃI KHỦNG 50%");
        cut.Markup.Should().Contain("350");
        cut.Markup.Should().Contain("EXCLUSIVE SALE");
        cut.Markup.Should().Contain("MUA NGAY HOM NAY");
        cut.Markup.Should().Contain("Xem gợi ý size");
    }

    [Fact]
    public async Task CatalogPage_CarouselNavigatesBanners_WhenMultipleActiveBannersExist()
    {
        // Arrange
        var banner1 = new BannerDto
        {
            MaBanner = 1,
            TieuDe = "BANNER CHIẾN DỊCH SỐ 1",
            PhuDe = "GIẢM 20%",
            GiaNoiBat = "199K",
            NhanBadge = "ROUND 1",
            HinhAnh = "images/b1.jpg"
        };
        var banner2 = new BannerDto
        {
            MaBanner = 2,
            TieuDe = "BANNER CHIẾN DỊCH SỐ 2",
            PhuDe = "GIẢM 40%",
            GiaNoiBat = "299K",
            NhanBadge = "ROUND 2",
            HinhAnh = "images/b2.jpg"
        };
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync())
            .ReturnsAsync(new List<BannerDto> { banner1, banner2 });
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        // Act 1: Initial render shows banner 1
        var cut = Render<Catalog>();
        cut.Markup.Should().Contain("BANNER CHIẾN DỊCH SỐ 1");

        // Act 2: Click next button to advance carousel
        var nextBtn = cut.Find("button.next-btn");
        await cut.InvokeAsync(() => nextBtn.Click());

        // Assert: Banner 2 is now shown
        cut.Markup.Should().Contain("BANNER CHIẾN DỊCH SỐ 2");
    }

    [Fact]
    public void CatalogPage_ShowsQuickEditBannerButton_WhenAdminOrManagerIsLoggedIn()
    {
        // Arrange
        _mockAuthState.Setup(x => x.IsAuthenticated).Returns(true);
        _mockAuthState.Setup(x => x.UserRole).Returns(RoleConstants.AdminChuoi);
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync()).ReturnsAsync(new List<BannerDto>());
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        // Act
        var cut = Render<Catalog>();

        // Assert: Admin quick edit badge is present linking to /admin/banners
        var editBadge = cut.Find("a.hero-admin-edit-badge");
        editBadge.Should().NotBeNull();
        editBadge.GetAttribute("href").Should().Be("/admin/banners");
    }

    [Fact(DisplayName = "BANNER-RBAC-01: Quản lý chi nhánh không được hiển thị nút sửa Banner & Chiến dịch")]
    public void CatalogPage_HidesQuickEditBannerButton_WhenBranchManagerIsLoggedIn()
    {
        // Arrange
        _mockAuthState.Setup(x => x.IsAuthenticated).Returns(true);
        _mockAuthState.Setup(x => x.UserRole).Returns(RoleConstants.QuanLyChiNhanh);
        _mockCatalogApi.Setup(x => x.GetActiveBannersAsync()).ReturnsAsync(new List<BannerDto>());
        _mockCatalogApi.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(new List<CategoryDto>());
        _mockCatalogApi.Setup(x => x.GetBrandsAsync()).ReturnsAsync(new List<BrandDto>());
        _mockCatalogApi.Setup(x => x.GetProductsAsync(It.IsAny<ProductQueryParameters>()))
            .ReturnsAsync(new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 0, 1, 10));

        // Act
        var cut = Render<Catalog>();

        // Assert: Quản lý chi nhánh không được phép sửa banner chiến dịch
        cut.FindAll("a.hero-admin-edit-badge").Should().BeEmpty();
        cut.Markup.Should().NotContain("Quản lý Banner & Chiến dịch");
    }

    [Theory(DisplayName = "PRODUCT-SOLD-01: ProductCard hiển thị chính xác số lượng đã bán online")]
    [InlineData(162, "Đã bán online 162")]
    [InlineData(85, "Đã bán online 85")]
    [InlineData(0, "Đã bán online")]
    public void ProductCard_Renders_SoldOnlineQuantity_Correctly(int daBan, string expectedText)
    {
        // Arrange
        var product = new ProductListItemDto
        {
            MaSanPham = 10,
            TenSanPham = "Áo Thun Cổ Tròn QF",
            GiaThapNhat = 150000,
            GiaCaoNhat = 150000,
            TongTonKho = 20,
            DaBanOnline = daBan
        };

        // Act
        var cut = Render<ProductCard>(parameters => parameters.Add(p => p.Product, product));

        // Assert
        var soldElement = cut.Find(".product-sold-text");
        soldElement.Should().NotBeNull();
        soldElement.TextContent.Should().Contain(expectedText);
    }

    #endregion
}

