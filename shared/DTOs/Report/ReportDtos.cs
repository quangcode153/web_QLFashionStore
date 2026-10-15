using System;

namespace Shared.DTOs.Report;

/// <summary>
/// DTO Báo cáo doanh thu theo ngày từ View vw_DoanhThuTheoNgay (FR-23)
/// </summary>
public class DailyRevenueReportDto
{
    public DateTime NgayBan { get; set; }
    public int TongSoDon { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal TongTienGiam { get; set; }
    public decimal DoanhThuThucTe { get; set; }
}

/// <summary>
/// DTO Top sản phẩm bán chạy nhất từ View vw_TopSanPhamBanChay (FR-24)
/// </summary>
public class TopSellingProductDto
{
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string TenDanhMuc { get; set; } = string.Empty;
    public int TongSoLuongDaBan { get; set; }
    public decimal TongDoanhThuMangLai { get; set; }
}

/// <summary>
/// DTO Báo cáo tài chính theo khoảng thời gian từ Stored Procedure sp_BaoCaoDoanhThuTheoKhoangThoiGian (FR-25)
/// </summary>
public class FinancialRangeReportDto
{
    public int TongSoDonHoanThanh { get; set; }
    public int TongSoKhachHang { get; set; }
    public decimal TongTienHang { get; set; }
    public decimal TongTienKhuyenMai { get; set; }
    public decimal TongDoanhThuThuc { get; set; }
}

/// <summary>
/// DTO Bảng xếp hạng doanh số bán hàng theo từng chi nhánh & kênh online
/// </summary>
public class BranchSalesLeaderboardDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = string.Empty;
    public string LoaiChiNhanh { get; set; } = string.Empty;
    public bool IsOnlineWarehouse { get; set; }
    public int TongSoDon { get; set; }
    public decimal TongDoanhThu { get; set; }
    public decimal TyLeDongGop { get; set; } // % đóng góp
}

/// <summary>
/// DTO Giám sát sản lượng tồn kho và giá trị tồn theo từng chi nhánh
/// </summary>
public class BranchInventorySummaryDto
{
    public int MaChiNhanh { get; set; }
    public string TenChiNhanh { get; set; } = string.Empty;
    public bool IsOnlineWarehouse { get; set; }
    public int TongSoMatHang { get; set; }
    public int TongSoLuongTon { get; set; }
    public int SoMatHangSapHet { get; set; }
    public decimal TongGiaTriTonKho { get; set; }
}
