using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public BrandsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetBrands()
    {
        var result = await _catalogService.GetBrandsAsync();
        return Ok(ApiResponse<IEnumerable<BrandDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},AdminChuoi,QuanLyHeThong")]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TenThuongHieu))
        {
            return BadRequest(ApiResponse<int>.Fail("Tên thương hiệu không được để trống."));
        }

        try
        {
            var id = await _catalogService.CreateBrandAsync(request);
            return StatusCode(201, ApiResponse<int>.Ok(id, "Tạo thương hiệu mới thành công."));
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

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},AdminChuoi,QuanLyHeThong")]
    public async Task<IActionResult> DeleteBrand(int id)
    {
        if (id <= 0)
        {
            return BadRequest(ApiResponse<bool>.Fail("Mã thương hiệu không hợp lệ."));
        }

        try
        {
            var success = await _catalogService.DeleteBrandAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<bool>.Fail("Không tìm thấy thương hiệu để xóa."));
            }

            return Ok(ApiResponse<bool>.Ok(true, "Xóa thương hiệu không còn hợp tác thành công."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<bool>.Fail(ex.Message));
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
