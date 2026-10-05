using System.Data;
using Dapper;
using Shared.DTOs.Catalog;

namespace Backend.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<ProductListItemDto>> GetProductsCatalogAsync();
    Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId);
    Task<IEnumerable<ProductListItemDto>> SearchProductsAsync(ProductFilterRequestDto filter);
}

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<ProductListItemDto>> GetProductsCatalogAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM vw_DanhSachSanPhamWebsite";
        return await connection.QueryAsync<ProductListItemDto>(sql);
    }

    public async Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM vw_ChiTietBienTheSanPham WHERE ma_san_pham = @ProductId";
        return await connection.QueryAsync<ProductVariantDto>(sql, new { ProductId = productId });
    }

    public async Task<IEnumerable<ProductListItemDto>> SearchProductsAsync(ProductFilterRequestDto filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM dbo.fn_TimKiemSanPhamWeb(
                @TuKhoa, 
                @MaDanhMuc, 
                @MaThuongHieu, 
                @GiaMin, 
                @GiaMax
            )";
        return await connection.QueryAsync<ProductListItemDto>(sql, filter);
    }
}
