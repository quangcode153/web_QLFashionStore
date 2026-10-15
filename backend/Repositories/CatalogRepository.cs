using System.Data;
using Dapper;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Repositories;

public interface ICatalogRepository
{
    Task<PagedResult<ProductListItemDto>> GetProductsPagedAsync(ProductQueryParameters query);
    Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId);
    Task<IEnumerable<ProductImageDto>> GetProductImagesAsync(int productId);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<IEnumerable<BrandDto>> GetBrandsAsync();
    Task<ProductVariantDto?> GetVariantBySkuAsync(string sku);
    Task<int> CreateProductAsync(CreateProductRequest request);
    Task<int> CreateProductVariantAsync(CreateProductVariantRequest request);
}

public class CatalogRepository : ICatalogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CatalogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateProductVariantAsync(CreateProductVariantRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO BienTheSanPham (ma_san_pham, ma_sku, mau_sac, kich_co, so_luong_ton, gia_nhap, gia_ban, ngay_cap_nhat)
            VALUES (@MaSanPham, @MaSku, @MauSac, @KichCo, @SoLuongTon, @GiaNhap, @GiaBan, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() as int);
        ";
        var id = await connection.ExecuteScalarAsync<int>(sql, request);
        if (request.SoLuongTon > 0)
        {
            const string logSql = @"
                INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi)
                VALUES (@MaBienThe, @SoLuongTon, 'NHAP_HANG', NULL, 'HE_THONG');
            ";
            await connection.ExecuteAsync(logSql, new { MaBienThe = id, request.SoLuongTon });
        }
        return id;
    }

    public async Task<int> CreateProductAsync(CreateProductRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var imagesJson = System.Text.Json.JsonSerializer.Serialize(request.Images);

        var parameters = new DynamicParameters();
        parameters.Add("@TenSanPham", request.TenSanPham);
        parameters.Add("@MaDanhMuc", request.MaDanhMuc);
        parameters.Add("@MaThuongHieu", request.MaThuongHieu);
        parameters.Add("@ChatLieu", request.ChatLieu);
        parameters.Add("@DanhSachAnhJson", imagesJson);
        parameters.Add("@MaSanPhamMoi", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "dbo.sp_TaoSanPhamMoi",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        return parameters.Get<int>("@MaSanPhamMoi");
    }

    public async Task<ProductVariantDto?> GetVariantBySkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_bien_the AS MaBienThe,
                ma_san_pham AS MaSanPham,
                ten_san_pham AS TenSanPham,
                ma_sku AS MaSku,
                mau_sac AS MauSac,
                kich_co AS KichCo,
                gia_ban AS GiaBan,
                so_luong_ton AS SoLuongTon,
                tinh_trang_hang AS TinhTrangHang,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WHERE ha.ma_san_pham = vw_ChiTietBienTheSanPham.ma_san_pham AND ha.la_anh_chinh = 1) AS HinhAnh
            FROM vw_ChiTietBienTheSanPham
            WHERE ma_sku = @Sku;
        ";
        return await connection.QueryFirstOrDefaultAsync<ProductVariantDto>(sql, new { Sku = sku.Trim() });
    }

    public async Task<PagedResult<ProductListItemDto>> GetProductsPagedAsync(ProductQueryParameters query)
    {
        using var connection = _connectionFactory.CreateConnection();

        // 1. Chuẩn hóa tham số phân trang
        var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var pageSize = query.PageSize <= 0 ? 10 : (query.PageSize > 100 ? 100 : query.PageSize);
        var offset = (pageNumber - 1) * pageSize;

        // 2. Xác định thứ tự sắp xếp sạch sẽ
        string orderByClause = (query.SortBy?.ToLowerInvariant()) switch
        {
            "price_asc" => "GiaThapNhat ASC, MaSanPham DESC",
            "price_desc" => "GiaCaoNhat DESC, MaSanPham DESC",
            "name_asc" => "TenSanPham ASC, MaSanPham DESC",
            "name_desc" => "TenSanPham DESC, MaSanPham DESC",
            _ => "MaSanPham DESC"
        };

        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("@Offset", offset);
        dynamicParams.Add("@PageSize", pageSize);

        var whereConditions = new List<string> { "sp.da_xoa = 0" };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var rawSearch = query.Search.Replace('-', ' ').Replace('_', ' ').Trim();
            var tokens = rawSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var tokenConditions = new List<string>();
            for (int i = 0; i < tokens.Length; i++)
            {
                var pName = $"@SearchToken_{i}";
                var normalizedToken = tokens[i].Replace('đ', 'd').Replace('Đ', 'D');
                dynamicParams.Add(pName, $"%{normalizedToken}%");

                tokenConditions.Add($@"
                    (
                        REPLACE(REPLACE(sp.ten_san_pham, N'đ', N'd'), N'Đ', N'D') COLLATE Latin1_General_CI_AI LIKE {pName} COLLATE Latin1_General_CI_AI
                        OR REPLACE(REPLACE(dm.ten_danh_muc, N'đ', N'd'), N'Đ', N'D') COLLATE Latin1_General_CI_AI LIKE {pName} COLLATE Latin1_General_CI_AI
                        OR REPLACE(REPLACE(th.ten_thuong_hieu, N'đ', N'd'), N'Đ', N'D') COLLATE Latin1_General_CI_AI LIKE {pName} COLLATE Latin1_General_CI_AI
                        OR REPLACE(REPLACE(ISNULL(sp.chat_lieu, ''), N'đ', N'd'), N'Đ', N'D') COLLATE Latin1_General_CI_AI LIKE {pName} COLLATE Latin1_General_CI_AI
                    )");
            }

            if (tokenConditions.Any())
            {
                whereConditions.Add($"({string.Join(" AND ", tokenConditions)})");
            }
        }

        if (query.BrandId.HasValue)
        {
            whereConditions.Add("sp.ma_thuong_hieu = @BrandId");
            dynamicParams.Add("@BrandId", query.BrandId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            whereConditions.Add(@"sp.ma_danh_muc IN (
                SELECT ma_danh_muc FROM DanhMuc WITH (NOLOCK)
                WHERE ma_danh_muc = @CategoryId 
                   OR ma_danh_muc_cha = @CategoryId
                   OR ma_danh_muc_cha IN (SELECT ma_danh_muc FROM DanhMuc WITH (NOLOCK) WHERE ma_danh_muc_cha = @CategoryId)
            )");
            dynamicParams.Add("@CategoryId", query.CategoryId.Value);
        }

        string whereClause = string.Join(" AND ", whereConditions);

        // Kiểm tra xem có lọc giá hay không
        bool hasPriceFilter = query.MinPrice.HasValue || query.MaxPrice.HasValue;
        var havingConditions = new List<string>();
        if (query.MinPrice.HasValue)
        {
            havingConditions.Add("MAX(bt.gia_ban) >= @MinPrice");
            dynamicParams.Add("@MinPrice", query.MinPrice.Value);
        }
        if (query.MaxPrice.HasValue)
        {
            havingConditions.Add("MIN(bt.gia_ban) <= @MaxPrice");
            dynamicParams.Add("@MaxPrice", query.MaxPrice.Value);
        }
        string havingClause = havingConditions.Any() ? "HAVING " + string.Join(" AND ", havingConditions) : "";

        string countSql;
        if (!hasPriceFilter)
        {
            // TỐI ƯU SIÊU TỐC: Không lọc giá thì không cần join biến thể và không cần group by trong câu COUNT!
            countSql = $@"
                SELECT COUNT(1)
                FROM SanPham sp WITH (NOLOCK)
                JOIN DanhMuc dm WITH (NOLOCK) ON sp.ma_danh_muc = dm.ma_danh_muc
                JOIN ThuongHieu th WITH (NOLOCK) ON sp.ma_thuong_hieu = th.ma_thuong_hieu
                WHERE {whereClause};
            ";
        }
        else
        {
            countSql = $@"
                SELECT COUNT(1)
                FROM (
                    SELECT sp.ma_san_pham
                    FROM SanPham sp WITH (NOLOCK)
                    JOIN DanhMuc dm WITH (NOLOCK) ON sp.ma_danh_muc = dm.ma_danh_muc
                    JOIN ThuongHieu th WITH (NOLOCK) ON sp.ma_thuong_hieu = th.ma_thuong_hieu
                    JOIN BienTheSanPham bt WITH (NOLOCK) ON sp.ma_san_pham = bt.ma_san_pham
                    WHERE {whereClause}
                    GROUP BY sp.ma_san_pham
                    {havingClause}
                ) AS SubTotal;
            ";
        }

        string dataSql = $@"
            WITH FilteredProducts AS (
                SELECT 
                    sp.ma_san_pham AS MaSanPham,
                    sp.ten_san_pham AS TenSanPham,
                    dm.ten_danh_muc AS TenDanhMuc,
                    th.ten_thuong_hieu AS TenThuongHieu,
                    sp.chat_lieu AS ChatLieu,
                    MIN(bt.gia_ban) AS GiaThapNhat,
                    MAX(bt.gia_ban) AS GiaCaoNhat,
                    SUM(bt.so_luong_ton) AS TongTonKho
                FROM SanPham sp WITH (NOLOCK)
                JOIN DanhMuc dm WITH (NOLOCK) ON sp.ma_danh_muc = dm.ma_danh_muc
                JOIN ThuongHieu th WITH (NOLOCK) ON sp.ma_thuong_hieu = th.ma_thuong_hieu
                JOIN BienTheSanPham bt WITH (NOLOCK) ON sp.ma_san_pham = bt.ma_san_pham
                WHERE {whereClause}
                GROUP BY 
                    sp.ma_san_pham,
                    sp.ten_san_pham,
                    dm.ten_danh_muc,
                    th.ten_thuong_hieu,
                    sp.chat_lieu
                {havingClause}
            ),
            PagedSlice AS (
                SELECT * FROM FilteredProducts
                ORDER BY {orderByClause}
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY
            )
            SELECT 
                p.*,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WITH (NOLOCK) WHERE ha.ma_san_pham = p.MaSanPham AND ha.la_anh_chinh = 1) AS AnhDaiDien
            FROM PagedSlice p;
        ";

        string fullSql = countSql + dataSql;

        using var multi = await connection.QueryMultipleAsync(fullSql, dynamicParams, commandTimeout: 30);
        var totalCount = await multi.ReadFirstAsync<int>();
        var items = await multi.ReadAsync<ProductListItemDto>();

        return new PagedResult<ProductListItemDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_bien_the AS MaBienThe,
                ma_san_pham AS MaSanPham,
                ten_san_pham AS TenSanPham,
                ma_sku AS MaSku,
                mau_sac AS MauSac,
                kich_co AS KichCo,
                gia_ban AS GiaBan,
                so_luong_ton AS SoLuongTon,
                tinh_trang_hang AS TinhTrangHang,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WHERE ha.ma_san_pham = @ProductId AND ha.la_anh_chinh = 1) AS HinhAnh
            FROM vw_ChiTietBienTheSanPham
            WHERE ma_san_pham = @ProductId;
        ";
        return await connection.QueryAsync<ProductVariantDto>(sql, new { ProductId = productId });
    }

    public async Task<IEnumerable<ProductImageDto>> GetProductImagesAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_hinh_anh AS MaHinhAnh,
                ma_san_pham AS MaSanPham,
                duong_dan_anh AS DuongDanAnh,
                la_anh_chinh AS LaAnhChinh
            FROM HinhAnhSanPham
            WHERE ma_san_pham = @ProductId
            ORDER BY la_anh_chinh DESC, ma_hinh_anh ASC;
        ";
        return await connection.QueryAsync<ProductImageDto>(sql, new { ProductId = productId });
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_danh_muc AS MaDanhMuc,
                ma_danh_muc_cha AS MaDanhMucCha,
                ten_danh_muc AS TenDanhMuc
            FROM DanhMuc
            ORDER BY ten_danh_muc;
        ";
        return await connection.QueryAsync<CategoryDto>(sql);
    }

    public async Task<IEnumerable<BrandDto>> GetBrandsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                ma_thuong_hieu AS MaThuongHieu,
                ten_thuong_hieu AS TenThuongHieu,
                xuat_xu AS XuatXu
            FROM ThuongHieu
            ORDER BY ten_thuong_hieu;
        ";
        return await connection.QueryAsync<BrandDto>(sql);
    }
}
