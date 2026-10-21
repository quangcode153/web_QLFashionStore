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
        public async Task<IActionResult> ConfirmPaid(int orderId, [FromQuery] string? transactionRef)
        {
            var ownership = await _orderService.GetOrderOwnershipAsync(orderId);
            if (!ownership.Exists)
            {
                return NotFound(ApiResponse<bool>.Fail($"Không tìm thấy đơn hàng #{orderId}"));
            }

            // Ghi nhận thanh toán: Đơn hàng vẫn bảo toàn trạng thái chuẩn "Chờ xử lý" trong CSDL
            // để Điều phối viên và Thủ kho tiếp nhận duyệt, đóng gói và chuyển sang "Đang giao hàng"
            var currentAddress = await _orderRepository.GetOrderShippingAddressAsync(orderId) ?? "";
            if (!currentAddress.Contains("ĐÃ THANH TOÁN") && !currentAddress.Contains("ĐÃ CHUYỂN KHOẢN"))
            {
                string updatedAddress;
                if (currentAddress.Contains("[PTTT: COD"))
                {
                    updatedAddress = currentAddress.Replace("[PTTT: COD - THU TIỀN KHI GIAO HÀNG]", "[PTTT: ĐÃ XÁC NHẬN CHUYỂN KHOẢN QR (THU HỘ 0Đ)]");
                }
                else
                {
                    updatedAddress = $"[PTTT: ĐÃ XÁC NHẬN CHUYỂN KHOẢN QR (THU HỘ 0Đ)] {currentAddress}".Trim();
                }
                await _orderRepository.UpdateOrderShippingAddressAsync(orderId, updatedAddress);
            }

            return Ok(ApiResponse<bool>.Ok(true, $"Đã ghi nhận chuyển khoản cho đơn hàng #{orderId} thành công! Đơn hàng đang được bộ phận giao vận chuẩn bị."));
        }
    }
}
