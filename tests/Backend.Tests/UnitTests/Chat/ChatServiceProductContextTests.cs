using System.Net;
using Backend.Services;
using FashionStore.Shared.DTOs.Chat;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace Backend.Tests.UnitTests.Chat;

public class ChatServiceProductContextTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ChatServiceProductContextTests()
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

    [Fact]
    public async Task ProcessChat_WithProductContext_StockInquiry_ReturnsAccurateVariantStock()
    {
        // ARRANGE: Giả lập Microservice timeout/offline để kích hoạt logic fallback
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto
        {
            Message = "mẫu này còn hàng không shop?",
            ProductContext = new ProductContextDto
            {
                Id = 101,
                Name = "Áo Thun Graphic QF Noir",
                Price = 249000,
                Color = "Đen",
                Size = "XL",
                Sku = "AT-NOIR-BLK-XL",
                Stock = 8,
                HasSelectedVariant = true
            }
        };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Reply.Should().Contain("Áo Thun Graphic QF Noir");
        result.Reply.Should().Contain("Đen");
        result.Reply.Should().Contain("XL");
        result.Reply.Should().Contain("8");
    }

    [Fact]
    public async Task ProcessChat_WithProductContext_MaterialInquiry_ReturnsAccurateMaterial()
    {
        // ARRANGE
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto
        {
            Message = "chất liệu áo này mặc có mát không?",
            ProductContext = new ProductContextDto
            {
                Id = 102,
                Name = "Áo Sơ Mi Lụa Dài Tay QF",
                Material = "Lụa Modal cao cấp chống nhăn",
                Price = 389000
            }
        };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Reply.Should().Contain("Áo Sơ Mi Lụa Dài Tay QF");
        result.Reply.Should().Contain("Lụa Modal cao cấp chống nhăn");
    }

    [Fact]
    public async Task ProcessChat_WithProductContext_StylingInquiry_ReturnsPersonalizedOutfitAdvice()
    {
        // ARRANGE
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var service = new ChatService(_httpClient, _configuration);
        var request = new ChatRequestDto
        {
            Message = "mẫu này phối đồ thế nào đẹp?",
            ProductContext = new ProductContextDto
            {
                Id = 103,
                Name = "Quần Jeans Slimfit Rách Gối QF",
                Price = 450000
            }
        };

        // ACT
        var result = await service.ProcessChatAsync(request);

        // ASSERT
        result.Should().NotBeNull();
        result.Reply.Should().Contain("Quần Jeans Slimfit Rách Gối QF");
        result.Reply.ToLower().Should().Contain("phối");
    }
}

