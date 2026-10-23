using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Report;
using Xunit;

namespace Backend.Tests.UnitTests.Reports;

/// <summary>
/// Unit Test kiểm thử độc lập logic nghiệp vụ của ReportService (không qua WebHost hay Mock HTTP)
/// Đảm bảo tính khắt khe, kiểm thử toàn diện các ca biên, ma trận tham số và Exception Safety.
/// </summary>
public class ReportServiceTests
{
    private readonly Mock<IReportRepository> _mockRepo;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _mockRepo = new Mock<IReportRepository>();
        _service = new ReportService(_mockRepo.Object);
    }

    #region TOP SELLING PRODUCTS TESTS

    [Theory(DisplayName = "UNIT-REP-01: Kiểm tra tham số top hợp lệ (1..100) gọi đúng Repository")]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetTopSellingProductsAsync_WithValidTop_ReturnsSuccessAndCallsRepo(int top)
    {
        // Arrange
        _mockRepo.Setup(r => r.GetTopSellingProductsAsync(top))
            .ReturnsAsync(new List<TopSellingProductDto>
            {
                new() { MaSanPham = 1, TenSanPham = "Áo Thun Polo", TongSoLuongDaBan = 100 }
            });

        // Act
        var result = await _service.GetTopSellingProductsAsync(top);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        _mockRepo.Verify(r => r.GetTopSellingProductsAsync(top), Times.Once);
    }

    [Theory(DisplayName = "UNIT-REP-02: Kiểm tra tham số top dị biệt (âm, 0, vượt trần 100) bị chặn không gọi Repo")]
    [InlineData(-10)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(9999)]
    public async Task GetTopSellingProductsAsync_WithInvalidTop_ReturnsFailWithoutCallingRepo(int top)
    {
        // Act
        var result = await _service.GetTopSellingProductsAsync(top);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("1 đến 100");
        _mockRepo.Verify(r => r.GetTopSellingProductsAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region FINANCIAL RANGE TESTS

    [Fact(DisplayName = "UNIT-REP-03: Kiểm tra khoảng ngày hợp lệ (fromDate < toDate) gọi đúng Repository")]
    public async Task GetFinancialRangeReportAsync_WithValidRange_ReturnsSuccess()
    {
        // Arrange
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 7);
        _mockRepo.Setup(r => r.GetFinancialRangeReportAsync(from, to))
            .ReturnsAsync(new FinancialRangeReportDto
            {
                TongSoDonHoanThanh = 50,
                TongDoanhThuThuc = 50000000
            });

        // Act
        var result = await _service.GetFinancialRangeReportAsync(from, to);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TongSoDonHoanThanh.Should().Be(50);
        _mockRepo.Verify(r => r.GetFinancialRangeReportAsync(from, to), Times.Once);
    }

    [Fact(DisplayName = "UNIT-REP-04: Kiểm tra ca biên đặc biệt (fromDate == toDate) hợp lệ (báo cáo trong ngày)")]
    public async Task GetFinancialRangeReportAsync_WhenFromDateEqualsToDate_ReturnsSuccess()
    {
        // Arrange
        var sameDate = new DateTime(2026, 10, 7);
        _mockRepo.Setup(r => r.GetFinancialRangeReportAsync(sameDate, sameDate))
            .ReturnsAsync(new FinancialRangeReportDto
            {
                TongSoDonHoanThanh = 10,
                TongDoanhThuThuc = 10000000
            });

        // Act
        var result = await _service.GetFinancialRangeReportAsync(sameDate, sameDate);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TongSoDonHoanThanh.Should().Be(10);
        _mockRepo.Verify(r => r.GetFinancialRangeReportAsync(sameDate, sameDate), Times.Once);
    }

    [Fact(DisplayName = "UNIT-REP-05: Kiểm tra ca biên sai logic (fromDate > toDate) bị chặn và không gọi Repo")]
    public async Task GetFinancialRangeReportAsync_WhenFromDateGreaterThanToDate_ReturnsFail()
    {
        // Arrange
        var from = new DateTime(2026, 10, 10);
        var to = new DateTime(2026, 10, 1);

        // Act
        var result = await _service.GetFinancialRangeReportAsync(from, to);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Từ ngày không được lớn hơn đến ngày");
        _mockRepo.Verify(r => r.GetFinancialRangeReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-REP-06: Kiểm tra tham số ngày null bị chặn hoàn toàn")]
    [InlineData(null, "2026-10-07")]
    [InlineData("2026-10-01", null)]
    [InlineData(null, null)]
    public async Task GetFinancialRangeReportAsync_WithNullDates_ReturnsFail(string? fromStr, string? toStr)
    {
        // Arrange
        DateTime? from = fromStr != null ? DateTime.Parse(fromStr) : null;
        DateTime? to = toStr != null ? DateTime.Parse(toStr) : null;

        // Act
        var result = await _service.GetFinancialRangeReportAsync(from, to);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.GetFinancialRangeReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    #endregion

    #region EXCEPTION RESILIENCE TESTS

    [Fact(DisplayName = "UNIT-REP-07: Khả năng chống chịu đứt gãy DB - Repository ném ngoại lệ không làm sập Service")]
    public async Task Service_WhenRepositoryThrowsException_CatchesAndReturnsSafeFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetDailyRevenueAsync())
            .ThrowsAsync(new TimeoutException("SQL Server Connection Timeout"));

        // Act
        var result = await _service.GetDailyRevenueAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi truy xuất báo cáo");
    }

    #endregion
}
