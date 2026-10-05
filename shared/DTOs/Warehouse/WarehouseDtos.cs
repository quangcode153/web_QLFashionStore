namespace Shared.DTOs.Warehouse;

public class ImportStockRequestDto
{
    public int MaNhaCungCap { get; set; }
    public int MaNhanVien { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
}

public class LowStockAlertDto
{
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string MaSku { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public int SoLuongTon { get; set; }
    public decimal GiaNhap { get; set; }
}
