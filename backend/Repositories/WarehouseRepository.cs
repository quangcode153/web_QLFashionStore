using System.Data;
using Dapper;
using Shared.DTOs.Warehouse;

namespace Backend.Repositories;

public interface IWarehouseRepository
{
    Task ImportStockAsync(ImportStockRequestDto request);
    Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync();
    Task<IEnumerable<LowStockAlertDto>> GetAllWarehouseVariantsAsync();
    Task<IEnumerable<SupplierDto>> GetSuppliersAsync();
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequestDto request);
}

public class WarehouseRepository : IWarehouseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public WarehouseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task ImportStockAsync(ImportStockRequestDto request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaNhaCungCap", request.MaNhaCungCap);
        parameters.Add("@MaNhanVien", request.MaNhanVien);
        parameters.Add("@MaBienThe", request.MaBienThe);
        parameters.Add("@SoLuong", request.SoLuong);
        parameters.Add("@DonGiaNhap", request.DonGiaNhap);
        parameters.Add("@MaChiNhanh", request.MaChiNhanh > 0 ? request.MaChiNhanh : (int?)null);

        await connection.ExecuteAsync("dbo.sp_NhapHangVaoKho", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM vw_CanhBaoSapHetHang";
        return await connection.QueryAsync<LowStockAlertDto>(sql);
    }

    public async Task<IEnumerable<LowStockAlertDto>> GetAllWarehouseVariantsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                bt.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.ma_sku AS MaSku,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                bt.so_luong_ton AS SoLuongTon,
                bt.gia_nhap AS GiaNhap
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            WHERE sp.da_xoa = 0
            ORDER BY sp.ten_san_pham, bt.mau_sac, bt.kich_co;
        ";
        return await connection.QueryAsync<LowStockAlertDto>(sql);
    }

    public async Task<IEnumerable<SupplierDto>> GetSuppliersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_nha_cung_cap AS MaNhaCungCap,
                ten_nha_cung_cap AS TenNhaCungCap,
                dien_thoai_lien_he AS DienThoaiLienHe
            FROM NhaCungCap
            ORDER BY ten_nha_cung_cap;
        ";
        return await connection.QueryAsync<SupplierDto>(sql);
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequestDto request)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string checkSql = @"
            SELECT 
                ma_nha_cung_cap AS MaNhaCungCap,
                ten_nha_cung_cap AS TenNhaCungCap,
                dien_thoai_lien_he AS DienThoaiLienHe
            FROM NhaCungCap WITH (NOLOCK)
            WHERE LOWER(LTRIM(RTRIM(ten_nha_cung_cap))) = LOWER(LTRIM(RTRIM(@TenNhaCungCap)));
        ";
        var existing = await connection.QueryFirstOrDefaultAsync<SupplierDto>(checkSql, new { request.TenNhaCungCap });
        if (existing != null)
        {
            return existing;
        }

        const string insertSql = @"
            INSERT INTO NhaCungCap (ten_nha_cung_cap, dien_thoai_lien_he)
            OUTPUT INSERTED.ma_nha_cung_cap AS MaNhaCungCap, INSERTED.ten_nha_cung_cap AS TenNhaCungCap, INSERTED.dien_thoai_lien_he AS DienThoaiLienHe
            VALUES (@TenNhaCungCap, @DienThoaiLienHe);
        ";
        return await connection.QuerySingleAsync<SupplierDto>(insertSql, new 
        { 
            TenNhaCungCap = request.TenNhaCungCap.Trim(), 
            DienThoaiLienHe = string.IsNullOrWhiteSpace(request.DienThoaiLienHe) ? null : request.DienThoaiLienHe.Trim() 
        });
    }
}
