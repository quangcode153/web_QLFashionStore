using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Order;
using Shared.DTOs.Payment;
using Xunit;

namespace Backend.Tests.UnitTests
{
    public class PaymentServiceTests
    {
        private readonly Mock<IOrderRepository> _mockOrderRepo;
        private readonly PaymentService _paymentService;

        public PaymentServiceTests()
        {
            _mockOrderRepo = new Mock<IOrderRepository>();
            _paymentService = new PaymentService(_mockOrderRepo.Object);
        }

        [Theory]
        [InlineData(1001, 50000, "MOMO")]
        [InlineData(1002, 250000, "MOMO")]
        [InlineData(1003, 1200000, "MOMO")]
        public void GenerateQrPayment_MoMoMethod_ShouldReturnValidMoMoPayloadAndPngBase64(int orderId, decimal amount, string method)
        {
            // Arrange
            var req = new QrPaymentRequestDto
            {
                OrderId = orderId,
                Amount = amount,
                PaymentMethod = method
            };

            // Act
            var res = _paymentService.GenerateQrPayment(req);

            // Assert
            res.Should().NotBeNull();
            res.OrderId.Should().Be(orderId);
            res.Amount.Should().Be(amount);
            res.PaymentMethod.Should().Be("MOMO");
            res.AccountNumber.Should().Be("0869161050");
            res.TransferContent.Should().Be($"DH{orderId}");
            res.RawQrString.Should().StartWith("2|99|0869161050");
            res.RawQrString.Should().Contain(amount.ToString("0"));
            res.QrDataUrl.Should().StartWith("data:image/png;base64,");
            res.QrDataUrl.Length.Should().BeGreaterThan(100);
        }

        [Theory]
        [InlineData(2001, 75000, "VIETQR")]
        [InlineData(2002, 350000, "VIETQR")]
        [InlineData(2003, 2000000, "VIETQR")]
        public void GenerateQrPayment_VietQrMethod_ShouldReturnValidEmvCoPayloadAndPngBase64(int orderId, decimal amount, string method)
        {
            // Arrange
            var req = new QrPaymentRequestDto
            {
                OrderId = orderId,
                Amount = amount,
                PaymentMethod = method
            };

            // Act
            var res = _paymentService.GenerateQrPayment(req);

            // Assert
            res.Should().NotBeNull();
            res.OrderId.Should().Be(orderId);
            res.Amount.Should().Be(amount);
            res.PaymentMethod.Should().Be("VIETQR");
            res.BankName.Should().Contain("MB");
            res.RawQrString.Should().StartWith("000201"); // Chuẩn EMVCo
            res.RawQrString.Should().Contain("5303704"); // Tiền tệ VND 704
            res.RawQrString.Should().Contain("5802VN");  // Quốc gia VN
            res.QrDataUrl.Should().StartWith("data:image/png;base64,");
            res.QrDataUrl.Length.Should().BeGreaterThan(100);
        }

        [Fact]
        public async Task GetOrderPaymentQrAsync_ExistingOrder_ShouldFetchAndGenerate()
        {
            // Arrange
            int orderId = 8888;
            _mockOrderRepo.Setup(r => r.GetReceiptDetailsAsync(orderId))
                .ReturnsAsync(new ReceiptPrintDto
                {
                    MaHoaDon = orderId,
                    TongThanhToan = 499000,
                    TrangThai = "Chờ xử lý"
                });

            // Act
            var res = await _paymentService.GetOrderPaymentQrAsync(orderId, "MOMO");

            // Assert
            res.Should().NotBeNull();
            res!.OrderId.Should().Be(orderId);
            res.Amount.Should().Be(499000);
            res.AccountNumber.Should().Be("0869161050");
        }
    }
}
