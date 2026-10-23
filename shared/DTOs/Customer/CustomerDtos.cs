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
    public string? TrangThaiHoanTra { get; set; }
    public int TongSoLuongDaMua { get; set; }
    public int TongSoLuongDaTra { get; set; }
    public string? DiaChiGiaoHang { get; set; }
    public string? LoaiDonHang { get; set; }

    public string PhuongThucThanhToan
    {
        get
        {
            if (string.Equals(LoaiDonHang, "TAI_QUAY", StringComparison.OrdinalIgnoreCase))
            {
                return "Tại quầy (POS)";
            }
            if (string.IsNullOrEmpty(DiaChiGiaoHang))
            {
                return "COD (Thanh toán khi nhận hàng)";
            }
            if (DiaChiGiaoHang.Contains("VIETQR", StringComparison.OrdinalIgnoreCase))
            {
                return "Chuyển khoản VietQR";
            }
            if (DiaChiGiaoHang.Contains("MOMO", StringComparison.OrdinalIgnoreCase))
            {
                return "Ví MoMo";
            }
            return "COD (Thanh toán khi nhận hàng)";
        }
    }

    public string DiaChiGiaoHangClean
    {
        get
        {
            if (string.IsNullOrEmpty(DiaChiGiaoHang)) return string.Empty;
            var idx = DiaChiGiaoHang.IndexOf(']');
            if (idx >= 0 && idx < DiaChiGiaoHang.Length - 1)
            {
                return DiaChiGiaoHang.Substring(idx + 1).Trim();
            }
            return DiaChiGiaoHang;
        }
    }

    public bool ConHangCoTheHoan => 
        (TongSoLuongDaMua > 0 ? (TongSoLuongDaMua > TongSoLuongDaTra) : (SoMatHang > 0 && TongSoLuongDaTra == 0)) && 
        (TrangThai == "Đã hoàn thành" || TrangThai == "Hoàn thành") && 
        TrangThaiHoanTra != "Cần duyệt" && 
        TrangThaiHoanTra != "Đang xử lý";
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

