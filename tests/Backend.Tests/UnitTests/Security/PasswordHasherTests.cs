using System;
using Backend.Security;
using FluentAssertions;
using Xunit;

namespace Backend.Tests.UnitTests.Security;

/// <summary>
/// Kịch bản kiểm thử bảo mật mật khẩu dựa trên đặc tả TEST_SPECIFICATION.md:
/// - TS-SEC-01: Băm mật khẩu PBKDF2 ngẫu nhiên Salt
/// - TS-SEC-02: Khóa bí mật Pepper bắt buộc (Fail-Fast)
/// </summary>
public class PasswordHasherTests
{
    private const string ValidPepper = "A_Secure_Secret_Pepper_Key_123456789";

    [Theory(DisplayName = "TS-SEC-01-Matrix: Băm và xác thực thành công qua ma trận nhiều loại mật khẩu (Unicode, Ký tự lạ, Dài, Ngắn)")]
    [InlineData("Admin@123")]
    [InlineData("MậtKhẩu_TiếngViệt_CóDấu_2026!#$")]
    [InlineData("12345678")]
    [InlineData("P@$$w0rd_With_Very_Long_Length_More_Than_64_Characters_Super_Secure_And_Robust_1234567890!@#$%^&*()")]
    [InlineData("   password_with_spaces   ")]
    public void HashPassword_ShouldSuccessfullyHashAndVerify_AcrossPasswordMatrix(string rawPassword)
    {
        // Arrange
        var hasher = new PasswordHasher(ValidPepper);

        // Act - Vòng 1: Băm 2 lần liên tiếp
        var hash1 = hasher.HashPassword(rawPassword);
        var hash2 = hasher.HashPassword(rawPassword);

        // Assert - Vòng 2: Kiểm tra cấu trúc phân tách và Salt ngẫu nhiên
        hash1.Should().Contain(":");
        hash2.Should().Contain(":");
        hash1.Should().NotBe(hash2, "Cùng mật khẩu băm 2 lần phải sinh 2 salt ngẫu nhiên khác nhau");

        // Assert - Vòng 3: Xác thực đúng mật khẩu
        hasher.VerifyPassword(rawPassword, hash1).Should().BeTrue("Mật khẩu gốc phải xác thực thành công");
        hasher.VerifyPassword(rawPassword, hash2).Should().BeTrue("Mật khẩu gốc phải xác thực thành công");

        // Assert - Vòng 4: Mật khẩu sai lệch 1 ký tự phải bị từ chối
        hasher.VerifyPassword(rawPassword + "_wrong", hash1).Should().BeFalse("Mật khẩu sai lệch phải bị từ chối");
    }

    [Theory(DisplayName = "TS-SEC-01-Defense: Xác thực an toàn phòng thủ trả về false với các chuỗi hash hỏng, rác, hoặc tấn công")]
    [InlineData("admin123", null)]
    [InlineData("admin123", "")]
    [InlineData("admin123", "   ")]
    [InlineData("admin123", "khong_co_dau_hai_cham")]
    [InlineData("admin123", "mot:hai:ba_qua_nhieu_dau_hai_cham")]
    [InlineData("admin123", "invalidbase64salt:invalidbase64hash")]
    [InlineData("admin123", "' OR '1'='1")]
    public void VerifyPassword_ShouldReturnFalseSafely_WhenHashIsMalformedOrTampered(string password, string? malformedHash)
    {
        // Arrange
        var hasher = new PasswordHasher(ValidPepper);

        // Act
        var result = hasher.VerifyPassword(password, malformedHash!);

        // Assert
        result.Should().BeFalse("Hệ thống phải trả về false an toàn thay vì crash/ném exception khi hash bị sai định dạng");
    }

    [Theory(DisplayName = "TS-SEC-02: Fail-Fast ném ngoại lệ nếu thiếu Pepper hoặc Pepper quá ngắn (< 16 ký tự)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short_pepper")] // < 16 ký tự
    public void Constructor_ShouldThrowInvalidOperationException_WhenPepperIsMissingOrTooShort(string? invalidPepper)
    {
        // Act & Assert
        Action act = () => new PasswordHasher(invalidPepper!);

        act.Should().Throw<InvalidOperationException>(
            "Hệ thống bắt buộc phải dừng ngay lập tức nếu Pepper không an toàn theo chính sách Fail-Fast");
    }
}
