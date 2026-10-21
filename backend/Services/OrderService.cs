using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.Enums;

namespace Backend.Services;

public interface IOrderService
{
    Task<ApiResponse<int>> CreateOrderAsync(CreateOrderRequestDto request);
    Task<ApiResponse<bool>> UpdateOrderStatusAsync(int orderId, string newStatus);
    Task<ApiResponse<IEnumerable<OrderSummaryDto>>> GetAllOrdersAsync(int? branchId = null, string? userRole = null, int? userBranchId = null);
    Task<ApiResponse<IEnumerable<OrderDetailItemDto>>> GetOrderDetailsAsync(int orderId);
    Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId);
    Task<ApiResponse<ReceiptPrintDto>> GetReceiptForPrintAsync(int orderId);
    Task<ApiResponse<ReturnOrderResultDto>> ProcessReturnAsync(int orderId, ReturnOrderRequestDto request, int staffId);
    Task<ApiResponse<OrderReturnLookupDto>> GetOrderForReturnLookupAsync(int orderId);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICatalogRepository? _catalogRepository;
    private readonly IHubContext<NotificationHub>? _notificationHub;
    private readonly IHubContext<InventoryHub>? _inventoryHub;

    public OrderService(
        IOrderRepository orderRepository,
        IHubContext<NotificationHub>? notificationHub = null,
        IHubContext<InventoryHub>? inventoryHub = null,
        ICatalogRepository? catalogRepository = null)
    {
        _orderRepository = orderRepository;
        _notificationHub = notificationHub;
        _inventoryHub = inventoryHub;
        _catalogRepository = catalogRepository;
    }

    public async Task<ApiResponse<OrderReturnLookupDto>> GetOrderForReturnLookupAsync(int orderId)
    {
        if (orderId <= 0)
        {
            return ApiResponse<OrderReturnLookupDto>.Fail("Mã hóa đơn tra cứu không hợp lệ.");
        }

        try
        {
            var data = await _orderRepository.GetOrderForReturnLookupAsync(orderId);
            if (data == null)
            {
                return ApiResponse<OrderReturnLookupDto>.Fail("Hóa đơn không tồn tại trên hệ thống.");
            }

            if (!data.IsEligibleForReturn && string.IsNullOrWhiteSpace(data.IneligibilityReason))
            {
                data.IneligibilityReason = $"Chỉ hóa đơn đã hoàn thành mới có thể đổi trả. Trạng thái hiện tại: {data.TrangThai}.";
            }

            return ApiResponse<OrderReturnLookupDto>.Ok(data, "Tra cứu hóa đơn đổi trả thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderReturnLookupDto>.Fail($"Lỗi khi tra cứu hóa đơn đổi trả: {ex.Message}");
        }
    }


    public async Task<ApiResponse<int>> CreateOrderAsync(CreateOrderRequestDto request)
    {
        if (request == null)
        {
            return ApiResponse<int>.Fail("Thông tin đơn hàng không được để trống.");
        }

        if (request.Items == null || !request.Items.Any())
        {
            return ApiResponse<int>.Fail("Đơn hàng phải chứa ít nhất một sản phẩm.");
        }

        if (request.Items.Any(i => i.MaBienThe <= 0 || i.SoLuong <= 0))
        {
            return ApiResponse<int>.Fail("Mã biến thể và số lượng sản phẩm phải lớn hơn 0.");
        }

        if (_catalogRepository != null)
        {
            var variantIds = request.Items.Select(i => i.MaBienThe).Distinct();
            var availability = await _catalogRepository.CheckVariantsAvailabilityAsync(variantIds);
            var unavailable = availability.FirstOrDefault(x => x.DaAn);
            if (unavailable != null)
            {
                var variantDesc = (!string.IsNullOrWhiteSpace(unavailable.MauSac) || !string.IsNullOrWhiteSpace(unavailable.KichCo))
                    ? $" ({unavailable.MauSac} - {unavailable.KichCo})"
                    : "";
                return ApiResponse<int>.Fail($"Sản phẩm '{unavailable.TenSanPham}'{variantDesc} tạm thời không cung cấp.");
            }
        }

        try
        {
            var orderId = (request.MaChiNhanh.HasValue && request.MaChiNhanh.Value > 0)
                ? await _orderRepository.CreateOrderAsync(request.MaKhachHang, request.MaNhanVien, request.MaGiamGia, request.MaChiNhanh.Value)
                : await _orderRepository.CreateOrderAsync(request.MaKhachHang, request.MaNhanVien, request.MaGiamGia);

            foreach (var item in request.Items)
            {
                await _orderRepository.AddOrderItemAsync(orderId, item.MaBienThe, item.SoLuong);
            }

            if (!string.IsNullOrWhiteSpace(request.TrangThai))
            {
                await _orderRepository.UpdateOrderStatusAsync(orderId, request.TrangThai);
            }

            if (!string.IsNullOrWhiteSpace(request.DiaChiGiaoHang))
            {
                await _orderRepository.UpdateOrderShippingAddressAsync(orderId, request.DiaChiGiaoHang);
            }

            if (_notificationHub != null)
            {
                try
                {
                    await _notificationHub.Clients.All.SendAsync("NewOrderPlaced", orderId, "Đơn hàng mới đã được khởi tạo.");
                }
                catch
                {
                    // Fail-safe: Lỗi gửi broadcast không làm gián đoạn nghiệp vụ tạo đơn
                }
            }

            return ApiResponse<int>.Ok(orderId, "Tạo đơn hàng thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi tạo đơn hàng: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        if (orderId <= 0)
        {
            return ApiResponse<bool>.Fail("Mã đơn hàng không hợp lệ (phải > 0).");
        }

        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return ApiResponse<bool>.Fail("Trạng thái không được để trống.");
        }

        if (!OrderStatusHelper.TryNormalizeStatus(newStatus, out var normalizedStatus))
        {
            return ApiResponse<bool>.Fail($"Trạng thái '{newStatus}' không hợp lệ. Các giá trị được chấp nhận: {OrderStatusHelper.GetAllowedStatusString()}.");
        }

        var currentStatus = await _orderRepository.GetOrderStatusAsync(orderId);
        if (string.IsNullOrEmpty(currentStatus))
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy đơn hàng #{orderId} trên hệ thống.");
        }

        if (string.Equals(currentStatus, "Đã hoàn thành", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail($"Đơn hàng #{orderId} đã hoàn thành, không thể thay đổi trạng thái (chỉ xử lý qua quy trình hoàn trả hàng).");
        }

        if (string.Equals(currentStatus, "Đã hủy", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail($"Đơn hàng #{orderId} đã hủy, không thể thay đổi trạng thái.");
        }

        if (string.Equals(currentStatus, "Đã hoàn trả", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail($"Đơn hàng #{orderId} đã hoàn trả, không thể thay đổi trạng thái.");
        }

        try
        {
            await _orderRepository.UpdateOrderStatusAsync(orderId, normalizedStatus);
            return ApiResponse<bool>.Ok(true, $"Cập nhật trạng thái thành '{normalizedStatus}' thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<OrderSummaryDto>>> GetAllOrdersAsync(int? branchId = null, string? userRole = null, int? userBranchId = null)
    {
        int? effectiveBranchId;

        bool isMultiBranchAdmin = string.Equals(userRole, Shared.Constants.RoleConstants.AdminChuoi, StringComparison.OrdinalIgnoreCase) 
                               || string.Equals(userRole, Shared.Constants.RoleConstants.QuanLyHeThong, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(userRole, "Administrator", StringComparison.OrdinalIgnoreCase);

        if (isMultiBranchAdmin)
        {
            // Admin toàn chuỗi: Nếu truyền chi nhánh cụ thể (> 0) thì lọc theo chi nhánh đó; nếu null hoặc 0 ("Tất cả chi nhánh") thì truy vấn toàn bộ hệ thống
            effectiveBranchId = (branchId.HasValue && branchId.Value > 0) ? branchId.Value : null;
        }
        else
        {
            // Quản lý chi nhánh / Điều phối viên (Order Coordinator) / Nhân viên:
            // Chỉ được xem và điều phối các đơn hàng thuộc chi nhánh mình đang quản lý (userBranchId).
            // Ví dụ: Điều phối viên chi nhánh online chỉ xử lý các đơn hàng onl của kho online. Tương tự các chi nhánh khác.
            effectiveBranchId = (userBranchId.HasValue && userBranchId.Value > 0) ? userBranchId.Value : 1;
        }

        var orders = await _orderRepository.GetAllOrdersAsync(effectiveBranchId);
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

    public async Task<ApiResponse<ReceiptPrintDto>> GetReceiptForPrintAsync(int orderId)
    {
        if (orderId <= 0)
        {
            return ApiResponse<ReceiptPrintDto>.Fail("Mã hóa đơn không hợp lệ.");
        }

        try
        {
            var receipt = await _orderRepository.GetReceiptDetailsAsync(orderId);
            if (receipt == null)
            {
                return ApiResponse<ReceiptPrintDto>.Fail("Hóa đơn không tồn tại trên hệ thống.");
            }
            return ApiResponse<ReceiptPrintDto>.Ok(receipt, "Lấy thông tin in hóa đơn thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<ReceiptPrintDto>.Fail($"Lỗi khi lấy thông tin in hóa đơn: {ex.Message}");
        }
    }

    public async Task<ApiResponse<ReturnOrderResultDto>> ProcessReturnAsync(int orderId, ReturnOrderRequestDto request, int staffId)
    {
        if (orderId <= 0 || staffId <= 0)
        {
            return ApiResponse<ReturnOrderResultDto>.Fail("Thông tin đơn hàng hoặc nhân viên xử lý không hợp lệ.");
        }

        if (request == null)
        {
            return ApiResponse<ReturnOrderResultDto>.Fail("Dữ liệu yêu cầu đổi trả không được để trống.");
        }

        if (request.MaBienThe <= 0 || request.SoLuongTra <= 0)
        {
            return ApiResponse<ReturnOrderResultDto>.Fail("Mã biến thể và số lượng trả phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(request.LyDoTra))
        {
            return ApiResponse<ReturnOrderResultDto>.Fail("Vui lòng nhập lý do đổi trả hàng.");
        }

        try
        {
            var (success, message, data) = await _orderRepository.ProcessOrderReturnAsync(
                orderId, request.MaBienThe, request.SoLuongTra, request.LyDoTra, staffId);

            if (!success || data == null)
            {
                return ApiResponse<ReturnOrderResultDto>.Fail(message);
            }

            if (_inventoryHub != null)
            {
                try
                {
                    await _inventoryHub.Clients.All.SendAsync("InventoryUpdated", request.MaBienThe, request.SoLuongTra, "DOI_TRA");
                }
                catch
                {
                    // Fail-safe: Lỗi gửi broadcast không làm gián đoạn nghiệp vụ đổi trả
                }
            }

            return ApiResponse<ReturnOrderResultDto>.Ok(data, message);
        }
        catch (Exception ex)
        {
            return ApiResponse<ReturnOrderResultDto>.Fail($"Lỗi khi xử lý đổi trả hàng: {ex.Message}");
        }
    }
}
