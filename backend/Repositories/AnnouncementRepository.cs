using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Shared.DTOs.Announcement;

namespace Backend.Repositories;

public class AnnouncementRepository : IAnnouncementRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AnnouncementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAnnouncementAsync(CreateAnnouncementRequestDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ThongBaoNoiBo (tieu_de, noi_dung, muc_do_uu_tien, pham_vi_gui, ma_chi_nhanh, ma_nguoi_gui, da_xoa, ngay_tao)
            VALUES (@TieuDe, @NoiDung, @MucDoUuTien, @PhamViGui, @MaChiNhanh, @MaNguoiGui, 0, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var id = await connection.ExecuteScalarAsync<int>(sql, dto);
        return id;
    }

    public async Task<IEnumerable<InternalAnnouncementDto>> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null, int? maTaiKhoan = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT 
                tb.ma_thong_bao AS MaThongBao,
                tb.tieu_de AS TieuDe,
                tb.noi_dung AS NoiDung,
                tb.muc_do_uu_tien AS MucDoUuTien,
                tb.pham_vi_gui AS PhamViGui,
                tb.ma_chi_nhanh AS MaChiNhanh,
                tb.ma_nguoi_gui AS MaNguoiGui,
                nv.ten_nhan_vien AS TenNguoiGui,
                nv.vai_tro AS ChucVuNguoiGui,
                tb.ngay_tao AS NgayTao,
                CAST(CASE WHEN doc.ma_thong_bao IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS DaDoc
            FROM ThongBaoNoiBo tb
            JOIN NhanVien nv ON tb.ma_nguoi_gui = nv.ma_nhan_vien
            LEFT JOIN ThongBao_NguoiDoc doc ON tb.ma_thong_bao = doc.ma_thong_bao AND doc.ma_tai_khoan = @MaTaiKhoan
            WHERE tb.da_xoa = 0";

        if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
        {
            sql += " AND (tb.ma_chi_nhanh IS NULL OR tb.ma_chi_nhanh = @MaChiNhanh OR tb.pham_vi_gui = N'Toàn hệ thống')";
        }

        if (!string.IsNullOrWhiteSpace(phamVi) && phamVi != "ALL")
        {
            sql += " AND (tb.pham_vi_gui = @PhamVi OR tb.pham_vi_gui = N'Toàn hệ thống')";
        }

        sql += " ORDER BY tb.ngay_tao DESC;";

        var list = await connection.QueryAsync<InternalAnnouncementDto>(sql, new { PhamVi = phamVi, MaChiNhanh = maChiNhanh, MaTaiKhoan = maTaiKhoan });
        return list;
    }

    public async Task<bool> MarkAsReadAsync(int maThongBao, int maTaiKhoan)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM ThongBao_NguoiDoc WHERE ma_thong_bao = @MaThongBao AND ma_tai_khoan = @MaTaiKhoan)
            BEGIN
                INSERT INTO ThongBao_NguoiDoc (ma_thong_bao, ma_tai_khoan, ngay_doc)
                VALUES (@MaThongBao, @MaTaiKhoan, GETDATE());
            END";
        await connection.ExecuteAsync(sql, new { MaThongBao = maThongBao, MaTaiKhoan = maTaiKhoan });
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int maTaiKhoan, string? phamVi = null, int? maChiNhanh = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            INSERT INTO ThongBao_NguoiDoc (ma_thong_bao, ma_tai_khoan, ngay_doc)
            SELECT tb.ma_thong_bao, @MaTaiKhoan, GETDATE()
            FROM ThongBaoNoiBo tb
            WHERE tb.da_xoa = 0
              AND NOT EXISTS (
                  SELECT 1 FROM ThongBao_NguoiDoc doc 
                  WHERE doc.ma_thong_bao = tb.ma_thong_bao AND doc.ma_tai_khoan = @MaTaiKhoan
              )";

        if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
        {
            sql += " AND (tb.ma_chi_nhanh IS NULL OR tb.ma_chi_nhanh = @MaChiNhanh OR tb.pham_vi_gui = N'Toàn hệ thống')";
        }

        if (!string.IsNullOrWhiteSpace(phamVi) && phamVi != "ALL")
        {
            sql += " AND (tb.pham_vi_gui = @PhamVi OR tb.pham_vi_gui = N'Toàn hệ thống')";
        }

        var affected = await connection.ExecuteAsync(sql, new { MaTaiKhoan = maTaiKhoan, PhamVi = phamVi, MaChiNhanh = maChiNhanh });
        return affected;
    }
}
