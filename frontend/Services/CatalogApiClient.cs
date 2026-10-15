using System.Net.Http.Json;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Frontend.Services;

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _http;

    public CatalogApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQueryParameters query)
    {
        var url = $"api/products?pageNumber={query.PageNumber}&pageSize={query.PageSize}&sortBy={query.SortBy}";
        if (!string.IsNullOrWhiteSpace(query.Search)) url += $"&search={Uri.EscapeDataString(query.Search)}";
        if (query.CategoryId.HasValue) url += $"&categoryId={query.CategoryId.Value}";
        if (query.BrandId.HasValue) url += $"&brandId={query.BrandId.Value}";
        if (query.MinPrice.HasValue) url += $"&minPrice={query.MinPrice.Value}";
        if (query.MaxPrice.HasValue) url += $"&maxPrice={query.MaxPrice.Value}";

        var resp = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>(url);
        return resp?.Data ?? new PagedResult<ProductListItemDto>();
    }

    public async Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId)
    {
        var resp = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductVariantDto>>>($"api/products/{productId}/variants");
        return resp?.Data ?? Enumerable.Empty<ProductVariantDto>();
    }

    public async Task<IEnumerable<ProductImageDto>> GetProductImagesAsync(int productId)
    {
        var resp = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductImageDto>>>($"api/products/{productId}/images");
        return resp?.Data ?? Enumerable.Empty<ProductImageDto>();
    }

    public async Task<IEnumerable<CategoryDto>> GetCategoriesAsync()
    {
        var resp = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<CategoryDto>>>("api/categories");
        return resp?.Data ?? Enumerable.Empty<CategoryDto>();
    }

    public async Task<IEnumerable<BrandDto>> GetBrandsAsync()
    {
        var resp = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<BrandDto>>>("api/brands");
        return resp?.Data ?? Enumerable.Empty<BrandDto>();
    }

    public async Task<IEnumerable<BannerDto>> GetActiveBannersAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<IEnumerable<BannerDto>>("api/banners");
            return resp ?? Enumerable.Empty<BannerDto>();
        }
        catch
        {
            return Enumerable.Empty<BannerDto>();
        }
    }

    public async Task<IEnumerable<BannerDto>> GetAllBannersAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<IEnumerable<BannerDto>>("api/banners/all");
            return resp ?? Enumerable.Empty<BannerDto>();
        }
        catch
        {
            return Enumerable.Empty<BannerDto>();
        }
    }

    public async Task<bool> CreateBannerAsync(CreateUpdateBannerDto dto)
    {
        var resp = await _http.PostAsJsonAsync("api/banners", dto);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateBannerAsync(int id, CreateUpdateBannerDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"api/banners/{id}", dto);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteBannerAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/banners/{id}");
        return resp.IsSuccessStatusCode;
    }
}
