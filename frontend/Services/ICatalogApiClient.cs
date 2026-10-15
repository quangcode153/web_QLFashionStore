using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Frontend.Services;

public interface ICatalogApiClient
{
    Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductQueryParameters query);
    Task<IEnumerable<ProductVariantDto>> GetProductVariantsAsync(int productId);
    Task<IEnumerable<ProductImageDto>> GetProductImagesAsync(int productId);
    Task<IEnumerable<CategoryDto>> GetCategoriesAsync();
    Task<IEnumerable<BrandDto>> GetBrandsAsync();
    Task<IEnumerable<BannerDto>> GetActiveBannersAsync();
    Task<IEnumerable<BannerDto>> GetAllBannersAsync();
    Task<bool> CreateBannerAsync(CreateUpdateBannerDto dto);
    Task<bool> UpdateBannerAsync(int id, CreateUpdateBannerDto dto);
    Task<bool> DeleteBannerAsync(int id);
}
