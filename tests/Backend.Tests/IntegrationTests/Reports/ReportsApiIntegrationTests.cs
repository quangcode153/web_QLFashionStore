using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Report;
using Xunit;

namespace Backend.Tests.IntegrationTests.Reports;

/// <summary>
/// Kịch bản kiểm thử tích hợp P1-6 (Phân hệ Báo cáo & Thống kê Quản trị theo TDD Test-First):
/// - FR-23: Báo cáo doanh thu theo ngày từ vw_DoanhThuTheoNgay (GET /api/reports/daily-revenue)
/// - FR-24: Top 20 sản phẩm bán chạy nhất từ vw_TopSanPhamBanChay (GET /api/reports/top-selling)
/// - FR-25: Báo cáo doanh thu theo khoảng thời gian từ sp_BaoCaoDoanhThuTheoKhoangThoiGian (GET /api/reports/financial-range)
/// </summary>
public class ReportsApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ReportsApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string GetManagerToken(string role = RoleConstants.QuanLyHeThong) =>
        TestTokenHelper.GenerateToken(2, "manager_test", role);

    private string GetStaffToken(string role = RoleConstants.ThuNgan) =>
        TestTokenHelper.GenerateToken(3, "staff_test", role);

    private string GetCustomerToken() =>
        TestTokenHelper.GenerateToken(1, "customer_test", RoleConstants.KhachHang);

    #region VÒNG 1: HAPPY PATH BÁO CÁO DỮ LIỆU

    [Fact(DisplayName = "TS-REP-01: Quản lý gọi GET /api/reports/daily-revenue trả về 200 OK và danh sách doanh thu theo ngày")]
    public async Task GetDailyRevenue_WithManagerToken_Returns200WithDailyRevenueData()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/daily-revenue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<DailyRevenueReportDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "TS-REP-02: Quản lý gọi GET /api/reports/top-selling trả về 200 OK và danh sách top mặt hàng bán chạy")]
    public async Task GetTopSelling_WithManagerToken_Returns200WithTopSellingData()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/top-selling");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<TopSellingProductDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "TS-REP-03: Quản lý gọi GET /api/reports/financial-range với khoảng ngày hợp lệ trả về 200 OK và dữ liệu tổng hợp")]
    public async Task GetFinancialRange_WithValidDates_Returns200WithFinancialSummary()
    {
        // Arrange
        var fromDate = "2026-10-01";
        var toDate = "2026-10-07";
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/financial-range?fromDate={fromDate}&toDate={toDate}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FinancialRangeReportDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TongSoDonHoanThanh.Should().BeGreaterThanOrEqualTo(0);
        body.Data.TongDoanhThuThuc.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact(DisplayName = "TS-REP-03B: Quản lý gọi GET /api/reports/financial-range với fromDate == toDate (cùng ngày) trả về 200 OK")]
    public async Task GetFinancialRange_WithSameDayRange_Returns200OK()
    {
        // Arrange
        var sameDate = "2026-10-07";
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/financial-range?fromDate={sameDate}&toDate={sameDate}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FinancialRangeReportDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    #endregion

    #region VÒNG 2: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (BOUNDARY & VALIDATION MATRIX)

    [Theory(DisplayName = "TS-REP-04A: Kiểm tra tham số top dị biệt (âm, 0, >100) trả về 400 Bad Request")]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(500)]
    public async Task GetTopSelling_WithInvalidTopParameter_Returns400BadRequest(int top)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/top-selling?top={top}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<TopSellingProductDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("1 đến 100");
    }

    [Theory(DisplayName = "TS-REP-04B: Kiểm tra chặn tham số ngày không hợp lệ hoặc đảo ngược trả về 400 Bad Request")]
    [InlineData("2026-10-10", "2026-10-01")] // fromDate > toDate
    [InlineData("invalid-date", "2026-10-07")] // Sai định dạng fromDate
    [InlineData("2026-10-01", "not-a-date")] // Sai định dạng toDate
    [InlineData("", "2026-10-07")] // Thiếu fromDate
    [InlineData("2026-10-01", "")] // Thiếu toDate
    public async Task GetFinancialRange_WithInvalidDateParameters_Returns400BadRequest(string fromDate, string toDate)
    {
        // Arrange
        var url = $"/api/reports/financial-range?fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetManagerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FinancialRangeReportDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & CÔ LẬP VAI TRÒ (RBAC MATRIX)

    [Theory(DisplayName = "TS-REP-05A: Các vai trò quản lý cấp cao có quyền truy cập báo cáo (200 OK)")]
    [InlineData(RoleConstants.AdminChuoi)]
    [InlineData(RoleConstants.QuanLyHeThong)]
    [InlineData(RoleConstants.QuanLyChiNhanh)]
    public async Task Reports_WithManagerRoles_Returns200OK(string role)
    {
        // Arrange
        var token = GetManagerToken(role);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/daily-revenue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory(DisplayName = "TS-REP-05B: Các vai trò nhân viên cấp dưới và khách hàng bị chặn truy cập báo cáo (403 Forbidden)")]
    [InlineData(RoleConstants.ThuNgan)]
    [InlineData(RoleConstants.ThuKho)]
    [InlineData(RoleConstants.NhanVienBanHang)]
    [InlineData(RoleConstants.KhachHang)]
    public async Task Reports_WithNonManagerRoles_Returns403Forbidden(string role)
    {
        // Arrange
        var token = GetStaffToken(role);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/daily-revenue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TS-REP-05C: Truy cập báo cáo không có Token xác thực bị chặn 401 Unauthorized")]
    public async Task Reports_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/daily-revenue");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
