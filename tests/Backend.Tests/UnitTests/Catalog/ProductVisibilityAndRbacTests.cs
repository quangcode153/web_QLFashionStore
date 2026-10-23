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

public class ProductVisibilityAndRbacTests
{
    private readonly Mock<ICatalogRepository> _mockCatalogRepo;
    private readonly CatalogService _service;

    public ProductVisibilityAndRbacTests()
    {
        _mockCatalogRepo = new Mock<ICatalogRepository>();
        _service = new CatalogService(_mockCatalogRepo.Object);
    }

    #region VÒNG 1: KIỂM SOÁT ẨN / HIỆN SẢN PHẨM (PRODUCT VISIBILITY TOGGLE)

    [Fact(DisplayName = "VIS-01: Admin chuyển đổi trạng thái ẩn/hiện sản phẩm hợp lệ thành công")]
    public async Task ToggleProductStatusAsync_ValidProductId_CallsRepoAndReturnsTrue()
    {
        // Arrange
        _mockCatalogRepo.Setup(r => r.ToggleProductStatusAsync(10))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ToggleProductStatusAsync(10);

        // Assert
        result.Should().BeTrue();
        _mockCatalogRepo.Verify(r => r.ToggleProductStatusAsync(10), Times.Once);
    }

    [Theory(DisplayName = "VIS-02: Chuyển đổi trạng thái sản phẩm với ID không hợp lệ (<= 0) ném ngoại lệ")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task ToggleProductStatusAsync_InvalidId_ThrowsArgumentException(int invalidId)
    {
        // Act
        Func<Task> act = async () => await _service.ToggleProductStatusAsync(invalidId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ID sản phẩm không hợp lệ*");
        _mockCatalogRepo.Verify(r => r.ToggleProductStatusAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region VÒNG 2: KIỂM SOÁT ẨN / HIỆN BIẾN THỂ (VARIANT VISIBILITY TOGGLE)

    [Fact(DisplayName = "VIS-03: Admin chuyển đổi trạng thái ẩn/hiện biến thể hợp lệ thành công")]
    public async Task ToggleVariantStatusAsync_ValidVariantId_CallsRepoAndReturnsTrue()
    {
        // Arrange
        _mockCatalogRepo.Setup(r => r.ToggleVariantStatusAsync(25))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ToggleVariantStatusAsync(25);

        // Assert
        result.Should().BeTrue();
        _mockCatalogRepo.Verify(r => r.ToggleVariantStatusAsync(25), Times.Once);
    }

    [Theory(DisplayName = "VIS-04: Chuyển đổi trạng thái biến thể với ID không hợp lệ (<= 0) ném ngoại lệ")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(-100)]
    public async Task ToggleVariantStatusAsync_InvalidId_ThrowsArgumentException(int invalidId)
    {
        // Act
        Func<Task> act = async () => await _service.ToggleVariantStatusAsync(invalidId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ID biến thể không hợp lệ*");
        _mockCatalogRepo.Verify(r => r.ToggleVariantStatusAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region VÒNG 3: CÔ LẬP HIỂN THỊ STOREFRONT VS ADMIN PORTAL (ISOLATION GATE)

    [Fact(DisplayName = "VIS-05: Storefront lấy danh sách biến thể chỉ thấy các biến thể đang hoạt động (DaXoa = false)")]
    public async Task GetProductVariantsAsync_ForStorefront_OnlyReturnsActiveVariants()
    {
        // Arrange
        var mockVariants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, TenSanPham = "Áo Thun Đen", MauSac = "Đen", KichCo = "M", DaXoa = false },
            new() { MaBienThe = 2, TenSanPham = "Áo Thun Đen", MauSac = "Đen", KichCo = "L", DaXoa = false }
        };

        _mockCatalogRepo.Setup(r => r.GetProductVariantsAsync(1))
            .ReturnsAsync(mockVariants);

        // Act
        var result = await _service.GetProductVariantsAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(v => !v.DaXoa);
    }

    [Fact(DisplayName = "VIS-06: Admin Portal lấy biến thể bao gồm cả các biến thể đã bị ẩn để quản lý/khôi phục")]
    public async Task GetProductVariantsForAdminAsync_ReturnsBothActiveAndHiddenVariants()
    {
        // Arrange
        var allVariants = new List<ProductVariantDto>
        {
            new() { MaBienThe = 1, TenSanPham = "Áo Thun Đen", MauSac = "Đen", KichCo = "M", DaXoa = false },
            new() { MaBienThe = 2, TenSanPham = "Áo Thun Đen", MauSac = "Đen", KichCo = "L", DaXoa = true } // Đã ẩn
        };

        _mockCatalogRepo.Setup(r => r.GetProductVariantsForAdminAsync(1))
            .ReturnsAsync(allVariants);

        // Act
        var result = await _service.GetProductVariantsForAdminAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Any(v => v.DaXoa).Should().BeTrue();
    }

    [Fact(DisplayName = "VIS-07: GetProductsPagedAsync với IncludeAll = true nạp đúng tham số sang Repository")]
    public async Task GetProductsPagedAsync_WithIncludeAll_PassesIncludeAllToRepository()
    {
        // Arrange
        var query = new ProductQueryParameters
        {
            PageNumber = 1,
            PageSize = 10,
            IncludeAll = true,
            DaXoa = null
        };
        var expectedResult = new PagedResult<ProductListItemDto>(new List<ProductListItemDto>(), 38, 1, 10);
        _mockCatalogRepo.Setup(r => r.GetProductsPagedAsync(It.Is<ProductQueryParameters>(q => q.IncludeAll && q.DaXoa == null)))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.GetProductsPagedAsync(query);

        // Assert
        result.TotalCount.Should().Be(38);
        _mockCatalogRepo.Verify(r => r.GetProductsPagedAsync(It.Is<ProductQueryParameters>(q => q.IncludeAll && q.DaXoa == null)), Times.Once);
    }

    #endregion
}
