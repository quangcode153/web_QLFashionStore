using System;

namespace Shared.DTOs.Feedback;

public class CreateFeedbackRequestDto
{
    public string HoTen { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string LoaiPhanHoi { get; set; } = "Góp ý sản phẩm";
    public string TieuDe { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
}

public class FeedbackDto
{
    public int MaPhanHoi { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string LoaiPhanHoi { get; set; } = string.Empty;
    public string TieuDe { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public string TrangThai { get; set; } = "Chưa xử lý";
    public int? MaNhanVienTiepNhan { get; set; }
    public string? GhiChuXuLy { get; set; }
    public int? MaHoaDon { get; set; }
    public int? MaBienThe { get; set; }
    public int? SoLuongTra { get; set; }
    public string? HinhThucHoan { get; set; }
    public string? DiaChiThuHoi { get; set; }
    public int? MaChiNhanhHen { get; set; }
    public string? TenChiNhanhHen { get; set; }
    public string? DiaChiChiNhanhHen { get; set; }
    public string? DiaChiNhanHangHoanVe { get; set; }
    public int? MaChiNhanhDonHang { get; set; }
    public string? TenChiNhanhDonHang { get; set; }
    public DateTime NgayGui { get; set; }
    public DateTime NgayCapNhat { get; set; }
}
