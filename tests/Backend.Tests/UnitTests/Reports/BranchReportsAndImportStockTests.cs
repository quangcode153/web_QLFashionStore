using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Report;
using Shared.DTOs.Warehouse;
using Xunit;

namespace Backend.Tests.UnitTests.Reports;

/// <summary>
/// Unit tests for Branch Reports and Branch Scoped Stock Imports
/// Compliant with Multi-Round Testing Standard (Rule 6) and Targeted Scoped Testing (Rule 9).
/// </summary>
public class BranchReportsAndImportStockTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly ReportService _reportService;
    private readonly Mock<IWarehouseRepository> _mockWarehouseRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditRepo;
    private readonly WarehouseService _warehouseService;

    public BranchReportsAndImportStockTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _reportService = new ReportService(_mockReportRepo.Object);

        _mockWarehouseRepo = new Mock<IWarehouseRepository>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();
        _warehouseService = new WarehouseService(
            _mockWarehouseRepo.Object,
            null,
            _mockAuditRepo.Object
        );
    }

    #region ROUND 1: BRANCH SALES LEADERBOARD & INVENTORY SUMMARY

    [Fact(DisplayName = "BRANCH-REP-01: GetBranchSalesLeaderboardAsync trả về danh sách xếp hạng thành công")]
    public async Task GetBranchSalesLeaderboardAsync_ReturnsSuccessWithOrderedBranches()
    {
        // Arrange
        var mockLeaderboard = new List<BranchSalesLeaderboardDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", LoaiChiNhanh = "Cửa hàng bán lẻ", IsOnlineWarehouse = false, TongSoDon = 120, TongDoanhThu = 100_000_000m, TyLeDongGop = 80m },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", LoaiChiNhanh = "Kho tổng online", IsOnlineWarehouse = true, TongSoDon = 25, TongDoanhThu = 25_000_000m, TyLeDongGop = 20m }
        };

        _mockReportRepo.Setup(r => r.GetBranchSalesLeaderboardAsync())
            .ReturnsAsync(mockLeaderboard);

        // Act
        var result = await _reportService.GetBranchSalesLeaderboardAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2);
        result.Data.First().MaChiNhanh.Should().Be(1);
        result.Data.First().TongDoanhThu.Should().Be(100_000_000m);
        result.Data.Last().IsOnlineWarehouse.Should().BeTrue();
        _mockReportRepo.Verify(r => r.GetBranchSalesLeaderboardAsync(), Times.Once);
    }

    [Fact(DisplayName = "BRANCH-REP-02: GetBranchSalesLeaderboardAsync bắt ngoại lệ an toàn khi DB gặp lỗi")]
    public async Task GetBranchSalesLeaderboardAsync_WhenDbFails_ReturnsFailureResponse()
    {
        // Arrange
        _mockReportRepo.Setup(r => r.GetBranchSalesLeaderboardAsync())
            .ThrowsAsync(new InvalidOperationException("Connection closed unexpectedly"));

        // Act
        var result = await _reportService.GetBranchSalesLeaderboardAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi truy xuất bảng xếp hạng doanh số");
    }

    [Fact(DisplayName = "BRANCH-REP-03: GetBranchInventorySummaryAsync trả về số lượng tồn kho từng chi nhánh")]
    public async Task GetBranchInventorySummaryAsync_ReturnsSuccessWithInventorySummaries()
    {
        // Arrange
        var mockSummary = new List<BranchInventorySummaryDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", IsOnlineWarehouse = false, TongSoMatHang = 350, TongSoLuongTon = 12000, SoMatHangSapHet = 5, TongGiaTriTonKho = 2_400_000_000m },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng QF Tân Bình", IsOnlineWarehouse = true, TongSoMatHang = 350, TongSoLuongTon = 15000, SoMatHangSapHet = 2, TongGiaTriTonKho = 3_000_000_000m }
        };

        _mockReportRepo.Setup(r => r.GetBranchInventorySummaryAsync())
            .ReturnsAsync(mockSummary);

        // Act
        var result = await _reportService.GetBranchInventorySummaryAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(2);
        result.Data.First().TongSoLuongTon.Should().Be(12000);
        _mockReportRepo.Verify(r => r.GetBranchInventorySummaryAsync(), Times.Once);
    }

    [Fact(DisplayName = "BRANCH-REP-04: GetBranchInventorySummaryAsync bắt ngoại lệ an toàn khi DB gặp lỗi")]
    public async Task GetBranchInventorySummaryAsync_WhenDbFails_ReturnsFailureResponse()
    {
        // Arrange
        _mockReportRepo.Setup(r => r.GetBranchInventorySummaryAsync())
            .ThrowsAsync(new TimeoutException("Database query timeout"));

        // Act
        var result = await _reportService.GetBranchInventorySummaryAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi truy xuất tổng hợp tồn kho");
    }

    #endregion

    #region ROUND 2: IMPORT STOCK WITH BRANCH SCOPING

    [Theory(DisplayName = "BRANCH-IMP-01: Nhập kho vào chi nhánh cụ thể gọi đúng Repository và ghi Audit Log")]
    [InlineData(1, 10, 150000)]
    [InlineData(2, 50, 200000)]
    [InlineData(9, 100, 180000)]
    public async Task ImportStockAsync_WithValidBranch_CallsRepositoryAndAudits(int branchId, int quantity, decimal price)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaChiNhanh = branchId,
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 5,
            SoLuong = quantity,
            DonGiaNhap = price
        };

        _mockWarehouseRepo.Setup(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _warehouseService.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("thành công");
        _mockWarehouseRepo.Verify(r => r.ImportStockAsync(It.Is<ImportStockRequestDto>(
            req => req.MaChiNhanh == branchId && req.SoLuong == quantity && req.DonGiaNhap == price
        )), Times.Once);
        _mockAuditRepo.Verify(a => a.CreateAuditLogAsync(It.Is<Shared.DTOs.Audit.CreateAuditLogRequestDto>(
            log => log.LoaiThaoTac == "NHAP_HANG" && log.MaChiNhanh == branchId && log.NoiDungChiTiet.Contains($"kho chi nhánh #{branchId}")
        )), Times.Once);
    }

    [Theory(DisplayName = "BRANCH-IMP-02: Nhập kho với số lượng hoặc đơn giá bất hợp lệ bị chặn")]
    [InlineData(0, 100000)]
    [InlineData(-5, 100000)]
    [InlineData(10, -1000)]
    public async Task ImportStockAsync_WithInvalidQuantityOrPrice_ReturnsFailure(int quantity, decimal price)
    {
        // Arrange
        var request = new ImportStockRequestDto
        {
            MaChiNhanh = 1,
            MaNhaCungCap = 1,
            MaNhanVien = 2,
            MaBienThe = 5,
            SoLuong = quantity,
            DonGiaNhap = price
        };

        // Act
        var result = await _warehouseService.ImportStockAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        _mockWarehouseRepo.Verify(r => r.ImportStockAsync(It.IsAny<ImportStockRequestDto>()), Times.Never);
    }

    [Fact(DisplayName = "BRANCH-IMP-03: AuditLogService lọc đúng nhật ký theo mã chi nhánh khi được truyền")]
    public async Task AuditLogService_GetLogsAsync_PassesBranchIdToRepository()
    {
        // Arrange
        var auditService = new AuditLogService(_mockAuditRepo.Object);
        var expectedLogs = new List<Shared.DTOs.Audit.AuditLogDto>
        {
            new() { MaNhatKy = 1, LoaiThaoTac = "NHAP_HANG", MaChiNhanh = 2, NoiDungChiTiet = "Nhập kho chi nhánh #2" }
        };

        _mockAuditRepo.Setup(r => r.GetAuditLogsAsync("NHAP_HANG", 2, 50))
            .ReturnsAsync(expectedLogs);

        // Act
        var res = await auditService.GetLogsAsync("NHAP_HANG", 2, 50);

        // Assert
        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
        _mockAuditRepo.Verify(r => r.GetAuditLogsAsync("NHAP_HANG", 2, 50), Times.Once);
    }

    #endregion
}
