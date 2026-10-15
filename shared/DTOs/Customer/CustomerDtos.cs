using System;

namespace Shared.DTOs.Customer;

public class CustomerProfileDto
{
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string? Email { get; set; }
    public int DiemThuong { get; set; }
    public int MaCapDo { get; set; }
    public string TenCapDo { get; set; } = "Thường";
    public decimal PhanTramGiamGia { get; set; }
    public decimal HeSoTichDiem { get; set; } = 1.0m;
    public decimal TongChiTieu { get; set; }
}

public class CustomerOrderHistoryItemDto
{
    public int MaHoaDon { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public decimal TienHang { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public int SoMatHang { get; set; }
}

public class RankEvaluationResultDto
{
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public decimal TongChiTieu { get; set; }
    public string CapDoMoi { get; set; } = string.Empty;
}

public class CustomerAccountDto
{
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? TenDangNhap { get; set; }
    public string? DienThoai { get; set; }
    public string? Email { get; set; }
    public int DiemThuong { get; set; }
    public int MaCapDo { get; set; }
    public string TenCapDo { get; set; } = "Thường";
    public decimal PhanTramGiamGia { get; set; }
    public decimal TongChiTieu { get; set; }
    public bool DaXoa { get; set; }
    public int TongSoDonHang { get; set; }
}

public class CreateCustomerAccountDto
{
    public string TenKhachHang { get; set; } = string.Empty;
    public string? TenDangNhap { get; set; }
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string MatKhau { get; set; } = "123456";
    public int MaCapDo { get; set; } = 1;
    public int DiemThuong { get; set; } = 0;
}

public class UpdateCustomerAccountDto
{
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string? Email { get; set; }
    public int MaCapDo { get; set; } = 1;
    public int DiemThuong { get; set; }
    public bool? DaXoa { get; set; }
}

