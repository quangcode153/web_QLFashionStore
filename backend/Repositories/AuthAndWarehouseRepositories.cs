using System.Data;
using Dapper;
using Shared.DTOs.Auth;
using Shared.DTOs.Warehouse;

namespace Backend.Repositories;

public class UserAuthModel
{
    public int MaTaiKhoan { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public bool DangHoatDong { get; set; }
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty;
}

public interface IAuthRepository
{
    Task<UserAuthModel?> GetUserByUsernameAsync(string username);
    Task UpdatePasswordHashAsync(int accountId, string newHash);
}

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<UserAuthModel?> GetUserByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                tk.ma_tai_khoan,
                tk.ten_dang_nhap,
                tk.mat_khau_hash,
                tk.dang_hoat_dong,
                nv.ma_nhan_vien,
                nv.ten_nhan_vien,
                nv.vai_tro
            FROM TaiKhoan tk
            JOIN NhanVien nv ON tk.ma_nhan_vien = nv.ma_nhan_vien
            WHERE tk.ten_dang_nhap = @Username;";

        return await connection.QueryFirstOrDefaultAsync<UserAuthModel>(sql, new { Username = username });
    }

    public async Task UpdatePasswordHashAsync(int accountId, string newHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE TaiKhoan SET mat_khau_hash = @Hash WHERE ma_tai_khoan = @Id;";
        await connection.ExecuteAsync(sql, new { Hash = newHash, Id = accountId });
    }
}

public interface IWarehouseRepository
{
    Task ImportStockAsync(ImportStockRequestDto request);
    Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync();
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

        await connection.ExecuteAsync("dbo.sp_NhapHangVaoKho", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM vw_CanhBaoSapHetHang";
        return await connection.QueryAsync<LowStockAlertDto>(sql);
    }
}
