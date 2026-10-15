using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;

namespace Frontend.Services;

public class CustomerApiClient : ICustomerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime? _jsRuntime;

    public CustomerApiClient(HttpClient httpClient, IJSRuntime? jsRuntime = null)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    private async Task AttachAuthHeaderAsync()
    {
        if (_jsRuntime == null) return;
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch { }
    }

    public async Task<CustomerProfileDto?> GetProfileAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<CustomerProfileDto>>("api/customers/me/profile");
            if (res?.Data != null) return res.Data;
        }
        catch { }

        return null;
    }

    public async Task<List<CustomerOrderHistoryItemDto>?> GetOrderHistoryAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<CustomerOrderHistoryItemDto>>>("api/customers/me/orders");
            if (res?.Data != null) return res.Data;
        }
        catch { }

        return new List<CustomerOrderHistoryItemDto>();
    }

    public async Task<List<OrderDetailItemDto>?> GetOrderDetailsAsync(int orderId)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _httpClient.GetFromJsonAsync<ApiResponse<List<OrderDetailItemDto>>>($"api/orders/{orderId}/details");
            return res?.Data ?? new List<OrderDetailItemDto>();
        }
        catch
        {
            try
            {
                var list = await _httpClient.GetFromJsonAsync<List<OrderDetailItemDto>>($"api/orders/{orderId}/details");
                return list ?? new List<OrderDetailItemDto>();
            }
            catch
            {
                return new List<OrderDetailItemDto>();
            }
        }
    }

    public async Task<RankEvaluationResultDto?> EvaluateRankAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/customers/eval-ranks", new { });
            if (response.IsSuccessStatusCode)
            {
                var apiRes = await response.Content.ReadFromJsonAsync<ApiResponse<List<RankEvaluationResultDto>>>();
                return apiRes?.Data?.FirstOrDefault();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<global::Shared.DTOs.Common.ApiResponse<int>> SubmitFeedbackAsync(global::Shared.DTOs.Feedback.CreateFeedbackRequestDto request)
    {
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/feedback", request);
            var apiRes = await res.Content.ReadFromJsonAsync<global::Shared.DTOs.Common.ApiResponse<int>>();
            return apiRes ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi gửi phản hồi: {ex.Message}");
        }
    }

    public async Task<global::Shared.DTOs.Common.ApiResponse<int>> SubmitReturnRequestAsync(global::Shared.DTOs.Feedback.CreateReturnRequestDto request)
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _httpClient.PostAsJsonAsync("api/feedback/return-request", request);
            var apiRes = await res.Content.ReadFromJsonAsync<global::Shared.DTOs.Common.ApiResponse<int>>();
            return apiRes ?? ApiResponse<int>.Fail("Không nhận được phản hồi từ máy chủ.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi gửi yêu cầu hoàn trả: {ex.Message}");
        }
    }

    public async Task<global::Shared.DTOs.Common.ApiResponse<List<global::Shared.DTOs.Feedback.EligibleReturnOrderDto>>> GetEligibleReturnOrdersAsync()
    {
        await AttachAuthHeaderAsync();
        try
        {
            var res = await _httpClient.GetFromJsonAsync<global::Shared.DTOs.Common.ApiResponse<List<global::Shared.DTOs.Feedback.EligibleReturnOrderDto>>>("api/feedback/eligible-return-orders");
            return res ?? ApiResponse<List<global::Shared.DTOs.Feedback.EligibleReturnOrderDto>>.Fail("Không thể lấy danh sách đơn hoàn trả.");
        }
        catch (Exception ex)
        {
            return ApiResponse<List<global::Shared.DTOs.Feedback.EligibleReturnOrderDto>>.Fail($"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<List<global::Shared.DTOs.Warehouse.BranchDto>?> GetBranchesAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<global::Shared.DTOs.Common.ApiResponse<List<global::Shared.DTOs.Warehouse.BranchDto>>>("api/branches");
            return res?.Data ?? new List<global::Shared.DTOs.Warehouse.BranchDto>();
        }
        catch
        {
            return new List<global::Shared.DTOs.Warehouse.BranchDto>();
        }
    }
}

