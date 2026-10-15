using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;

namespace Frontend.Services;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ValidateVoucherResponseDto?> ValidateVoucherAsync(ValidateVoucherRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/promotions/validate", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
            }

            var errorResponse = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
            return errorResponse ?? new ValidateVoucherResponseDto { Valid = false, Message = "Mã voucher không hợp lệ" };
        }
        catch (Exception ex)
        {
            return new ValidateVoucherResponseDto { Valid = false, Message = $"Lỗi kết nối máy chủ: {ex.Message}" };
        }
    }

    public async Task<OrderSummaryDto?> CreateOrderAsync(CreateOrderRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/orders", request);
            if (response.IsSuccessStatusCode)
            {
                var apiRes = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
                if (apiRes != null && apiRes.Success)
                {
                    return new OrderSummaryDto
                    {
                        MaHoaDon = apiRes.Data,
                        NgayLapHoaDon = DateTime.Now,
                        TrangThai = "Chờ xử lý"
                    };
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<CustomerProfileDto?> GetMyProfileAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<CustomerProfileDto>>("api/customers/me/profile");
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }
}
