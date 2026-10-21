using System.Text;
using Backend.Repositories;
using QRCoder;
using Shared.DTOs.Payment;

namespace Backend.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IOrderRepository _orderRepository;

        // Cấu hình thanh toán đọc từ môi trường hoặc fallback
        private readonly string _momoPhone;
        private readonly string _momoAccountName;
        private readonly string _storeBankCode;
        private readonly string _storeBankAccount;
        private readonly string _storeAccountName;

        public PaymentService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;

            _momoPhone = Environment.GetEnvironmentVariable("MOMO_PHONE") ?? "0869161050";
            _momoAccountName = Environment.GetEnvironmentVariable("MOMO_ACCOUNT_NAME") ?? "QUANG";
            _storeBankCode = Environment.GetEnvironmentVariable("STORE_BANK_CODE") ?? "MB";
            _storeBankAccount = Environment.GetEnvironmentVariable("STORE_BANK_ACCOUNT") ?? "0869161050";
            _storeAccountName = Environment.GetEnvironmentVariable("STORE_ACCOUNT_NAME") ?? "QF NOIR FASHION";
        }

        public QrPaymentResponseDto GenerateQrPayment(QrPaymentRequestDto request)
        {
            var isMoMo = string.Equals(request.PaymentMethod?.Trim(), "MOMO", StringComparison.OrdinalIgnoreCase);

            string rawQr;
            string bankName;
            string accountNumber;
            string accountName;
            string transferContent = $"DH{request.OrderId}";

            if (isMoMo)
            {
                // Chuẩn chuỗi P2P MoMo Transfer
                accountNumber = _momoPhone;
                accountName = _momoAccountName;
                bankName = "Ví Điện Tử MoMo";
                rawQr = $"2|99|{_momoPhone}|{_momoAccountName}||0|0|{request.Amount:0}|{transferContent}|transfer_myqr";
            }
            else
            {
                // Chuẩn VietQR (NAPAS EMVCo)
                accountNumber = _storeBankAccount;
                accountName = _storeAccountName;
                bankName = $"Ngân hàng {_storeBankCode.ToUpper()} (MBBank)";
                rawQr = GenerateVietQrString(_storeBankCode, _storeBankAccount, request.Amount.ToString("0"), transferContent);
            }

            // Sinh ảnh PNG Base64 Data URI siêu tốc bằng QRCoder
            string qrDataUrl = RenderQrDataUrl(rawQr);

            return new QrPaymentResponseDto
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = isMoMo ? "MOMO" : "VIETQR",
                AccountNumber = accountNumber,
                AccountName = accountName,
                BankName = bankName,
                TransferContent = transferContent,
                QrDataUrl = qrDataUrl,
                RawQrString = rawQr
            };
        }

        public async Task<QrPaymentResponseDto?> GetOrderPaymentQrAsync(int orderId, string paymentMethod)
        {
            var receipt = await _orderRepository.GetReceiptDetailsAsync(orderId);
            if (receipt == null) return null;

            var res = GenerateQrPayment(new QrPaymentRequestDto
            {
                OrderId = receipt.MaHoaDon,
                Amount = receipt.TongThanhToan,
                PaymentMethod = paymentMethod
            });

            var address = await _orderRepository.GetOrderShippingAddressAsync(orderId);
            if (!string.IsNullOrEmpty(address) && (address.Contains("ĐÃ THANH TOÁN") || address.Contains("ĐÃ CHUYỂN KHOẢN")))
            {
                res.IsPaidConfirmed = true;
            }

            return res;
        }

        private static string RenderQrDataUrl(string text)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrBytes = qrCode.GetGraphic(20);
            return $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";
        }

        /// <summary>
        /// Tạo VietQR code theo chuẩn EMV QR Code (ISO/IEC 13239 CRC16)
        /// </summary>
        private static string GenerateVietQrString(string bankCode, string accountNumber, string amount, string message)
        {
            string bankBin = GetBankBin(bankCode);

            // Path 12: Bank BIN + Account
            var path12 = $"00{bankBin.Length:D2}{bankBin}01{accountNumber.Length:D2}{accountNumber}";

            // Path 11: Merchant Account Information
            var path11 = $"0010A00000072701{path12.Length:D2}{path12}0208QRIBFTTA";

            // Field 38:
            var path1 = $"38{path11.Length:D2}{path11}";

            // Field 62: Additional Data (Message)
            var path21 = $"08{message.Length:D2}{message}";

            // Field 53 (704=VND), 54 (Amount), 58 (VN), 62 (Additional Data)
            var path2 = $"530370454{amount.Length:D2}{amount}5802VN62{path21.Length:D2}{path21}";

            // Root payload
            var raw = $"000201010212{path1}{path2}6304";

            // Checksum CRC-16
            string checksum = GenerateCrc16(raw);
            return $"{raw}{checksum}";
        }

        private static string GenerateCrc16(string text)
        {
            ushort crc = 0xFFFF;
            ushort polynomial = 0x1021;
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ polynomial);
                    else
                        crc = (ushort)(crc << 1);
                }
            }

            return crc.ToString("X4");
        }

        private static string GetBankBin(string bankCode)
        {
            return bankCode.ToUpperInvariant() switch
            {
                "MB" or "MBBANK" => "970422",
                "VCB" or "VIETCOMBANK" => "970436",
                "ICB" or "VIETINBANK" => "970415",
                "BIDV" => "970418",
                "TCB" or "TECHCOMBANK" => "970407",
                "ACB" => "970416",
                "VPB" or "VPBANK" => "970432",
                "TPB" or "TPBANK" => "970423",
                "STB" or "SACOMBANK" => "970403",
                "OCB" => "970448",
                "TIMO" => "963388",
                "CAKE" => "546034",
                _ => "970422" // Mặc định MBBank
            };
        }
    }
}
