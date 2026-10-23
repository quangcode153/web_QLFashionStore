using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
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

    [HttpGet("{id}/branch-stock")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranchStock(int id)
    {
        var result = await _catalogService.GetProductBranchStockAsync(id);
        return Ok(ApiResponse<IEnumerable<ProductBranchStockDto>>.Ok(result));
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
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
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
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
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
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
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

    [HttpPatch("{id}/toggle-status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},AdminChuoi,QuanLyHeThong")]
    public async Task<IActionResult> ToggleProductStatus(int id)
    {
        try
        {
            var success = await _catalogService.ToggleProductStatusAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy sản phẩm cần cập nhật."));
            }

            return Ok(ApiResponse<bool>.Ok(true, "Cập nhật trạng thái hiển thị sản phẩm thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<bool>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpPatch("variants/{variantId}/toggle-status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},AdminChuoi,QuanLyHeThong")]
    public async Task<IActionResult> ToggleVariantStatus(int variantId)
    {
        try
        {
            var success = await _catalogService.ToggleVariantStatusAsync(variantId);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy biến thể cần cập nhật."));
            }

            return Ok(ApiResponse<bool>.Ok(true, "Cập nhật trạng thái hiển thị biến thể thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<bool>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpGet("{id}/variants/admin")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
    public async Task<IActionResult> GetVariantsForAdmin(int id)
    {
        var result = await _catalogService.GetProductVariantsForAdminAsync(id);
        return Ok(ApiResponse<IEnumerable<ProductVariantDto>>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<bool>.Fail("Dữ liệu cập nhật không hợp lệ."));
            }

            request.MaSanPham = id;
            var success = await _catalogService.UpdateProductAsync(request);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy sản phẩm để cập nhật."));
            }

            return Ok(ApiResponse<bool>.Ok(true, "Cập nhật thông tin sản phẩm thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<bool>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpPost("check-availability")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckAvailability([FromBody] CheckVariantAvailabilityRequest request)
    {
        if (request == null || request.VariantIds == null || !request.VariantIds.Any())
        {
            return Ok(ApiResponse<IEnumerable<VariantAvailabilityDto>>.Ok(Enumerable.Empty<VariantAvailabilityDto>()));
        }

        var result = await _catalogService.CheckVariantsAvailabilityAsync(request.VariantIds);
        return Ok(ApiResponse<IEnumerable<VariantAvailabilityDto>>.Ok(result));
    }

    [HttpPut("variants/{variantId}/discount")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
    public async Task<IActionResult> UpdateVariantDiscount(int variantId, [FromBody] UpdateVariantDiscountRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(ApiResponse<bool>.Fail("Dữ liệu giảm giá không hợp lệ."));

            var success = await _catalogService.UpdateVariantDiscountAsync(variantId, request.PhanTramGiamGia);
            if (!success)
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy biến thể để cập nhật giảm giá."));

            return Ok(ApiResponse<bool>.Ok(true, "Cập nhật % giảm giá biến thể thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<bool>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }

    [HttpPut("{id}/discount")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},{RoleConstants.QuanLyChiNhanh},AdminChuoi,QuanLyChiNhanh,QuanLyHeThong")]
    public async Task<IActionResult> UpdateProductDiscount(int id, [FromBody] UpdateProductDiscountRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(ApiResponse<bool>.Fail("Dữ liệu giảm giá không hợp lệ."));

            var success = await _catalogService.UpdateProductDiscountAsync(id, request.PhanTramGiamGia);
            if (!success)
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy sản phẩm để cập nhật giảm giá."));

            return Ok(ApiResponse<bool>.Ok(true, "Cập nhật % giảm giá cho toàn bộ biến thể thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<bool>.Fail($"Lỗi hệ thống: {ex.Message}"));
        }
    }
}
