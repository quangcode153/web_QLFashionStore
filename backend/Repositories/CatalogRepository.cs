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
    Task<IEnumerable<ProductBranchStockDto>> GetProductBranchStockAsync(int productId);
    Task<int> CreateCategoryAsync(CreateCategoryRequest request);
    Task<int> CreateBrandAsync(CreateBrandRequest request);
    Task<bool> ToggleProductStatusAsync(int productId);
    Task<bool> ToggleVariantStatusAsync(int variantId);
    Task<IEnumerable<ProductVariantDto>> GetProductVariantsForAdminAsync(int productId);
    Task<bool> UpdateProductAsync(UpdateProductRequest request);
    Task<IEnumerable<VariantAvailabilityDto>> CheckVariantsAvailabilityAsync(IEnumerable<int> variantIds);
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
                END AS TinhTrangHang,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WHERE ha.ma_san_pham = bt.ma_san_pham AND ha.la_anh_chinh = 1) AS HinhAnh
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            LEFT JOIN TonKhoChiNhanh tk WITH (NOLOCK) ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
            WHERE bt.ma_sku = @Sku;
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

        var whereConditions = new List<string>();
        if (query.DaXoa.HasValue)
        {
            whereConditions.Add("sp.da_xoa = @DaXoaFilter");
            dynamicParams.Add("@DaXoaFilter", query.DaXoa.Value ? 1 : 0);
        }
        else if (!query.IncludeAll)
        {
            whereConditions.Add("sp.da_xoa = 0");
        }

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

        string whereClause = whereConditions.Any() ? string.Join(" AND ", whereConditions) : "1 = 1";

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
                    SUM(bt.so_luong_ton) AS TongTonKho,
                    ISNULL(sp.da_xoa, 0) AS DaXoa
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
                    sp.chat_lieu,
                    sp.da_xoa
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
                bt.ma_bien_the AS MaBienThe,
                bt.ma_san_pham AS MaSanPham,
                sp.ten_san_pham AS TenSanPham,
                bt.ma_sku AS MaSku,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                bt.gia_ban AS GiaBan,
                ISNULL(tk.so_luong_ton, 0) AS SoLuongTon,
                ISNULL(bt.da_xoa, 0) AS DaXoa,
                CASE 
                    WHEN ISNULL(tk.so_luong_ton, 0) > 0 THEN N'Còn hàng'
                    ELSE N'Hết hàng'
                END AS TinhTrangHang,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WHERE ha.ma_san_pham = @ProductId AND ha.la_anh_chinh = 1) AS HinhAnh
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            LEFT JOIN TonKhoChiNhanh tk WITH (NOLOCK) ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
            WHERE bt.ma_san_pham = @ProductId AND sp.da_xoa = 0 AND ISNULL(bt.da_xoa, 0) = 0;
        ";
        return await connection.QueryAsync<ProductVariantDto>(sql, new { ProductId = productId });
    }

    public async Task<IEnumerable<ProductBranchStockDto>> GetProductBranchStockAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                cn.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                cn.dia_chi AS DiaChi,
                ISNULL(cn.is_online_warehouse, 0) AS IsOnlineWarehouse,
                bt.ma_bien_the AS MaBienThe,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                bt.ma_sku AS MaSku,
                ISNULL(tk.so_luong_ton, 0) AS SoLuongTon
            FROM ChiNhanh cn WITH (NOLOCK)
            CROSS JOIN BienTheSanPham bt WITH (NOLOCK)
            LEFT JOIN TonKhoChiNhanh tk WITH (NOLOCK) 
                ON cn.ma_chi_nhanh = tk.ma_chi_nhanh AND bt.ma_bien_the = tk.ma_bien_the
            WHERE bt.ma_san_pham = @ProductId AND cn.dang_hoat_dong = 1
            ORDER BY cn.is_online_warehouse DESC, cn.ma_chi_nhanh ASC, bt.mau_sac, bt.kich_co;
        ";
        return await connection.QueryAsync<ProductBranchStockDto>(sql, new { ProductId = productId });
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

    public async Task<int> CreateCategoryAsync(CreateCategoryRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO DanhMuc (ten_danh_muc, ma_danh_muc_cha)
            VALUES (@TenDanhMuc, @MaDanhMucCha);
            SELECT CAST(SCOPE_IDENTITY() as int);
        ";
        return await connection.ExecuteScalarAsync<int>(sql, new { 
            TenDanhMuc = request.TenDanhMuc.Trim(), 
            request.MaDanhMucCha 
        });
    }

    public async Task<int> CreateBrandAsync(CreateBrandRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO ThuongHieu (ten_thuong_hieu, xuat_xu)
            VALUES (@TenThuongHieu, @XuatXu);
            SELECT CAST(SCOPE_IDENTITY() as int);
        ";
        return await connection.ExecuteScalarAsync<int>(sql, new { 
            TenThuongHieu = request.TenThuongHieu.Trim(), 
            XuatXu = string.IsNullOrWhiteSpace(request.XuatXu) ? "Việt Nam" : request.XuatXu.Trim() 
        });
    }

    public async Task<bool> ToggleProductStatusAsync(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE SanPham 
            SET da_xoa = CASE WHEN ISNULL(da_xoa, 0) = 1 THEN 0 ELSE 1 END,
                ngay_cap_nhat = GETDATE()
            WHERE ma_san_pham = @ProductId;
        ";
        var affected = await connection.ExecuteAsync(sql, new { ProductId = productId });
        return affected > 0;
    }

    public async Task<bool> ToggleVariantStatusAsync(int variantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE BienTheSanPham 
            SET da_xoa = CASE WHEN ISNULL(da_xoa, 0) = 1 THEN 0 ELSE 1 END,
                ngay_cap_nhat = GETDATE()
            WHERE ma_bien_the = @VariantId;
        ";
        var affected = await connection.ExecuteAsync(sql, new { VariantId = variantId });
        return affected > 0;
    }

    public async Task<IEnumerable<ProductVariantDto>> GetProductVariantsForAdminAsync(int productId)
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
                ISNULL(bt.da_xoa, 0) AS DaXoa,
                CASE 
                    WHEN ISNULL(tk.so_luong_ton, 0) > 0 THEN N'Còn hàng'
                    ELSE N'Hết hàng'
                END AS TinhTrangHang,
                (SELECT TOP 1 duong_dan_anh FROM HinhAnhSanPham ha WHERE ha.ma_san_pham = @ProductId AND ha.la_anh_chinh = 1) AS HinhAnh
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            LEFT JOIN TonKhoChiNhanh tk WITH (NOLOCK) ON bt.ma_bien_the = tk.ma_bien_the AND tk.ma_chi_nhanh = 9
            WHERE bt.ma_san_pham = @ProductId;
        ";
        return await connection.QueryAsync<ProductVariantDto>(sql, new { ProductId = productId });
    }

    public async Task<bool> UpdateProductAsync(UpdateProductRequest request)
    {
        if (request == null || request.MaSanPham <= 0) return false;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE SanPham 
            SET ten_san_pham = @TenSanPham,
                ma_danh_muc = @MaDanhMuc,
                ma_thuong_hieu = @MaThuongHieu,
                chat_lieu = @ChatLieu
            WHERE ma_san_pham = @MaSanPham;

            IF @AnhDaiDien IS NOT NULL AND LEN(TRIM(@AnhDaiDien)) > 0
            BEGIN
                IF EXISTS (SELECT 1 FROM HinhAnhSanPham WHERE ma_san_pham = @MaSanPham AND la_anh_chinh = 1)
                BEGIN
                    UPDATE HinhAnhSanPham 
                    SET duong_dan_anh = @AnhDaiDien 
                    WHERE ma_san_pham = @MaSanPham AND la_anh_chinh = 1;
                END
                ELSE
                BEGIN
                    INSERT INTO HinhAnhSanPham (ma_san_pham, duong_dan_anh, la_anh_chinh)
                    VALUES (@MaSanPham, @AnhDaiDien, 1);
                END
            END
        ";
        var rows = await connection.ExecuteAsync(sql, request);
        return rows > 0;
    }

    public async Task<IEnumerable<VariantAvailabilityDto>> CheckVariantsAvailabilityAsync(IEnumerable<int> variantIds)
    {
        if (variantIds == null || !variantIds.Any())
            return Enumerable.Empty<VariantAvailabilityDto>();

        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                bt.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                CAST(CASE WHEN sp.da_xoa = 1 OR ISNULL(bt.da_xoa, 0) = 1 THEN 1 ELSE 0 END AS BIT) AS DaAn
            FROM BienTheSanPham bt WITH (NOLOCK)
            JOIN SanPham sp WITH (NOLOCK) ON bt.ma_san_pham = sp.ma_san_pham
            WHERE bt.ma_bien_the IN @VariantIds;
        ";
        var list = (await connection.QueryAsync<VariantAvailabilityDto>(sql, new { VariantIds = variantIds.Distinct() })).ToList();
        foreach (var item in list)
        {
            if (item.DaAn)
            {
                item.ThongBao = "Sản phẩm tạm thời không cung cấp";
            }
        }
        return list;
    }
}
