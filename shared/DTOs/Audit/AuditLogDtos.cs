using System;

namespace Shared.DTOs.Audit;

public class AuditLogDto
{
    public int MaNhatKy { get; set; }
    public string LoaiThaoTac { get; set; } = string.Empty;
    public string DoiTuong { get; set; } = string.Empty;
    public string NoiDungChiTiet { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string? ChucVu { get; set; }
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? IpDiaChi { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.Now;
}

public class CreateAuditLogRequestDto
{
    public string LoaiThaoTac { get; set; } = string.Empty;
    public string DoiTuong { get; set; } = string.Empty;
    public string NoiDungChiTiet { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
    public int? MaChiNhanh { get; set; }
    public string? IpDiaChi { get; set; }
}
