using System.Security.Claims;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.Enums;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.NhanVienBanHang},{RoleConstants.ThuNgan},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> GetAll([FromQuery] int? branchId)
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        int? userBranchId = null;
        var branchClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst("ma_chi_nhanh")?.Value;
        if (int.TryParse(branchClaim, out var parsedBranch))
        {
            userBranchId = parsedBranch;
        }
        else if (branchId.HasValue && branchId.Value > 0)
        {
            userBranchId = branchId.Value;
        }

        var isMultiBranchAdmin = string.Equals(userRole, RoleConstants.AdminChuoi, StringComparison.OrdinalIgnoreCase) 
                              || string.Equals(userRole, RoleConstants.QuanLyHeThong, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(userRole, "Administrator", StringComparison.OrdinalIgnoreCase);

        // Với vai trò trực thuộc chi nhánh (Điều phối viên, Quản lý chi nhánh, Thu ngân):
        // Luôn ép buộc branchId theo chi nhánh phân công của tài khoản (userBranchId)
        int? effectiveQueryBranch = isMultiBranchAdmin ? branchId : userBranchId;

        var result = await _orderService.GetAllOrdersAsync(effectiveQueryBranch, userRole, userBranchId);
        return Ok(result);
    }

    [HttpGet("{id}/details")]
    [Authorize]
    public async Task<IActionResult> GetDetails(int id)
    {
        var (exists, customerId) = await _orderService.GetOrderOwnershipAsync(id);
        if (!exists)
        {
            return NotFound(ApiResponse<string>.Fail("Đơn hàng không tồn tại trên hệ thống."));
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var isStaff = RoleConstants.IsStaff(userRole);

        if (!isStaff)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out var currentUserId) || customerId != currentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse<string>.Fail("Bạn không có quyền truy cập thông tin đơn hàng này (IDOR Protection)."));
            }
        }

        var result = await _orderService.GetOrderDetailsAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request)
    {
        if (request.MaChiNhanh == null || request.MaChiNhanh <= 0)
        {
            var branchClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst("ma_chi_nhanh")?.Value;
            if (int.TryParse(branchClaim, out var bId) && bId > 0)
            {
                request.MaChiNhanh = bId;
            }
        }

        var result = await _orderService.CreateOrderAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return CreatedAtAction(nameof(GetDetails), new { id = result.Data }, result);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.NhanVienBanHang},{RoleConstants.ThuNgan},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status)
    {
        if (!OrderStatusHelper.TryNormalizeStatus(status, out var normalizedStatus))
        {
            return BadRequest(ApiResponse<string>.Fail(
                $"Trạng thái '{status}' không hợp lệ. Các giá trị được chấp nhận: {OrderStatusHelper.GetAllowedStatusString()}."));
        }

        var (exists, _) = await _orderService.GetOrderOwnershipAsync(id);
        if (!exists)
        {
            return NotFound(ApiResponse<string>.Fail("Đơn hàng không tồn tại trên hệ thống."));
        }

        var result = await _orderService.UpdateOrderStatusAsync(id, normalizedStatus);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("{id}/print")]
    [Authorize]
    public async Task<IActionResult> PrintReceipt(int id)
    {
        var (exists, customerId) = await _orderService.GetOrderOwnershipAsync(id);
        if (!exists)
        {
            return NotFound(ApiResponse<string>.Fail("Đơn hàng không tồn tại trên hệ thống."));
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var isStaff = RoleConstants.IsStaff(userRole);

        if (!isStaff)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out var currentUserId) || customerId != currentUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse<string>.Fail("Bạn không có quyền in hóa đơn này (IDOR Protection)."));
            }
        }

        var result = await _orderService.GetReceiptForPrintAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("{id}/return-lookup")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.NhanVienBanHang},{RoleConstants.ThuNgan},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> GetOrderForReturnLookup(int id)
    {
        var result = await _orderService.GetOrderForReturnLookupAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("{id}/return")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVien},{RoleConstants.NhanVienBanHang},{RoleConstants.ThuNgan},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> ReturnOrder(int id, [FromBody] ReturnOrderRequestDto request)
    {
        var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;

        int.TryParse(staffIdClaim, out var staffId);

        var result = await _orderService.ProcessReturnAsync(id, request, staffId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

