using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Shared.DTOs.Report;
using Shared.DTOs.Warehouse;
using global::Shared.DTOs.Auth;

using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Frontend.Services;

public class AdminApiClient : IAdminApiClient
{
    private readonly HttpClient _http;
    private readonly IJSRuntime? _js;

    public AdminApiClient(HttpClient http, IJSRuntime? js = null)
    {
        _http = http;
        _js = js;
    }

    private async Task AttachAuthHeaderAsync()
    {
        if (_js == null) return;
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (!string.IsNullOrEmpty(token))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch { }
    }

    public async Task<List<DailyRevenueReportDto>?> GetDailyRevenueAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<DailyRevenueReportDto>>>("api/reports/daily-revenue");
            return res?.Data?.ToList() ?? new List<DailyRevenueReportDto>();
        }
        catch
        {
            return new List<DailyRevenueReportDto>();
        }
    }

    public async Task<List<TopSellingProductDto>?> GetTopSellingProductsAsync(int top = 10)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<TopSellingProductDto>>>($"api/reports/top-selling?top={top}");
            return res?.Data?.ToList() ?? new List<TopSellingProductDto>();
        }
        catch
        {
            return new List<TopSellingProductDto>();
        }
    }

    public async Task<FinancialRangeReportDto?> GetFinancialReportAsync(DateTime start, DateTime end)
    {
        try
        {
            var fromStr = start.ToString("yyyy-MM-dd");
            var toStr = end.ToString("yyyy-MM-dd");
            var res = await _http.GetFromJsonAsync<ApiResponse<FinancialRangeReportDto>>($"api/reports/financial-range?fromDate={fromStr}&toDate={toStr}");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<BranchSalesLeaderboardDto>?> GetBranchSalesLeaderboardAsync()
    {
        try
        {
            await AttachAuthHeaderAsync();
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<BranchSalesLeaderboardDto>>>("api/reports/branch-sales");
            return res?.Data?.ToList() ?? new List<BranchSalesLeaderboardDto>();
        }
        catch
        {
            return new List<BranchSalesLeaderboardDto>();
        }
    }

    public async Task<List<BranchInventorySummaryDto>?> GetBranchInventorySummaryAsync()
    {
        try
        {
            await AttachAuthHeaderAsync();
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<BranchInventorySummaryDto>>>("api/reports/branch-inventory-summary");
            return res?.Data?.ToList() ?? new List<BranchInventorySummaryDto>();
        }
        catch
        {
            return new List<BranchInventorySummaryDto>();
        }
    }

    public async Task<List<LowStockAlertDto>?> GetLowStockAlertsAsync(int threshold = 10)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<LowStockAlertDto>>>("api/warehouse/low-stock");
            return res?.Data?.ToList() ?? new List<LowStockAlertDto>();
        }
        catch
        {
            return new List<LowStockAlertDto>();
        }
    }

    public async Task<List<LowStockAlertDto>?> GetAllWarehouseVariantsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<LowStockAlertDto>>>("api/warehouse/variants");
            return res?.Data?.ToList() ?? new List<LowStockAlertDto>();
        }
        catch
        {
            return new List<LowStockAlertDto>();
        }
    }

    public async Task<List<SupplierDto>?> GetSuppliersAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<SupplierDto>>>("api/warehouse/suppliers");
            return res?.Data?.ToList() ?? new List<SupplierDto>();
        }
        catch
        {
            return new List<SupplierDto>();
        }
    }

    public async Task<SupplierDto?> CreateSupplierAsync(CreateSupplierRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/warehouse/suppliers", request);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadFromJsonAsync<ApiResponse<SupplierDto>>();
                return body?.Data;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ImportStockAsync(ImportStockRequestDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/warehouse/import", request);
            if (!res.IsSuccessStatusCode)
            {
                try
                {
                    var errorRes = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                    if (errorRes != null && !string.IsNullOrWhiteSpace(errorRes.Message))
                    {
                        throw new InvalidOperationException(errorRes.Message);
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                    // Ignore JSON parse errors and return false
                }
            }
            return res.IsSuccessStatusCode;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<BranchDto>?> GetBranchesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<BranchDto>>>("api/branches");
            return res?.Data ?? new List<BranchDto>();
        }
        catch
        {
            return new List<BranchDto>();
        }
    }

    public async Task<ApiResponse<int>> CreateBranchAsync(CreateBranchDto dto)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/branches", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return result ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi tạo chi nhánh: {ex.Message}");
        }
    }

    public async Task<List<BranchInventoryDto>?> GetBranchInventoryAsync(int branchId, string? search = null)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(search) 
                ? $"api/branches/{branchId}/inventory"
                : $"api/branches/{branchId}/inventory?search={Uri.EscapeDataString(search.Trim())}";
            var res = await _http.GetFromJsonAsync<ApiResponse<List<BranchInventoryDto>>>(url);
            return res?.Data ?? new List<BranchInventoryDto>();
        }
        catch
        {
            return new List<BranchInventoryDto>();
        }
    }

    public Task<List<OrderSummaryDto>?> GetAllOrdersAsync() => GetAllOrdersAsync(null);

    public async Task<List<OrderSummaryDto>?> GetAllOrdersAsync(int? branchId)
    {
        try
        {
            var url = branchId.HasValue && branchId.Value > 0
                ? $"api/orders?branchId={branchId.Value}"
                : "api/orders";
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<OrderSummaryDto>>>(url);
            return res?.Data?.ToList() ?? new List<OrderSummaryDto>();
        }
        catch
        {
            return new List<OrderSummaryDto>();
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        try
        {
            var res = await _http.PutAsync($"api/orders/{orderId}/status?status={Uri.EscapeDataString(status)}", null);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OrderDetailItemDto>?> GetOrderDetailsAsync(int orderId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<OrderDetailItemDto>>>($"api/orders/{orderId}/details");
            return res?.Data?.ToList() ?? new List<OrderDetailItemDto>();
        }
        catch
        {
            return new List<OrderDetailItemDto>();
        }
    }

    public Task<PagedResult<ProductListItemDto>?> GetProductsAsync(int page, int pageSize) 
        => GetProductsAsync(page, pageSize, null, true, null);

    public async Task<PagedResult<ProductListItemDto>?> GetProductsAsync(int page = 1, int pageSize = 10, bool? daXoa = null, bool includeAll = false, string? search = null)
    {
        try
        {
            var url = $"api/products?pageNumber={page}&pageSize={pageSize}";
            if (daXoa.HasValue)
                url += $"&daXoa={daXoa.Value.ToString().ToLowerInvariant()}";
            if (includeAll)
                url += "&includeAll=true";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search.Trim())}";

            var res = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>(url);
            return res?.Data;
        }
        catch
        {
            return new PagedResult<ProductListItemDto>();
        }
    }

    public async Task<ApiResponse<bool>> UpdateProductAsync(int id, UpdateProductRequest request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/products/{id}", request);
            if (!res.IsSuccessStatusCode)
            {
                var errContent = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
                return errContent ?? ApiResponse<bool>.Fail($"Cập nhật thất bại (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return content ?? ApiResponse<bool>.Ok(true, "Cập nhật sản phẩm thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ToggleProductStatusAsync(int productId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PatchAsync($"api/products/{productId}/toggle-status", null);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return ApiResponse<bool>.Fail("Bạn không có quyền thay đổi trạng thái sản phẩm (Chỉ Admin toàn chuỗi mới được phép).");
                return ApiResponse<bool>.Fail($"Lỗi thao tác (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return content ?? ApiResponse<bool>.Fail("Không nhận được phản hồi.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ToggleVariantStatusAsync(int variantId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PatchAsync($"api/products/variants/{variantId}/toggle-status", null);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return ApiResponse<bool>.Fail("Bạn không có quyền thay đổi trạng thái biến thể (Chỉ Admin toàn chuỗi mới được phép).");
                return ApiResponse<bool>.Fail($"Lỗi thao tác (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return content ?? ApiResponse<bool>.Fail("Không nhận được phản hồi.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<List<ProductVariantDto>> GetProductVariantsForAdminAsync(int productId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductVariantDto>>>($"api/products/{productId}/variants/admin");
            return res?.Data?.ToList() ?? new List<ProductVariantDto>();
        }
        catch
        {
            return new List<ProductVariantDto>();
        }
    }

    public async Task<ApiResponse<bool>> UpdateVariantDiscountAsync(int variantId, int discountPercent)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/products/variants/{variantId}/discount", new UpdateVariantDiscountRequest { PhanTramGiamGia = discountPercent });
            if (!res.IsSuccessStatusCode)
            {
                var errorText = await res.Content.ReadAsStringAsync();
                try
                {
                    var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                        return errObj;
                }
                catch { }
                return ApiResponse<bool>.Fail($"Lỗi cập nhật giảm giá (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return content ?? ApiResponse<bool>.Fail("Không nhận được phản hồi.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdateProductDiscountAsync(int productId, int discountPercent)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/products/{productId}/discount", new UpdateProductDiscountRequest { PhanTramGiamGia = discountPercent });
            if (!res.IsSuccessStatusCode)
            {
                var errorText = await res.Content.ReadAsStringAsync();
                try
                {
                    var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<bool>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                        return errObj;
                }
                catch { }
                return ApiResponse<bool>.Fail($"Lỗi cập nhật giảm giá (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return content ?? ApiResponse<bool>.Fail("Không nhận được phản hồi.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateProductAsync(CreateProductRequest request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/products", request);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return ApiResponse<int>.Fail("Bạn không có quyền hạn tạo sản phẩm (403 Forbidden).");
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return ApiResponse<int>.Fail("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại (401 Unauthorized).");

                var errorText = await res.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    try
                    {
                        var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<int>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                            return errObj;
                    }
                    catch { }
                }
                return ApiResponse<int>.Fail($"Lỗi máy chủ (HTTP {(int)res.StatusCode} - {res.ReasonPhrase})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return content ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi kết nối khi tạo sản phẩm: {ex.Message}");
        }
    }

    public async Task<List<ProductVariantDto>> GetProductVariantsAsync(int productId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductVariantDto>>>($"api/products/{productId}/variants");
            return res?.Data?.ToList() ?? new List<ProductVariantDto>();
        }
        catch
        {
            return new List<ProductVariantDto>();
        }
    }

    public async Task<ApiResponse<int>> CreateProductVariantAsync(CreateProductVariantRequest request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/products/variants", request);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return ApiResponse<int>.Fail("Bạn không có quyền hạn tạo biến thể (403 Forbidden).");
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return ApiResponse<int>.Fail("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại (401 Unauthorized).");

                var errorText = await res.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    try
                    {
                        var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<int>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                            return errObj;
                    }
                    catch { }
                }
                return ApiResponse<int>.Fail($"Lỗi máy chủ (HTTP {(int)res.StatusCode} - {res.ReasonPhrase})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return content ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi kết nối khi tạo biến thể: {ex.Message}");
        }
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<CategoryDto>>>("api/categories");
            return res?.Data?.ToList() ?? new List<CategoryDto>();
        }
        catch
        {
            return new List<CategoryDto>();
        }
    }

    public async Task<List<BrandDto>> GetBrandsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<BrandDto>>>("api/brands");
            return res?.Data?.ToList() ?? new List<BrandDto>();
        }
        catch
        {
            return new List<BrandDto>();
        }
    }

    public async Task<ApiResponse<int>> CreateCategoryAsync(CreateCategoryRequest request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/categories", request);
            if (!res.IsSuccessStatusCode)
            {
                var errorText = await res.Content.ReadAsStringAsync();
                try
                {
                    var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<int>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                        return errObj;
                }
                catch { }
                return ApiResponse<int>.Fail($"Lỗi máy chủ khi tạo danh mục (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return content ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateBrandAsync(CreateBrandRequest request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/brands", request);
            if (!res.IsSuccessStatusCode)
            {
                var errorText = await res.Content.ReadAsStringAsync();
                try
                {
                    var errObj = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<int>>(errorText, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (errObj != null && !string.IsNullOrWhiteSpace(errObj.Message))
                        return errObj;
                }
                catch { }
                return ApiResponse<int>.Fail($"Lỗi máy chủ khi tạo thương hiệu (HTTP {(int)res.StatusCode})");
            }
            var content = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return content ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }


    public async Task<List<AccountSummaryDto>?> GetAccountsAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<AccountSummaryDto>>>("api/auth/accounts");
            return res?.Data ?? new List<AccountSummaryDto>();
        }
        catch
        {
            return new List<AccountSummaryDto>();
        }
    }

    public async Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/staff/create", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result ?? ApiResponse<bool>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi tạo tài khoản: {ex.Message}");
        }
    }

    public async Task<bool> ToggleAccountStatusAsync(int accountId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsync($"api/auth/accounts/{accountId}/toggle", null);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<bool>> RegisterCustomerAsync(RegisterCustomerDto request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/register", request);
            var content = await res.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                bool success = false;
                if (root.TryGetProperty("success", out var sProp) || root.TryGetProperty("Success", out sProp))
                {
                    success = sProp.GetBoolean();
                }
                else
                {
                    success = res.IsSuccessStatusCode;
                }

                string message = string.Empty;
                if (root.TryGetProperty("message", out var mProp) || root.TryGetProperty("Message", out mProp))
                {
                    message = mProp.GetString() ?? string.Empty;
                }

                return success
                    ? ApiResponse<bool>.Ok(true, string.IsNullOrWhiteSpace(message) ? "Đăng ký tài khoản thành công!" : message)
                    : ApiResponse<bool>.Fail(string.IsNullOrWhiteSpace(message) ? $"Đăng ký không thành công ({res.StatusCode})" : message);
            }
            catch
            {
                return res.IsSuccessStatusCode
                    ? ApiResponse<bool>.Ok(true, "Đăng ký tài khoản thành công!")
                    : ApiResponse<bool>.Fail($"Lỗi hệ thống ({res.StatusCode}): {content}");
            }
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi kết nối máy chủ: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Customer.CustomerAccountDto>?> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (capDo.HasValue) queryParams.Add($"capDo={capDo.Value}");
            if (daXoa.HasValue) queryParams.Add($"daXoa={daXoa.Value}");

            var url = "api/customers" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
            var res = await _http.GetFromJsonAsync<ApiResponse<List<global::Shared.DTOs.Customer.CustomerAccountDto>>>(url);
            return res?.Data ?? new List<global::Shared.DTOs.Customer.CustomerAccountDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Customer.CustomerAccountDto>();
        }
    }

    public async Task<bool> ToggleCustomerStatusAsync(int customerId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsync($"api/customers/{customerId}/toggle-status", null);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<bool>> UpdateCustomerAccountAsync(global::Shared.DTOs.Customer.UpdateCustomerAccountDto dto)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/customers/{dto.MaKhachHang}", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result ?? ApiResponse<bool>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi cập nhật: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> CreateCustomerAccountAsync(global::Shared.DTOs.Customer.CreateCustomerAccountDto dto)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/customers", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return result ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi tạo tài khoản khách hàng: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Customer.CustomerOrderHistoryItemDto>?> GetCustomerOrdersForAdminAsync(int customerId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<global::Shared.DTOs.Customer.CustomerOrderHistoryItemDto>>>($"api/customers/{customerId}/orders");
            return res?.Data ?? new List<global::Shared.DTOs.Customer.CustomerOrderHistoryItemDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Customer.CustomerOrderHistoryItemDto>();
        }
    }

    public async Task<ApiResponse<IEnumerable<global::Shared.DTOs.Customer.RankEvaluationResultDto>>> EvaluateMemberRanksAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsync("api/customers/eval-ranks", null);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<global::Shared.DTOs.Customer.RankEvaluationResultDto>>>();
            return result ?? ApiResponse<IEnumerable<global::Shared.DTOs.Customer.RankEvaluationResultDto>>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<global::Shared.DTOs.Customer.RankEvaluationResultDto>>.Fail($"Lỗi xét duyệt: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Feedback.FeedbackDto>?> GetFeedbacksAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<global::Shared.DTOs.Feedback.FeedbackDto>>>("api/feedback");
            return res?.Data ?? new List<global::Shared.DTOs.Feedback.FeedbackDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Feedback.FeedbackDto>();
        }
    }

    public async Task<bool> UpdateFeedbackStatusAsync(int id, string status, string? notes = null)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsync($"api/feedback/{id}/status?status={Uri.EscapeDataString(status)}&notes={Uri.EscapeDataString(notes ?? "")}", null);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<bool>> ProcessReturnRequestAsync(int id, global::Shared.DTOs.Feedback.ProcessReturnRequestDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/feedback/{id}/process-return", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result ?? ApiResponse<bool>.Fail("Không nhận được phản hồi từ hệ thống.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi xử lý hoàn trả: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Audit.AuditLogDto>?> GetAuditLogsAsync(string? loaiThaoTac = null, int? maChiNhanh = null, int limit = 100)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var url = $"api/audit-logs?limit={limit}";
            if (!string.IsNullOrWhiteSpace(loaiThaoTac) && loaiThaoTac != "ALL")
            {
                url += $"&loaiThaoTac={Uri.EscapeDataString(loaiThaoTac)}";
            }
            if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
            {
                url += $"&maChiNhanh={maChiNhanh.Value}";
            }
            var res = await _http.GetFromJsonAsync<ApiResponse<List<global::Shared.DTOs.Audit.AuditLogDto>>>(url);
            return res?.Data ?? new List<global::Shared.DTOs.Audit.AuditLogDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminApiClient] Lỗi tải nhật ký kiểm toán: {ex.Message}");
            throw;
        }
    }

    public async Task<List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>?> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var url = "api/announcements?";
            if (!string.IsNullOrWhiteSpace(phamVi) && phamVi != "ALL")
            {
                url += $"phamVi={Uri.EscapeDataString(phamVi)}&";
            }
            if (maChiNhanh.HasValue && maChiNhanh.Value > 0)
            {
                url += $"maChiNhanh={maChiNhanh.Value}&";
            }
            url = url.TrimEnd('?', '&');
            var res = await _http.GetFromJsonAsync<ApiResponse<List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>>>(url);
            return res?.Data ?? new List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Announcement.InternalAnnouncementDto>();
        }
    }

    public async Task<ApiResponse<int>> CreateAnnouncementAsync(global::Shared.DTOs.Announcement.CreateAnnouncementRequestDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/announcements", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return result ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi phát thông báo: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Promotion.PromotionDto>?> GetAllPromotionsAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<IEnumerable<global::Shared.DTOs.Promotion.PromotionDto>>>("api/promotions/all");
            return res?.Data?.ToList() ?? new List<global::Shared.DTOs.Promotion.PromotionDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Promotion.PromotionDto>();
        }
    }

    public async Task<ApiResponse<int>> CreatePromotionAsync(global::Shared.DTOs.Promotion.CreatePromotionRequestDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PostAsJsonAsync("api/promotions", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return result ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi tạo mã giảm giá: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> UpdatePromotionAsync(int id, global::Shared.DTOs.Promotion.UpdatePromotionRequestDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.PutAsJsonAsync($"api/promotions/{id}", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result ?? ApiResponse<bool>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi cập nhật mã giảm giá: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> DeletePromotionAsync(int id)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _http.DeleteAsync($"api/promotions/{id}");
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            return result ?? ApiResponse<bool>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi xóa mã giảm giá: {ex.Message}");
        }
    }
}

