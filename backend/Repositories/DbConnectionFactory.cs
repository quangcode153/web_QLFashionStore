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
            ?? "Server=.\\SQLEXPRESS;Database=FashionStore;User Id=sa;Password=1532006Quang;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
