using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;
using Shared.DTOs.Auth;
using Shared.DTOs.Catalog;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var response = await _authService.LoginAsync(request);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCatalog()
    {
        var result = await _productService.GetProductsCatalogAsync();
        return Ok(result);
    }

    [HttpGet("{id}/variants")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVariants(int id)
    {
        var result = await _productService.GetProductVariantsAsync(id);
        return Ok(result);
    }

    [HttpPost("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromBody] ProductFilterRequestDto filter)
    {
        var result = await _productService.SearchProductsAsync(filter);
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVienBanHang}")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _orderService.GetAllOrdersAsync();
        return Ok(result);
    }

    [HttpGet("{id}/details")]
    [Authorize]
    public async Task<IActionResult> GetDetails(int id)
    {
        var result = await _orderService.GetOrderDetailsAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [AllowAnonymous] // Cho phép cả khách mua online lẫn nhân viên bán hàng tại quầy
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request)
    {
        var result = await _orderService.CreateOrderAsync(request);
        return CreatedAtAction(nameof(GetDetails), new { id = result.Data }, result);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyChiNhanh},{RoleConstants.NhanVienBanHang}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] string status)
    {
        var result = await _orderService.UpdateOrderStatusAsync(id, status);
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpPost("import")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> ImportStock([FromBody] ImportStockRequestDto request)
    {
        var result = await _warehouseService.ImportStockAsync(request);
        return Ok(result);
    }

    [HttpGet("low-stock")]
    [Authorize(Roles = $"{RoleConstants.AdminChuoi},{RoleConstants.QuanLyChiNhanh}")]
    public async Task<IActionResult> GetLowStockAlerts()
    {
        var result = await _warehouseService.GetLowStockAlertsAsync();
        return Ok(result);
    }
}
