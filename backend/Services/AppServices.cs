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
        var user = await _authRepository.GetUserByUsernameAsync(request.TenDangNhap);

        if (user == null || !user.DangHoatDong)
        {
            return ApiResponse<LoginResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa!");
        }

        if (!_passwordHasher.VerifyPassword(request.MatKhau, user.MatKhauHash))
        {
            return ApiResponse<LoginResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác!");
        }

        // Tự động nâng cấp mật khẩu cũ sang PBKDF2 + Secret Key (Pepper) nếu chưa dùng định dạng mới
        if (!user.MatKhauHash.Contains(':'))
        {
            var upgradedHash = _passwordHasher.HashPassword(request.MatKhau);
            await _authRepository.UpdatePasswordHashAsync(user.MaTaiKhoan, upgradedHash);
        }

        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user.MaTaiKhoan, user.TenDangNhap, user.VaiTro, user.TenNhanVien);

        var response = new LoginResponseDto
        {
            MaTaiKhoan = user.MaTaiKhoan,
            TenDangNhap = user.TenDangNhap,
            MaNhanVien = user.MaNhanVien,
            TenNhanVien = user.TenNhanVien,
            VaiTro = user.VaiTro,
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
    Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId);
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

    public async Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId)
    {
        return await _orderRepository.GetOrderOwnershipAsync(orderId);
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
