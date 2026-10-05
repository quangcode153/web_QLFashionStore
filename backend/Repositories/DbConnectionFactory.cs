using System.Data;
using Microsoft.Data.SqlClient;

namespace Backend.Repositories;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration["CONNECTION_STRING"] 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("CẤU HÌNH CSDL: Chưa thiết lập chuỗi kết nối. Vui lòng định nghĩa biến môi trường CONNECTION_STRING trong file .env hoặc appsettings.json.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
