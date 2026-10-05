namespace Shared.DTOs.Order;

public class CreateOrderItemDto
{
    public int MaBienThe { get; set; }
    public int SoLuong { get; set; }
}

public class CreateOrderRequestDto
{
    public int? MaKhachHang { get; set; }
    public int MaNhanVien { get; set; }
    public string? MaGiamGia { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class OrderDetailItemDto
{
    public string TenSanPham { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}

public class OrderSummaryDto
{
    public int MaHoaDon { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? SdtKhachHang { get; set; }
    public string? CapDoThanhVien { get; set; }
    public string NguoiLapDon { get; set; } = string.Empty;
    public string? MaGiamGia { get; set; }
    public decimal TienHang { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
}
