using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using Backend.WebSockets;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.UnitTests.Orders;

/// <summary>
/// Unit Test kiểm thử Phân hệ Kho Hàng Lỗi và Nghiệp vụ Đổi trả đơn hàng tại quầy POS (Bước 3).
/// Tuân thủ nghiêm ngặt Tiêu chuẩn Kiểm thử Đa Vòng (Rule 6 - Multi-Round Testing Standard):
/// - VÒNG 1: Ma trận Tham số & Giá trị Biên (orderId, staffId, variantId, quantity <= 0, reason null/rỗng)
/// - VÒNG 2: Chuỗi Xử lý Kho Hàng Lỗi & Ngoại lệ (so_luong_loi, TRA_HANG_LOI, SignalR Fail-safe, Chống trả thừa)
/// - VÒNG 3: Ma trận Trạng thái Tra cứu Hóa đơn Đổi trả (Đã hoàn thành, Chờ xử lý, Đang giao hàng, Đã hủy)
/// </summary>
public class DefectiveReturnInventoryTests
{
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly Mock<IHubContext<InventoryHub>> _mockInventoryHub;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly OrderService _service;

    public DefectiveReturnInventoryTests()
    {
        _mockRepo = new Mock<IOrderRepository>();
        _mockInventoryHub = new Mock<IHubContext<InventoryHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        _mockInventoryHub.Setup(h => h.Clients).Returns(_mockClients.Object);

        _service = new OrderService(_mockRepo.Object, inventoryHub: _mockInventoryHub.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory(DisplayName = "VÒNG 1: Chặn đổi trả khi mã đơn hàng hoặc mã nhân viên <= 0")]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-99, 1)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public async Task ProcessReturnAsync_InvalidOrderOrStaffId_ReturnsFail(int orderId, int staffId)
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị rách đường may"
        };

        // Act
        var result = await _service.ProcessReturnAsync(orderId, request, staffId);

        // Assert
        result.Success.Should().BeFalse("Mã đơn hàng hoặc mã nhân viên <= 0 phải bị chặn");
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "VÒNG 1: Chặn đổi trả khi request null")]
    public async Task ProcessReturnAsync_NullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.ProcessReturnAsync(10, null!, 1);

        // Assert
        result.Success.Should().BeFalse("Request null phải bị từ chối");
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đổi trả khi mã biến thể hoặc số lượng trả <= 0")]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(5, 0)]
    [InlineData(5, -2)]
    [InlineData(-10, -5)]
    public async Task ProcessReturnAsync_InvalidVariantOrQuantity_ReturnsFail(int variantId, int quantity)
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = variantId,
            SoLuongTra = quantity,
            LyDoTra = "Lỗi bung chỉ"
        };

        // Act
        var result = await _service.ProcessReturnAsync(10, request, 1);

        // Assert
        result.Success.Should().BeFalse("Mã biến thể và số lượng trả <= 0 phải bị chặn");
        result.Message.Should().Contain("phải lớn hơn 0");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đổi trả khi lý do trả hàng null hoặc rỗng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessReturnAsync_EmptyReason_ReturnsFail(string? emptyReason)
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = emptyReason
        };

        // Act
        var result = await _service.ProcessReturnAsync(10, request, 1);

        // Assert
        result.Success.Should().BeFalse("Lý do trả hàng rỗng phải bị chặn");
        result.Message.Should().Contain("lý do");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn tra cứu hóa đơn đổi trả khi mã hóa đơn <= 0")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task GetOrderForReturnLookupAsync_InvalidOrderId_ReturnsFail(int invalidOrderId)
    {
        // Act
        var result = await _service.GetOrderForReturnLookupAsync(invalidOrderId);

        // Assert
        result.Success.Should().BeFalse("Mã hóa đơn <= 0 phải bị chặn khi tra cứu");
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.GetOrderForReturnLookupAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region VÒNG 2: CHUỖI XỬ LÝ KHO HÀNG LỖI & NGOẠI LỆ (DEFECTIVE INVENTORY & STATE PIPELINE)

    [Fact(DisplayName = "VÒNG 2: Tiếp nhận trả hàng hợp lệ, gọi Repository ghi nhận và phát SignalR")]
    public async Task ProcessReturnAsync_ValidRequest_ReturnsSuccessAndBroadcasts()
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 4,
            SoLuongTra = 2,
            LyDoTra = "Hàng bị ố màu do lỗi sản xuất"
        };

        var returnDto = new ReturnOrderResultDto
        {
            MaDonTra = 101,
            MaHoaDon = 15,
            MaBienThe = 4,
            SoLuongTra = 2,
            LyDoTra = request.LyDoTra,
            TrangThai = "Đã xử lý"
        };

        _mockRepo.Setup(r => r.ProcessOrderReturnAsync(15, 4, 2, request.LyDoTra, 2))
            .ReturnsAsync((true, "Đổi trả hàng thành công và nhập vào kho hàng lỗi.", returnDto));

        // Act
        var result = await _service.ProcessReturnAsync(15, request, 2);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MaDonTra.Should().Be(101);
        result.Data.SoLuongTra.Should().Be(2);
        result.Data.TrangThai.Should().Be("Đã xử lý");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(15, 4, 2, request.LyDoTra, 2), Times.Once);
        _mockClientProxy.Verify(p => p.SendCoreAsync("InventoryUpdated", It.Is<object[]>(o => (string)o[2] == "DOI_TRA"), default), Times.Once);
    }

    [Fact(DisplayName = "VÒNG 2: Xử lý thất bại từ Repository (VD: số lượng trả vượt quá số lượng mua) trả về Fail đúng mô tả")]
    public async Task ProcessReturnAsync_RepoReturnsFailure_ReturnsFail()
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 4,
            SoLuongTra = 5,
            LyDoTra = "Khách muốn trả 5 cái nhưng chỉ mua 2 cái"
        };

        _mockRepo.Setup(r => r.ProcessOrderReturnAsync(15, 4, 5, request.LyDoTra, 2))
            .ReturnsAsync((false, "Số lượng trả (5) vượt quá số lượng còn lại có thể trả (2) trong đơn hàng.", null));

        // Act
        var result = await _service.ProcessReturnAsync(15, request, 2);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("vượt quá số lượng");
    }

    [Fact(DisplayName = "VÒNG 2: An toàn ngoại lệ khi Repository ném Exception không làm crash hệ thống")]
    public async Task ProcessReturnAsync_RepoThrows_ReturnsFailGracefully()
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 4,
            SoLuongTra = 1,
            LyDoTra = "Hàng lỗi may"
        };

        _mockRepo.Setup(r => r.ProcessOrderReturnAsync(15, 4, 1, request.LyDoTra, 2))
            .ThrowsAsync(new InvalidOperationException("Mất kết nối máy chủ cơ sở dữ liệu."));

        // Act
        var result = await _service.ProcessReturnAsync(15, request, 2);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi xử lý đổi trả hàng");
        result.Message.Should().Contain("Mất kết nối");
    }

    #endregion

    #region VÒNG 3: MA TRẬN TRẠNG THÁI TRA CỨU HÓA ĐƠN ĐỔI TRẢ (RETURN LOOKUP & STATUS MATRIX)

    [Fact(DisplayName = "VÒNG 3: Tra cứu hóa đơn không tồn tại trả về lỗi rõ ràng")]
    public async Task GetOrderForReturnLookupAsync_OrderNotFound_ReturnsFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetOrderForReturnLookupAsync(9999))
            .ReturnsAsync((OrderReturnLookupDto?)null);

        // Act
        var result = await _service.GetOrderForReturnLookupAsync(9999);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "VÒNG 3: Tra cứu hóa đơn 'Đã hoàn thành' trả về đầy đủ điều kiện đổi trả và số lượng còn lại")]
    public async Task GetOrderForReturnLookupAsync_CompletedOrder_ReturnsEligibleWithItems()
    {
        // Arrange
        var lookupDto = new OrderReturnLookupDto
        {
            MaHoaDon = 100,
            NgayLapHoaDon = DateTime.Now.AddDays(-2),
            TenKhachHang = "Nguyễn Văn A",
            SdtKhachHang = "0987654321",
            ThuNgan = "Trần Thu Ngân",
            TienHang = 500000,
            TienGiamGia = 50000,
            TongThanhToan = 450000,
            TrangThai = "Đã hoàn thành",
            Items = new List<OrderReturnLookupItemDto>
            {
                new()
                {
                    MaBienThe = 10,
                    TenSanPham = "Áo Thun Premium Unisex",
                    MauSac = "Đen",
                    KichCo = "L",
                    DonGia = 250000,
                    SoLuongMua = 2,
                    SoLuongDaTra = 1
                }
            }
        };

        _mockRepo.Setup(r => r.GetOrderForReturnLookupAsync(100))
            .ReturnsAsync(lookupDto);

        // Act
        var result = await _service.GetOrderForReturnLookupAsync(100);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsEligibleForReturn.Should().BeTrue();
        result.Data.IneligibilityReason.Should().BeNull();
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].SoLuongConLai.Should().Be(1, "Mua 2 trả 1 thì còn lại 1 có thể trả");
    }

    [Theory(DisplayName = "VÒNG 3: Tra cứu hóa đơn chưa hoàn thành bị đánh dấu không đủ điều kiện đổi trả")]
    [InlineData("Chờ xử lý")]
    [InlineData("Đang giao hàng")]
    [InlineData("Đã hủy")]
    public async Task GetOrderForReturnLookupAsync_IneligibleStatus_MarksIneligibleWithReason(string uncompletedStatus)
    {
        // Arrange
        var lookupDto = new OrderReturnLookupDto
        {
            MaHoaDon = 200,
            NgayLapHoaDon = DateTime.Now.AddDays(-1),
            TenKhachHang = "Lê Thị B",
            ThuNgan = "Trần Thu Ngân",
            TrangThai = uncompletedStatus,
            IneligibilityReason = $"Chỉ hóa đơn đã hoàn thành mới có thể đổi trả. Trạng thái hiện tại: {uncompletedStatus}."
        };

        _mockRepo.Setup(r => r.GetOrderForReturnLookupAsync(200))
            .ReturnsAsync(lookupDto);

        // Act
        var result = await _service.GetOrderForReturnLookupAsync(200);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsEligibleForReturn.Should().BeFalse();
        result.Data.IneligibilityReason.Should().Contain(uncompletedStatus);
    }

    #endregion
}
