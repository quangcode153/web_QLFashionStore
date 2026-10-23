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
    public decimal GiaGocNiNhat { get; set; }
    public int PhanTramGiamGia { get; set; }
    public bool CoGiamGia => PhanTramGiamGia > 0;
    public int TongTonKho { get; set; }
    public string? AnhDaiDien { get; set; }
    public bool DaXoa { get; set; }
    public int DaBanOnline { get; set; }
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
    public int PhanTramGiamGia { get; set; }
    public decimal GiaSauGiam => PhanTramGiamGia > 0 ? Math.Round(GiaBan * (100 - PhanTramGiamGia) / 100m, 0) : GiaBan;
    public int SoLuongTon { get; set; }
    public string TinhTrangHang { get; set; } = string.Empty;
    public string? HinhAnh { get; set; }
    public bool DaXoa { get; set; }
}

public class ProductImageDto
{
    public int MaHinhAnh { get; set; }
    public int MaSanPham { get; set; }
    public string DuongDanAnh { get; set; } = string.Empty;
    public bool LaAnhChinh { get; set; }
}

public class ProductFilterRequestDto
{
    public string? TuKhoa { get; set; }
    public int? MaDanhMuc { get; set; }
    public int? MaThuongHieu { get; set; }
    public decimal? GiaMin { get; set; }
    public decimal? GiaMax { get; set; }
}

public class CategoryDto
{
    public int MaDanhMuc { get; set; }
    public int? MaDanhMucCha { get; set; }
    public string TenDanhMuc { get; set; } = string.Empty;
}

public class BrandDto
{
    public int MaThuongHieu { get; set; }
    public string TenThuongHieu { get; set; } = string.Empty;
    public string? XuatXu { get; set; }
}

public class CreateProductImageDto
{
    public string DuongDanAnh { get; set; } = string.Empty;
    public bool LaAnhChinh { get; set; } = false;
}

public class CreateProductRequest
{
    public string TenSanPham { get; set; } = string.Empty;
    public int MaDanhMuc { get; set; }
    public int MaThuongHieu { get; set; }
    public string? ChatLieu { get; set; }
    public List<CreateProductImageDto> Images { get; set; } = new();
}

public class CreateProductVariantRequest
{
    public int MaSanPham { get; set; }
    public string MaSku { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public decimal GiaNhap { get; set; }
    public decimal GiaBan { get; set; }
    public int PhanTramGiamGia { get; set; } = 0;
    public int SoLuongTon { get; set; }
}

public class UpdateVariantDiscountRequest
{
    public int MaBienThe { get; set; }
    public int PhanTramGiamGia { get; set; }
}

public class UpdateProductDiscountRequest
{
    public int MaSanPham { get; set; }
    public int PhanTramGiamGia { get; set; }
}

public class ProductBranchStockDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = string.Empty;
    public string? DiaChi { get; set; }
    public bool IsOnlineWarehouse { get; set; }
    public int MaBienThe { get; set; }
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public string MaSku { get; set; } = string.Empty;
    public int SoLuongTon { get; set; }
}

public class CreateCategoryRequest
{
    public string TenDanhMuc { get; set; } = string.Empty;
    public int? MaDanhMucCha { get; set; }
}

public class CreateBrandRequest
{
    public string TenThuongHieu { get; set; } = string.Empty;
    public string? XuatXu { get; set; }
}

public class UpdateProductRequest
{
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public int MaDanhMuc { get; set; }
    public int MaThuongHieu { get; set; }
    public string? ChatLieu { get; set; }
    public string? AnhDaiDien { get; set; }
}

public class CheckVariantAvailabilityRequest
{
    public List<int> VariantIds { get; set; } = new();
}

public class VariantAvailabilityDto
{
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string? MauSac { get; set; }
    public string? KichCo { get; set; }
    public bool DaAn { get; set; }
    public string? ThongBao { get; set; }
}
