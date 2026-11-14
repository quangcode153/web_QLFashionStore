using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Frontend.Pages;
using Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Xunit;

namespace Frontend.Tests.Components;

public class ProductDetailRealtimeStockComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalRService;

    public ProductDetailRealtimeStockComponentTests()
    {
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockCartService = new Mock<ICartService>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalRService = new Mock<ISignalRNotificationService>();

        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalRService.Object);
    }

    private List<ProductVariantDto> CreateSampleVariants(int productId = 101)
    {
        return new List<ProductVariantDto>
        {
            new ProductVariantDto
            {
                MaBienThe = 501,
                MaSanPham = productId,
                TenSanPham = "Áo Polo Pique QF Noir",
                MaSku = "POLO-BLK-L",
                MauSac = "Đen",
                KichCo = "L",
                GiaBan = 299000,
                SoLuongTon = 5,
                HinhAnh = "/images/polo-black.jpg"
            }
        };
    }

    [Fact]
    public void ProductDetail_WhenSomeoneBuysItem_DecreasesStockInRealtimeWithoutReload()
    {
        // ARRANGE
        Action<int, int, string>? inventoryCallback = null;
        _mockSignalRService.SetupAdd(s => s.OnInventoryUpdated += It.IsAny<Action<int, int, string>>())
            .Callback<Action<int, int, string>>(handler => inventoryCallback = handler);

        var variants = CreateSampleVariants(101);
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(101))
            .ReturnsAsync(variants);
        _mockCatalogApi.Setup(x => x.GetProductImagesAsync(101))
            .ReturnsAsync(new List<ProductImageDto>());

        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 101));

        // Chọn màu Đen và size L
        cut.WaitForAssertion(() => cut.FindAll(".swatch-color-btn").Should().NotBeEmpty());
        cut.Find(".swatch-color-btn").Click();
        cut.Find(".swatch-size-btn").Click();

        // Ban đầu tồn kho là 5
        cut.Markup.Should().Contain("Còn lại 5 sản phẩm");

        // ACT: Giả lập có người khác mua 2 cái và SignalR phát sự kiện
        inventoryCallback.Should().NotBeNull("Component phải đăng ký lắng nghe sự kiện OnInventoryUpdated");
        inventoryCallback!.Invoke(501, 2, "XUAT_BAN");

        // ASSERT: Tồn kho trên màn hình lập tức giảm từ 5 xuống 3 mà không cần reload
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Còn lại 3 sản phẩm");
        });
    }

    [Fact]
    public void ProductDetail_WhenLastItemIsSold_SwitchesToOutOfStockInRealtime()
    {
        // ARRANGE
        Action<int, int, string>? inventoryCallback = null;
        _mockSignalRService.SetupAdd(s => s.OnInventoryUpdated += It.IsAny<Action<int, int, string>>())
            .Callback<Action<int, int, string>>(handler => inventoryCallback = handler);

        var variants = CreateSampleVariants(101);
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(101))
            .ReturnsAsync(variants);
        _mockCatalogApi.Setup(x => x.GetProductImagesAsync(101))
            .ReturnsAsync(new List<ProductImageDto>());

        var cut = Render<ProductDetail>(parameters => parameters.Add(p => p.id, 101));

        // Chọn biến thể có 5 cái
        cut.WaitForAssertion(() => cut.FindAll(".swatch-color-btn").Should().NotBeEmpty());
        cut.Find(".swatch-color-btn").Click();
        cut.Find(".swatch-size-btn").Click();

        // ACT: Người khác mua hết sạch 5 cái
        inventoryCallback.Should().NotBeNull();
        inventoryCallback!.Invoke(501, 5, "XUAT_BAN");

        // ASSERT: Màn hình lập tức đổi sang trạng thái Hết hàng
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Kho Online: Hết hàng");
        });
    }
}

