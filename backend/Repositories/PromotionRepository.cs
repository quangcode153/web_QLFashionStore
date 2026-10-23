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
    Task<IEnumerable<PromotionEntity>> GetAllPromotionsAsync();
    Task<int> CreatePromotionAsync(PromotionEntity entity);
    Task<bool> UpdatePromotionAsync(PromotionEntity entity);
    Task<bool> DeletePromotionAsync(int id);
    Task<bool> ExistsCodeAsync(string code, int? excludeId = null);
    Task<bool> HasCustomerUsedPromotionAsync(int customerId, int promotionId);
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

    public async Task<IEnumerable<PromotionEntity>> GetAllPromotionsAsync()
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
            ORDER BY ma_khuyen_mai DESC;";
        return await connection.QueryAsync<PromotionEntity>(sql);
    }

    public async Task<int> CreatePromotionAsync(PromotionEntity entity)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ChuongTrinhKhuyenMai (
                ten_khuyen_mai, ma_giam_gia, phan_tram_giam, so_tien_giam,
                ngay_bat_dau, ngay_ket_thuc, mua_toi_thieu, so_luong_dung
            ) VALUES (
                @TenKhuyenMai, @MaGiamGia, @PhanTramGiam, @SoTienGiam,
                @NgayBatDau, @NgayKetThuc, @MuaToiThieu, @SoLuongDung
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        return await connection.ExecuteScalarAsync<int>(sql, entity);
    }

    public async Task<bool> UpdatePromotionAsync(PromotionEntity entity)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE ChuongTrinhKhuyenMai
            SET ten_khuyen_mai = @TenKhuyenMai,
                ma_giam_gia = @MaGiamGia,
                phan_tram_giam = @PhanTramGiam,
                so_tien_giam = @SoTienGiam,
                ngay_bat_dau = @NgayBatDau,
                ngay_ket_thuc = @NgayKetThuc,
                mua_toi_thieu = @MuaToiThieu,
                so_luong_dung = @SoLuongDung
            WHERE ma_khuyen_mai = @MaKhuyenMai;";
        var rows = await connection.ExecuteAsync(sql, entity);
        return rows > 0;
    }

    public async Task<bool> DeletePromotionAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            IF EXISTS (SELECT 1 FROM HoaDon WHERE ma_khuyen_mai = @Id)
            BEGIN
                UPDATE ChuongTrinhKhuyenMai 
                SET so_luong_dung = 0, ngay_ket_thuc = DATEADD(day, -1, CAST(GETDATE() AS DATE))
                WHERE ma_khuyen_mai = @Id;
                SELECT 1;
            END
            ELSE
            BEGIN
                DELETE FROM ChuongTrinhKhuyenMai WHERE ma_khuyen_mai = @Id;
                SELECT 1;
            END";
        var res = await connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return res > 0;
    }

    public async Task<bool> ExistsCodeAsync(string code, int? excludeId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1) FROM ChuongTrinhKhuyenMai
            WHERE UPPER(LTRIM(RTRIM(ma_giam_gia))) = UPPER(LTRIM(RTRIM(@Code)))
              AND (@ExcludeId IS NULL OR ma_khuyen_mai <> @ExcludeId);";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Code = code, ExcludeId = excludeId });
        return count > 0;
    }

    public async Task<bool> HasCustomerUsedPromotionAsync(int customerId, int promotionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM HoaDon 
                WHERE ma_khach_hang = @CustomerId 
                  AND ma_khuyen_mai = @PromotionId 
                  AND trang_thai <> N'Đã hủy'
            ) THEN 1 ELSE 0 END;";
        var res = await connection.ExecuteScalarAsync<int>(sql, new { CustomerId = customerId, PromotionId = promotionId });
        return res == 1;
    }
}
