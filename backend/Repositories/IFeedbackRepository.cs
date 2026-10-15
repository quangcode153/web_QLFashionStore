using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Feedback;

namespace Backend.Repositories;

public interface IFeedbackRepository
{
    Task<int> CreateFeedbackAsync(CreateFeedbackRequestDto request);
    Task<IEnumerable<FeedbackDto>> GetAllFeedbacksAsync();
    Task<bool> UpdateFeedbackStatusAsync(int id, string status, int staffId, string? notes);
    Task<FeedbackDto?> GetFeedbackByIdAsync(int id);
    Task<int> CreateReturnRequestAsync(CreateReturnRequestDto request, int? customerId);
    Task<bool> ProcessReturnApprovalAsync(int feedbackId, int staffId, string? notes);
    Task<bool> ProcessReturnReceiptAsync(int feedbackId, int staffId, string? notes);
    Task<bool> ProcessReturnRejectionAsync(int feedbackId, int staffId, string? notes);
    Task<IEnumerable<EligibleReturnOrderDto>> GetEligibleOrdersForReturnAsync(int customerId);
}

