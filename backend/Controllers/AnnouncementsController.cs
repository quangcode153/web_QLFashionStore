using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Announcement;

namespace Backend.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAnnouncements([FromQuery] string? phamVi = null, [FromQuery] int? maChiNhanh = null)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        int? currentUserId = int.TryParse(userIdClaim, out var uid) ? uid : null;

        var result = await _announcementService.GetAnnouncementsAsync(phamVi, maChiNhanh, currentUserId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequestDto request)
    {
        var result = await _announcementService.CreateAnnouncementAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] int id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized(Shared.DTOs.Common.ApiResponse<bool>.Fail("Vui lòng đăng nhập để đánh dấu đã đọc."));
        }

        var result = await _announcementService.MarkAsReadAsync(id, currentUserId);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead([FromQuery] string? phamVi = null, [FromQuery] int? maChiNhanh = null)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized(Shared.DTOs.Common.ApiResponse<int>.Fail("Vui lòng đăng nhập để đánh dấu đã đọc tất cả."));
        }

        var result = await _announcementService.MarkAllAsReadAsync(currentUserId, phamVi, maChiNhanh);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
