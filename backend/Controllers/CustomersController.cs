using System.Security.Claims;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Lấy thông tin hồ sơ, điểm thưởng và cấp độ thành viên của khách hàng hiện tại (FEAT-CUS-02).
    /// </summary>
    [HttpGet("me/profile")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<string>.Fail("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _customerService.GetMyProfileAsync(userId.Value);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Tra cứu thông tin khách hàng VIP theo số điện thoại (phục vụ thu ngân bán lẻ tại quầy POS).
    /// </summary>
    [HttpGet("by-phone")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPhone([FromQuery] string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(ApiResponse<string>.Fail("Số điện thoại không được để trống."));
        }

        var result = await _customerService.GetProfileByPhoneAsync(phone);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Tra cứu lịch sử mua hàng cá nhân của khách hàng hiện tại (FEAT-CUS-01 / TS-CUS-01).
    /// </summary>
    [HttpGet("me/orders")]
    [Authorize]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<string>.Fail("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _customerService.GetMyOrderHistoryAsync(userId.Value);
        return Ok(result);
    }

    /// <summary>
    /// Kích hoạt thủ tục xét duyệt và cập nhật hạng thành viên tự động (FEAT-CUS-03 / sp_TuDongXetHangThanhVien).
    /// Dành riêng cho Quản trị viên và Quản lý chi nhánh.
    /// </summary>
    [HttpPost("eval-ranks")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> EvaluateMemberRanks()
    {
        var result = await _customerService.EvaluateMemberRanksAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ tài khoản khách hàng trong hệ thống (dành cho Admin / Quản lý).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> GetAllCustomers([FromQuery] string? search, [FromQuery] int? capDo, [FromQuery] bool? daXoa)
    {
        var result = await _customerService.GetAllCustomersAsync(search, capDo, daXoa);
        return Ok(result);
    }

    /// <summary>
    /// Khóa hoặc Mở khóa tài khoản khách hàng.
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> ToggleCustomerStatus(int id)
    {
        var result = await _customerService.ToggleCustomerStatusAsync(id);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật thông tin tài khoản khách hàng (Hạng thành viên, điểm thưởng, liên hệ).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerAccountDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse<bool>.Fail("Dữ liệu không hợp lệ."));
        }
        dto.MaKhachHang = id;
        var result = await _customerService.UpdateCustomerAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Tạo mới tài khoản khách hàng từ trang Quản trị.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerAccountDto dto)
    {
        var result = await _customerService.CreateCustomerAsync(dto);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return StatusCode(201, result);
    }

    /// <summary>
    /// Xem lịch sử đơn hàng của một khách hàng cụ thể (dành cho Quản trị viên).
    /// </summary>
    [HttpGet("{id}/orders")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> GetCustomerOrders(int id)
    {
        var result = await _customerService.GetCustomerOrderHistoryForAdminAsync(id);
        return Ok(result);
    }

    private int? GetCurrentUserId()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (int.TryParse(claimValue, out var userId))
        {
            return userId;
        }

        return null;
    }
}
