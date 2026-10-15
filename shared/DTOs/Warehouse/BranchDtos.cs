using System;

namespace Shared.DTOs.Warehouse;

public class BranchDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = string.Empty;
    public string DiaChi { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string LoaiChiNhanh { get; set; } = "Cửa hàng bán lẻ"; // "Cửa hàng bán lẻ" hoặc "Kho tổng online"
    public bool IsOnlineWarehouse { get; set; }
    public bool DangHoatDong { get; set; } = true;
    public DateTime NgayTao { get; set; }
}

public class CreateBranchDto
{
    public string TenChiNhanh { get; set; } = string.Empty;
    public string DiaChi { get; set; } = string.Empty;
    public string? DienThoai { get; set; }
    public string LoaiChiNhanh { get; set; } = "Cửa hàng bán lẻ";
}

public class BranchInventoryDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = string.Empty;
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string? MaSku { get; set; }
    public string? Barcode { get; set; }
    public int SoLuongTon { get; set; }
    public int MucCanhBaoTon { get; set; } = 10;
    public decimal GiaBan { get; set; }
}

public class AdjustBranchInventoryDto
{
    public int MaChiNhanh { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuongThayDoi { get; set; } // Dương: cộng thêm, Âm: trừ bớt
    public string LyDo { get; set; } = string.Empty;
    public int MaNhanVien { get; set; }
}
