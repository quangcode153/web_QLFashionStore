using System.Data;
using Dapper;
using Shared.DTOs.Order;

namespace Backend.Repositories;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode);
    Task AddOrderItemAsync(int orderId, int variantId, int quantity);
    Task UpdateOrderStatusAsync(int orderId, string newStatus);
    Task<IEnumerable<OrderSummaryDto>> GetAllOrdersAsync();
    Task<IEnumerable<OrderDetailItemDto>> GetOrderDetailsAsync(int orderId);
    Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId);
}

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaKhachHang", customerId);
        parameters.Add("@MaNhanVien", employeeId);
        parameters.Add("@MaGiamGia", couponCode);
        parameters.Add("@MaHoaDonMoi", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dbo.sp_TaoHoaDonMoi", parameters, commandType: CommandType.StoredProcedure);
        return parameters.Get<int>("@MaHoaDonMoi");
    }

    public async Task AddOrderItemAsync(int orderId, int variantId, int quantity)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);
        parameters.Add("@MaBienThe", variantId);
        parameters.Add("@SoLuong", quantity);

        await connection.ExecuteAsync("dbo.sp_ThemSanPhamVaoHoaDon", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);
        parameters.Add("@TrangThaiMoi", newStatus);

        await connection.ExecuteAsync("dbo.sp_CapNhatTrangThaiHoaDon", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<OrderSummaryDto>> GetAllOrdersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM vw_QuanLyDonHang ORDER BY ngay_lap_hoa_don DESC";
        return await connection.QueryAsync<OrderSummaryDto>(sql);
    }

    public async Task<IEnumerable<OrderDetailItemDto>> GetOrderDetailsAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);

        using var multi = await connection.QueryMultipleAsync("dbo.sp_InChiTietHoaDon", parameters, commandType: CommandType.StoredProcedure);
        // Đọc qua kết quả thứ nhất (thông tin hóa đơn)
        await multi.ReadFirstOrDefaultAsync();
        // Kết quả thứ hai là danh sách chi tiết các mặt hàng
        var items = await multi.ReadAsync<OrderDetailItemDto>();
        return items;
    }

    public async Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT ma_hoa_don AS OrderId, ma_khach_hang AS CustomerId FROM HoaDon WHERE ma_hoa_don = @orderId";
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { orderId });
        if (row == null)
        {
            return (false, null);
        }
        return (true, (int?)row.CustomerId);
    }
}
