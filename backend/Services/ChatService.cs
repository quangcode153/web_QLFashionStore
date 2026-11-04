using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FashionStore.Shared.DTOs.Chat;
using Microsoft.Extensions.Configuration;

namespace Backend.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _chatbotUrl;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public ChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _chatbotUrl = Environment.GetEnvironmentVariable("CHATBOT_URL")
            ?? configuration["ChatbotUrl"]
            ?? "http://localhost:3001";
    }

    public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return new ChatResponseDto
            {
                Reply = "Nội dung tin nhắn không được để trống. Quý khách vui lòng nhập câu hỏi để em hỗ trợ nhé ạ!",
                Engine = "Backend Validator"
            };
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeout);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"{_chatbotUrl.TrimEnd('/')}/api/chat",
                jsonContent,
                cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var chatResponse = await JsonSerializer.DeserializeAsync<ChatResponseDto>(
                    contentStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);

                if (chatResponse != null && !string.IsNullOrWhiteSpace(chatResponse.Reply))
                {
                    return chatResponse;
                }
            }
        }
        catch (Exception ex)
        {
            // Bắt TaskCanceledException (Timeout) và HttpRequestException (Connection Refused)
            // Kích hoạt Fallback Stylist Engine an toàn, tuyệt đối không quăng lỗi ra phía Client
            Console.WriteLine($"[ChatService Fallback Activated] Nguyên nhân: {ex.Message}");
        }

        // TẦNG BẢO HIỂM NỘI BỘ (BACKEND FALLBACK STYLIST ENGINE)
        return GenerateFallbackResponse(request.Message);
    }

    public async Task<object> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var response = await _httpClient.GetAsync($"{_chatbotUrl.TrimEnd('/')}/api/health", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<object>(raw) ?? new { status = "OK" };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatService Health Warning] Microservice offline: {ex.Message}");
        }

        return new
        {
            status = "DEGRADED",
            fallbackAvailable = true,
            engine = "Backend Fallback Stylist",
            message = "Stylist AI Microservice đang bận hoặc offline. Hệ thống tư vấn dự phòng tại Backend đang trực tuyến 100%."
        };
    }

    private static ChatResponseDto GenerateFallbackResponse(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();

        // 1. Phân tích chiều cao, cân nặng để tư vấn size
        if (Regex.IsMatch(lower, @"(m\d{2}|\d{3}\s*cm|\d{2}\s*kg|size|nặng|cao)"))
        {
            string recommendedSize = "L";
            if (lower.Contains("50kg") || lower.Contains("55kg") || lower.Contains("1m60") || lower.Contains("1m62"))
                recommendedSize = "S";
            else if (lower.Contains("60kg") || lower.Contains("62kg") || lower.Contains("1m65") || lower.Contains("1m68"))
                recommendedSize = "M";
            else if (lower.Contains("70kg") || lower.Contains("75kg") || lower.Contains("1m75") || lower.Contains("1m78"))
                recommendedSize = "XL";
            else if (lower.Contains("80kg") || lower.Contains("85kg") || lower.Contains("90kg"))
                recommendedSize = "XXL";

            return new ChatResponseDto
            {
                Reply = $"Dạ, dựa trên số đo của quý khách, em đề xuất quý khách nên chọn size {recommendedSize} để mặc vừa vặn và thoải mái nhất theo chuẩn form dáng của QF NOIR ạ. Nếu quý khách thích mặc rộng rãi kiểu Oversize đường phố, mình có thể tăng thêm 1 size nhé ạ!",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 2. Hỏi về các loại áo
        if (lower.Contains("áo"))
        {
            return new ChatResponseDto
            {
                Reply = "Dạ, hiện tại QF NOIR đang có sẵn các nhóm áo thời trang cao cấp sau để quý khách lựa chọn ạ:\n" +
                        "1. Áo Thun Nam: Thun Cotton Compact 220gsm (187.000đ), Áo Thun Cổ Tròn Premium (210.000đ), Áo Oversize Form Rộng (255.000đ).\n" +
                        "2. Áo Polo Nam: Polo Pique CVC co giãn 4 chiều thoáng khí (219.000đ).\n" +
                        "3. Áo Sơ Mi Nam: Sơ mi Oxford Dệt Tự Nhiên (256.000đ), Sơ mi Bamboo chống nhăn không cần ủi (475.000đ), Sơ mi Linen Pháp (464.000đ).\n" +
                        "4. Áo Khoác Nam: Áo khoác Bomber gió cản nước 2 lớp (171.000đ).\n" +
                        "Quý khách đang quan tâm đến mẫu áo nào để em kiểm tra màu sắc và bảng size chi tiết ạ?",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 3. Hỏi về các loại quần
        if (lower.Contains("quần"))
        {
            return new ChatResponseDto
            {
                Reply = "Dạ, QF NOIR có đa dạng các dòng quần nam hiện đại chuẩn phong cách Minimalist ạ:\n" +
                        "1. Quần Tây & Kaki: Quần tây lưng thun ẩn co giãn, Quần Kaki ống đứng tôn dáng lịch lãm.\n" +
                        "2. Quần Jean: Jean Cotton pha co giãn màu Đen và Xanh Indigo bền màu.\n" +
                        "3. Quần Short & Jogger: Short Kaki năng động, Jogger Nỉ Da Cá thể thao mùa hè.\n" +
                        "Quý khách đang tìm quần mặc đi làm công sở hay đi chơi dạo phố để em hỗ trợ tư vấn phối đồ ạ?",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 4. Hỏi về khuyến mãi / voucher
        if (lower.Contains("khuyến mãi") || lower.Contains("voucher") || lower.Contains("giảm giá") || lower.Contains("sale"))
        {
            return new ChatResponseDto
            {
                Reply = "Dạ, hiện tại shop đang áp dụng các chương trình khuyến mãi và ưu đãi rất hấp dẫn ạ:\n" +
                        "- Mã giảm giá Voucher trực tiếp khi thanh toán tại giỏ hàng.\n" +
                        "- Miễn phí vận chuyển toàn quốc cho đơn hàng từ 500.000đ.\n" +
                        "- Chiết khấu độc quyền thẻ thành viên VIP (Bạc: 5%, Vàng: 10%, Kim Cương: 15%) cộng dồn điểm thưởng sau mỗi lần mua sắm.\n" +
                        "Quý khách nhớ áp mã tại bước Đặt hàng để nhận ưu đãi nhé ạ!",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 5. Mặc định
        return new ChatResponseDto
        {
            Reply = "Dạ, em là Stylist AI của QF NOIR! Em có thể hỗ trợ quý khách tư vấn bảng size chuẩn, kiểm tra tình trạng còn hàng và gợi ý cách phối đồ thời trang. Quý khách vui lòng cho em biết chiều cao, cân nặng hoặc sản phẩm quý khách đang quan tâm để em hỗ trợ ngay nhé ạ!",
            Engine = "Backend Fallback Stylist"
        };
    }
}
