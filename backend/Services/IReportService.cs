using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Common;
using Shared.DTOs.Report;

namespace Backend.Services;

public interface IReportService
{
    Task<ApiResponse<IEnumerable<DailyRevenueReportDto>>> GetDailyRevenueAsync();
    Task<ApiResponse<IEnumerable<TopSellingProductDto>>> GetTopSellingProductsAsync(int top = 20);
    Task<ApiResponse<FinancialRangeReportDto>> GetFinancialRangeReportAsync(DateTime? fromDate, DateTime? toDate);
    Task<ApiResponse<IEnumerable<BranchSalesLeaderboardDto>>> GetBranchSalesLeaderboardAsync();
    Task<ApiResponse<IEnumerable<BranchInventorySummaryDto>>> GetBranchInventorySummaryAsync();
}
