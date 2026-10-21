namespace Shared.DTOs.Payment
{
    public class QrPaymentRequestDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "MOMO"; // MOMO hoặc VIETQR
        public string? Note { get; set; }
    }

    public class QrPaymentResponseDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string TransferContent { get; set; } = string.Empty;
        public string QrDataUrl { get; set; } = string.Empty; // Base64 data:image/png;base64,...
        public string RawQrString { get; set; } = string.Empty;
        public bool IsPaidConfirmed { get; set; }
    }
}
