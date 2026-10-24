using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace Backend.Controllers;

/// <summary>
/// Proxy điều hướng yêu cầu Chatbot từ Frontend/Vercel sang Microservice Chatbot Node.js (Port 3001).
/// Giải quyết triệt để lỗi Mixed Content và tránh cần thêm đường hầm tunnel thứ 2.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _chatbotUrl;

    public ChatController(IConfiguration configuration)
    {
        _chatbotUrl = Environment.GetEnvironmentVariable("CHATBOT_URL") 
            ?? configuration["ChatbotUrl"] 
            ?? "http://localhost:3001";
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();
            var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_chatbotUrl.TrimEnd('/')}/api/chat", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return Content(responseContent, response.Content.Headers.ContentType?.MediaType ?? "application/json", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { reply = $"Không thể kết nối tới Chatbot AI Server: {ex.Message}" });
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_chatbotUrl.TrimEnd('/')}/api/health");
            var responseContent = await response.Content.ReadAsStringAsync();
            return Content(responseContent, "application/json", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { status = "ERROR", error = ex.Message });
        }
    }
}
