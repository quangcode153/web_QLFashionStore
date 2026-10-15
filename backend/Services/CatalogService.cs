using Backend.Repositories;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Services;

public interface ICatalogService
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

public class CatalogService : ICatalogService
{
    private readonly ICatalogRepository _catalogRepository;

    public CatalogService(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<int> CreateProductVariantAsync(CreateProductVariantRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.MaSanPham <= 0)
            throw new ArgumentException("Mã sản phẩm không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.MauSac))
            throw new ArgumentException("Màu sắc biến thể không được để trống.");

        if (string.IsNullOrWhiteSpace(request.KichCo))
            throw new ArgumentException("Kích cỡ biến thể không được để trống.");

        if (request.GiaBan < 0)
            throw new ArgumentException("Giá bán không được nhỏ hơn 0.");

        if (request.GiaNhap < 0)
            throw new ArgumentException("Giá nhập không được nhỏ hơn 0.");

        if (request.SoLuongTon < 0)
            throw new ArgumentException("Số lượng tồn không được nhỏ hơn 0.");

        request.MauSac = request.MauSac.Trim();
        request.KichCo = request.KichCo.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(request.MaSku))
        {
            var rawColor = RemoveDiacritics(request.MauSac).ToUpperInvariant().Replace(" ", "");
            request.MaSku = $"QF-SP{request.MaSanPham:D2}-{rawColor}-{request.KichCo}";
        }
        else
        {
            request.MaSku = request.MaSku.Trim().ToUpperInvariant();
        }

        return await _catalogRepository.CreateProductVariantAsync(request);
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
        var stringBuilder = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var result = stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        return result.Replace("đ", "d").Replace("Đ", "D");
    }

    public async Task<int> CreateProductAsync(CreateProductRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.TenSanPham))
            throw new ArgumentException("Tên sản phẩm không được để trống.");

        if (request.Images == null || !request.Images.Any())
            throw new ArgumentException("Sản phẩm bắt buộc phải có ít nhất 1 hình ảnh.");

        foreach (var img in request.Images)
        {
            if (string.IsNullOrWhiteSpace(img.DuongDanAnh))
                throw new ArgumentException("Đường dẫn hình ảnh không hợp lệ.");
        }

        if (!request.Images.Any(i => i.LaAnhChinh))
        {
            request.Images.First().LaAnhChinh = true;
        }

        return await _catalogRepository.CreateProductAsync(request);
    }

    public async Task<ProductVariantDto?> GetVariantBySkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        return await _catalogRepository.GetVariantBySkuAsync(sku.Trim());
    }

    public async Task<PagedResult<ProductListItemDto>> GetProductsPagedAsync(ProductQueryParameters query)
    {
        // Chuẩn hóa clamping tham số ở tầng Service
        var normalizedQuery = new ProductQueryParameters
        {
            PageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber,
            PageSize = query.PageSize <= 0 ? 10 : (query.PageSize > 100 ? 100 : query.PageSize),
            Search = query.Search,
            CategoryId = query.CategoryId,
            BrandId = query.BrandId,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            SortBy = query.SortBy
        };

        return await _catalogRepository.GetProductsPagedAsync(normalizedQuery);
    }

    public async Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId)
    {
        if (productId <= 0)
        {
            return Enumerable.Empty<ProductVariantDto>();
        }

        return await _catalogRepository.GetProductVariantsAsync(productId);
    }

    public async Task<IEnumerable<ProductImageDto>> GetProductImagesAsync(int productId)
    {
        if (productId <= 0)
        {
            return Enumerable.Empty<ProductImageDto>();
        }

        return await _catalogRepository.GetProductImagesAsync(productId);
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        return await _catalogRepository.GetCategoriesAsync();
    }

    public async Task<IEnumerable<BrandDto>> GetBrandsAsync()
    {
        return await _catalogRepository.GetBrandsAsync();
    }
}
