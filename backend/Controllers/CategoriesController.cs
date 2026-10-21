using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CategoriesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _catalogService.GetCategoriesAsync();
        return Ok(ApiResponse<IEnumerable<CategoryDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyHeThong},AdminChuoi,QuanLyHeThong")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TenDanhMuc))
        {
            return BadRequest(ApiResponse<int>.Fail("Tên danh mục không được để trống."));
        }

        try
        {
            var id = await _catalogService.CreateCategoryAsync(request);
            return StatusCode(201, ApiResponse<int>.Ok(id, "Tạo danh mục mới thành công."));
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
