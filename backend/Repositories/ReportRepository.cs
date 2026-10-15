using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Shared.DTOs.Report;

namespace Backend.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<DailyRevenueReportDto>> GetDailyRevenueAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ngay_ban AS NgayBan,
                tong_so_don AS TongSoDon,
                tong_tien_hang AS TongTienHang,
                tong_tien_giam AS TongTienGiam,
                doanh_thu_thuc_te AS DoanhThuThucTe
            FROM vw_DoanhThuTheoNgay
            ORDER BY ngay_ban DESC;
        ";
        return await connection.QueryAsync<DailyRevenueReportDto>(sql);
    }

    public async Task<IEnumerable<TopSellingProductDto>> GetTopSellingProductsAsync(int top = 20)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP (@Top)
                ma_san_pham AS MaSanPham,
                ten_san_pham AS TenSanPham,
                ten_danh_muc AS TenDanhMuc,
                tong_so_luong_da_ban AS TongSoLuongDaBan,
                tong_doanh_thu_mang_lai AS TongDoanhThuMangLai
            FROM vw_TopSanPhamBanChay
            ORDER BY tong_so_luong_da_ban DESC;
        ";
        return await connection.QueryAsync<TopSellingProductDto>(sql, new { Top = top });
    }

    public async Task<FinancialRangeReportDto> GetFinancialRangeReportAsync(DateTime fromDate, DateTime toDate)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@TuNgay", fromDate.Date);
        parameters.Add("@DenNgay", toDate.Date);

        var result = await connection.QueryFirstOrDefaultAsync<FinancialRangeReportDto>(
            "dbo.sp_BaoCaoDoanhThuTheoKhoangThoiGian",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return result ?? new FinancialRangeReportDto();
    }

    public async Task<IEnumerable<BranchSalesLeaderboardDto>> GetBranchSalesLeaderboardAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            WITH BranchTotals AS (
                SELECT 
                    cn.ma_chi_nhanh AS MaChiNhanh,
                    cn.ten_chi_nhanh AS TenChiNhanh,
                    cn.loai_chi_nhanh AS LoaiChiNhanh,
                    CASE 
                        WHEN cn.loai_chi_nhanh = N'Kho tổng online' OR cn.ma_chi_nhanh = 9 THEN CAST(1 AS BIT) 
                        ELSE CAST(0 AS BIT) 
                    END AS IsOnlineWarehouse,
                    COUNT(hd.ma_hoa_don) AS TongSoDon,
                    ISNULL(SUM(hd.tong_thanh_toan), 0) AS TongDoanhThu
                FROM ChiNhanh cn
                LEFT JOIN HoaDon hd ON cn.ma_chi_nhanh = hd.ma_chi_nhanh AND hd.trang_thai = N'Đã hoàn thành'
                WHERE cn.dang_hoat_dong = 1
                GROUP BY cn.ma_chi_nhanh, cn.ten_chi_nhanh, cn.loai_chi_nhanh
            ),
            GrandTotal AS (
                SELECT ISNULL(SUM(TongDoanhThu), 0) AS AllRevenue FROM BranchTotals
            )
            SELECT 
                bt.MaChiNhanh,
                bt.TenChiNhanh,
                bt.LoaiChiNhanh,
                bt.IsOnlineWarehouse,
                bt.TongSoDon,
                bt.TongDoanhThu,
                CASE 
                    WHEN gt.AllRevenue > 0 THEN ROUND((bt.TongDoanhThu * 100.0) / gt.AllRevenue, 2)
                    ELSE 0 
                END AS TyLeDongGop
            FROM BranchTotals bt
            CROSS JOIN GrandTotal gt
            ORDER BY bt.TongDoanhThu DESC;
        ";
        return await connection.QueryAsync<BranchSalesLeaderboardDto>(sql);
    }

    public async Task<IEnumerable<BranchInventorySummaryDto>> GetBranchInventorySummaryAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                cn.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                CASE 
                    WHEN cn.loai_chi_nhanh = N'Kho tổng online' OR cn.ma_chi_nhanh = 9 THEN CAST(1 AS BIT) 
                    ELSE CAST(0 AS BIT) 
                END AS IsOnlineWarehouse,
                COUNT(DISTINCT tk.ma_bien_the) AS TongSoMatHang,
                ISNULL(SUM(tk.so_luong_ton), 0) AS TongSoLuongTon,
                ISNULL(SUM(CASE WHEN tk.so_luong_ton <= 10 THEN 1 ELSE 0 END), 0) AS SoMatHangSapHet,
                ISNULL(SUM(CAST(tk.so_luong_ton AS DECIMAL(15,2)) * ISNULL(bt.gia_nhap, 0)), 0) AS TongGiaTriTonKho
            FROM ChiNhanh cn
            LEFT JOIN TonKhoChiNhanh tk ON cn.ma_chi_nhanh = tk.ma_chi_nhanh
            LEFT JOIN BienTheSanPham bt ON tk.ma_bien_the = bt.ma_bien_the
            WHERE cn.dang_hoat_dong = 1
            GROUP BY cn.ma_chi_nhanh, cn.ten_chi_nhanh, cn.loai_chi_nhanh
            ORDER BY cn.ma_chi_nhanh ASC;
        ";
        return await connection.QueryAsync<BranchInventorySummaryDto>(sql);
    }
}
