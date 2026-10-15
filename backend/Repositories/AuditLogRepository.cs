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
            INSERT INTO NhatKyHeThong (loai_thao_tac, doi_tuong, noi_dung_chi_tiet, ma_nhan_vien, ip_dia_chi, thoi_gian)
            VALUES (@LoaiThaoTac, @DoiTuong, @NoiDungChiTiet, @MaNhanVien, @IpDiaChi, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var id = await connection.ExecuteScalarAsync<int>(sql, dto);
        return id;
    }

    public async Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(string? loaiThaoTac = null, int limit = 100)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT TOP (@Limit)
                nk.ma_nhat_ky AS MaNhatKy,
                nk.loai_thao_tac AS LoaiThaoTac,
                nk.doi_tuong AS DoiTuong,
                nk.noi_dung_chi_tiet AS NoiDungChiTiet,
                nk.ma_nhan_vien AS MaNhanVien,
                nv.ten_nhan_vien AS TenNhanVien,
                nv.vai_tro AS ChucVu,
                nk.ip_dia_chi AS IpDiaChi,
                nk.thoi_gian AS ThoiGian
            FROM NhatKyHeThong nk
            JOIN NhanVien nv ON nk.ma_nhan_vien = nv.ma_nhan_vien";

        if (!string.IsNullOrWhiteSpace(loaiThaoTac) && loaiThaoTac != "ALL")
        {
            sql += " WHERE nk.loai_thao_tac = @LoaiThaoTac";
        }

        sql += " ORDER BY nk.thoi_gian DESC;";

        var logs = await connection.QueryAsync<AuditLogDto>(sql, new { LoaiThaoTac = loaiThaoTac, Limit = limit });
        return logs;
    }
}
