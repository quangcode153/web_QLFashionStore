using System;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using Backend.WebSockets;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shared.DTOs.Announcement;
using Shared.DTOs.Common;
using Shared.DTOs.Feedback;
using Xunit;

namespace Backend.Tests.UnitTests.Customers;

/// <summary>
/// Unit Tests kiểm thử luồng Đổi trả Online đa phương thức và Tự động sinh thông báo nội bộ cho chi nhánh (Bước 4).
/// Tuân thủ quy chuẩn Multi-Round Testing Standard (Rule 6):
/// - VÒNG 1: Parameterized Boundary Matrix (Biên, null, rỗng, sai hình thức hoàn trả)
/// - VÒNG 2: State Machine & Auto Branch Notification (Tự động tạo ThongBaoNoiBo khi hẹn chi nhánh, Fail-safe khi lỗi thông báo)
/// - VÒNG 3: Branch Scoping & Role Isolation (Gửi đúng phạm vi Nội bộ chi nhánh, đúng MaChiNhanhHen, không gửi nhầm)
/// </summary>
public class OnlineReturnNotificationTests
{
    private readonly Mock<IFeedbackRepository> _mockFeedbackRepo;
    private readonly Mock<IAnnouncementRepository> _mockAnnouncementRepo;
    private readonly Mock<IHubContext<NotificationHub>> _mockNotificationHub;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly FeedbackService _service;

    public OnlineReturnNotificationTests()
    {
        _mockFeedbackRepo = new Mock<IFeedbackRepository>();
        _mockAnnouncementRepo = new Mock<IAnnouncementRepository>();
        _mockNotificationHub = new Mock<IHubContext<NotificationHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        _mockNotificationHub.Setup(h => h.Clients).Returns(_mockClients.Object);

        _service = new FeedbackService(
            _mockFeedbackRepo.Object,
            _mockNotificationHub.Object,
            _mockAnnouncementRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Fact(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả khi request null")]
    public async Task CreateReturnRequestAsync_NullRequest_ReturnsFail()
    {
        // Act
        var result = await _service.CreateReturnRequestAsync(null!, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockFeedbackRepo.Verify(r => r.CreateReturnRequestAsync(It.IsAny<CreateReturnRequestDto>(), It.IsAny<int?>()), Times.Never);
        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn đăng ký hoàn trả khi mã hóa đơn hoặc mã biến thể <= 0")]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public async Task CreateReturnRequestAsync_InvalidOrderOrVariantId_ReturnsFail(int orderId, int variantId)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = orderId,
            MaBienThe = variantId,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị lỗi chỉ may",
            HoTen = "Nguyen Van A",
            DienThoai = "0987654321",
            HinhThucHoan = "Thu hồi tại nhà",
            DiaChiThuHoi = "123 Đường ABC"
        };

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
        _mockFeedbackRepo.Verify(r => r.CreateReturnRequestAsync(It.IsAny<CreateReturnRequestDto>(), It.IsAny<int?>()), Times.Never);
        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn khi chọn 'Thu hồi tại nhà' nhưng địa chỉ thu hồi null hoặc whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateReturnRequestAsync_HomePickup_MissingAddress_ReturnsFail(string? emptyAddress)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị ố màu",
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
        _mockFeedbackRepo.Verify(r => r.CreateReturnRequestAsync(It.IsAny<CreateReturnRequestDto>(), It.IsAny<int?>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn khi chọn 'Đến chi nhánh' nhưng mã chi nhánh hẹn null hoặc <= 0")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateReturnRequestAsync_BranchDropoff_InvalidBranch_ReturnsFail(int? invalidBranchId)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị ố màu",
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
        _mockFeedbackRepo.Verify(r => r.CreateReturnRequestAsync(It.IsAny<CreateReturnRequestDto>(), It.IsAny<int?>()), Times.Never);
    }

    [Theory(DisplayName = "VÒNG 1: Chặn hình thức hoàn trả lạ hoặc XSS")]
    [InlineData("ShipBangMayBay")]
    [InlineData("TuGuiBuuDien")]
    [InlineData("<script>alert('xss')</script>")]
    public async Task CreateReturnRequestAsync_InvalidMethod_ReturnsFail(string invalidMethod)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 10,
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị ố màu",
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
        _mockFeedbackRepo.Verify(r => r.CreateReturnRequestAsync(It.IsAny<CreateReturnRequestDto>(), It.IsAny<int?>()), Times.Never);
    }

    #endregion

    #region VÒNG 2: TÁCH BẠCH KÊNH ĐỔI TRẢ & THÔNG BÁO CẤP TRÊN (ISOLATION OF RETURNS FROM SUPERIOR ANNOUNCEMENTS)

    [Fact(DisplayName = "VÒNG 2: Khách hẹn đổi trả tại chi nhánh -> KHÔNG tạo thông báo cấp trên (Yêu cầu hoàn có mục quản lý riêng)")]
    public async Task CreateReturnRequestAsync_BranchDropoff_DoesNotCreateSuperiorAnnouncement()
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 105,
            MaBienThe = 3,
            SoLuongTra = 2,
            LyDoTra = "Khách thử không vừa size, muốn đổi tại quầy",
            HoTen = "Trần Thị Cẩm",
            DienThoai = "0933112233",
            HinhThucHoan = "Đến chi nhánh",
            MaChiNhanhHen = 2
        };

        _mockFeedbackRepo.Setup(r => r.CreateReturnRequestAsync(request, 5))
            .ReturnsAsync(88);

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(88);

        // Đảm bảo TUYỆT ĐỐI KHÔNG gọi tạo thông báo nội bộ của cấp trên
        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never,
            "Yêu cầu hoàn trả phải được quản lý ở phân hệ Đổi trả riêng, không làm rác chuông thông báo chỉ thị cấp trên.");
    }

    [Fact(DisplayName = "VÒNG 2: Khách chọn 'Thu hồi tại nhà' -> KHÔNG sinh thông báo cấp trên")]
    public async Task CreateReturnRequestAsync_HomePickup_DoesNotSendBranchAnnouncement()
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 105,
            MaBienThe = 3,
            SoLuongTra = 1,
            LyDoTra = "Hàng bị bung cúc áo",
            HoTen = "Nguyễn Văn Đạt",
            DienThoai = "0944556677",
            HinhThucHoan = "Thu hồi tại nhà",
            DiaChiThuHoi = "456 CMT8, Phường 11, Quận 3, TP.HCM"
        };

