using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class CatalogServiceTests
{
    private readonly Mock<ICatalogRepository> _mockCatalogRepo;
    private readonly CatalogService _service;

    public CatalogServiceTests()
    {
        _mockCatalogRepo = new Mock<ICatalogRepository>();
        _service = new CatalogService(_mockCatalogRepo.Object);
    }

    #region TRA CỨU BIẾN THỂ THEO SKU (POS)

    [Fact(DisplayName = "UNIT-CAT-SKU-01: Tra cứu SKU hợp lệ trả về đúng thông tin biến thể")]
    public async Task GetVariantBySkuAsync_ValidSku_ReturnsVariant()
    {
        // Arrange
        var variant = new ProductVariantDto
        {
            MaBienThe = 1,
            MaSanPham = 1,
            TenSanPham = "Áo Thun Cotton",
            MaSku = "AT-COT-01",
            GiaBan = 150000,
            MauSac = "Đen",
            KichCo = "L"
        };
        _mockCatalogRepo.Setup(r => r.GetVariantBySkuAsync("AT-COT-01"))
            .ReturnsAsync(variant);

        // Act
        var result = await _service.GetVariantBySkuAsync("AT-COT-01");

        // Assert
        result.Should().NotBeNull();
        result!.MaSku.Should().Be("AT-COT-01");
        result.GiaBan.Should().Be(150000);
        result.MauSac.Should().Be("Đen");
    }

    [Theory(DisplayName = "UNIT-CAT-SKU-02: Tra cứu SKU với chuỗi rỗng/khoảng trắng/null trả về null ngay không gọi Repo")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetVariantBySkuAsync_EmptyOrWhitespaceSku_ReturnsNullWithoutCallingRepo(string? sku)
    {
        // Act
        var result = await _service.GetVariantBySkuAsync(sku!);

        // Assert
        result.Should().BeNull();
        _mockCatalogRepo.Verify(r => r.GetVariantBySkuAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-CAT-SKU-03: Tra cứu SKU không tồn tại trong Repo trả về null")]
    public async Task GetVariantBySkuAsync_NonExistentSku_ReturnsNull()
    {
        // Arrange
        _mockCatalogRepo.Setup(r => r.GetVariantBySkuAsync("SKU-999"))
            .ReturnsAsync((ProductVariantDto?)null);

        // Act
        var result = await _service.GetVariantBySkuAsync("SKU-999");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region TRUY VẤN BIẾN THỂ THEO ID SẢN PHẨM

    [Fact(DisplayName = "UNIT-CAT-VAR-01: Truy vấn biến thể với ID sản phẩm hợp lệ gọi Repo")]
    public async Task GetProductVariantsAsync_ValidProductId_ReturnsVariants()
    {
        // Arrange
        var variants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, MaSanPham = 1, MaSku = "AT-01", GiaBan = 100000 }
        };
        _mockCatalogRepo.Setup(r => r.GetProductVariantsAsync(1))
            .ReturnsAsync(variants);

        // Act
        var result = await _service.GetProductVariantsAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        _mockCatalogRepo.Verify(r => r.GetProductVariantsAsync(1), Times.Once);
    }

    #endregion

    #region TẠO BIẾN THỂ SẢN PHẨM MỚI (MULTI-ROUND TESTING)

    [Fact(DisplayName = "UNIT-CAT-CRTVAR-01: Tạo biến thể hợp lệ gọi Repo và tự động sinh SKU chuẩn nếu để trống")]
    public async Task CreateProductVariantAsync_ValidDataWithoutSku_GeneratesSkuAndCallsRepo()
    {
        // Arrange
        var request = new CreateProductVariantRequest
        {
            MaSanPham = 5,
            MauSac = "Đen",
            KichCo = "XL",
            GiaNhap = 120000,
            GiaBan = 220000,
            SoLuongTon = 15,
            MaSku = ""
        };

        _mockCatalogRepo.Setup(r => r.CreateProductVariantAsync(It.IsAny<CreateProductVariantRequest>()))
            .ReturnsAsync(105);

        // Act
        var newId = await _service.CreateProductVariantAsync(request);

        // Assert
        newId.Should().Be(105);
        request.MaSku.Should().Be("QF-SP05-DEN-XL");
        _mockCatalogRepo.Verify(r => r.CreateProductVariantAsync(It.Is<CreateProductVariantRequest>(x => 
            x.MaSku == "QF-SP05-DEN-XL" && x.MaSanPham == 5 && x.MauSac == "Đen" && x.KichCo == "XL")), Times.Once);
    }

    [Theory(DisplayName = "UNIT-CAT-CRTVAR-02: Tham số không hợp lệ ném ArgumentException")]
    [InlineData(0, "Đen", "L", 100000, 200000, 10)]      // MaSanPham = 0
    [InlineData(-1, "Đen", "L", 100000, 200000, 10)]     // MaSanPham âm
    [InlineData(1, "", "L", 100000, 200000, 10)]        // MauSac rỗng
    [InlineData(1, "   ", "L", 100000, 200000, 10)]     // MauSac whitespace
    [InlineData(1, "Đen", "", 100000, 200000, 10)]       // KichCo rỗng
    [InlineData(1, "Đen", "L", -1000, 200000, 10)]      // GiaNhap âm
    [InlineData(1, "Đen", "L", 100000, -5000, 10)]      // GiaBan âm
    [InlineData(1, "Đen", "L", 100000, 200000, -1)]     // SoLuongTon âm
    public async Task CreateProductVariantAsync_InvalidParameters_ThrowsArgumentException(
        int pid, string color, string size, decimal importPrice, decimal salePrice, int stock)
    {
        var request = new CreateProductVariantRequest
        {
            MaSanPham = pid,
            MauSac = color,
            KichCo = size,
            GiaNhap = importPrice,
            GiaBan = salePrice,
            SoLuongTon = stock
        };

        var act = () => _service.CreateProductVariantAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
        _mockCatalogRepo.Verify(r => r.CreateProductVariantAsync(It.IsAny<CreateProductVariantRequest>()), Times.Never);
    }

    #endregion

    #region QUẢN LÝ GIẢM GIÁ BIẾN THỂ & SẢN PHẨM (PROMOTION & SALE DISCOUNT)

    [Theory(DisplayName = "UNIT-CAT-DISC-01: % Giảm giá biến thể ngoài phạm vi 0-99 ném ArgumentOutOfRangeException")]
    [InlineData(-1)]
    [InlineData(-50)]
    [InlineData(100)]
    [InlineData(150)]
    public async Task UpdateVariantDiscountAsync_InvalidPercentage_ThrowsArgumentOutOfRangeException(int percent)
    {
        var act = () => _service.UpdateVariantDiscountAsync(1, percent);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _mockCatalogRepo.Verify(r => r.UpdateVariantDiscountAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-CAT-DISC-02: ID biến thể <= 0 ném ArgumentException")]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task UpdateVariantDiscountAsync_InvalidVariantId_ThrowsArgumentException(int variantId)
    {
        var act = () => _service.UpdateVariantDiscountAsync(variantId, 20);
        await act.Should().ThrowAsync<ArgumentException>();
        _mockCatalogRepo.Verify(r => r.UpdateVariantDiscountAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-CAT-DISC-03: % Giảm giá hợp lệ (0-99%) cập nhật thành công")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(99)]
    public async Task UpdateVariantDiscountAsync_ValidParameters_ReturnsTrue(int percent)
    {
        _mockCatalogRepo.Setup(r => r.UpdateVariantDiscountAsync(10, percent))
            .ReturnsAsync(true);

        var result = await _service.UpdateVariantDiscountAsync(10, percent);

        result.Should().BeTrue();
        _mockCatalogRepo.Verify(r => r.UpdateVariantDiscountAsync(10, percent), Times.Once);
    }

    [Theory(DisplayName = "UNIT-CAT-DISC-04: % Giảm giá toàn bộ sản phẩm ngoài phạm vi 0-99 ném ArgumentOutOfRangeException")]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task UpdateProductDiscountAsync_InvalidPercentage_ThrowsArgumentOutOfRangeException(int percent)
    {
        var act = () => _service.UpdateProductDiscountAsync(1, percent);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _mockCatalogRepo.Verify(r => r.UpdateProductDiscountAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-CAT-DISC-05: ID sản phẩm <= 0 ném ArgumentException")]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task UpdateProductDiscountAsync_InvalidProductId_ThrowsArgumentException(int productId)
    {
        var act = () => _service.UpdateProductDiscountAsync(productId, 30);
        await act.Should().ThrowAsync<ArgumentException>();
        _mockCatalogRepo.Verify(r => r.UpdateProductDiscountAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-CAT-DISC-06: Áp dụng giảm giá hợp lệ cho toàn bộ sản phẩm")]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(99)]
    public async Task UpdateProductDiscountAsync_ValidParameters_ReturnsTrue(int percent)
    {
        _mockCatalogRepo.Setup(r => r.UpdateProductDiscountAsync(5, percent))
            .ReturnsAsync(true);

        var result = await _service.UpdateProductDiscountAsync(5, percent);

        result.Should().BeTrue();
        _mockCatalogRepo.Verify(r => r.UpdateProductDiscountAsync(5, percent), Times.Once);
    }

    #endregion
}
