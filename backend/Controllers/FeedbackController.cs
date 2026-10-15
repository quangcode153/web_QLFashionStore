using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Feedback;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackRequestDto request)
    {
        var result = await _feedbackService.CreateFeedbackAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> GetAllFeedbacks()
    {
        var result = await _feedbackService.GetAllFeedbacksAsync();
        return Ok(result);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status, [FromQuery] string? notes)
    {
        var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        int.TryParse(staffIdClaim, out var staffId);

        var result = await _feedbackService.UpdateFeedbackStatusAsync(id, status, staffId > 0 ? staffId : 1, notes);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("return-request")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateReturnRequest([FromBody] CreateReturnRequestDto request)
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        int? customerId = int.TryParse(customerIdClaim, out var cId) ? cId : null;

        var result = await _feedbackService.CreateReturnRequestAsync(request, customerId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id}/process-return")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},{RoleConstants.OrderCoordinator}")]
    public async Task<IActionResult> ProcessReturn(int id, [FromBody] ProcessReturnRequestDto request)
    {
        var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        int.TryParse(staffIdClaim, out var staffId);

        var result = await _feedbackService.ProcessReturnRequestAsync(id, request, staffId > 0 ? staffId : 1);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("eligible-return-orders")]
    [Authorize]
    public async Task<IActionResult> GetEligibleReturnOrders()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(customerIdClaim, out var customerId) || customerId <= 0)
        {
            return Unauthorized(ApiResponse<string>.Fail("Vui lòng đăng nhập để xem danh sách đơn hàng có thể hoàn trả."));
        }

        var result = await _feedbackService.GetEligibleOrdersForReturnAsync(customerId);
        return Ok(result);
    }
}

