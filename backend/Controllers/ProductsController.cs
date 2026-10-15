using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQueryParameters query)
    {
        var result = await _catalogService.GetProductsPagedAsync(query);
        return Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(result));
    }

    [HttpGet("{id}/variants")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVariants(int id)
    {
        var result = await _catalogService.GetProductVariantsAsync(id);
        return Ok(ApiResponse<IEnumerable<ProductVariantDto>>.Ok(result));
    }

    [HttpGet("{id}/images")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImages(int id)
    {
        var result = await _catalogService.GetProductImagesAsync(id);
        return Ok(ApiResponse<IEnumerable<ProductImageDto>>.Ok(result));
    }

    [HttpGet("variant-by-sku")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVariantBySku([FromQuery] string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return BadRequest(ApiResponse<ProductVariantDto>.Fail("Mã SKU không được để trống."));
        }

        var result = await _catalogService.GetVariantBySkuAsync(sku);
        if (result == null)
        {
            return NotFound(ApiResponse<ProductVariantDto>.Fail($"Không tìm thấy sản phẩm có SKU '{sku}'."));
        }

        return Ok(ApiResponse<ProductVariantDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "AdminChuoi,QuanLyChiNhanh")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<int>.Fail("Dữ liệu sản phẩm không hợp lệ."));
        }

        try
        {
            var newId = await _catalogService.CreateProductAsync(request);
            return StatusCode(201, ApiResponse<int>.Ok(newId, "Tạo sản phẩm mới thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<int>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<int>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpPost("{id}/variants")]
    [Authorize(Roles = "AdminChuoi,QuanLyChiNhanh")]
    public async Task<IActionResult> CreateVariant(int id, [FromBody] CreateProductVariantRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<int>.Fail("Dữ liệu biến thể không hợp lệ."));
        }

        request.MaSanPham = id;
        try
        {
            var variantId = await _catalogService.CreateProductVariantAsync(request);
            return StatusCode(201, ApiResponse<int>.Ok(variantId, "Tạo biến thể mới thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<int>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<int>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpPost("variants")]
    [Authorize(Roles = "AdminChuoi,QuanLyChiNhanh")]
    public async Task<IActionResult> CreateVariantDirect([FromBody] CreateProductVariantRequest request)
    {
        if (request == null || request.MaSanPham <= 0)
        {
            return BadRequest(ApiResponse<int>.Fail("Vui lòng chọn sản phẩm hợp lệ cho biến thể."));
        }

        try
        {
            var variantId = await _catalogService.CreateProductVariantAsync(request);
            return StatusCode(201, ApiResponse<int>.Ok(variantId, "Tạo biến thể mới thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<int>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<int>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }
}
