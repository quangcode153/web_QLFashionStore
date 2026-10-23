using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Promotion;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    /// <summary>
    /// Xác thực mã voucher và tính tiền giảm giá tức thì trước khi đặt hàng (FEAT-PRO-02).
    /// Hỗ trợ cả khách vãng lai, khách hàng đăng nhập và nhân viên thu ngân POS.
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateVoucher([FromBody] ValidateVoucherRequestDto request)
    {
        var result = await _promotionService.ValidateVoucherAsync(request);
        return Ok(result.Data);
    }

    /// <summary>
    /// Lấy danh sách các mã voucher & khuyến mãi đang có hiệu lực (FEAT-PRO-01).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivePromotions()
    {
        var result = await _promotionService.GetActivePromotionsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách mã giảm giá phục vụ quản trị Admin chuỗi.
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Shared.Constants.RoleConstants.AdminChuoi},{Shared.Constants.RoleConstants.QuanLyHeThong}")]
    public async Task<IActionResult> GetAllPromotions()
    {
        var result = await _promotionService.GetAllPromotionsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Tạo mã giảm giá mới và cấu hình thời gian khả dụng (Admin chuỗi).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Shared.Constants.RoleConstants.AdminChuoi},{Shared.Constants.RoleConstants.QuanLyHeThong}")]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionRequestDto request)
    {
        var result = await _promotionService.CreatePromotionAsync(request);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật thông tin mã giảm giá và thời gian khả dụng (Admin chuỗi).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{Shared.Constants.RoleConstants.AdminChuoi},{Shared.Constants.RoleConstants.QuanLyHeThong}")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] UpdatePromotionRequestDto request)
    {
        if (request != null) request.MaKhuyenMai = id;
        var result = await _promotionService.UpdatePromotionAsync(request!);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Xóa hoặc thu hồi mã giảm giá (Admin chuỗi).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Shared.Constants.RoleConstants.AdminChuoi},{Shared.Constants.RoleConstants.QuanLyHeThong}")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var result = await _promotionService.DeletePromotionAsync(id);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
