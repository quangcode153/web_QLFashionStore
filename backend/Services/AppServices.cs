using Backend.Repositories;
using Backend.Security;
using Shared.DTOs.Auth;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;

namespace Backend.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IAuthRepository authRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
    {
        _authRepository = authRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var passwordHash = _passwordHasher.HashPassword(request.MatKhau);
        var user = await _authRepository.AuthenticateAsync(request.TenDangNhap, passwordHash);

        if (user == null)
        {
            return ApiResponse<LoginResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác!");
        }

        int userId = (int)user.ma_tai_khoan;
        string username = (string)user.ten_dang_nhap;
        string role = (string)user.vai_tro;
        string fullName = (string)user.ten_nhan_vien;
        int employeeId = (int)user.ma_nhan_vien;

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(userId, username, role, fullName);

        var response = new LoginResponseDto
        {
            MaTaiKhoan = userId,
            TenDangNhap = username,
            MaNhanVien = employeeId,
            TenNhanVien = fullName,
            VaiTro = role,
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<LoginResponseDto>.Ok(response, "Đăng nhập thành công");
    }
}

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

public interface IOrderService
{
    Task<ApiResponse<int>> CreateOrderAsync(CreateOrderRequestDto request);
    Task<ApiResponse<bool>> UpdateOrderStatusAsync(int orderId, string newStatus);
    Task<ApiResponse<IEnumerable<OrderSummaryDto>>> GetAllOrdersAsync();
    Task<ApiResponse<IEnumerable<OrderDetailItemDto>>> GetOrderDetailsAsync(int orderId);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<ApiResponse<int>> CreateOrderAsync(CreateOrderRequestDto request)
    {
        var orderId = await _orderRepository.CreateOrderAsync(request.MaKhachHang, request.MaNhanVien, request.MaGiamGia);

        foreach (var item in request.Items)
        {
            await _orderRepository.AddOrderItemAsync(orderId, item.MaBienThe, item.SoLuong);
        }

        return ApiResponse<int>.Ok(orderId, "Tạo đơn hàng thành công");
    }

    public async Task<ApiResponse<bool>> UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        await _orderRepository.UpdateOrderStatusAsync(orderId, newStatus);
        return ApiResponse<bool>.Ok(true, $"Cập nhật trạng thái thành '{newStatus}' thành công");
    }

    public async Task<ApiResponse<IEnumerable<OrderSummaryDto>>> GetAllOrdersAsync()
    {
        var orders = await _orderRepository.GetAllOrdersAsync();
        return ApiResponse<IEnumerable<OrderSummaryDto>>.Ok(orders);
    }

    public async Task<ApiResponse<IEnumerable<OrderDetailItemDto>>> GetOrderDetailsAsync(int orderId)
    {
        var items = await _orderRepository.GetOrderDetailsAsync(orderId);
        return ApiResponse<IEnumerable<OrderDetailItemDto>>.Ok(items);
    }
}

public interface IWarehouseService
{
    Task<ApiResponse<bool>> ImportStockAsync(ImportStockRequestDto request);
    Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetLowStockAlertsAsync();
}

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _warehouseRepository;

    public WarehouseService(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    public async Task<ApiResponse<bool>> ImportStockAsync(ImportStockRequestDto request)
    {
        await _warehouseRepository.ImportStockAsync(request);
        return ApiResponse<bool>.Ok(true, "Nhập kho thành công");
    }

    public async Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetLowStockAlertsAsync()
    {
        var alerts = await _warehouseRepository.GetLowStockAlertsAsync();
        return ApiResponse<IEnumerable<LowStockAlertDto>>.Ok(alerts);
    }
}
