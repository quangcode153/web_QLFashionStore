using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Catalog;
using Xunit;

namespace Backend.Tests.UnitTests.Catalog;

public class ProductImageValidationTests
{
    private readonly Mock<ICatalogRepository> _mockRepo;
    private readonly CatalogService _service;

    public ProductImageValidationTests()
    {
        _mockRepo = new Mock<ICatalogRepository>();
        _service = new CatalogService(_mockRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Fact(DisplayName = "RED-IMG-01: Chặn tạo sản phẩm khi danh sách hình ảnh rỗng")]
    public async Task CreateProduct_EmptyImagesList_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            TenSanPham = "Áo Polo YaMe Coolmax",
            MaDanhMuc = 1,
            MaThuongHieu = 1,
            Images = new List<CreateProductImageDto>()
        };

        // Act & Assert
        var act = async () => await _service.CreateProductAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Sản phẩm bắt buộc phải có ít nhất 1 hình ảnh*");
    }

    [Fact(DisplayName = "RED-IMG-02: Chặn tạo sản phẩm khi danh sách hình ảnh là NULL")]
    public async Task CreateProduct_NullImagesList_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            TenSanPham = "Áo Thun YaMe Basic",
            MaDanhMuc = 1,
            MaThuongHieu = 1,
            Images = null!
        };

        // Act & Assert
        var act = async () => await _service.CreateProductAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Sản phẩm bắt buộc phải có ít nhất 1 hình ảnh*");
    }

    [Theory(DisplayName = "RED-IMG-03: Chặn tạo sản phẩm khi đường dẫn ảnh là rỗng, khoảng trắng hoặc null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateProduct_InvalidImageUrl_ThrowsArgumentException(string? invalidUrl)
    {
        // Arrange
        var request = new CreateProductRequest
        {
            TenSanPham = "Quần Jean Slimfit",
            MaDanhMuc = 2,
            MaThuongHieu = 1,
            Images = new List<CreateProductImageDto>
            {
                new() { DuongDanAnh = invalidUrl!, LaAnhChinh = true }
            }
        };

        // Act & Assert
        var act = async () => await _service.CreateProductAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Đường dẫn hình ảnh không hợp lệ*");
    }

    #endregion

    #region VÒNG 2: CHUỖI XỬ LÝ & TOÀN VẸN ẢNH CHÍNH (STATE PIPELINE)

    [Fact(DisplayName = "RED-IMG-04: Tạo sản phẩm hợp lệ có ảnh -> Gọi Repository thành công")]
    public async Task CreateProduct_ValidImages_CallsRepositoryAndReturnsId()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            TenSanPham = "Áo Khoác Dù YaMe 2026",
            MaDanhMuc = 3,
            MaThuongHieu = 1,
            Images = new List<CreateProductImageDto>
            {
                new() { DuongDanAnh = "/images/ao_khoac_du_069_xam.jpg", LaAnhChinh = true }
            }
        };
        _mockRepo.Setup(r => r.CreateProductAsync(It.IsAny<CreateProductRequest>()))
            .ReturnsAsync(1099);

        // Act
        var result = await _service.CreateProductAsync(request);

        // Assert
        result.Should().Be(1099);
        _mockRepo.Verify(r => r.CreateProductAsync(It.Is<CreateProductRequest>(req => 
            req.Images.Count == 1 && req.Images[0].LaAnhChinh)), Times.Once);
    }

    [Fact(DisplayName = "RED-IMG-05: Khi người dùng không chỉ định ảnh chính -> Tự động đặt ảnh đầu tiên là ảnh chính")]
    public async Task CreateProduct_NoMainImageSpecified_AutoSetsFirstImageAsMain()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            TenSanPham = "Quần Kaki Suông YaMe",
            MaDanhMuc = 2,
            MaThuongHieu = 1,
            Images = new List<CreateProductImageDto>
            {
                new() { DuongDanAnh = "/images/quan_kaki_018_xanh.jpg", LaAnhChinh = false },
                new() { DuongDanAnh = "/images/quan_kaki_018_phu.jpg", LaAnhChinh = false }
            }
        };
        _mockRepo.Setup(r => r.CreateProductAsync(It.IsAny<CreateProductRequest>()))
            .ReturnsAsync(1100);

        // Act
        var result = await _service.CreateProductAsync(request);

        // Assert
        result.Should().Be(1100);
        _mockRepo.Verify(r => r.CreateProductAsync(It.Is<CreateProductRequest>(req => 
            req.Images[0].LaAnhChinh == true)), Times.Once);
    }

    #endregion
}
