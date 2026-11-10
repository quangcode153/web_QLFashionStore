using System.Net;
using System.Text.Json;
using Backend.Services;
using FashionStore.Shared.DTOs.Chat;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace Backend.Tests.UnitTests.Chat;

public class ChatServiceGroqOnlineTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ChatServiceGroqOnlineTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);

        var configValues = new Dictionary<string, string?>
        {
            ["ChatbotUrl"] = "http://localhost:3001",
            ["Groq:ApiKey"] = "gsk_test_mock_key_12345",
            ["Groq:Model"] = "qwen/qwen3.8-27b"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact(DisplayName = "TS-CHAT-GROQ-01: Khi Microservice 3001 offline, ChatService gọi trực tiếp Groq Cloud Online thay vì trả canned template")]
    public async Task ProcessChat_WhenMicroserviceOffline_ShouldCallGroqOnlineDirectly()
    {
        // ARRANGE:
        // Lần 1: Gọi tới Microservice 3001 bị sập (HttpRequestException)
        // Lần 2: Gọi tới Groq API (https://api.groq.com/openai/v1/chat/completions) trả về 200 OK với câu trả lời Stylist
        var groqResponseBody = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = "Với áo sơ mi, bạn nên phối cùng quần tây hoặc quần kaki để tạo phong cách thanh lịch, sang trọng!"
                    }
                }
            }
        });

        _httpHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused on 3001"))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(groqResponseBody, System.Text.Encoding.UTF8, "application/json")
            });

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto { Message = "bạn thấy tôi có áo sơ mi thì nên mua thêm quần gì" };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT: Phải nhận được câu trả lời từ Groq Online chứ không phải template offline
        result.Should().NotBeNull();
        result.Engine.Should().Be("Groq Cloud (Online)");
        result.Reply.Should().Contain("quần tây hoặc quần kaki");
        result.Reply.Should().NotContain("Dạ, hiện tại QF NOIR đang có sẵn các nhóm áo thời trang cao cấp sau");
    }
}

