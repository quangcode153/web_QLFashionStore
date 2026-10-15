using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Shared.DTOs.Common;
using Shared.DTOs.Report;

namespace Backend.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;

    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<ApiResponse<IEnumerable<DailyRevenueReportDto>>> GetDailyRevenueAsync()
    {
        try
        {
            var data = await _reportRepository.GetDailyRevenueAsync();
            return ApiResponse<IEnumerable<DailyRevenueReportDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<DailyRevenueReportDto>>.Fail($"Lỗi khi truy xuất báo cáo doanh thu theo ngày: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<TopSellingProductDto>>> GetTopSellingProductsAsync(int top = 20)
    {
        if (top <= 0 || top > 100)
        {
            return ApiResponse<IEnumerable<TopSellingProductDto>>.Fail("Số lượng sản phẩm bán chạy cần xem (top) phải nằm trong khoảng từ 1 đến 100.");
        }

        try
        {
            var data = await _reportRepository.GetTopSellingProductsAsync(top);
            return ApiResponse<IEnumerable<TopSellingProductDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<TopSellingProductDto>>.Fail($"Lỗi khi truy xuất danh sách sản phẩm bán chạy: {ex.Message}");
        }
    }

    public async Task<ApiResponse<FinancialRangeReportDto>> GetFinancialRangeReportAsync(DateTime? fromDate, DateTime? toDate)
    {
        if (!fromDate.HasValue || !toDate.HasValue)
        {
            return ApiResponse<FinancialRangeReportDto>.Fail("Từ ngày và đến ngày không được để trống hoặc sai định dạng.");
        }

        if (fromDate.Value.Date > toDate.Value.Date)
        {
            return ApiResponse<FinancialRangeReportDto>.Fail("Từ ngày không được lớn hơn đến ngày.");
        }

        try
        {
            var data = await _reportRepository.GetFinancialRangeReportAsync(fromDate.Value, toDate.Value);
            return ApiResponse<FinancialRangeReportDto>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<FinancialRangeReportDto>.Fail($"Lỗi khi lập báo cáo tài chính theo khoảng thời gian: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<BranchSalesLeaderboardDto>>> GetBranchSalesLeaderboardAsync()
    {
        try
        {
            var data = await _reportRepository.GetBranchSalesLeaderboardAsync();
            return ApiResponse<IEnumerable<BranchSalesLeaderboardDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<BranchSalesLeaderboardDto>>.Fail($"Lỗi khi truy xuất bảng xếp hạng doanh số chi nhánh: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<BranchInventorySummaryDto>>> GetBranchInventorySummaryAsync()
    {
        try
        {
            var data = await _reportRepository.GetBranchInventorySummaryAsync();
            return ApiResponse<IEnumerable<BranchInventorySummaryDto>>.Ok(data);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<BranchInventorySummaryDto>>.Fail($"Lỗi khi truy xuất tổng hợp tồn kho chi nhánh: {ex.Message}");
        }
    }
}
