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
        const string sql = @"
            SELECT 
                bt.ma_bien_the AS MaBienThe,
                bt.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                bt.ma_sku AS MaSku,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                bt.gia_ban AS GiaBan,
                ISNULL(tk.so_luong_ton, 0) AS SoLuongTon,
                CASE 
                    WHEN ISNULL(tk.so_luong_ton, 0) > 0 THEN N'Còn hàng'
                    ELSE N'Hết hàng'
                END AS TinhTrangHang
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            LEFT JOIN TonKhoChiNhanh tk WITH (NOLOCK) ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
            WHERE bt.ma_san_pham = @ProductId AND sp.da_xoa = 0;";
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
