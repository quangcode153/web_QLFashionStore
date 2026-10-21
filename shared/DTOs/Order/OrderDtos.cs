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
    public int? MaChiNhanh { get; set; }
    public string? MaGiamGia { get; set; }
    public string? TrangThai { get; set; }
    public string? DiaChiGiaoHang { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class OrderDetailItemDto
{
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
    public int SoLuongDaTra { get; set; }
    public int SoLuongConLai => Math.Max(0, SoLuong - SoLuongDaTra);
    public bool DaTraHet => SoLuongConLai == 0;
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
    public int MaChiNhanh { get; set; } = 1;
    public string TenChiNhanh { get; set; } = "Chi nhánh 1 (Hội sở)";
    public string? LoaiDonHang { get; set; } = "ONLINE";
    public string? DiaChiGiaoHang { get; set; }
    public decimal SoTienDaHoan { get; set; } = 0;
    public bool CoHoanTra => SoTienDaHoan > 0;
}

public class ReceiptItemDto
{
    public string TenSanPham { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }
}

public class ReceiptPrintDto
{
    public int MaHoaDon { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? SdtKhachHang { get; set; }
    public string ThuNgan { get; set; } = string.Empty;
    public string? TenKhuyenMai { get; set; }
    public decimal TienHang { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public List<ReceiptItemDto> Items { get; set; } = new();
}

public class ReturnOrderRequestDto
{
    public int MaBienThe { get; set; }
    public int SoLuongTra { get; set; }
    public string? LyDoTra { get; set; }
}

public class ReturnOrderResultDto
{
    public int MaDonTra { get; set; }
    public int MaHoaDon { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuongTra { get; set; }
    public string LyDoTra { get; set; } = string.Empty;
    public string TrangThai { get; set; } = string.Empty;
}

public class OrderReturnLookupItemDto
{
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public decimal DonGia { get; set; }
    public int SoLuongMua { get; set; }
    public int SoLuongDaTra { get; set; }
    public int SoLuongConLai => Math.Max(0, SoLuongMua - SoLuongDaTra);
}

public class OrderReturnLookupDto
{
    public int MaHoaDon { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string? SdtKhachHang { get; set; }
    public string ThuNgan { get; set; } = string.Empty;
    public decimal TienHang { get; set; }
    public decimal TienGiamGia { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public bool IsEligibleForReturn => string.Equals(TrangThai?.Trim(), "Đã hoàn thành", StringComparison.OrdinalIgnoreCase);
    public string? IneligibilityReason { get; set; }

    // Thông tin địa chỉ & nơi nhận hàng lỗi hoàn về
    public string? DiaChiGiaoHang { get; set; }
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public string? DiaChiChiNhanh { get; set; }
    public string? HinhThucHoan { get; set; }
    public string? DiaChiThuHoi { get; set; }
    public int? MaChiNhanhHen { get; set; }
    public string? TenChiNhanhHen { get; set; }
    public string? DiaChiChiNhanhHen { get; set; }
    public string? DiaChiNhanHangHoanVe { get; set; }

    public List<OrderReturnLookupItemDto> Items { get; set; } = new();
}

