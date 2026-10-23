using System.Net;
using System.Net.Http.Json;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.DTOs.Auth;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.IntegrationTests.Security;

/// <summary>
/// Kịch bản kiểm thử tích hợp xác thực và rate limiting dựa trên TEST_SPECIFICATION.md:
/// - TS-SEC-03: Đăng nhập thành công với tài khoản hợp lệ
/// - TS-SEC-04: Đăng nhập thất bại do sai mật khẩu
/// - TS-SEC-05: Rate Limiting chặn Brute-Force
/// </summary>
public class AuthIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "TS-SEC-03: Đăng nhập thành công trả về 200 OK và Token JWT hợp lệ")]
    public async Task Login_ShouldReturn200AndValidJwtToken_WhenCredentialsAreValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        var request = new LoginRequestDto
        {
            TenDangNhap = "admin",
            MatKhau = "admin123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Tài khoản admin với mật khẩu admin123 phải đăng nhập thành công");

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Token.Should().NotBeNullOrWhiteSpace("Hệ thống phải cấp phát Bearer Token sau khi đăng nhập thành công");
        apiResponse.Data.VaiTro.Should().Be("Admin chuỗi", "Vai trò trả về phải đúng với phân quyền hệ thống");
    }

    [Theory(DisplayName = "TS-SEC-04-Matrix: Đăng nhập thất bại trả về 400 Bad Request trên ma trận input không hợp lệ hoặc tấn công")]
    [InlineData("", "password123")]
    [InlineData("   ", "password123")]
    [InlineData("admin", "")]
    [InlineData("admin", "   ")]
    [InlineData("admin", "wrong_password_123")]
    [InlineData("non_existing_user", "password123")]
    [InlineData("admin' --", "password123")]
    [InlineData("' OR '1'='1", "' OR '1'='1")]
    public async Task Login_ShouldReturn400BadRequest_AcrossInvalidCredentialsMatrix(string username, string password)
    {
        // Arrange
        var client = _factory.CreateClient();
        // Dùng IP riêng biệt để không kích hoạt Rate Limiting của test case khác
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.0.1.{Math.Abs(username.GetHashCode()) % 200 + 10}");
        var request = new LoginRequestDto
        {
            TenDangNhap = username,
            MatKhau = password
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            $"Đăng nhập với username='{username}' không hợp lệ phải bị từ chối với 400 Bad Request");

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().NotBeNullOrWhiteSpace("Hệ thống phải trả về thông báo lỗi rõ ràng");
    }

    [Fact(DisplayName = "TS-SEC-05-Isolation: Rate Limiting chặn đúng IP vi phạm và KHÔNG chặn oan IP hợp lệ khác")]
    public async Task Login_ShouldBlockAbusiveIp_WhileAllowingCleanIpConcurrently()
    {
        // Arrange
        var clientAbusive = _factory.CreateClient();
        var abusiveIp = "192.168.100.99";
        clientAbusive.DefaultRequestHeaders.Add("X-Forwarded-For", abusiveIp);

        var clientClean = _factory.CreateClient();
        var cleanIp = "192.168.100.88";
        clientClean.DefaultRequestHeaders.Add("X-Forwarded-For", cleanIp);

        var badRequest = new LoginRequestDto
        {
            TenDangNhap = "brute_force_bot",
            MatKhau = "wrong_password"
        };

        // Act - Vòng 1: Gửi liên tiếp 5 requests từ clientAbusive (chưa vượt quá hạn ngạch)
        for (int i = 1; i <= 5; i++)
        {
            var res = await clientAbusive.PostAsJsonAsync("/api/auth/login", badRequest);
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"Yêu cầu thứ {i} trong hạn mức phải được xử lý");
        }

        // Act - Vòng 2: Yêu cầu thứ 6 từ clientAbusive vượt hạn mức
        var blockedResponse = await clientAbusive.PostAsJsonAsync("/api/auth/login", badRequest);

        // Act - Vòng 3: Ngay lúc clientAbusive bị chặn, clientClean gửi request
        var cleanResponse = await clientClean.PostAsJsonAsync("/api/auth/login", badRequest);

        // Assert
        blockedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, 
            "Client vi phạm gửi yêu cầu thứ 6 phải bị chặn với mã 429 Too Many Requests");
        
        cleanResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "Client trong sạch không được phép bị ảnh hưởng bởi Rate Limiter của client khác (Chống chặn chéo IP)");
    }
}
