namespace Backend.Entities;

public class SanPham
{
    public int MaSanPham { get; set; }
    public int MaDanhMuc { get; set; }
    public int MaThuongHieu { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string? ChatLieu { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime NgayCapNhat { get; set; }
    public bool DaXoa { get; set; }
}

public class BienTheSanPham
{
    public int MaBienThe { get; set; }
    public int MaSanPham { get; set; }
    public string MaSku { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public int SoLuongTon { get; set; }
    public decimal GiaNhap { get; set; }
    public decimal GiaBan { get; set; }
    public DateTime NgayCapNhat { get; set; }
}

public class HoaDon
{
    public int MaHoaDon { get; set; }
    public int? MaKhachHang { get; set; }
    public int MaNhanVien { get; set; }
    public int? MaKhuyenMai { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public decimal TienHang { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}

public class ChiTietHoaDon
{
    public int MaChiTietHoaDon { get; set; }
    public int MaHoaDon { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
}

public class KhachHang
{
    public int MaKhachHang { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string? Email { get; set; }
    public int DiemThuong { get; set; }
    public int MaCapDo { get; set; }
    public bool DaXoa { get; set; }
}

public class NhanVien
{
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string VaiTro { get; set; } = string.Empty;
    public bool DaXoa { get; set; }
}

public class TaiKhoan
{
    public int MaTaiKhoan { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
    public bool DangHoatDong { get; set; }
}

public class NhatKyKho
{
    public int MaNhatKy { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuongThayDoi { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public int? NguoiThayDoi { get; set; }
    public string LoaiNguoiThayDoi { get; set; } = "NHAN_VIEN";
    public DateTime ThoiGian { get; set; }
}
