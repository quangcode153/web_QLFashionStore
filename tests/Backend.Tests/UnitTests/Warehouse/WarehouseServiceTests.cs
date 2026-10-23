using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Warehouse;
using Xunit;

namespace Backend.Tests.UnitTests.Warehouse;

/// <summary>
/// Unit Test độc lập cho WarehouseService (kiểm thử khắt khe logic nhập kho, cảnh báo tồn, NCC)
/// </summary>
public class WarehouseServiceTests
{
    private readonly Mock<IWarehouseRepository> _mockRepo;
    private readonly WarehouseService _service;

    public WarehouseServiceTests()
    {
        _mockRepo = new Mock<IWarehouseRepository>();
        _service = new WarehouseService(_mockRepo.Object);
    }

    #region NHẬP KHO (IMPORT STOCK) TESTS

    [Fact(DisplayName = "UNIT-WH-01: Nhập kho hợp lệ thành công và gọi đúng Repository")]
    public async Task ImportStockAsync_WithValidRequest_ReturnsSuccessAndCallsRepo()
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 3,
            SoLuong = 50,
            DonGiaNhap = 120000
        };

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("thành công");
        _mockRepo.Verify(r => r.ImportStockAsync(request), Times.Once);
    }

    [Fact(DisplayName = "UNIT-WH-02: Nhập kho với request null bị chặn không gọi Repo")]
    public async Task ImportStockAsync_WithNullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.ImportStockAsync(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-WH-03: Nhập kho với số lượng dị biệt (<= 0) bị chặn không gọi Repo")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ImportStockAsync_WithInvalidQuantity_ReturnsFail(int qty)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 3,
            SoLuong = qty,
            DonGiaNhap = 100000
        };

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Số lượng");
        _mockRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-WH-04: Nhập kho với đơn giá âm (< 0) bị chặn không gọi Repo")]
    [InlineData(-1)]
    [InlineData(-50000)]
    public async Task ImportStockAsync_WithNegativePrice_ReturnsFail(decimal price)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 3,
            SoLuong = 10,
            DonGiaNhap = price
        };

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đơn giá");
        _mockRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-WH-05: Nhập kho với khóa ngoại không hợp lệ (NCC, NV, Biến thể <= 0) bị chặn")]
    [InlineData(0, 1, 1)] // NCC = 0
    [InlineData(-1, 1, 1)] // NCC = -1
    [InlineData(1, 0, 1)] // NV = 0
    [InlineData(1, 1, 0)] // Biến thể = 0
    public async Task ImportStockAsync_WithInvalidForeignKeys_ReturnsFail(int ncc, int nv, int bt)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = ncc,
            MaNhanVien = nv,
            MaBienThe = bt,
            SoLuong = 10,
            DonGiaNhap = 100000
        };

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-WH-07: Chặn nhập kho các sản phẩm/biến thể đã ẩn với cảnh báo 'Hàng đã tạm thời ngừng cung cấp'")]
    [InlineData(37)]
    [InlineData(38)]
    public async Task ImportStockAsync_WhenVariantIsHidden_RejectsWithHiddenWarning(int hiddenVariantId)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = hiddenVariantId,
            SoLuong = 50,
            DonGiaNhap = 100000
        };

        _mockRepo.Setup(r => r.IsVariantHiddenAsync(hiddenVariantId)).ReturnsAsync(true);

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Hàng đã tạm thời ngừng cung cấp");
        _mockRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-WH-06: Nhập kho khi Repository ném ngoại lệ (Exception Resilience)")]
    public async Task ImportStockAsync_WhenRepoThrowsException_ReturnsSafeFail()
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 3,
            SoLuong = 10,
            DonGiaNhap = 100000
        };

        _mockRepo.Setup(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()))
            .ThrowsAsync(new InvalidOperationException("Foreign Key Constraint Violation"));

        // Act
        var result = await _service.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi nhập kho");
    }

    #endregion

    #region CẢNH BÁO TỒN KHO & NCC TESTS

    [Fact(DisplayName = "UNIT-WH-07: Lấy cảnh báo tồn kho an toàn khi Repository ném ngoại lệ")]
    public async Task GetLowStockAlertsAsync_WhenRepoThrows_ReturnsSafeFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetLowStockAlertsAsync())
            .ThrowsAsync(new TimeoutException("Database Timeout"));

        // Act
        var result = await _service.GetLowStockAlertsAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi lấy danh sách cảnh báo");
    }

    [Fact(DisplayName = "UNIT-WH-08: Lấy danh sách NCC an toàn khi Repository ném ngoại lệ")]
    public async Task GetSuppliersAsync_WhenRepoThrows_ReturnsSafeFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetSuppliersAsync())
            .ThrowsAsync(new TimeoutException("Database Timeout"));

        // Act
        var result = await _service.GetSuppliersAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi lấy danh sách nhà cung cấp");
    }

    [Theory(DisplayName = "UNIT-WH-09: Tạo NCC với tên rỗng, null hoặc khoảng trắng bị từ chối")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSupplierAsync_WithInvalidName_ReturnsFail(string? invalidName)
    {
        // Arrange
        var request = new CreateSupplierRequestDto
        {
            TenNhaCungCap = invalidName!,
            DienThoaiLienHe = "0901234567"
        };

        // Act
        var result = await _service.CreateSupplierAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Tên nhà cung cấp không được để trống");
        _mockRepo.Verify(r => r.CreateSupplierAsync(It.IsAny<CreateSupplierRequestDto>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-WH-10: Tạo NCC hợp lệ thành công và gọi đúng Repository")]
    public async Task CreateSupplierAsync_WithValidData_ReturnsSuccessAndCallsRepo()
    {
        // Arrange
        var request = new CreateSupplierRequestDto
        {
            TenNhaCungCap = "Xưởng May Thần Tốc",
            DienThoaiLienHe = "0988776655"
        };
        var createdSupplier = new SupplierDto
        {
            MaNhaCungCap = 99,
            TenNhaCungCap = "Xưởng May Thần Tốc",
            DienThoaiLienHe = "0988776655"
        };
        _mockRepo.Setup(r => r.CreateSupplierAsync(It.IsAny<CreateSupplierRequestDto>()))
            .ReturnsAsync(createdSupplier);

        // Act
        var result = await _service.CreateSupplierAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MaNhaCungCap.Should().Be(99);
        result.Data.TenNhaCungCap.Should().Be("Xưởng May Thần Tốc");
        _mockRepo.Verify(r => r.CreateSupplierAsync(request), Times.Once);
    }

    #endregion
}
