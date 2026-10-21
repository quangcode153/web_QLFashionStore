using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Catalog;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BannersController : ControllerBase
{
    private readonly IBannerService _bannerService;

    public BannersController(IBannerService bannerService)
    {
        _bannerService = bannerService;
    }

    /// <summary>
    /// Lấy danh sách banner quảng cáo đang kích hoạt để hiển thị Hero Slider ngoài trang chủ Storefront.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveBanners()
    {
        var banners = await _bannerService.GetActiveBannersAsync();
        return Ok(banners);
    }

    /// <summary>
    /// Lấy tất cả banner trong hệ thống phục vụ trang quản trị Admin.
    /// </summary>
    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllBanners()
    {
        var banners = await _bannerService.GetAllBannersAsync();
        return Ok(banners);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một banner theo mã ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBannerById(int id)
    {
        var banner = await _bannerService.GetByIdAsync(id);
        if (banner == null) return NotFound(new { message = $"Không tìm thấy banner với mã ID: {id}" });
        return Ok(banner);
    }

    /// <summary>
    /// Tạo mới banner quảng cáo (Chỉ dành cho Quản trị viên).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> CreateBanner([FromBody] CreateUpdateBannerDto dto)
    {
        try
        {
            var created = await _bannerService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBannerById), new { id = created.MaBanner }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật thông tin banner quảng cáo (Chỉ dành cho Quản trị viên).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> UpdateBanner(int id, [FromBody] CreateUpdateBannerDto dto)
    {
        try
        {
            var success = await _bannerService.UpdateAsync(id, dto);
            if (!success) return NotFound(new { message = $"Không tìm thấy banner với mã ID: {id}" });
            return Ok(new { message = "Cập nhật banner thành công." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa banner quảng cáo (Chỉ dành cho Quản trị viên).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},Admin")]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var success = await _bannerService.DeleteAsync(id);
        if (!success) return NotFound(new { message = $"Không tìm thấy banner với mã ID: {id}" });
        return Ok(new { message = "Xóa banner thành công." });
    }
}
