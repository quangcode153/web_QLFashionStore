using Backend.Services;
using FashionStore.Shared.DTOs.Chat;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Proxy điều hướng yêu cầu Chatbot từ Frontend sang Microservice Chatbot Node.js (Port 3001)
/// Tích hợp Tầng Bảo Hiểm (Backend Fallback Stylist) tự động ứng cứu khi Microservice bận hoặc timeout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _chatService.ProcessChatAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var health = await _chatService.CheckHealthAsync(cancellationToken);
        return Ok(health);
    }
}
