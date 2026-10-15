using System;
using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Report;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("daily-revenue")]
    public async Task<IActionResult> GetDailyRevenue()
    {
        var result = await _reportService.GetDailyRevenueAsync();
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("top-selling")]
    public async Task<IActionResult> GetTopSelling([FromQuery] int top = 20)
    {
        var result = await _reportService.GetTopSellingProductsAsync(top);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("financial-range")]
    public async Task<IActionResult> GetFinancialRange([FromQuery] string? fromDate, [FromQuery] string? toDate)
    {
        if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
        {
            return BadRequest(ApiResponse<FinancialRangeReportDto>.Fail("Từ ngày và đến ngày không được để trống."));
        }

        if (!DateTime.TryParse(fromDate, out var parsedFromDate))
        {
            return BadRequest(ApiResponse<FinancialRangeReportDto>.Fail("Định dạng từ ngày không hợp lệ."));
        }

        if (!DateTime.TryParse(toDate, out var parsedToDate))
        {
            return BadRequest(ApiResponse<FinancialRangeReportDto>.Fail("Định dạng đến ngày không hợp lệ."));
        }

        var result = await _reportService.GetFinancialRangeReportAsync(parsedFromDate, parsedToDate);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("branch-sales")]
    public async Task<IActionResult> GetBranchSalesLeaderboard()
    {
        var result = await _reportService.GetBranchSalesLeaderboardAsync();
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("branch-inventory-summary")]
    public async Task<IActionResult> GetBranchInventorySummary()
    {
        var result = await _reportService.GetBranchInventorySummaryAsync();
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
