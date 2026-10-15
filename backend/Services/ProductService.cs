using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Services;

public interface IProductService
{
    Task<ApiResponse<IEnumerable<ProductListItemDto>>> GetProductsCatalogAsync();
    Task<ApiResponse<IEnumerable<ProductVariantDto>>> GetProductVariantsAsync(int productId);
    Task<ApiResponse<IEnumerable<ProductListItemDto>>> SearchProductsAsync(ProductFilterRequestDto filter);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ApiResponse<IEnumerable<ProductListItemDto>>> GetProductsCatalogAsync()
    {
        var products = await _productRepository.GetProductsCatalogAsync();
        return ApiResponse<IEnumerable<ProductListItemDto>>.Ok(products);
    }

    public async Task<ApiResponse<IEnumerable<ProductVariantDto>>> GetProductVariantsAsync(int productId)
    {
        var variants = await _productRepository.GetProductVariantsAsync(productId);
        return ApiResponse<IEnumerable<ProductVariantDto>>.Ok(variants);
    }

    public async Task<ApiResponse<IEnumerable<ProductListItemDto>>> SearchProductsAsync(ProductFilterRequestDto filter)
    {
        var result = await _productRepository.SearchProductsAsync(filter);
        return ApiResponse<IEnumerable<ProductListItemDto>>.Ok(result);
    }
}
