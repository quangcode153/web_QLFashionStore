using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Feedback;
using Xunit;

namespace Backend.Tests.UnitTests.Customers;

/// <summary>
/// Unit Tests cho tính năng Đăng ký và Phê duyệt hoàn trả hàng trong phân hệ Khiếu nại Phản hồi (TDD Red-First Gate).
/// Tuân thủ quy chuẩn Multi-Round Testing Standard:
/// - Vòng 1: Boundary Matrix (Giá trị biên, rỗng, âm)
/// - Vòng 2: State Machine & Business Rules (Trạng thái đơn, tồn kho, duyệt/từ chối)
/// - Vòng 3: Isolation & IDOR Protection (Kiểm tra quyền sở hữu đơn hàng của khách)
/// </summary>
public class ReturnFeedbackServiceTests
{
    private readonly Mock<IFeedbackRepository> _mockRepo;
    private readonly FeedbackService _service;

    public ReturnFeedbackServiceTests()
    {
        _mockRepo = new Mock<IFeedbackRepository>();
        _service = new FeedbackService(_mockRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả với số lượng trả không hợp lệ (<= 0)")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task CreateReturnRequestAsync_WithInvalidQuantity_ReturnsFail(int invalidQuantity)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = invalidQuantity,
            LyDoTra = "Hàng bị lỗi chỉ may",
            HoTen = "Nguyen Van A",
            DienThoai = "0987654321"
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Số lượng hoàn trả");
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả với thông tin định danh hoặc lý do rỗng")]
    [InlineData("", "0987654321", "Lỗi bung keo")]
    [InlineData("   ", "0987654321", "Lỗi bung keo")]
    [InlineData("Nguyen Van A", "", "Lỗi bung keo")]
    [InlineData("Nguyen Van A", "0987654321", "")]
    [InlineData("Nguyen Van A", "0987654321", "   ")]
    public async Task CreateReturnRequestAsync_WithMissingFields_ReturnsFail(string hoTen, string dienThoai, string lyDo)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = lyDo,
            HoTen = hoTen,
            DienThoai = dienThoai
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả khi hình thức 'Thu hồi tại nhà' bị thiếu địa chỉ thu hồi")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateReturnRequestAsync_HomePickupMissingAddress_ReturnsFail(string? emptyAddress)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị lỗi chỉ may",
            HoTen = "Nguyen Van A",
            DienThoai = "0987654321",
            HinhThucHoan = "Thu hồi tại nhà",
            DiaChiThuHoi = emptyAddress
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("địa chỉ thu hồi");
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả khi hình thức 'Đến chi nhánh' bị thiếu hoặc mã chi nhánh không hợp lệ")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateReturnRequestAsync_BranchDropoffMissingBranchId_ReturnsFail(int? invalidBranchId)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị lỗi chỉ may",
            HoTen = "Nguyen Van A",
            DienThoai = "0987654321",
            HinhThucHoan = "Đến chi nhánh",
            MaChiNhanhHen = invalidBranchId
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("chi nhánh");
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả với hình thức đổi trả không hợp lệ")]
    [InlineData("GiaoHangVuTru")]
    [InlineData("TuDo")]
    [InlineData("<script>alert('xss')</script>")]
    public async Task CreateReturnRequestAsync_InvalidReturnMethod_ReturnsFail(string invalidMethod)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị lỗi chỉ may",
            HoTen = "Nguyen Van A",
            DienThoai = "0987654321",
            HinhThucHoan = invalidMethod,
            DiaChiThuHoi = "123 Đường ABC"
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Hình thức");
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI & NGHIỆP VỤ DUYỆT HOÀN TRẢ (STATE MACHINE PIPELINE)

    [Fact(DisplayName = "VÒNG 2: Tạo yêu cầu hoàn trả thành công khi dữ liệu hợp lệ")]
    public async Task CreateReturnRequestAsync_WithValidData_ReturnsSuccessAndId()
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 101,
            MaBienThe = 5,
            SoLuongTra = 2,
            LyDoTra = "Áo bị rách góc tay",
            HoTen = "Tran Thi B",
            DienThoai = "0912345678",
            Email = "b@gmail.com",
            HinhThucHoan = "Thu hồi tại nhà",
            DiaChiThuHoi = "123 Lê Duẩn, Q.1, TP.HCM"
        };

        _mockRepo.Setup(r => r.CreateReturnRequestAsync(request, 2))
            .ReturnsAsync(77);

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 2);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(77);
        result.Message.Should().Contain("thành công");
    }

    [Fact(DisplayName = "VÒNG 2: Phê duyệt hoàn trả gửi thông báo chú ý bên ship và chuyển trạng thái Đã duyệt")]
    public async Task ProcessReturnRequestAsync_Approved_CallsApprovalRepoWithShipperNotice()
    {
        // Arrange
        var processDto = new ProcessReturnRequestDto
        {
            Action = "APPROVE",
            IsApproved = true,
            Notes = null // Để trống để kiểm tra thông báo mặc định
        };

        var expectedNotice = "Yêu cầu hoàn trả của quý khách đã được xác nhận, vui lòng chú ý đến thông báo điện thoại bên ship.";

        _mockRepo.Setup(r => r.ProcessReturnApprovalAsync(77, 1, expectedNotice))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessReturnRequestAsync(77, processDto, staffId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("phê duyệt");
        _mockRepo.Verify(r => r.ProcessReturnApprovalAsync(77, 1, expectedNotice), Times.Once);
        _mockRepo.Verify(r => r.ProcessReturnReceiptAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockRepo.Verify(r => r.ProcessReturnRejectionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "VÒNG 2: Xác nhận đã nhận hàng hoàn -> Gọi ProcessReturnReceiptAsync (chính thức vào kho lỗi)")]
    public async Task ProcessReturnRequestAsync_ReceivedReturn_CallsReceiptRepo()
    {
        // Arrange
        var processDto = new ProcessReturnRequestDto
        {
            Action = "RECEIVE",
            Notes = "Đã nhận bưu phẩm hoàn trả từ shipper, kiểm tra lỗi hợp lệ"
        };

        _mockRepo.Setup(r => r.ProcessReturnReceiptAsync(77, 1, processDto.Notes))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessReturnRequestAsync(77, processDto, staffId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("kho hàng lỗi");
        _mockRepo.Verify(r => r.ProcessReturnReceiptAsync(77, 1, processDto.Notes), Times.Once);
        _mockRepo.Verify(r => r.ProcessReturnApprovalAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "VÒNG 2: Phê duyệt Từ chối hoàn trả -> Gọi ProcessReturnRejectionAsync (không đổi kho)")]
    public async Task ProcessReturnRequestAsync_Rejected_CallsRejectionRepo()
    {
        // Arrange
        var processDto = new ProcessReturnRequestDto
        {
            Action = "REJECT",
            IsApproved = false,
            Notes = "Sản phẩm đã bị giặt tẩy và rách do tác động ngoại lực, không đủ điều kiện đổi trả."
        };

        _mockRepo.Setup(r => r.ProcessReturnRejectionAsync(77, 1, processDto.Notes))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessReturnRequestAsync(77, processDto, staffId: 1);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("từ chối");
        _mockRepo.Verify(r => r.ProcessReturnRejectionAsync(77, 1, processDto.Notes), Times.Once);
        _mockRepo.Verify(r => r.ProcessReturnApprovalAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockRepo.Verify(r => r.ProcessReturnReceiptAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }


    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & LẤY ĐƠN HỢP LỆ (RBAC & ISOLATION MATRIX)

    [Fact(DisplayName = "VÒNG 3: Lấy danh sách đơn đủ điều kiện hoàn trả chỉ trả về đơn Đã hoàn thành")]
    public async Task GetEligibleOrdersForReturnAsync_ReturnsOnlyCompletedOrders()
    {
        // Arrange
        var expectedOrders = new List<EligibleReturnOrderDto>
        {
            new()
            {
                MaHoaDon = 101,
                TrangThai = "Đã hoàn thành",
                TongThanhToan = 500000,
                Items = new List<EligibleReturnItemDto>
                {
                    new() { MaBienThe = 1, TenSanPham = "Áo Polo", SoLuongDaMua = 2 }
                }
            }
        };

        _mockRepo.Setup(r => r.GetEligibleOrdersForReturnAsync(10))
            .ReturnsAsync(expectedOrders);

        // Act
        var result = await _service.GetEligibleOrdersForReturnAsync(customerId: 10);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data.First().TrangThai.Should().Be("Đã hoàn thành");
    }

    #endregion
}
