using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Report;
using Shared.DTOs.Warehouse;
using global::Shared.DTOs.Auth;

namespace Frontend.Services;

public interface IAdminApiClient
{
    // Báo cáo & Thống kê (FR-23, FR-24, FR-25)
    Task<List<DailyRevenueReportDto>?> GetDailyRevenueAsync();
    Task<List<TopSellingProductDto>?> GetTopSellingProductsAsync(int top = 10);
    Task<FinancialRangeReportDto?> GetFinancialReportAsync(DateTime start, DateTime end);
    Task<List<BranchSalesLeaderboardDto>?> GetBranchSalesLeaderboardAsync();
    Task<List<BranchInventorySummaryDto>?> GetBranchInventorySummaryAsync();

    // Kho & Nhà cung cấp (FR-19, FR-20, FR-22)
    Task<List<LowStockAlertDto>?> GetLowStockAlertsAsync(int threshold = 10);
    Task<List<LowStockAlertDto>?> GetAllWarehouseVariantsAsync();
    Task<List<SupplierDto>?> GetSuppliersAsync();
    Task<SupplierDto?> CreateSupplierAsync(CreateSupplierRequestDto request);
    Task<bool> ImportStockAsync(ImportStockRequestDto request);
    Task<List<BranchDto>?> GetBranchesAsync();

    Task<List<OrderSummaryDto>?> GetAllOrdersAsync();
    Task<List<OrderSummaryDto>?> GetAllOrdersAsync(int? branchId);
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
    Task<List<OrderDetailItemDto>?> GetOrderDetailsAsync(int orderId);

    // Quản lý sản phẩm & Biến thể (FR-03)
    Task<PagedResult<ProductListItemDto>?> GetProductsAsync(int page = 1, int pageSize = 10);
    Task<ApiResponse<int>> CreateProductAsync(CreateProductRequest request);
    Task<List<ProductVariantDto>> GetProductVariantsAsync(int productId);
    Task<ApiResponse<int>> CreateProductVariantAsync(CreateProductVariantRequest request);
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<List<BrandDto>> GetBrandsAsync();

    // Quản lý tài khoản & Phân quyền (RBAC)
    Task<List<AccountSummaryDto>?> GetAccountsAsync();
    Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request);
    Task<bool> ToggleAccountStatusAsync(int accountId);
    Task<ApiResponse<bool>> RegisterCustomerAsync(RegisterCustomerDto request);

    // Quản lý tài khoản khách hàng & VIP (FEAT-CUS)
    Task<List<global::Shared.DTOs.Customer.CustomerAccountDto>?> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null);
    Task<bool> ToggleCustomerStatusAsync(int customerId);
    Task<ApiResponse<bool>> UpdateCustomerAccountAsync(global::Shared.DTOs.Customer.UpdateCustomerAccountDto dto);
    Task<ApiResponse<int>> CreateCustomerAccountAsync(global::Shared.DTOs.Customer.CreateCustomerAccountDto dto);
    Task<List<global::Shared.DTOs.Customer.CustomerOrderHistoryItemDto>?> GetCustomerOrdersForAdminAsync(int customerId);
    Task<ApiResponse<IEnumerable<global::Shared.DTOs.Customer.RankEvaluationResultDto>>> EvaluateMemberRanksAsync();

    // Quản lý Khiếu nại & Góp ý (FR-36, FR-37)
    Task<List<global::Shared.DTOs.Feedback.FeedbackDto>?> GetFeedbacksAsync();
    Task<bool> UpdateFeedbackStatusAsync(int id, string status, string? notes = null);
    Task<ApiResponse<bool>> ProcessReturnRequestAsync(int id, global::Shared.DTOs.Feedback.ProcessReturnRequestDto request);

    // Nhật ký hệ thống & Biến động (Audit Logs)
    Task<List<global::Shared.DTOs.Audit.AuditLogDto>?> GetAuditLogsAsync(string? loaiThaoTac = null, int limit = 100);

    // Thông báo nội bộ (Internal Announcements)
    Task<List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>?> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null);
    Task<ApiResponse<int>> CreateAnnouncementAsync(global::Shared.DTOs.Announcement.CreateAnnouncementRequestDto request);
}

