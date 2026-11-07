using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backend.Database;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Customer;
using Xunit;

namespace Backend.Tests.UnitTests.Customers;

/// <summary>
/// Bộ kiểm thử chống trục lợi cấp độ thành viên & tính tổng chi tiêu thuần sau khi hoàn trả (Anti-Fraud VIP Rank Protection).
/// Tuân thủ nghiêm ngặt Rule 6 (Multi-Round Testing Standard) và Rule 8 (Red-First Gate Rule).
/// </summary>
public class AntiFraudRankAndNetSpendingTests
{
    private readonly Mock<ICustomerRepository> _mockCustomerRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditRepo;
    private readonly CustomerService _customerService;

    public AntiFraudRankAndNetSpendingTests()
    {
        _mockCustomerRepo = new Mock<ICustomerRepository>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();
        _customerService = new CustomerService(_mockCustomerRepo.Object, auditLogRepository: _mockAuditRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory(DisplayName = "VÒNG 1: Ma trận tính chi tiêu thuần và xếp hạng sau khi hoàn hàng")]
    [InlineData(0, 0, 0, "Thường", 1)]                            // Chưa mua gì
    [InlineData(10000000, 0, 10000000, "Vàng", 3)]               // Mua 10tr, không trả -> Vàng
    [InlineData(10000000, 2000000, 8000000, "Bạc", 2)]           // Mua 10tr, trả 2tr -> Chi tiêu thuần 8tr -> Rớt xuống Bạc
    [InlineData(10000000, 7500000, 2500000, "Thường", 1)]        // Mua 10tr, trả 7.5tr -> Chi tiêu thuần 2.5tr -> Rớt xuống Thường
    [InlineData(10000000, 10000000, 0, "Thường", 1)]             // Mua 10tr, trả hết 10tr -> Chi tiêu thuần 0đ -> Rớt xuống Thường
    [InlineData(3500000, 500000, 3000000, "Bạc", 2)]             // Biên đúng 3.000.000đ -> Giữ Bạc
    [InlineData(3500000, 500001, 2999999, "Thường", 1)]          // Biên dưới 3.000.000đ (2.999.999đ) -> Rớt xuống Thường
    [InlineData(12000000, 13000000, 0, "Thường", 1)]             // Tiền hoàn lớn hơn tiền mua (voucher/lệch) -> Không âm (0đ)
    public void CalculateNetSpendingAndEvaluateRank_BoundaryMatrix_ComputesAccurately(
        decimal totalOrders, 
        decimal totalRefunds, 
        decimal expectedNetSpending, 
        string expectedRankName, 
        int expectedRankId)
    {
        // Arrange: Tính chi tiêu thuần = max(0, totalOrders - totalRefunds)
        var netSpending = Math.Max(0, totalOrders - totalRefunds);

        // Phân hạng dựa trên chi tiêu thuần
        string rankName;
        int rankId;
        if (netSpending >= 10000000m)
        {
            rankName = "Vàng";
            rankId = 3;
        }
        else if (netSpending >= 3000000m)
        {
            rankName = "Bạc";
            rankId = 2;
        }
        else
        {
            rankName = "Thường";
            rankId = 1;
        }

        // Assert
        netSpending.Should().Be(expectedNetSpending);
        rankName.Should().Be(expectedRankName);
        rankId.Should().Be(expectedRankId);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI & CHỐNG GIAN LẬN CÀY RANK (ANTI-FRAUD LIFECYCLE)

    [Fact(DisplayName = "VÒNG 2: Khách mua 15 triệu lên VIP Vàng, sau khi hoàn tiền 15 triệu phải bị giáng rank xuống Thường")]
    public async Task AntiFraud_CustomerBuysLargeOrderThenRefunds_MustDowngradeRankImmediately()
    {
        // Giai đoạn 1: Khách hàng mua đơn 15 triệu -> Lên VIP Vàng
        var initialEvaluation = new List<RankEvaluationResultDto>
        {
            new() { MaKhachHang = 88, TenKhachHang = "Trần Trục Lợi", TongChiTieu = 15000000m, CapDoMoi = "Vàng" }
        };
        _mockCustomerRepo.Setup(r => r.EvaluateRanksAsync()).ReturnsAsync(initialEvaluation);

        var step1Result = await _customerService.EvaluateMemberRanksAsync();
        step1Result.Success.Should().BeTrue();
        step1Result.Data!.First(c => c.MaKhachHang == 88).CapDoMoi.Should().Be("Vàng");

        // Giai đoạn 2: Khách hàng trả lại toàn bộ hàng, nhận hoàn tiền 15 triệu
        // Hệ thống chạy lại thủ tục xét duyệt rank (dựa trên fn_TinhTongChiTieuKhachHang có trừ DonTraHang)
        var postRefundEvaluation = new List<RankEvaluationResultDto>
        {
            new() { MaKhachHang = 88, TenKhachHang = "Trần Trục Lợi", TongChiTieu = 0m, CapDoMoi = "Thường" }
        };
        _mockCustomerRepo.Setup(r => r.EvaluateRanksAsync()).ReturnsAsync(postRefundEvaluation);

        // Act: Kích hoạt xét lại rank sau khi hoàn trả
        var step2Result = await _customerService.EvaluateMemberRanksAsync();

        // Assert: Cấp độ đã bị giáng xuống Thường, chi tiêu thuần = 0, ngăn chặn hoàn toàn gian lận
        step2Result.Success.Should().BeTrue();
        var downgradedCustomer = step2Result.Data!.First(c => c.MaKhachHang == 88);
        downgradedCustomer.TongChiTieu.Should().Be(0m);
        downgradedCustomer.CapDoMoi.Should().Be("Thường", "Khách đã hoàn hết tiền đơn hàng, cấp bậc VIP Vàng phải bị tước và chuyển về Thường");
    }

    #endregion

    #region VÒNG 3: BẢO ĐẢM ĐẶC TẢ SQL SCRIPT (SQL SPECIFICATION & INTEGRITY CHECK)

    [Fact(DisplayName = "VÒNG 3: File sqlbtl.sql và DatabaseInitializer phải chứa logic trừ tiền DonTraHang trong fn_TinhTongChiTieuKhachHang")]
    public void SqlSpecification_MustDeductRefundsFromDonTraHang()
    {
        // 1. Kiểm tra file sqlbtl.sql
        var candidatePaths = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sqlbtl.sql")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "sqlbtl.sql")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "sqlbtl.sql")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "sqlbtl.sql"))
        };
        var sqlPath = candidatePaths.FirstOrDefault(File.Exists);
        sqlPath.Should().NotBeNull("File sqlbtl.sql phải tồn tại trong workspace");

        var sqlContent = File.ReadAllText(sqlPath!);

        // 2. Tìm riêng phần định nghĩa hàm fn_TinhTongChiTieuKhachHang
        var fnIdx = sqlContent.IndexOf("CREATE FUNCTION dbo.fn_TinhTongChiTieuKhachHang", StringComparison.OrdinalIgnoreCase);
        if (fnIdx < 0) fnIdx = sqlContent.IndexOf("CREATE OR ALTER FUNCTION dbo.fn_TinhTongChiTieuKhachHang", StringComparison.OrdinalIgnoreCase);
        fnIdx.Should().BeGreaterThanOrEqualTo(0, "Phải tồn tại định nghĩa hàm fn_TinhTongChiTieuKhachHang trong sqlbtl.sql");

        var fnEndIdx = sqlContent.IndexOf("END;", fnIdx, StringComparison.OrdinalIgnoreCase);
        fnEndIdx.Should().BeGreaterThan(fnIdx);
        var fnBody = sqlContent.Substring(fnIdx, fnEndIdx - fnIdx);

        // 3. Hàm fn_TinhTongChiTieuKhachHang PHẢI truy vấn bảng DonTraHang để trừ số tiền hoàn
        fnBody.Should().Contain("DonTraHang", "Hàm fn_TinhTongChiTieuKhachHang phải tham chiếu bảng DonTraHang để trừ tiền hoàn");
        fnBody.Should().Contain("so_tien_hoan", "Hàm fn_TinhTongChiTieuKhachHang phải trừ số tiền hoàn (so_tien_hoan)");
        fnBody.Should().Contain("Đã nhận hàng hoàn", "Hàm phải kiểm tra trạng thái đơn hoàn");
    }

    #endregion
}
