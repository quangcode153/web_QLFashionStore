using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Common;
using Shared.DTOs.Payment;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly Backend.Repositories.IOrderRepository _orderRepository;

        public PaymentController(
            IPaymentService paymentService, 
            IOrderService orderService,
            Backend.Repositories.IOrderRepository orderRepository)
        {
            _paymentService = paymentService;
            _orderService = orderService;
            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Tạo mã QR thanh toán động theo số tiền và mã đơn (Hỗ trợ MoMo & VietQR)
        /// </summary>
        [HttpPost("qr-generate")]
        [AllowAnonymous]
        public IActionResult GenerateQr([FromBody] QrPaymentRequestDto request)
        {
            if (request.Amount <= 0)
            {
                return BadRequest(ApiResponse<QrPaymentResponseDto>.Fail("Số tiền thanh toán phải lớn hơn 0"));
            }

            var result = _paymentService.GenerateQrPayment(request);
            return Ok(ApiResponse<QrPaymentResponseDto>.Ok(result, "Tạo mã QR thành công"));
        }

        /// <summary>
        /// Lấy mã QR thanh toán theo mã đơn hàng đã có trong hệ thống
        /// </summary>
        [HttpGet("qr-order/{orderId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOrderQr(int orderId, [FromQuery] string method = "MOMO")
        {
            var result = await _paymentService.GetOrderPaymentQrAsync(orderId, method);
            if (result == null)
            {
                return NotFound(ApiResponse<QrPaymentResponseDto>.Fail($"Không tìm thấy đơn hàng #{orderId}"));
            }

            return Ok(ApiResponse<QrPaymentResponseDto>.Ok(result, "Lấy mã QR đơn hàng thành công"));
        }

        /// <summary>
        /// Khách hàng hoặc thu ngân xác nhận đã chuyển khoản thành công
        /// </summary>
        [HttpPost("confirm-paid/{orderId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmPaid(int orderId, [FromQuery] string? method = "MOMO", [FromQuery] string? transactionRef = null)
        {
            var ownership = await _orderService.GetOrderOwnershipAsync(orderId);
            if (!ownership.Exists)
            {
                return NotFound(ApiResponse<bool>.Fail($"Không tìm thấy đơn hàng #{orderId}"));
            }

            // Ghi nhận thanh toán: Cập nhật tag PTTT sang MoMo hoặc VietQR tương ứng
            string normalizedMethod = (method ?? "MOMO").ToUpperInvariant();
            string newTag;
            string methodLabel;

            if (normalizedMethod == "VIETQR" || normalizedMethod == "CHUYEN_KHOAN")
            {
                newTag = "[PTTT: VIETQR MBBANK - ĐÃ CHUYỂN KHOẢN (THU HỘ 0Đ)]";
                methodLabel = "VietQR MBBank";
            }
            else
            {
                newTag = "[PTTT: VÍ MOMO - ĐÃ THANH TOÁN QR (THU HỘ 0Đ)]";
                methodLabel = "Ví MoMo";
            }

            var currentAddress = await _orderRepository.GetOrderShippingAddressAsync(orderId) ?? "";
            string updatedAddress;

            if (currentAddress.Contains("[PTTT:"))
            {
                var closeBracket = currentAddress.IndexOf(']');
                if (closeBracket >= 0)
                {
                    var remaining = currentAddress.Substring(closeBracket + 1).Trim();
                    updatedAddress = $"{newTag} {remaining}".Trim();
                }
                else
                {
                    updatedAddress = $"{newTag} {currentAddress}".Trim();
                }
            }
            else
            {
                updatedAddress = $"{newTag} {currentAddress}".Trim();
            }

            await _orderRepository.UpdateOrderShippingAddressAsync(orderId, updatedAddress);

            return Ok(ApiResponse<bool>.Ok(true, $"Đã ghi nhận thanh toán {methodLabel} cho đơn hàng #{orderId} thành công! Đơn hàng đang được bộ phận giao vận chuẩn bị."));
        }
    }
}
