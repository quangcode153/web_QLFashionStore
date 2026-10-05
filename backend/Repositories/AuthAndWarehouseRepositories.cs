using System.Data;
using Dapper;
using Shared.DTOs.Auth;
using Shared.DTOs.Warehouse;

namespace Backend.Repositories;

public interface IAuthRepository
{
    Task<dynamic?> AuthenticateAsync(string username, string passwordHash);
}

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<dynamic?> AuthenticateAsync(string username, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@TenDangNhap", username);
        parameters.Add("@MatKhauHash", passwordHash);

        return await connection.QueryFirstOrDefaultAsync("dbo.sp_XacThucDangNhap", parameters, commandType: CommandType.StoredProcedure);
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
