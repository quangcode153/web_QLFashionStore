using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
