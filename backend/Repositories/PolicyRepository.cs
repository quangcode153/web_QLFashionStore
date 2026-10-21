using System.Data;
using Dapper;
using Shared.DTOs.Catalog;

namespace Backend.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PolicyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<PolicyDto>> GetActivePoliciesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_chinh_sach AS MaChinhSach,
                slug AS Slug,
                tieu_de AS TieuDe,
                tom_tat AS TomTat,
                noi_dung AS NoiDung,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat,
                ngay_tao AS NgayTao,
                ngay_cap_nhat AS NgayCapNhat,
                nguoi_cap_nhat AS NguoiCapNhat
            FROM ChinhSach
            WHERE kich_hoat = 1
            ORDER BY thu_tu ASC, ma_chinh_sach ASC;";

        return await connection.QueryAsync<PolicyDto>(sql);
    }

    public async Task<IEnumerable<PolicyDto>> GetAllPoliciesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_chinh_sach AS MaChinhSach,
                slug AS Slug,
                tieu_de AS TieuDe,
                tom_tat AS TomTat,
                noi_dung AS NoiDung,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat,
                ngay_tao AS NgayTao,
                ngay_cap_nhat AS NgayCapNhat,
                nguoi_cap_nhat AS NguoiCapNhat
            FROM ChinhSach
            ORDER BY thu_tu ASC, ma_chinh_sach ASC;";

        return await connection.QueryAsync<PolicyDto>(sql);
    }

    public async Task<PolicyDto?> GetBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_chinh_sach AS MaChinhSach,
                slug AS Slug,
                tieu_de AS TieuDe,
                tom_tat AS TomTat,
                noi_dung AS NoiDung,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat,
                ngay_tao AS NgayTao,
                ngay_cap_nhat AS NgayCapNhat,
                nguoi_cap_nhat AS NguoiCapNhat
            FROM ChinhSach
            WHERE slug = @Slug;";

        return await connection.QueryFirstOrDefaultAsync<PolicyDto>(sql, new { Slug = slug });
    }

    public async Task<PolicyDto?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_chinh_sach AS MaChinhSach,
                slug AS Slug,
                tieu_de AS TieuDe,
                tom_tat AS TomTat,
                noi_dung AS NoiDung,
                thu_tu AS ThuTu,
                kich_hoat AS KichHoat,
                ngay_tao AS NgayTao,
                ngay_cap_nhat AS NgayCapNhat,
                nguoi_cap_nhat AS NguoiCapNhat
            FROM ChinhSach
            WHERE ma_chinh_sach = @Id;";

        return await connection.QueryFirstOrDefaultAsync<PolicyDto>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(CreateUpdatePolicyDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ChinhSach (slug, tieu_de, tom_tat, noi_dung, thu_tu, kich_hoat, ngay_tao, ngay_cap_nhat, nguoi_cap_nhat)
            VALUES (@Slug, @TieuDe, @TomTat, @NoiDung, @ThuTu, @KichHoat, GETDATE(), GETDATE(), @NguoiCapNhat);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<bool> UpdateAsync(int id, CreateUpdatePolicyDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE ChinhSach
            SET slug = @Slug,
                tieu_de = @TieuDe,
                tom_tat = @TomTat,
                noi_dung = @NoiDung,
                thu_tu = @ThuTu,
                kich_hoat = @KichHoat,
                ngay_cap_nhat = GETDATE(),
                nguoi_cap_nhat = @NguoiCapNhat
            WHERE ma_chinh_sach = @Id;";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            Id = id,
            dto.Slug,
            dto.TieuDe,
            dto.TomTat,
            dto.NoiDung,
            dto.ThuTu,
            dto.KichHoat,
            dto.NguoiCapNhat
        });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"DELETE FROM ChinhSach WHERE ma_chinh_sach = @Id;";
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }
}
