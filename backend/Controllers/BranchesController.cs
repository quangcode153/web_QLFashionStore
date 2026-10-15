using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Warehouse;

namespace Backend.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ các chi nhánh và Kho Tổng Online của hệ thống
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllBranches()
    {
        var result = await _branchService.GetAllBranchesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một chi nhánh
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranchById(int id)
    {
        var result = await _branchService.GetBranchByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Thêm một chi nhánh mới vào chuỗi. Hệ thống tự động kích hoạt khởi tạo toàn bộ kho chi nhánh với tồn = 0
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong}")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchDto dto)
    {
        var result = await _branchService.CreateBranchAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Xem tồn kho theo từng chi nhánh cụ thể (hoặc Kho Tổng Online)
    /// </summary>
    [HttpGet("{id}/inventory")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.ThuKho},{RoleConstants.NhanVien},{RoleConstants.ThuNgan}")]
    public async Task<IActionResult> GetBranchInventory(int id, [FromQuery] string? search = null)
    {
        var result = await _branchService.GetBranchInventoryAsync(id, search);
        return Ok(result);
    }

    /// <summary>
    /// Lấy tồn kho của một biến thể tại chi nhánh cụ thể
    /// </summary>
    [HttpGet("{id}/inventory/{variantId}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.ThuKho},{RoleConstants.NhanVien},{RoleConstants.ThuNgan}")]
    public async Task<IActionResult> GetVariantInventoryAtBranch(int id, int variantId)
    {
        var result = await _branchService.GetVariantInventoryAtBranchAsync(id, variantId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Điều chỉnh tồn kho tại chi nhánh (nhập thêm, hao hụt, kiểm kê)
    /// </summary>
    [HttpPost("{id}/adjust-inventory")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.ThuKho}")]
    public async Task<IActionResult> AdjustBranchInventory(int id, [FromBody] AdjustBranchInventoryDto dto)
    {
        dto.MaChiNhanh = id;
        var result = await _branchService.AdjustBranchInventoryAsync(dto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
