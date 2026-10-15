using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Common;
using Shared.DTOs.Feedback;

namespace Backend.Services;

public interface IFeedbackService
{
    Task<ApiResponse<int>> CreateFeedbackAsync(CreateFeedbackRequestDto request);
    Task<ApiResponse<IEnumerable<FeedbackDto>>> GetAllFeedbacksAsync();
    Task<ApiResponse<bool>> UpdateFeedbackStatusAsync(int id, string status, int staffId, string? notes);
    Task<ApiResponse<int>> CreateReturnRequestAsync(CreateReturnRequestDto request, int? customerId);
    Task<ApiResponse<bool>> ProcessReturnRequestAsync(int feedbackId, ProcessReturnRequestDto request, int staffId);
    Task<ApiResponse<IEnumerable<EligibleReturnOrderDto>>> GetEligibleOrdersForReturnAsync(int customerId);
}
