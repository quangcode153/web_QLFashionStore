using System;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.UnitTests.Orders;

/// <summary>
/// Unit Test kiểm thử độc lập OrderService (tập trung vào nghiệp vụ Đổi trả hàng và In hóa đơn POS K80)
/// </summary>
public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockRepo;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _mockRepo = new Mock<IOrderRepository>();
        _service = new OrderService(_mockRepo.Object);
    }

    #region TIẾP NHẬN ĐỔI TRẢ HÀNG (RETURN ORDER) TESTS

    [Fact(DisplayName = "UNIT-ORD-RET-01: Đổi trả hàng hợp lệ thành công và gọi đúng Repository")]
    public async Task ProcessReturnAsync_WithValidRequest_ReturnsSuccessAndData()
    {
        // Arrange
        var request = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 2,
            LyDoTra = "Hàng bị lỗi đường chỉ may"
        };

        var expectedResult = new ReturnOrderResultDto
        {
            MaDonTra = 1,
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 2,
            LyDoTra = "Hàng bị lỗi đường chỉ may",
            TrangThai = "Đã xử lý"
        };

        _mockRepo.Setup(r => r.ProcessOrderReturnAsync(10, 1, 2, "Hàng bị lỗi đường chỉ may", 5))
            .ReturnsAsync((true, "Đổi trả hàng thành công", expectedResult));

        // Act
        var result = await _service.ProcessReturnAsync(10, request, 5);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MaDonTra.Should().Be(1);
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(10, 1, 2, "Hàng bị lỗi đường chỉ may", 5), Times.Once);
    }

    [Fact(DisplayName = "UNIT-ORD-RET-02: Đổi trả hàng với request null bị chặn không gọi Repo")]
    public async Task ProcessReturnAsync_WithNullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.ProcessReturnAsync(10, null!, 5);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-ORD-RET-03: Đổi trả hàng với mã đơn hoặc nhân viên không hợp lệ (<= 0) bị chặn")]
    [InlineData(0, 5)] // orderId = 0
    [InlineData(-1, 5)] // orderId = -1
    [InlineData(10, 0)] // staffId = 0
    [InlineData(10, -2)] // staffId = -2
    public async Task ProcessReturnAsync_WithInvalidOrderOrStaffId_ReturnsFail(int orderId, int staffId)
    {
        // Arrange
        var request = new ReturnOrderRequestDto { MaBienThe = 1, SoLuongTra = 1, LyDoTra = "Đổi size" };

        // Act
        var result = await _service.ProcessReturnAsync(orderId, request, staffId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-ORD-RET-04: Đổi trả hàng với số lượng hoặc mã biến thể <= 0 bị chặn")]
    [InlineData(0, 1)] // variantId = 0
    [InlineData(-1, 1)] // variantId = -1
    [InlineData(1, 0)] // qty = 0
    [InlineData(1, -2)] // qty = -2
    public async Task ProcessReturnAsync_WithInvalidVariantOrQuantity_ReturnsFail(int variantId, int qty)
    {
        // Arrange
        var request = new ReturnOrderRequestDto { MaBienThe = variantId, SoLuongTra = qty, LyDoTra = "Đổi size" };

        // Act
        var result = await _service.ProcessReturnAsync(10, request, 5);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("lớn hơn 0");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Theory(DisplayName = "UNIT-ORD-RET-05: Đổi trả hàng không nhập lý do bị chặn")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ProcessReturnAsync_WithEmptyReason_ReturnsFail(string? reason)
    {
        // Arrange
        var request = new ReturnOrderRequestDto { MaBienThe = 1, SoLuongTra = 1, LyDoTra = reason };

        // Act
        var result = await _service.ProcessReturnAsync(10, request, 5);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("lý do");
        _mockRepo.Verify(r => r.ProcessOrderReturnAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region IN HÓA ĐƠN K80 (PRINT RECEIPT) TESTS

    [Fact(DisplayName = "UNIT-ORD-PRN-01: In hóa đơn với orderId <= 0 bị chặn ngay")]
    public async Task GetReceiptForPrintAsync_WithInvalidOrderId_ReturnsFail()
    {
        // Act
        var result = await _service.GetReceiptForPrintAsync(-1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.GetReceiptDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-ORD-PRN-02: In hóa đơn khi Repository ném ngoại lệ được bắt an toàn")]
    public async Task GetReceiptForPrintAsync_WhenRepoThrows_ReturnsSafeFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetReceiptDetailsAsync(10))
            .ThrowsAsync(new TimeoutException("Database Timeout"));

        // Act
        var result = await _service.GetReceiptForPrintAsync(10);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lỗi khi lấy thông tin in hóa đơn");
    }

    #endregion

    #region TẠO ĐƠN HÀNG (CREATE ORDER) TESTS

    [Fact(DisplayName = "UNIT-ORD-CRT-01: Tạo đơn hàng hợp lệ gọi đúng Repo và trả về mã hóa đơn")]
    public async Task CreateOrderAsync_ValidRequest_CallsRepoAndReturnsSuccess()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            MaNhanVien = 2,
            MaGiamGia = "SUMMER10",
            Items = new System.Collections.Generic.List<CreateOrderItemDto>
            {
                new() { MaBienThe = 10, SoLuong = 2 },
                new() { MaBienThe = 11, SoLuong = 1 }
            }
        };

        _mockRepo.Setup(r => r.CreateOrderAsync(1, 2, "SUMMER10"))
            .ReturnsAsync(101);
        _mockRepo.Setup(r => r.AddOrderItemAsync(101, It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(101);
        _mockRepo.Verify(r => r.CreateOrderAsync(1, 2, "SUMMER10"), Times.Once);
        _mockRepo.Verify(r => r.AddOrderItemAsync(101, 10, 2), Times.Once);
        _mockRepo.Verify(r => r.AddOrderItemAsync(101, 11, 1), Times.Once);
    }

    [Fact(DisplayName = "UNIT-ORD-CRT-02: Tạo đơn hàng với request null bị từ chối ngay")]
    public async Task CreateOrderAsync_NullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.CreateOrderAsync(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.CreateOrderAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-ORD-CRT-03: Tạo đơn hàng với danh sách items rỗng hoặc null bị từ chối")]
    public async Task CreateOrderAsync_EmptyItems_ReturnsFail()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            Items = new System.Collections.Generic.List<CreateOrderItemDto>()
        };

        // Act
        var result = await _service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ít nhất một sản phẩm");
    }

    [Theory(DisplayName = "UNIT-ORD-CRT-04: Tạo đơn hàng với biến thể <= 0 hoặc số lượng <= 0 bị từ chối")]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -2)]
    public async Task CreateOrderAsync_InvalidVariantOrQuantity_ReturnsFail(int variantId, int qty)
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            Items = new System.Collections.Generic.List<CreateOrderItemDto>
            {
                new() { MaBienThe = variantId, SoLuong = qty }
            }
        };

        // Act
        var result = await _service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("lớn hơn 0");
        _mockRepo.Verify(r => r.CreateOrderAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-ORD-CRT-05: Khi Repo ném ngoại lệ được bắt an toàn không crash hệ thống")]
    public async Task CreateOrderAsync_RepoThrows_ReturnsSafeFail()
    {
        // Arrange
        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            Items = new System.Collections.Generic.List<CreateOrderItemDto>
            {
                new() { MaBienThe = 1, SoLuong = 1 }
            }
        };

        _mockRepo.Setup(r => r.CreateOrderAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Hết hàng tồn kho"));

        // Act
        var result = await _service.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Hết hàng tồn kho");
    }

    [Fact(DisplayName = "UNIT-ORD-CRT-06: Khi có biến thể trong đơn đã bị ẩn/ngừng bán thì từ chối tạo đơn với cảnh báo tạm thời không cung cấp")]
    public async Task CreateOrderAsync_WhenVariantIsHidden_ReturnsFailWithUnavailableNotice()
    {
        // Arrange
        var mockCatalogRepo = new Mock<ICatalogRepository>();
        var serviceWithCatalog = new OrderService(_mockRepo.Object, catalogRepository: mockCatalogRepo.Object);

        var request = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            Items = new System.Collections.Generic.List<CreateOrderItemDto>
            {
                new() { MaBienThe = 99, SoLuong = 1 }
            }
        };

        mockCatalogRepo.Setup(c => c.CheckVariantsAvailabilityAsync(It.IsAny<System.Collections.Generic.IEnumerable<int>>()))
            .ReturnsAsync(new System.Collections.Generic.List<Shared.DTOs.Catalog.VariantAvailabilityDto>
            {
                new()
                {
                    MaBienThe = 99,
                    TenSanPham = "Áo Polo Đã Ẩn",
                    MauSac = "Đen",
                    KichCo = "L",
                    DaAn = true
                }
            });

        // Act
        var result = await serviceWithCatalog.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("tạm thời không cung cấp");
        result.Message.Should().Contain("Áo Polo Đã Ẩn");
        _mockRepo.Verify(r => r.CreateOrderAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    #endregion
}
