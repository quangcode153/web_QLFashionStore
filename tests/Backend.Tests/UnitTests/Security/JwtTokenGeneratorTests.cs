using System;
using System.Collections.Generic;
using Backend.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Backend.Tests.UnitTests.Security;

/// <summary>
/// Kịch bản kiểm thử Fail-Fast của JWT Token Generator dựa trên TEST_SPECIFICATION.md:
/// - TS-SEC-02: Khóa bí mật JWT bắt buộc (Fail-Fast)
/// </summary>
public class JwtTokenGeneratorTests
{
    [Theory(DisplayName = "TS-SEC-02: JwtTokenGenerator phải ném InvalidOperationException khi thiếu JWT_SECRET_KEY hoặc quá ngắn (< 32 ký tự)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short_key_under_32_chars!")] // < 32 ký tự
    public void GenerateToken_ShouldThrowInvalidOperationException_WhenSecretKeyIsMissingOrTooShort(string? invalidSecretKey)
    {
        // Arrange
        // Xóa biến môi trường nếu có để test cấu hình Configuration
        var previousEnv = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);

        try
        {
            var configData = new Dictionary<string, string?>();
            if (invalidSecretKey != null)
            {
                configData["JWT_SECRET_KEY"] = invalidSecretKey;
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var generator = new JwtTokenGenerator(configuration);

            // Act
            Action act = () => generator.GenerateToken(1, "admin", "Admin chuỗi", "Admin");

            // Assert
            act.Should().Throw<InvalidOperationException>(
                "Hệ thống bắt buộc phải dừng ngay lập tức nếu JWT_SECRET_KEY thiếu hoặc < 32 ký tự theo chính sách Fail-Fast");
        }
        finally
        {
            // Phục hồi biến môi trường sau test
            Environment.SetEnvironmentVariable("JWT_SECRET_KEY", previousEnv);
        }
    }

    [Theory(DisplayName = "TS-SEC-02-Matrix: JwtTokenGenerator sinh token hợp lệ và giải mã đúng toàn bộ Claims trên ma trận các Vai trò")]
    [InlineData(1, "admin", "Nguyễn Văn Admin", "Admin chuỗi")]
    [InlineData(2, "manager_dn", "Lê Quản Lý", "Quản lý chi nhánh")]
    [InlineData(3, "warehouse_01", "Trần Thủ Kho", "Thủ kho")]
    [InlineData(4, "cashier_hn", "Phạm Thu Ngân", "Thu ngân")]
    [InlineData(1005, "khach_vip", "Hoàng Khách Hàng", "Khách hàng")]
    public void GenerateToken_ShouldReturnValidToken_AndContainExactClaims_AcrossRoles(
        int userId,
        string username,
        string fullName,
        string role)
    {
        // Arrange
        var validKey = "A_Very_Long_And_Super_Secure_Secret_Key_At_Least_32_Chars!";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET_KEY"] = validKey,
                ["JWT_ISSUER"] = "FashionStoreTestIssuer",
                ["JWT_AUDIENCE"] = "FashionStoreTestAudience"
            })
            .Build();

        var generator = new JwtTokenGenerator(configuration);

        // Act - Vòng 1: Sinh token (chú ý thứ tự tham số: userId, username, role, fullName)
        var (token, expiresAt) = generator.GenerateToken(userId, username, role, fullName);

        // Assert - Vòng 2: Kiểm tra định dạng cơ bản
        token.Should().NotBeNullOrWhiteSpace("Token phải được sinh thành công");
        expiresAt.Should().BeAfter(DateTime.UtcNow, "Thời hạn token phải trong tương lai");

        // Assert - Vòng 3: Giải mã JWT Payload để kiểm tra độ chính xác của Claims (Chống may mắn sinh chuỗi rác)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue("Chuỗi token phải là JWT hợp lệ");

        var jwtToken = handler.ReadJwtToken(token);
        
        // Kiểm tra Claim NameIdentifier / Sub (UserId)
        var idClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid" || c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        idClaim.Should().NotBeNull();
        idClaim!.Value.Should().Be(userId.ToString());

        // Kiểm tra Claim Name / UniqueName (TenDangNhap)
        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == System.Security.Claims.ClaimTypes.Name);
        nameClaim.Should().NotBeNull();
        nameClaim!.Value.Should().Be(username);

        // Kiểm tra Claim Role
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be(role);

        // Kiểm tra Issuer & Audience
        jwtToken.Issuer.Should().Be("FashionStoreTestIssuer");
        jwtToken.Audiences.Should().Contain("FashionStoreTestAudience");
    }
}
