using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;

namespace Frontend.Services;

public interface ICustomerApiClient
{
    Task<CustomerProfileDto?> GetProfileAsync();
    Task<List<CustomerOrderHistoryItemDto>?> GetOrderHistoryAsync();
    Task<List<OrderDetailItemDto>?> GetOrderDetailsAsync(int orderId);
    Task<RankEvaluationResultDto?> EvaluateRankAsync();
    Task<global::Shared.DTOs.Common.ApiResponse<int>> SubmitFeedbackAsync(global::Shared.DTOs.Feedback.CreateFeedbackRequestDto request);
    Task<global::Shared.DTOs.Common.ApiResponse<int>> SubmitReturnRequestAsync(global::Shared.DTOs.Feedback.CreateReturnRequestDto request);
    Task<global::Shared.DTOs.Common.ApiResponse<List<global::Shared.DTOs.Feedback.EligibleReturnOrderDto>>> GetEligibleReturnOrdersAsync();
    Task<List<global::Shared.DTOs.Warehouse.BranchDto>?> GetBranchesAsync();
}
