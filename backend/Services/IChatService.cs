using FashionStore.Shared.DTOs.Chat;

namespace Backend.Services;

public interface IChatService
{
    Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default);
    Task<object> CheckHealthAsync(CancellationToken cancellationToken = default);
}

