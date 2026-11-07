using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Controllers;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class BrandManagementTests
{
    private readonly Mock<ICatalogRepository> _catalogRepoMock = new();
    private readonly CatalogService _catalogService;

    public BrandManagementTests()
    {
        _catalogService = new CatalogService(_catalogRepoMock.Object);
    }

    #region VÒNG 1: THỬ SAI - CHẶN XÓA THƯƠNG HIỆU KHI ĐANG CÓ SẢN PHẨM KINH DOANH

    [Fact(DisplayName = "VÒNG 1: Xóa thương hiệu đang có sản phẩm kinh doanh phải ném lỗi và từ chối xóa")]
    public async Task DeleteBrandAsync_WhenBrandHasActiveProducts_ShouldThrowInvalidOperationException()
    {
        // Arrange: Thương hiệu Mango (ID 9) đang có 6 sản phẩm
        int brandId = 9;
        _catalogRepoMock.Setup(r => r.DeleteBrandAsync(brandId))
            .ThrowsAsync(new InvalidOperationException("Không thể xóa thương hiệu vì đang có 6 sản phẩm kinh doanh."));

        // Act & Assert
        var act = () => _catalogService.DeleteBrandAsync(brandId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Không thể xóa thương hiệu*");
    }

    [Theory(DisplayName = "VÒNG 1: Mã thương hiệu không hợp lệ (<= 0) phải ném ArgumentException")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public async Task DeleteBrandAsync_WithInvalidId_ShouldThrowArgumentException(int invalidId)
    {
        // Act & Assert
        var act = () => _catalogService.DeleteBrandAsync(invalidId);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không hợp lệ*");
    }

    #endregion

    #region VÒNG 2: THỬ ĐÚNG - CHO PHÉP XÓA THƯƠNG HIỆU 0 SẢN PHẨM (TRÁNH KHÁCH VÀO KHÔNG CÓ HÀNG)

    [Fact(DisplayName = "VÒNG 2: Thương hiệu không còn hợp tác và có 0 sản phẩm (như Zara) phải xóa thành công")]
    public async Task DeleteBrandAsync_WhenBrandHasZeroProducts_ShouldReturnTrue()
    {
        // Arrange: Thương hiệu Zara (ID 2) có 0 sản phẩm
        int brandId = 2;
        _catalogRepoMock.Setup(r => r.DeleteBrandAsync(brandId))
            .ReturnsAsync(true);

        // Act
        var result = await _catalogService.DeleteBrandAsync(brandId);

        // Assert
        result.Should().BeTrue();
        _catalogRepoMock.Verify(r => r.DeleteBrandAsync(brandId), Times.Once);
    }

    #endregion

    #region VÒNG 3: CONTROLLER INTEGRITY & RESPONSE CODES

    [Fact(DisplayName = "VÒNG 3: BrandsController.DeleteBrand xóa thành công trả về 200 OK")]
    public async Task BrandsController_DeleteBrand_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<ICatalogService>();
        serviceMock.Setup(s => s.DeleteBrandAsync(2)).ReturnsAsync(true);
        var controller = new BrandsController(serviceMock.Object);

        // Act
        var response = await controller.DeleteBrand(2);

        // Assert
        var okResult = response as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var apiRes = okResult.Value as ApiResponse<bool>;
        apiRes.Should().NotBeNull();
        apiRes!.Success.Should().BeTrue();
    }

    [Fact(DisplayName = "VÒNG 3: BrandsController.DeleteBrand khi có sản phẩm ném lỗi trả về 400 BadRequest")]
    public async Task BrandsController_DeleteBrand_WhenServiceThrows_ReturnsBadRequest()
    {
        // Arrange
        var serviceMock = new Mock<ICatalogService>();
        serviceMock.Setup(s => s.DeleteBrandAsync(9))
            .ThrowsAsync(new InvalidOperationException("Không thể xóa thương hiệu vì đang có sản phẩm."));
        var controller = new BrandsController(serviceMock.Object);

        // Act
        var response = await controller.DeleteBrand(9);

        // Assert
        var badRequest = response as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
        var apiRes = badRequest.Value as ApiResponse<bool>;
        apiRes.Should().NotBeNull();
        apiRes!.Success.Should().BeFalse();
        apiRes.Message.Should().Contain("Không thể xóa thương hiệu");
    }

    #endregion
}

