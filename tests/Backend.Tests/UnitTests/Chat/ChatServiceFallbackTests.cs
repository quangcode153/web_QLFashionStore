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

public class ChatServiceFallbackTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ChatServiceFallbackTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);

        var configValues = new Dictionary<string, string?>
        {
            ["ChatbotUrl"] = "http://localhost:3001"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Theory]
    [InlineData("có những loại áo nào", "áo")]
    [InlineData("shop có những mẫu quần nào", "quần")]
    [InlineData("mình cao 1m72 nặng 65kg mặc size gì", "size")]
    [InlineData("shop có khuyến mãi gì không", "khuyến mãi")]
    [InlineData("tư vấn phối đồ đi chơi", "phối")]
    [InlineData("shop có balo hay phụ kiện gì", "balo")]
    public async Task ProcessChat_WhenMicroserviceTimesOut_ShouldReturnGracefulFallback(string userMsg, string expectedKeyword)
    {
        // ARRANGE: Giả lập Microservice bị Timeout quá thời gian
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 10 seconds elapsing."));

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto { Message = userMsg };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT: Phải nhận được phản hồi lịch sự, KHÔNG ĐƯỢC ném ngoại lệ hay trả chuỗi lỗi kỹ thuật
        result.Should().NotBeNull();
        result.Reply.Should().NotBeNullOrWhiteSpace();
        result.Reply.Should().NotContain("The request was canceled");
        result.Reply.Should().NotContain("HttpClient.Timeout");
        result.Reply.ToLower().Should().Contain(expectedKeyword);
        result.Engine.Should().Be("Backend Fallback Stylist");
    }

    [Fact]
    public async Task ProcessChat_WhenMicroserviceConnectionRefused_ShouldReturnGracefulFallback()
    {
        // ARRANGE: Giả lập Microservice bị sập hoàn toàn (Connection Refused)
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No connection could be made because the target machine actively refused it."));

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto { Message = "tư vấn size giúp mình" };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Reply.Should().NotBeNullOrWhiteSpace();
        result.Reply.Should().NotContain("actively refused it");
        result.Engine.Should().Be("Backend Fallback Stylist");
    }

    [Fact]
    public async Task ProcessChat_WhenMicroserviceRespondsSuccessfully_ShouldPassThrough()
    {
        // ARRANGE: Microservice phản hồi 200 OK chuẩn
        var expectedReply = "Dạ, hiện tại QF NOIR có các mẫu Áo polo Pique cao cấp giá 219.000đ ạ.";
        var responsePayload = new ChatResponseDto
        {
            Reply = expectedReply,
            Engine = "Groq Cloud (Online)"
        };

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responsePayload), System.Text.Encoding.UTF8, "application/json")
            });

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto { Message = "tư vấn áo polo" };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Reply.Should().Be(expectedReply);
        result.Engine.Should().Be("Groq Cloud (Online)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ProcessChat_WhenMessageIsEmpty_ShouldReturnValidationError(string? emptyMsg)
    {
        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto { Message = emptyMsg! };

        var result = await service.ProcessChatAsync(request);

        result.Should().NotBeNull();
        result.Reply.Should().Contain("Nội dung tin nhắn không được để trống");
    }

    [Fact]
    public async Task CheckHealth_WhenMicroserviceIsDown_ShouldReturnDegradedWithFallbackAvailable()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Server down"));

        var service = new ChatService(_httpClient, _configuration);

        var health = await service.CheckHealthAsync();

        health.Should().NotBeNull();
        var json = JsonSerializer.Serialize(health);
        json.Should().Contain("DEGRADED");
        json.Should().Contain("fallbackAvailable");
    }
}

