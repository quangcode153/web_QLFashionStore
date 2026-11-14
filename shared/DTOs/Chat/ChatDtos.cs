using System.Text.Json.Serialization;

namespace FashionStore.Shared.DTOs.Chat;

public class ChatRequestDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<ChatHistoryItemDto>? History { get; set; } = new();

    [JsonPropertyName("productContext")]
    public ProductContextDto? ProductContext { get; set; }
}

public class ProductContextDto
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("material")]
    public string? Material { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("stock")]
    public int? Stock { get; set; }

    [JsonPropertyName("hasSelectedVariant")]
    public bool HasSelectedVariant { get; set; }
}

public class ChatHistoryItemDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonPropertyName("toolsUsed")]
    public List<object>? ToolsUsed { get; set; } = new();

    [JsonPropertyName("engine")]
    public string? Engine { get; set; } = "Groq Cloud (Online)";
}

