namespace Shared.DTOs.Catalog;

public class PolicyDto
{
    public int MaChinhSach { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string TieuDe { get; set; } = string.Empty;
    public string? TomTat { get; set; }
    public string NoiDung { get; set; } = string.Empty;
    public int ThuTu { get; set; } = 1;
    public bool KichHoat { get; set; } = true;
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? NguoiCapNhat { get; set; }
}

public class CreateUpdatePolicyDto
{
    public string Slug { get; set; } = string.Empty;
    public string TieuDe { get; set; } = string.Empty;
    public string? TomTat { get; set; }
    public string NoiDung { get; set; } = string.Empty;
    public int ThuTu { get; set; } = 1;
    public bool KichHoat { get; set; } = true;
    public string? NguoiCapNhat { get; set; }
}