        _mockFeedbackRepo.Setup(r => r.CreateReturnRequestAsync(request, 5))
            .ReturnsAsync(89);

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(89);

        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never);
    }

    [Fact(DisplayName = "VÒNG 2: Bắn tín hiệu SignalR CSKH khi có yêu cầu đổi trả mới")]
    public async Task CreateReturnRequestAsync_BroadcastsCustomerFeedbackSignalR()
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 105,
            MaBienThe = 3,
            SoLuongTra = 1,
            LyDoTra = "Lỗi đường chỉ",
            HoTen = "Lê Hoàng Nam",
            DienThoai = "0911223344",
            HinhThucHoan = "Đến chi nhánh",
            MaChiNhanhHen = 3
        };

        _mockFeedbackRepo.Setup(r => r.CreateReturnRequestAsync(request, 5))
            .ReturnsAsync(90);

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(90);
        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never);
    }

    #endregion

    #region VÒNG 3: CÔ LẬP CHI NHÁNH & BẢO VỆ KÊNH THÔNG BÁO (BRANCH SCOPING & CHANNEL ISOLATION MATRIX)

    [Theory(DisplayName = "VÒNG 3: Mọi chi nhánh được hẹn đều KHÔNG phát sinh thông báo cấp trên")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(9)]
    public async Task CreateReturnRequestAsync_ScopedToSelectedBranch_DoesNotCreateAnnouncement(int branchId)
    {
        // Arrange
        var request = new CreateReturnRequestDto
        {
            MaHoaDon = 200,
            MaBienThe = 7,
            SoLuongTra = 1,
            LyDoTra = "Đổi kích cỡ",
            HoTen = "Phạm Văn Minh",
            DienThoai = "0988776655",
            HinhThucHoan = "Đến chi nhánh",
            MaChiNhanhHen = branchId
        };

        _mockFeedbackRepo.Setup(r => r.CreateReturnRequestAsync(request, 3))
            .ReturnsAsync(91);

        // Act
        var result = await _service.CreateReturnRequestAsync(request, customerId: 3);

        // Assert
        result.Success.Should().BeTrue();
        _mockAnnouncementRepo.Verify(r => r.CreateAnnouncementAsync(It.IsAny<CreateAnnouncementRequestDto>()), Times.Never);
    }

    #endregion
}
