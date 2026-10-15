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
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? LoaiThuKho { get; set; }
    public bool IsOnlineWarehouse { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class RegisterCustomerDto
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class CreateStaffAccountDto
{
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhau { get; set; } = string.Empty;
    public string TenNhanVien { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty; // "Quản lý chi nhánh", "Thủ kho", "Nhân viên bán hàng" / "Thu ngân"
    public int? MaChiNhanh { get; set; }
    public string? ChiNhanh { get; set; }
    public string? LoaiThuKho { get; set; } // "ONLINE" (Kho Tổng Online) hoặc "CHI_NHANH" (Thủ kho cơ sở)
}

public class AccountSummaryDto
{
    public int MaTaiKhoan { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty;
    public int? MaChiNhanh { get; set; }
    public string? ChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? LoaiThuKho { get; set; }
    public bool IsOnlineWarehouse { get; set; }
    public bool DangHoatDong { get; set; }
}

