using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpPost("import")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> ImportStock([FromBody] ImportStockRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<bool>.Fail("Dữ liệu nhập kho không được để trống."));
        }

        if (request.MaNhanVien <= 0)
        {
            var staffClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            if (int.TryParse(staffClaim, out var staffId) && staffId > 0)
            {
                request.MaNhanVien = staffId;
            }
        }

        var result = await _warehouseService.ImportStockAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("low-stock")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> GetLowStockAlerts()
    {
        var result = await _warehouseService.GetLowStockAlertsAsync();
        return Ok(result);
    }

    [HttpGet("variants")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> GetWarehouseVariants()
    {
        var result = await _warehouseService.GetAllWarehouseVariantsAsync();
        return Ok(result);
    }

    [HttpGet("suppliers")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> GetSuppliers()
    {
        var result = await _warehouseService.GetSuppliersAsync();
        return Ok(result);
    }

    [HttpPost("suppliers")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequestDto request)
    {
        var result = await _warehouseService.CreateSupplierAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
