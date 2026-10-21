using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Catalog;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyService _policyService;

    public PoliciesController(IPolicyService policyService)
    {
        _policyService = policyService;
    }

    /// <summary>
    /// Lấy danh sách chính sách bán hàng đang kích hoạt để hiển thị thanh điều hướng, footer và tabs ngoài Storefront.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivePolicies()
    {
        var policies = await _policyService.GetActivePoliciesAsync();
        return Ok(policies);
    }

    /// <summary>
    /// Lấy chi tiết nội dung chính sách theo slug định danh URL (cho Storefront đọc động).
    /// </summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPolicyBySlug(string slug)
    {
        var policy = await _policyService.GetBySlugAsync(slug);
        if (policy == null)
            return NotFound(new { message = $"Không tìm thấy chính sách với đường dẫn: {slug}" });

        return Ok(policy);
    }

    /// <summary>
    /// Lấy tất cả chính sách trong hệ thống phục vụ màn hình quản trị Admin (bao gồm cả trạng thái ẩn/hiện).
    /// </summary>
    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPolicies()
    {
        var policies = await _policyService.GetAllPoliciesAsync();
        return Ok(policies);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một chính sách theo mã ID (dành cho Admin chỉnh sửa modal).
    /// </summary>
    [HttpGet("id/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPolicyById(int id)
    {
        var policy = await _policyService.GetByIdAsync(id);
        if (policy == null)
            return NotFound(new { message = $"Không tìm thấy chính sách với mã ID: {id}" });

        return Ok(policy);
    }

    /// <summary>
    /// Tạo mới chính sách bán hàng (Chỉ dành cho Quản trị viên Admin).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> CreatePolicy([FromBody] CreateUpdatePolicyDto dto)
    {
        try
        {
            var created = await _policyService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetPolicyById), new { id = created.MaChinhSach }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật nội dung chính sách bán hàng (Chỉ dành cho Quản trị viên Admin).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> UpdatePolicy(int id, [FromBody] CreateUpdatePolicyDto dto)
    {
        try
        {
            var success = await _policyService.UpdateAsync(id, dto);
            if (!success)
                return NotFound(new { message = $"Không tìm thấy chính sách với mã ID: {id}" });

            return Ok(new { message = "Cập nhật chính sách bán hàng thành công." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa chính sách bán hàng (Chỉ dành cho Quản trị viên Admin).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> DeletePolicy(int id)
    {
        var success = await _policyService.DeleteAsync(id);
        if (!success)
            return NotFound(new { message = $"Không tìm thấy chính sách với mã ID: {id}" });

        return Ok(new { message = "Xóa chính sách bán hàng thành công." });
    }
}
