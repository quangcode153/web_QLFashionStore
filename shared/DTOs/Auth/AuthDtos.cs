namespace Shared.DTOs.Auth;

public class LoginRequestDto
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public int MaTaiKhoan { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
