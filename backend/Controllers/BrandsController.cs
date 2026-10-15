using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
