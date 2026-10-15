using System.Data;
using Dapper;
using Shared.DTOs.Catalog;

namespace Backend.Repositories;

public interface IBannerRepository
{
    Task<IEnumerable<BannerDto>> GetActiveBannersAsync();
    Task<IEnumerable<BannerDto>> GetAllBannersAsync();
    Task<BannerDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(CreateUpdateBannerDto dto);
    Task<bool> UpdateAsync(int id, CreateUpdateBannerDto dto);
    Task<bool> DeleteAsync(int id);
}

public class BannerRepository : IBannerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BannerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<BannerDto>> GetActiveBannersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_banner AS MaBanner,
                tieu_de AS TieuDe,
                phu_de AS PhuDe,
                gia_noi_bat AS GiaNoiBat,
                nhan_badge AS NhanBadge,
                hinh_anh AS HinhAnh,
                nut_chinh_text AS NutChinhText,
                nut_chinh_link AS NutChinhLink,
                nut_phu_text AS NutPhuText,
                nut_phu_link AS NutPhuLink,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat
            FROM Banner
            WHERE kich_hoat = 1
            ORDER BY thu_tu ASC, ma_banner ASC;";

        return await connection.QueryAsync<BannerDto>(sql);
    }

    public async Task<IEnumerable<BannerDto>> GetAllBannersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_banner AS MaBanner,
                tieu_de AS TieuDe,
                phu_de AS PhuDe,
                gia_noi_bat AS GiaNoiBat,
                nhan_badge AS NhanBadge,
                hinh_anh AS HinhAnh,
                nut_chinh_text AS NutChinhText,
                nut_chinh_link AS NutChinhLink,
                nut_phu_text AS NutPhuText,
                nut_phu_link AS NutPhuLink,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat
            FROM Banner
            ORDER BY thu_tu ASC, ma_banner ASC;";

        return await connection.QueryAsync<BannerDto>(sql);
    }

    public async Task<BannerDto?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_banner AS MaBanner,
                tieu_de AS TieuDe,
                phu_de AS PhuDe,
                gia_noi_bat AS GiaNoiBat,
                nhan_badge AS NhanBadge,
                hinh_anh AS HinhAnh,
                nut_chinh_text AS NutChinhText,
                nut_chinh_link AS NutChinhLink,
                nut_phu_text AS NutPhuText,
                nut_phu_link AS NutPhuLink,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat
            FROM Banner
            WHERE ma_banner = @Id;";

        return await connection.QueryFirstOrDefaultAsync<BannerDto>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(CreateUpdateBannerDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Banner (
                tieu_de, phu_de, gia_noi_bat, nhan_badge, hinh_anh, 
                nut_chinh_text, nut_chinh_link, nut_phu_text, nut_phu_link, 
                thu_tu, kich_hoat, ngay_tao, ngay_cap_nhat
            )
            VALUES (
                @TieuDe, @PhuDe, @GiaNoiBat, @NhanBadge, @HinhAnh, 
                @NutChinhText, @NutChinhLink, @NutPhuText, @NutPhuLink, 
                @ThuTu, @KichHoat, GETDATE(), GETDATE()
            );
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> UpdateAsync(int id, CreateUpdateBannerDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Banner
            SET 
                tieu_de = @TieuDe,
                phu_de = @PhuDe,
                gia_noi_bat = @GiaNoiBat,
                nhan_badge = @NhanBadge,
                hinh_anh = @HinhAnh,
                nut_chinh_text = @NutChinhText,
                nut_chinh_link = @NutChinhLink,
                nut_phu_text = @NutPhuText,
                nut_phu_link = @NutPhuLink,
                thu_tu = @ThuTu,
                kich_hoat = @KichHoat,
                ngay_cap_nhat = GETDATE()
            WHERE ma_banner = @Id;";

        var rows = await connection.ExecuteAsync(sql, new
        {
            Id = id,
            dto.TieuDe,
            dto.PhuDe,
            dto.GiaNoiBat,
            dto.NhanBadge,
            dto.HinhAnh,
            dto.NutChinhText,
            dto.NutChinhLink,
            dto.NutPhuText,
            dto.NutPhuLink,
            dto.ThuTu,
            dto.KichHoat
        });

        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"DELETE FROM Banner WHERE ma_banner = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }
}
