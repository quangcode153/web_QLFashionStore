using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Frontend.Pages;
using Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Shared.DTOs.Catalog;
using Xunit;

namespace Frontend.Tests.Components;

public class ProductDetailAiAssistantComponentTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IToastService> _mockToastService;

    public ProductDetailAiAssistantComponentTests()
    {
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockCartService = new Mock<ICartService>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
    }

    private List<ProductVariantDto> CreateSampleVariants(int productId = 101)
    {
        return new List<ProductVariantDto>
        {
            new ProductVariantDto
            {
                MaBienThe = 1001,
                MaSanPham = productId,
                TenSanPham = "Áo Polo Pique QF Noir Cao Cấp",
                MaSku = "POLO-BLK-M",
                MauSac = "Đen",
                KichCo = "M",
                GiaBan = 299000,
                SoLuongTon = 15,
                HinhAnh = "/images/polo-black.jpg"
            },
            new ProductVariantDto
            {
                MaBienThe = 1002,
                MaSanPham = productId,
                TenSanPham = "Áo Polo Pique QF Noir Cao Cấp",
                MaSku = "POLO-BLK-L",
                MauSac = "Đen",
                KichCo = "L",
                GiaBan = 299000,
                SoLuongTon = 20,
                HinhAnh = "/images/polo-black.jpg"
            },
            new ProductVariantDto
            {
                MaBienThe = 1003,
                MaSanPham = productId,
                TenSanPham = "Áo Polo Pique QF Noir Cao Cấp",
                MaSku = "POLO-WHT-L",
                MauSac = "Trắng",
                KichCo = "L",
                GiaBan = 299000,
                SoLuongTon = 0,
                HinhAnh = "/images/polo-white.jpg"
            }
        };
    }

    [Fact]
    public void ProductDetail_ShouldRender_AiAssistantButton()
    {
        // ARRANGE
        var variants = CreateSampleVariants(101);
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(101))
            .ReturnsAsync(variants);
        _mockCatalogApi.Setup(x => x.GetProductImagesAsync(101))
            .ReturnsAsync(new List<ProductImageDto>());

        // ACT
        var cut = Render<ProductDetail>(parameters => parameters
            .Add(p => p.id, 101)
        );

        // ASSERT: Nút Stylist AI hỗ trợ sản phẩm phải hiển thị trên giao diện
        var aiButtons = cut.FindAll(".stylist-ai-link, .btn-ai-product-assist");
        aiButtons.Should().NotBeEmpty("Phải có nút Stylist AI hỗ trợ trên trang chi tiết sản phẩm");
    }

    [Fact]
    public async Task ProductDetail_WhenClickingAiAssistant_InvokesJsInteropWithProductContext()
    {
        // ARRANGE
        var variants = CreateSampleVariants(101);
        _mockCatalogApi.Setup(x => x.GetProductVariantsAsync(101))
            .ReturnsAsync(variants);
        _mockCatalogApi.Setup(x => x.GetProductImagesAsync(101))
            .ReturnsAsync(new List<ProductImageDto>());

        var cut = Render<ProductDetail>(parameters => parameters
            .Add(p => p.id, 101)
        );

        // Chờ tải xong sản phẩm
        cut.WaitForAssertion(() => cut.FindAll(".pdp-title").Should().NotBeEmpty());

        // Chọn màu Đen và size M
        var colorButtons = cut.FindAll(".swatch-color-btn");
        colorButtons[0].Click(); // Đen

        var sizeButtons = cut.FindAll(".swatch-size-btn");
        sizeButtons[0].Click(); // M

        // ACT: Click vào nút Stylist AI
        var aiBtn = cut.Find(".stylist-ai-link, .btn-ai-product-assist");
        aiBtn.Click();

        // ASSERT: JSInterop phải được kích hoạt để mở chat và truyền context
        cut.WaitForAssertion(() =>
        {
            var invocations = JSInterop.Invocations;
            invocations.Should().Contain(inv => inv.Identifier == "qfOpenChat" || inv.Identifier == "qfSetProductContext");
        });
    }
}
