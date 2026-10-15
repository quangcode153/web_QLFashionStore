using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Report;

namespace Backend.Repositories;

public interface IReportRepository
{
    Task<IEnumerable<DailyRevenueReportDto>> GetDailyRevenueAsync();
    Task<IEnumerable<TopSellingProductDto>> GetTopSellingProductsAsync(int top = 20);
    Task<FinancialRangeReportDto> GetFinancialRangeReportAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<BranchSalesLeaderboardDto>> GetBranchSalesLeaderboardAsync();
    Task<IEnumerable<BranchInventorySummaryDto>> GetBranchInventorySummaryAsync();
}
