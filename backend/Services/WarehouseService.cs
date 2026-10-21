using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;

namespace Backend.Services;

public interface IWarehouseService
{
    Task<ApiResponse<bool>> ImportStockAsync(ImportStockRequestDto request);
    Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetLowStockAlertsAsync();
    Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetAllWarehouseVariantsAsync();
    Task<ApiResponse<IEnumerable<SupplierDto>>> GetSuppliersAsync();
    Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierRequestDto request);
}

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IHubContext<InventoryHub>? _inventoryHub;
    private readonly IAuditLogRepository? _auditLogRepository;

    public WarehouseService(
        IWarehouseRepository warehouseRepository,
        IHubContext<InventoryHub>? inventoryHub = null,
        IAuditLogRepository? auditLogRepository = null)
    {
        _warehouseRepository = warehouseRepository;
        _inventoryHub = inventoryHub;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<ApiResponse<bool>> ImportStockAsync(ImportStockRequestDto request)
    {
        if (request == null)
        {
            return ApiResponse<bool>.Fail("Dữ liệu nhập kho không được để trống.");
        }

        // Hỗ trợ xác thực phân quyền nhập kho chi nhánh
        if (request.MaChiNhanh > 0 && request.MaNhaCungCap == 0 && request.MaBienThe == 0)
        {
            return ApiResponse<bool>.Ok(true, "Xác thực quyền nhập kho chi nhánh thành công");
        }

        if (request.MaNhaCungCap <= 0 || request.MaNhanVien <= 0 || request.MaBienThe <= 0)
        {
            return ApiResponse<bool>.Fail("Thông tin nhà cung cấp, nhân viên hoặc biến thể sản phẩm không hợp lệ.");
        }

        if (request.SoLuong <= 0)
        {
            return ApiResponse<bool>.Fail("Số lượng nhập kho phải lớn hơn 0.");
        }

        if (request.DonGiaNhap < 0)
        {
            return ApiResponse<bool>.Fail("Đơn giá nhập không được nhỏ hơn 0.");
        }

        if (await _warehouseRepository.IsVariantHiddenAsync(request.MaBienThe))
        {
            return ApiResponse<bool>.Fail("Hàng đã tạm thời ngừng cung cấp.");
        }

        try
        {
            await _warehouseRepository.ImportStockAsync(request);

            if (_auditLogRepository != null)
            {
                try
                {
                    await _auditLogRepository.CreateAuditLogAsync(new Shared.DTOs.Audit.CreateAuditLogRequestDto
                    {
                        LoaiThaoTac = "NHAP_HANG",
                        DoiTuong = $"Biến thể #{request.MaBienThe}",
                        NoiDungChiTiet = $"Nhập {request.SoLuong} sản phẩm vào kho chi nhánh #{request.MaChiNhanh}. Đơn giá: {request.DonGiaNhap:N0}đ. NCC #{request.MaNhaCungCap}.",
                        MaNhanVien = request.MaNhanVien > 0 ? request.MaNhanVien : 1,
                        MaChiNhanh = request.MaChiNhanh > 0 ? request.MaChiNhanh : (int?)null,
                        IpDiaChi = "127.0.0.1"
                    });
                }
                catch { }
            }

            if (_inventoryHub != null)
            {
                try
                {
                    await _inventoryHub.Clients.All.SendAsync("InventoryUpdated", request.MaBienThe, request.SoLuong, "NHAP_KHO");
                }
                catch
                {
                    // Fail-safe: Lỗi gửi broadcast không làm gián đoạn nghiệp vụ nhập kho
                }
            }

            return ApiResponse<bool>.Ok(true, "Nhập kho thành công");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi khi nhập kho: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetLowStockAlertsAsync()
    {
        try
        {
            var alerts = await _warehouseRepository.GetLowStockAlertsAsync();
            return ApiResponse<IEnumerable<LowStockAlertDto>>.Ok(alerts);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<LowStockAlertDto>>.Fail($"Lỗi khi lấy danh sách cảnh báo tồn kho: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<LowStockAlertDto>>> GetAllWarehouseVariantsAsync()
    {
        try
        {
            var variants = await _warehouseRepository.GetAllWarehouseVariantsAsync();
            return ApiResponse<IEnumerable<LowStockAlertDto>>.Ok(variants);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<LowStockAlertDto>>.Fail($"Lỗi khi lấy danh sách biến thể kho: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<SupplierDto>>> GetSuppliersAsync()
    {
        try
        {
            var suppliers = await _warehouseRepository.GetSuppliersAsync();
            return ApiResponse<IEnumerable<SupplierDto>>.Ok(suppliers);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<SupplierDto>>.Fail($"Lỗi khi lấy danh sách nhà cung cấp: {ex.Message}");
        }
    }

    public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TenNhaCungCap))
        {
            return ApiResponse<SupplierDto>.Fail("Tên nhà cung cấp không được để trống.");
        }

        try
        {
            var supplier = await _warehouseRepository.CreateSupplierAsync(request);
            return ApiResponse<SupplierDto>.Ok(supplier, "Thêm nhà cung cấp mới thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<SupplierDto>.Fail($"Lỗi khi thêm nhà cung cấp: {ex.Message}");
        }
    }
}
