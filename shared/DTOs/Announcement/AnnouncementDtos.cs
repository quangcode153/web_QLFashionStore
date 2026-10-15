using System;

namespace Shared.DTOs.Announcement;

public class InternalAnnouncementDto
{
    public int MaThongBao { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public string MucDoUuTien { get; set; } = "Bình thường"; // Bình thường, Quan trọng, Khẩn cấp
    public string PhamViGui { get; set; } = "Toàn hệ thống"; // Toàn hệ thống, Nội bộ chi nhánh, Quản lý chi nhánh, Nhân viên bán hàng, Thủ kho
    public int? MaChiNhanh { get; set; }
    public int MaNguoiGui { get; set; }
    public string TenNguoiGui { get; set; } = string.Empty;
    public string? ChucVuNguoiGui { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public bool DaDoc { get; set; } = false;
}

public class CreateAnnouncementRequestDto
{
    public string TieuDe { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public string MucDoUuTien { get; set; } = "Bình thường";
    public string PhamViGui { get; set; } = "Toàn hệ thống";
    public int? MaChiNhanh { get; set; }
    public int MaNguoiGui { get; set; } = 1;
}
