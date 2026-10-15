using System.Data;
using Dapper;

namespace Backend.Repositories;

public class PromotionEntity
{
    public int MaKhuyenMai { get; set; }
    public string TenKhuyenMai { get; set; } = string.Empty;
    public string MaGiamGia { get; set; } = string.Empty;
    public decimal PhanTramGiam { get; set; }
    public decimal SoTienGiam { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public decimal MuaToiThieu { get; set; }
    public int? SoLuongDung { get; set; }
}

public interface IPromotionRepository
{
    Task<PromotionEntity?> GetByCodeAsync(string code);
    Task<IEnumerable<PromotionEntity>> GetActivePromotionsAsync();
    Task<decimal> CalculateDiscountDbAsync(int maKhuyenMai, decimal tienHang);
}

public class PromotionRepository : IPromotionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PromotionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PromotionEntity?> GetByCodeAsync(string code)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_khuyen_mai AS MaKhuyenMai,
                ten_khuyen_mai AS TenKhuyenMai,
                ma_giam_gia AS MaGiamGia,
                phan_tram_giam AS PhanTramGiam,
                so_tien_giam AS SoTienGiam,
                ngay_bat_dau AS NgayBatDau,
                ngay_ket_thuc AS NgayKetThuc,
                mua_toi_thieu AS MuaToiThieu,
                so_luong_dung AS SoLuongDung
            FROM ChuongTrinhKhuyenMai
            WHERE UPPER(LTRIM(RTRIM(ma_giam_gia))) = UPPER(LTRIM(RTRIM(@Code)));";

        return await connection.QueryFirstOrDefaultAsync<PromotionEntity>(sql, new { Code = code });
    }

    public async Task<IEnumerable<PromotionEntity>> GetActivePromotionsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_khuyen_mai AS MaKhuyenMai,
                ten_khuyen_mai AS TenKhuyenMai,
                ma_giam_gia AS MaGiamGia,
                phan_tram_giam AS PhanTramGiam,
                so_tien_giam AS SoTienGiam,
                ngay_bat_dau AS NgayBatDau,
                ngay_ket_thuc AS NgayKetThuc,
                mua_toi_thieu AS MuaToiThieu,
                so_luong_dung AS SoLuongDung
            FROM ChuongTrinhKhuyenMai
            WHERE (ngay_ket_thuc IS NULL OR ngay_ket_thuc >= CAST(GETDATE() AS DATE))
              AND (so_luong_dung IS NULL OR so_luong_dung > 0)
            ORDER BY ngay_bat_dau DESC;";

        return await connection.QueryAsync<PromotionEntity>(sql);
    }

    public async Task<decimal> CalculateDiscountDbAsync(int maKhuyenMai, decimal tienHang)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT dbo.fn_TinhTienGiamGiaVoucher(@MaKhuyenMai, @TienHang);";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { MaKhuyenMai = maKhuyenMai, TienHang = tienHang });
    }
}
