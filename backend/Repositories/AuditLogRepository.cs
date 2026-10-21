using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Shared.DTOs.Audit;

namespace Backend.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAuditLogAsync(CreateAuditLogRequestDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO NhatKyHeThong (loai_thao_tac, doi_tuong, noi_dung_chi_tiet, ma_nhan_vien, ma_chi_nhanh, ip_dia_chi, thoi_gian)
            VALUES (@LoaiThaoTac, @DoiTuong, @NoiDungChiTiet, @MaNhanVien, @MaChiNhanh, @IpDiaChi, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var id = await connection.ExecuteScalarAsync<int>(sql, dto);
        return id;
    }

    public async Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(string? loaiThaoTac = null, int? maChiNhanh = null, int limit = 100)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT TOP (@Limit)
                nk.ma_nhat_ky AS MaNhatKy,
                nk.loai_thao_tac AS LoaiThaoTac,
                nk.doi_tuong AS DoiTuong,
                nk.noi_dung_chi_tiet AS NoiDungChiTiet,
                nk.ma_nhan_vien AS MaNhanVien,
                ISNULL(nv.ten_nhan_vien, N'Quản trị viên') AS TenNhanVien,
                ISNULL(nv.vai_tro, N'Hệ thống') AS ChucVu,
                COALESCE(nk.ma_chi_nhanh, nv.ma_chi_nhanh) AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                nk.ip_dia_chi AS IpDiaChi,
                nk.thoi_gian AS ThoiGian
            FROM NhatKyHeThong nk
            LEFT JOIN NhanVien nv ON nk.ma_nhan_vien = nv.ma_nhan_vien
            LEFT JOIN ChiNhanh cn ON COALESCE(nk.ma_chi_nhanh, nv.ma_chi_nhanh) = cn.ma_chi_nhanh
            WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(loaiThaoTac) && loaiThaoTac != "ALL")
        {
            sql += " AND nk.loai_thao_tac = @LoaiThaoTac";
        }

        if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
        {
            sql += " AND COALESCE(nk.ma_chi_nhanh, nv.ma_chi_nhanh) = @MaChiNhanh";
        }

        sql += " ORDER BY nk.thoi_gian DESC;";

        var logs = await connection.QueryAsync<AuditLogDto>(sql, new { LoaiThaoTac = loaiThaoTac, MaChiNhanh = maChiNhanh, Limit = limit });
        return logs;
    }
}
