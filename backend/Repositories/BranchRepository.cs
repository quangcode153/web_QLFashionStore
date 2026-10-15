using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Shared.DTOs.Warehouse;

namespace Backend.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly string _connectionString;

    public BranchRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new System.InvalidOperationException("DefaultConnection not found in configuration.");
    }

    public async Task<IEnumerable<BranchDto>> GetAllBranchesAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                ma_chi_nhanh AS MaChiNhanh,
                ten_chi_nhanh AS TenChiNhanh,
                dia_chi AS DiaChi,
                dien_thoai AS DienThoai,
                loai_chi_nhanh AS LoaiChiNhanh,
                CASE WHEN loai_chi_nhanh = N'Kho tổng online' OR ma_chi_nhanh = 9 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOnlineWarehouse,
                dang_hoat_dong AS DangHoatDong,
                ngay_tao AS NgayTao
            FROM ChiNhanh
            ORDER BY ma_chi_nhanh ASC";

        return await connection.QueryAsync<BranchDto>(sql);
    }

    public async Task<BranchDto?> GetBranchByIdAsync(int branchId)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                ma_chi_nhanh AS MaChiNhanh,
                ten_chi_nhanh AS TenChiNhanh,
                dia_chi AS DiaChi,
                dien_thoai AS DienThoai,
                loai_chi_nhanh AS LoaiChiNhanh,
                CASE WHEN loai_chi_nhanh = N'Kho tổng online' OR ma_chi_nhanh = 9 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOnlineWarehouse,
                dang_hoat_dong AS DangHoatDong,
                ngay_tao AS NgayTao
            FROM ChiNhanh
            WHERE ma_chi_nhanh = @branchId";

        return await connection.QuerySingleOrDefaultAsync<BranchDto>(sql, new { branchId });
    }

    public async Task<int> CreateBranchAsync(CreateBranchDto dto)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO ChiNhanh (ten_chi_nhanh, dia_chi, dien_thoai, loai_chi_nhanh, dang_hoat_dong, ngay_tao)
            VALUES (@TenChiNhanh, @DiaChi, @DienThoai, @LoaiChiNhanh, 1, GETDATE());
            SELECT SCOPE_IDENTITY();";

        return await connection.ExecuteScalarAsync<int>(sql, dto);
    }

    public async Task<IEnumerable<BranchInventoryDto>> GetBranchInventoryAsync(int branchId, string? search = null)
    {
        using var connection = new SqlConnection(_connectionString);
        var sql = @"
            SELECT 
                tk.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                tk.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.kich_co AS KichCo,
                bt.mau_sac AS MauSac,
                bt.ma_sku AS MaSku,
                NULL AS Barcode,
                tk.so_luong_ton AS SoLuongTon,
                tk.muc_canh_bao_ton AS MucCanhBaoTon,
                bt.gia_ban AS GiaBan
            FROM TonKhoChiNhanh tk
            JOIN ChiNhanh cn ON tk.ma_chi_nhanh = cn.ma_chi_nhanh
            JOIN BienTheSanPham bt ON tk.ma_bien_the = bt.ma_bien_the
            JOIN SanPham sp ON bt.ma_san_pham = sp.ma_san_pham
            WHERE tk.ma_chi_nhanh = @branchId";

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += @" AND (
                sp.ten_san_pham LIKE @Search 
                OR bt.ma_sku LIKE @Search 
                OR bt.barcode LIKE @Search 
                OR bt.mau_sac LIKE @Search 
                OR bt.kich_co LIKE @Search
            )";
        }

        sql += " ORDER BY tk.so_luong_ton ASC, sp.ten_san_pham ASC";

        return await connection.QueryAsync<BranchInventoryDto>(sql, new 
        { 
            branchId, 
            Search = $"%{search?.Trim()}%" 
        });
    }

    public async Task<BranchInventoryDto?> GetVariantInventoryAtBranchAsync(int branchId, int variantId)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                tk.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                tk.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.kich_co AS KichCo,
                bt.mau_sac AS MauSac,
                bt.ma_sku AS MaSku,
                NULL AS Barcode,
                tk.so_luong_ton AS SoLuongTon,
                tk.muc_canh_bao_ton AS MucCanhBaoTon,
                bt.gia_ban AS GiaBan
            FROM TonKhoChiNhanh tk
            JOIN ChiNhanh cn ON tk.ma_chi_nhanh = cn.ma_chi_nhanh
            JOIN BienTheSanPham bt ON tk.ma_bien_the = bt.ma_bien_the
            JOIN SanPham sp ON bt.ma_san_pham = sp.ma_san_pham
            WHERE tk.ma_chi_nhanh = @branchId AND tk.ma_bien_the = @variantId";

        return await connection.QuerySingleOrDefaultAsync<BranchInventoryDto>(sql, new { branchId, variantId });
    }

    public async Task<bool> AdjustBranchInventoryAsync(int branchId, int variantId, int deltaQuantity, string reason, int staffId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var tx = connection.BeginTransaction();

        try
        {
            // Kiểm tra tồn kho hiện tại
            const string checkSql = @"
                SELECT so_luong_ton 
                FROM TonKhoChiNhanh WITH (UPDLOCK)
                WHERE ma_chi_nhanh = @branchId AND ma_bien_the = @variantId";

            var currentStock = await connection.ExecuteScalarAsync<int?>(checkSql, new { branchId, variantId }, tx);
            if (!currentStock.HasValue)
            {
                // Nếu chưa có dòng tồn kho thì tạo dòng mới
                const string insertInitSql = @"
                    INSERT INTO TonKhoChiNhanh (ma_chi_nhanh, ma_bien_the, so_luong_ton, muc_canh_bao_ton, ngay_cap_nhat)
                    VALUES (@branchId, @variantId, 0, 10, GETDATE())";
                await connection.ExecuteAsync(insertInitSql, new { branchId, variantId }, tx);
                currentStock = 0;
            }

            if (currentStock.Value + deltaQuantity < 0)
            {
                tx.Rollback();
                return false;
            }

            // Cập nhật tồn kho tại chi nhánh
            const string updateSql = @"
                UPDATE TonKhoChiNhanh
                SET so_luong_ton = so_luong_ton + @deltaQuantity,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_chi_nhanh = @branchId AND ma_bien_the = @variantId";

            await connection.ExecuteAsync(updateSql, new { branchId, variantId, deltaQuantity }, tx);

            // Ghi nhật ký kho
            const string logSql = @"
                INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi, thoi_gian, ma_chi_nhanh)
                VALUES (@variantId, @deltaQuantity, @reason, @staffId, 'NHAN_VIEN', GETDATE(), @branchId)";

            await connection.ExecuteAsync(logSql, new { variantId, deltaQuantity, reason, staffId, branchId }, tx);

            // Đồng bộ tổng tồn kho trên bảng BienTheSanPham
            const string syncTotalSql = @"
                UPDATE BienTheSanPham
                SET so_luong_ton = (
                    SELECT ISNULL(SUM(so_luong_ton), 0)
                    FROM TonKhoChiNhanh
                    WHERE ma_bien_the = @variantId
                )
                WHERE ma_bien_the = @variantId";

            await connection.ExecuteAsync(syncTotalSql, new { variantId }, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<int> GetStockQuantityAsync(int branchId, int variantId)
    {
        using var connection = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT ISNULL(so_luong_ton, 0)
            FROM TonKhoChiNhanh
            WHERE ma_chi_nhanh = @branchId AND ma_bien_the = @variantId";

        return await connection.ExecuteScalarAsync<int>(sql, new { branchId, variantId });
    }
}
