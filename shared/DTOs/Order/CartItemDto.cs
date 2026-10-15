namespace Shared.DTOs.Order;

public class CartItemDto
{
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string? HinhAnh { get; set; }
    public int MaBienThe { get; set; }
    public string MaSKU { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public decimal DonGia { get; set; }
    public int SoLuong { get; set; } = 1;
    public int SoLuongTon { get; set; } = 1;
    public decimal ThanhTien => DonGia * SoLuong;
}
