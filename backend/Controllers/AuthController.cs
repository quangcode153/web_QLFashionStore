using System.Security.Claims;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Constants;
using Shared.DTOs.Auth;
using Shared.DTOs.Common;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("LoginRateLimit")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var response = await _authService.LoginAsync(request);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>
    /// Đăng ký tài khoản khách hàng mua sắm trực tuyến (Khách hàng)
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerDto request)
    {
        var response = await _authService.RegisterCustomerAsync(request);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>
    /// Quản lý khu vực (chi nhánh) hoặc Quản lý đa khu vực (Admin chuỗi) tạo tài khoản nhân viên.
    /// - Quản lý chi nhánh: chỉ được tạo Nhân viên bán hàng / Thu ngân.
    /// - Quản lý đa chi nhánh: được tạo Quản lý chi nhánh, Thu ngân, Thủ kho, v.v.
    /// </summary>
    [HttpPost("staff/create")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> CreateStaffAccount([FromBody] CreateStaffAccountDto request)
    {
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        int? currentBranchId = int.TryParse(User.FindFirst("branchId")?.Value, out var bId) ? bId : null;
        var response = await _authService.CreateStaffAccountAsync(request, currentUserRole, currentBranchId);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>
    /// Lấy danh sách tài khoản theo phân quyền
    /// - Quản lý đa khu vực: Xem toàn bộ tài khoản
    /// - Quản lý khu vực: Xem danh sách nhân viên chi nhánh
    /// </summary>
    [HttpGet("accounts")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> GetAccounts()
    {
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var response = await _authService.GetAccountsAsync(currentUserRole);
        return Ok(response);
    }

    /// <summary>
    /// Khóa / Mở khóa tài khoản
    /// </summary>
    [HttpPost("accounts/{id:int}/toggle")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> ToggleAccount(int id)
    {
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var response = await _authService.ToggleAccountStatusAsync(id, currentUserRole);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}

