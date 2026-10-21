using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using global::Shared.DTOs.Catalog;
using global::Shared.DTOs.Common;
using global::Shared.DTOs.Customer;
using global::Shared.DTOs.Order;
using global::Shared.DTOs.Promotion;

namespace Frontend.Services;

public class PosApiClient : IPosApiClient
{
    private readonly HttpClient _http;

    public PosApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductListItemDto>?> GetFastSaleProductsAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>("api/products?pageSize=20");
            return res?.Data?.Items?.ToList() ?? new List<ProductListItemDto>();
        }
        catch
        {
            return new List<ProductListItemDto>();
        }
    }

    public async Task<ProductVariantDto?> SearchSkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;

        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<ProductVariantDto>>($"api/products/variant-by-sku?sku={Uri.EscapeDataString(sku)}");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<CustomerProfileDto?> LookupCustomerByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<CustomerProfileDto>>($"api/customers/by-phone?phone={Uri.EscapeDataString(phone)}");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ValidateVoucherResponseDto?> ValidateVoucherAsync(string code, decimal total)
    {
        try
        {
            var req = new ValidateVoucherRequestDto { MaGiamGia = code, TongTienDonHang = total };
            var response = await _http.PostAsJsonAsync("api/promotions/validate", req);
            if (!response.IsSuccessStatusCode) return null;
            var res = await response.Content.ReadFromJsonAsync<ApiResponse<ValidateVoucherResponseDto>>();
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<OrderSummaryDto?> CreatePosOrderAsync(CreateOrderRequestDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/orders", request);
            if (!response.IsSuccessStatusCode) return null;
            var res = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
            if (res != null && res.Success)
            {
                var targetStatus = !string.IsNullOrWhiteSpace(request.TrangThai) ? request.TrangThai : "Đã hoàn thành";
                try
                {
                    await _http.PutAsync($"api/orders/{res.Data}/status?status={Uri.EscapeDataString(targetStatus)}", null);
                }
                catch { }

                return new OrderSummaryDto
                {
                    MaHoaDon = res.Data,
                    NgayLapHoaDon = DateTime.Now,
                    TrangThai = targetStatus
                };
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ReceiptPrintDto?> GetReceiptPrintAsync(int orderId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<ReceiptPrintDto>>($"api/orders/{orderId}/print");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<OrderReturnLookupDto?> GetOrderForReturnLookupAsync(int orderId)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<OrderReturnLookupDto>>($"api/orders/{orderId}/return-lookup");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<int>?> ProcessReturnAsync(int orderId, ReturnOrderRequestDto request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"api/orders/{orderId}/return", request);
            var res = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
            return res;
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi kết nối khi gửi yêu cầu trả hàng: {ex.Message}");
        }
    }

    public async Task<List<ProductVariantDto>?> GetProductVariantsAsync(int productId)
    {
        if (productId <= 0) return new List<ProductVariantDto>();
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
}
