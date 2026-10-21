using Shared.DTOs.Payment;

namespace Backend.Services
{
    public interface IPaymentService
    {
        QrPaymentResponseDto GenerateQrPayment(QrPaymentRequestDto request);
        Task<QrPaymentResponseDto?> GetOrderPaymentQrAsync(int orderId, string paymentMethod);
    }
}
