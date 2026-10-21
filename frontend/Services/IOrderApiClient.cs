using System.Threading.Tasks;
using global::Shared.DTOs.Customer;
using global::Shared.DTOs.Order;
using global::Shared.DTOs.Payment;
using global::Shared.DTOs.Promotion;

namespace Frontend.Services;

public interface IOrderApiClient
{
    Task<ValidateVoucherResponseDto?> ValidateVoucherAsync(ValidateVoucherRequestDto request);
    Task<OrderSummaryDto?> CreateOrderAsync(CreateOrderRequestDto request);
    Task<CustomerProfileDto?> GetMyProfileAsync();
    Task<QrPaymentResponseDto?> GetOrderPaymentQrAsync(int orderId, string method = "MOMO");
    Task<bool> ConfirmPaymentPaidAsync(int orderId);
}
