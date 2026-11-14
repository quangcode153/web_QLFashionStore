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
    private readonly string? _groqApiKey;
    private readonly string _groqModel;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private const string StylistSystemPrompt = 
        "Bạn là Stylist AI tư vấn thời trang cao cấp của thương hiệu QF NOIR. " +
        "Nhiệm vụ của bạn là tư vấn phối đồ thông minh, đề xuất màu sắc, cách mix & match giữa áo và quần, " +
        "hướng dẫn chọn bảng size chuẩn theo chiều cao và cân nặng. " +
        "Phong cách: Lịch sự, nhã nhặn, sang trọng, tinh tế và sành điệu. Trả lời bằng tiếng Việt ngắn gọn, súc tích, tự nhiên.";

    public ChatService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _chatbotUrl = Environment.GetEnvironmentVariable("CHATBOT_URL")
            ?? configuration["ChatbotUrl"]
            ?? "http://localhost:3001";

        _groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? configuration["Groq:ApiKey"]
            ?? configuration["GROQ_API_KEY"];

        _groqModel = Environment.GetEnvironmentVariable("GROQ_MODEL")
            ?? configuration["Groq:Model"]
            ?? configuration["GROQ_MODEL"]
            ?? "qwen/qwen3.8-27b";
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

        // TẦNG 1: GỌI TỚI MICROSERVICE CHATBOT (Node.js) NẾU ĐANG CHẠY
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
            Console.WriteLine($"[ChatService Microservice Offline/Timeout] Nguyên nhân: {ex.Message}. Kích hoạt trực tiếp Groq Cloud AI...");
        }

        // TẦNG 2: GỌI TRỰC TIẾP GROQ CLOUD AI ONLINE (TỰ ĐỘNG KHI MICROSERVICE OFFLINE)
        if (!string.IsNullOrWhiteSpace(_groqApiKey))
        {
            try
            {
                var groqResponse = await CallGroqCloudDirectlyAsync(request, cancellationToken);
                if (groqResponse != null)
                {
                    return groqResponse;
                }
            }
            catch (Exception groqEx)
            {
                Console.WriteLine($"[ChatService Groq Direct Call Failed]: {groqEx.Message}");
            }
        }

        // TẦNG 3: BẢO HIỂM NỘI BỘ (BACKEND FALLBACK STYLIST ENGINE) KHI HOÀN TOÀN MẤT KẾT NỐI MẠNG
        return GenerateFallbackResponse(request.Message);
    }

    private async Task<ChatResponseDto?> CallGroqCloudDirectlyAsync(ChatRequestDto request, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var messages = new List<object>
        {
            new { role = "system", content = StylistSystemPrompt }
        };

        if (request.History != null)
        {
            foreach (var h in request.History)
            {
                var role = h.Role?.ToLowerInvariant() == "user" ? "user" : "assistant";
                var content = h.Content ?? "";
                if (!string.IsNullOrWhiteSpace(content))
                {
                    messages.Add(new { role, content });
                }
            }
        }

        messages.Add(new { role = "user", content = request.Message });

        var payload = new
        {
            model = _groqModel,
            messages = messages,
            temperature = 0.3,
            max_tokens = 600
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _groqApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cts.Token);
        if (response.IsSuccessStatusCode)
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentElem))
                {
                    var reply = contentElem.GetString();
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        return new ChatResponseDto
                        {
                            Reply = reply.Trim(),
                            Engine = "Groq Cloud (Online)"
                        };
                    }
                }
            }
        }
        return null;
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

        // 5. Tư vấn phối đồ / Mix & Match
        if (lower.Contains("phối") || lower.Contains("mix") || lower.Contains("kết hợp") || lower.Contains("đi chơi") || lower.Contains("công sở") || lower.Contains("đi tiệc"))
        {
            return new ChatResponseDto
            {
                Reply = "Dạ, Stylist AI gợi ý các công thức phối đồ chuẩn phong cách thời trang QF NOIR ạ:\n" +
                        "1. Đi làm công sở lịch lãm: Áo sơ mi Oxford hoặc Áo polo Pique phối cùng Quần tây slimfit hoặc Quần kaki Chinos, kèm Thắt lưng da bò thật.\n" +
                        "2. Dạo phố năng động: Áo thun Cotton basic hoặc Áo polo thể thao phối cùng Quần short cargo túi hộp hoặc Quần jean slimfit co giãn.\n" +
                        "3. Smart Casual thu đông: Áo len cổ lọ dệt mịn bên trong kết hợp Áo khoác bomber gió hoặc Áo khoác dù 2 lớp chống nước.\n" +
                        "Quý khách đang chuẩn bị trang phục cho dịp nào để em tư vấn chi tiết hơn nhé ạ!",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 6. Hỏi về Balo & Phụ kiện
        if (lower.Contains("balo") || lower.Contains("túi") || lower.Contains("ví") || lower.Contains("thắt lưng") || lower.Contains("phụ kiện"))
        {
            return new ChatResponseDto
            {
                Reply = "Dạ, QF NOIR có các dòng phụ kiện thời trang nam cao cấp sau ạ:\n" +
                        "- Balo Laptop Nam QF Chống Trộm vải Oxford kháng nước (390.000đ).\n" +
                        "- Túi Đeo Chéo Nam Da PU Kháng Nước cao cấp (260.000đ).\n" +
                        "- Túi Tote Canvas Nam QF Tối Giản (150.000đ).\n" +
                        "- Thắt Lưng Da Bò Nam Khóa Kim sang trọng (280.000đ).\n" +
                        "- Ví Da Nam Gấp Đôi Cao Cấp và Ví Cầm Tay Clutch Da Bò Thật.\n" +
                        "Quý khách đang quan tâm đến phụ kiện nào để em kiểm tra tình trạng kho hàng ạ?",
                Engine = "Backend Fallback Stylist"
            };
        }

        // 7. Mặc định
        return new ChatResponseDto
        {
            Reply = "Dạ, em là Stylist AI của QF NOIR! Em có thể hỗ trợ quý khách tư vấn bảng size chuẩn, kiểm tra tình trạng còn hàng và gợi ý cách phối đồ thời trang. Quý khách vui lòng cho em biết chiều cao, cân nặng hoặc sản phẩm quý khách đang quan tâm để em hỗ trợ ngay nhé ạ!",
            Engine = "Backend Fallback Stylist"
        };
    }
}
