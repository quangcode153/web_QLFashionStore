namespace Shared.DTOs.Catalog;

public class ProductListItemDto
{
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string TenDanhMuc { get; set; } = string.Empty;
    public string TenThuongHieu { get; set; } = string.Empty;
    public string? ChatLieu { get; set; }
    public decimal GiaThapNhat { get; set; }
    public decimal GiaCaoNhat { get; set; }
    public int TongTonKho { get; set; }
    public string? AnhDaiDien { get; set; }
}

public class ProductVariantDto
{
    public int MaBienThe { get; set; }
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string MaSku { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public decimal GiaBan { get; set; }
    public int SoLuongTon { get; set; }
    public string TinhTrangHang { get; set; } = string.Empty;
}

public class ProductFilterRequestDto
{
    public string? TuKhoa { get; set; }
    public int? MaDanhMuc { get; set; }
    public int? MaThuongHieu { get; set; }
    public decimal? GiaMin { get; set; }
    public decimal? GiaMax { get; set; }
}
