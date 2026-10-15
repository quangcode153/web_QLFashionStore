using System.Threading.Tasks;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;

namespace Frontend.Services;

public interface IOrderApiClient
{
    Task<ValidateVoucherResponseDto?> ValidateVoucherAsync(ValidateVoucherRequestDto request);
    Task<OrderSummaryDto?> CreateOrderAsync(CreateOrderRequestDto request);
    Task<CustomerProfileDto?> GetMyProfileAsync();
}
