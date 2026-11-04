using System.Text.Json.Serialization;

namespace FashionStore.Shared.DTOs.Chat;

public class ChatRequestDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<ChatHistoryItemDto>? History { get; set; } = new();
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

