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
}
