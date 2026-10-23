using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Customer;
using Xunit;

namespace Backend.Tests.UnitTests.Customers;

/// <summary>
/// Unit Tests phân hệ Khách hàng & Hồ sơ (P1-2 / TS-CUS-01..03)
/// Áp dụng Tiêu chuẩn Kiểm thử Đa Vòng (Rule 7)
/// </summary>
public class CustomerProfileUnitTests
{
    private readonly Mock<ICustomerRepository> _mockRepo;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepo;
    private readonly CustomerService _service;

    public CustomerProfileUnitTests()
    {
        _mockRepo = new Mock<ICustomerRepository>();
        _mockAuditLogRepo = new Mock<IAuditLogRepository>();
        _service = new CustomerService(_mockRepo.Object, auditLogRepository: _mockAuditLogRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task GetMyProfile_InvalidCustomerId_ReturnsFailResponse(int invalidId)
    {
        // Act
        var result = await _service.GetMyProfileAsync(invalidId);

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Contain("không hợp lệ");
    }

    [Fact]
    public async Task GetMyProfile_NonExistentCustomer_ReturnsNotFoundFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetProfileByIdAsync(999))
            .ReturnsAsync((CustomerProfileDto?)null);

        // Act
        var result = await _service.GetMyProfileAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Theory]
    [InlineData(1, "Thường", 0, 1.0, 1000000)]
    [InlineData(2, "Bạc", 5, 1.2, 5000000)]
    [InlineData(3, "Vàng", 10, 1.5, 20000000)]
    public async Task TS_CUS_03_GetMyProfile_ValidCustomer_ReturnsCorrectRankAndBenefits(
        int customerId, string expectedRank, decimal expectedDiscount, decimal expectedPointRate, decimal totalSpent)
    {
        // Arrange: Cấu hình dữ liệu khách hàng tương ứng theo BR-01 và BR-02
        var profile = new CustomerProfileDto
        {
            MaKhachHang = customerId,
            TenKhachHang = $"Khách hàng {expectedRank}",
            DiemThuong = 100,
            MaCapDo = customerId,
            TenCapDo = expectedRank,
            PhanTramGiamGia = expectedDiscount,
            HeSoTichDiem = expectedPointRate,
            TongChiTieu = totalSpent
        };
        _mockRepo.Setup(r => r.GetProfileByIdAsync(customerId)).ReturnsAsync(profile);

        // Act
        var result = await _service.GetMyProfileAsync(customerId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TenCapDo.Should().Be(expectedRank);
        result.Data.PhanTramGiamGia.Should().Be(expectedDiscount);
        result.Data.HeSoTichDiem.Should().Be(expectedPointRate);
        result.Data.TongChiTieu.Should().Be(totalSpent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetMyOrderHistory_InvalidCustomerId_ReturnsFail(int invalidId)
    {
        // Act
        var result = await _service.GetMyOrderHistoryAsync(invalidId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
    }

    [Fact]
    public async Task GetMyOrderHistory_CustomerWithNoOrders_ReturnsEmptyListSuccessfully()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetOrderHistoryAsync(10))
            .ReturnsAsync(new List<CustomerOrderHistoryItemDto>());

        // Act
        var result = await _service.GetMyOrderHistoryAsync(10);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().BeEmpty();
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI & XÉT HẠNG (STATE MACHINE LIFECYCLE)

    [Fact]
    public async Task EvaluateMemberRanks_ExecutesAndReturnsUpdatedRankSummary()
    {
        // Arrange: Giả lập thủ tục sp_TuDongXetHangThanhVien trả về danh sách khách hàng vừa được xếp hạng
        var rankResults = new List<RankEvaluationResultDto>
        {
            new() { MaKhachHang = 1, TenKhachHang = "Quân", TongChiTieu = 1000000, CapDoMoi = "Thường" },
            new() { MaKhachHang = 2, TenKhachHang = "Vy", TongChiTieu = 6000000, CapDoMoi = "Bạc" },
            new() { MaKhachHang = 3, TenKhachHang = "Nam", TongChiTieu = 25000000, CapDoMoi = "Vàng" }
        };
        _mockRepo.Setup(r => r.EvaluateRanksAsync()).ReturnsAsync(rankResults);

        // Act
        var result = await _service.EvaluateMemberRanksAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count().Should().Be(3);
        result.Data!.First(c => c.MaKhachHang == 2).CapDoMoi.Should().Be("Bạc");
        result.Data!.First(c => c.MaKhachHang == 3).CapDoMoi.Should().Be("Vàng");
        result.Message.Should().Contain("thành công");
    }

    #endregion

    #region VÒNG 3: TRA CỨU HỒ SƠ THEO SỐ ĐIỆN THOẠI (UNIT-CUS-PHONE)

    [Fact(DisplayName = "UNIT-CUS-PHN-01: Tra cứu theo SĐT hợp lệ trả về thành công và đúng thông tin")]
    public async Task GetProfileByPhone_ValidPhone_ReturnsSuccess()
    {
        // Arrange
        var customer = new CustomerProfileDto
        {
            MaKhachHang = 1,
            TenKhachHang = "Phạm Minh Quân",
            DienThoai = "0911111111"
        };
        _mockRepo.Setup(r => r.GetProfileByPhoneAsync("0911111111"))
            .ReturnsAsync(customer);

        // Act
        var result = await _service.GetProfileByPhoneAsync("0911111111");

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TenKhachHang.Should().Be("Phạm Minh Quân");
    }

    [Theory(DisplayName = "UNIT-CUS-PHN-02: Tra cứu với SĐT rỗng/space/null trả về thất bại")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetProfileByPhone_EmptyOrWhitespace_ReturnsFail(string? phone)
    {
        // Act
        var result = await _service.GetProfileByPhoneAsync(phone!);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
        _mockRepo.Verify(r => r.GetProfileByPhoneAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-CUS-PHN-03: SĐT không tồn tại trả về thất bại")]
    public async Task GetProfileByPhone_NotFound_ReturnsFail()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetProfileByPhoneAsync("0999999999"))
            .ReturnsAsync((CustomerProfileDto?)null);

        // Act
        var result = await _service.GetProfileByPhoneAsync("0999999999");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    #endregion

    #region QUẢN LÝ TÀI KHOẢN KHÁCH HÀNG (ADMIN CUSTOMER MANAGEMENT)

    [Fact(DisplayName = "UNIT-CUS-ADM-01: Lấy danh sách khách hàng gọi đúng Repository")]
    public async Task GetAllCustomers_CallsRepo_ReturnsSuccessWithList()
    {
        // Arrange
        var sampleList = new List<CustomerAccountDto>
        {
            new() { MaKhachHang = 1, TenKhachHang = "Nguyễn Văn A", DienThoai = "0901234567", MaCapDo = 1, TenCapDo = "Thường" }
        };
        _mockRepo.Setup(r => r.GetAllCustomersAsync("A", 1, false))
            .ReturnsAsync(sampleList);

        // Act
        var result = await _service.GetAllCustomersAsync("A", 1, false);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.First().TenKhachHang.Should().Be("Nguyễn Văn A");
    }

    [Theory(DisplayName = "UNIT-CUS-ADM-02: Khóa/Mở khóa với ID <= 0 trả về thất bại")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ToggleCustomerStatus_InvalidId_ReturnsFail(int id)
    {
        var result = await _service.ToggleCustomerStatusAsync(id);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không hợp lệ");
        _mockRepo.Verify(r => r.ToggleCustomerStatusAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-03: Khóa/Mở khóa thành công gọi Repo")]
    public async Task ToggleCustomerStatus_ValidId_ReturnsSuccess()
    {
        _mockRepo.Setup(r => r.ToggleCustomerStatusAsync(10)).ReturnsAsync(true);

        var result = await _service.ToggleCustomerStatusAsync(10);

        result.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.ToggleCustomerStatusAsync(10), Times.Once);
    }

    [Theory(DisplayName = "UNIT-CUS-ADM-04: Cập nhật khách hàng với dữ liệu thiếu trả về thất bại")]
    [InlineData(0, "Nguyễn Văn B")] // ID <= 0
    [InlineData(1, "")]             // Tên rỗng
    [InlineData(1, "   ")]          // Tên whitespace
    public async Task UpdateCustomer_InvalidInputs_ReturnsFail(int id, string name)
    {
        var dto = new UpdateCustomerAccountDto
        {
            MaKhachHang = id,
            TenKhachHang = name,
            DienThoai = "0987654321"
        };

        var result = await _service.UpdateCustomerAsync(dto);

        result.Success.Should().BeFalse();
        _mockRepo.Verify(r => r.UpdateCustomerProfileAsync(It.IsAny<UpdateCustomerAccountDto>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-05: Cập nhật thông tin khách hàng hợp lệ thành công")]
    public async Task UpdateCustomer_ValidInputs_ReturnsSuccess()
    {
        var dto = new UpdateCustomerAccountDto
        {
            MaKhachHang = 5,
            TenKhachHang = "Trần Thị C",
            DienThoai = "0911222333",
            MaCapDo = 2,
            DiemThuong = 100
        };
        _mockRepo.Setup(r => r.UpdateCustomerProfileAsync(dto)).ReturnsAsync(true);

        var result = await _service.UpdateCustomerAsync(dto);

        result.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.UpdateCustomerProfileAsync(dto), Times.Once);
    }

    [Theory(DisplayName = "UNIT-CUS-ADM-06: Tạo tài khoản khách hàng thiếu Tên hoặc SĐT trả về thất bại")]
    [InlineData("", "0912345678")]
    [InlineData("   ", "0912345678")]
    [InlineData("Nguyễn D", "")]
    [InlineData("Nguyễn D", "   ")]
    public async Task CreateCustomer_MissingRequiredFields_ReturnsFail(string name, string phone)
    {
        var dto = new CreateCustomerAccountDto
        {
            TenKhachHang = name,
            DienThoai = phone
        };

        var result = await _service.CreateCustomerAsync(dto);

        result.Success.Should().BeFalse();
        _mockRepo.Verify(r => r.CreateCustomerAsync(It.IsAny<CreateCustomerAccountDto>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-07: Tạo tài khoản khách hàng hợp lệ thành công và trả về ID")]
    public async Task CreateCustomer_ValidInputs_ReturnsNewId()
    {
        var dto = new CreateCustomerAccountDto
        {
            TenKhachHang = "Lê Văn E",
            DienThoai = "0933444555",
            MatKhau = "654321",
            MaCapDo = 1
        };
        _mockRepo.Setup(r => r.CreateCustomerAsync(It.IsAny<CreateCustomerAccountDto>(), It.IsAny<string>()))
            .ReturnsAsync(99);

        var result = await _service.CreateCustomerAsync(dto);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
        _mockRepo.Verify(r => r.CreateCustomerAsync(It.Is<CreateCustomerAccountDto>(x => x.TenKhachHang == "Lê Văn E"), It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-08: Khóa/Mở khóa tài khoản khách hàng bắt buộc ghi nhận vào Audit Log")]
    public async Task ToggleCustomerStatus_ValidId_LogsToAuditLogRepo()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetProfileByIdAsync(10))
            .ReturnsAsync(new CustomerProfileDto { MaKhachHang = 10, TenKhachHang = "Nguyễn Văn Test", DienThoai = "0900000000" });
        _mockRepo.Setup(r => r.ToggleCustomerStatusAsync(10)).ReturnsAsync(true);

        // Act
        var result = await _service.ToggleCustomerStatusAsync(10);

        // Assert
        result.Success.Should().BeTrue();
        _mockAuditLogRepo.Verify(a => a.CreateAuditLogAsync(It.Is<Shared.DTOs.Audit.CreateAuditLogRequestDto>(
            d => (d.LoaiThaoTac == "KHOA_TAI_KHOAN" || d.LoaiThaoTac == "MO_KHOA_TAI_KHOAN") &&
                 d.DoiTuong.Contains("Nguyễn Văn Test")
        )), Times.Once);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-09: Cập nhật thông tin khách hàng bắt buộc ghi nhận vào Audit Log")]
    public async Task UpdateCustomer_ValidInputs_LogsToAuditLogRepo()
    {
        // Arrange
        var dto = new UpdateCustomerAccountDto
        {
            MaKhachHang = 5,
            TenKhachHang = "Trần Thị C",
            DienThoai = "0911222333",
            MaCapDo = 2,
            DiemThuong = 100
        };
        _mockRepo.Setup(r => r.UpdateCustomerProfileAsync(dto)).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateCustomerAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        _mockAuditLogRepo.Verify(a => a.CreateAuditLogAsync(It.Is<Shared.DTOs.Audit.CreateAuditLogRequestDto>(
            d => d.LoaiThaoTac == "CAP_NHAT_TAI_KHOAN" &&
                 d.DoiTuong.Contains("Trần Thị C")
        )), Times.Once);
    }

    [Fact(DisplayName = "UNIT-CUS-ADM-10: Tạo tài khoản khách hàng bắt buộc ghi nhận vào Audit Log")]
    public async Task CreateCustomer_ValidInputs_LogsToAuditLogRepo()
    {
        // Arrange
        var dto = new CreateCustomerAccountDto
        {
            TenKhachHang = "Lê Văn E",
            DienThoai = "0933444555",
            MatKhau = "654321",
            MaCapDo = 1
        };
        _mockRepo.Setup(r => r.CreateCustomerAsync(It.IsAny<CreateCustomerAccountDto>(), It.IsAny<string>()))
            .ReturnsAsync(99);

        // Act
        var result = await _service.CreateCustomerAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        _mockAuditLogRepo.Verify(a => a.CreateAuditLogAsync(It.Is<Shared.DTOs.Audit.CreateAuditLogRequestDto>(
            d => d.LoaiThaoTac == "TAO_TAI_KHOAN" &&
                 d.DoiTuong.Contains("Lê Văn E")
        )), Times.Once);
    }

    #endregion
}
