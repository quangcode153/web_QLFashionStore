using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Promotion;
using Xunit;

namespace Backend.Tests.UnitTests.Promotions;

/// <summary>
/// Unit Tests kiểm thử nghiệp vụ Quản lý Mã giảm giá (Admin Coupon Management)
/// Tuân thủ chu trình TDD Red-First Gate (Rule 8) và Tiêu chuẩn Kiểm thử Đa Vòng (Rule 6)
/// </summary>
public class PromotionManagementUnitTests
{
    private readonly Mock<IPromotionRepository> _mockRepo;
    private readonly PromotionService _service;

    public PromotionManagementUnitTests()
    {
        _mockRepo = new Mock<IPromotionRepository>();
        _service = new PromotionService(_mockRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePromotion_EmptyCode_MustReturnFailure(string? invalidCode)
    {
        var request = new CreatePromotionRequestDto
        {
            TenKhuyenMai = "Khuyến mãi test",
            MaGiamGia = invalidCode!,
            PhanTramGiam = 10,
            NgayBatDau = DateTime.UtcNow.Date,
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(7)
        };

        var result = await _service.CreatePromotionAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Mã giảm giá không được để trống");
    }

    [Fact]
    public async Task CreatePromotion_StartDateGreaterThanEndDate_MustReturnFailure()
    {
        var request = new CreatePromotionRequestDto
        {
            TenKhuyenMai = "Khuyến mãi sai ngày",
            MaGiamGia = "INVALIDDATE",
            PhanTramGiam = 15,
            NgayBatDau = DateTime.UtcNow.Date.AddDays(10),
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(5) // Kết thúc trước khi bắt đầu
        };

        var result = await _service.CreatePromotionAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(0, -10000)]
    [InlineData(105, 0)] // % vượt quá 100%
    public async Task CreatePromotion_InvalidDiscountValues_MustReturnFailure(decimal percent, decimal amount)
    {
        var request = new CreatePromotionRequestDto
        {
            TenKhuyenMai = "Khuyến mãi sai chiết khấu",
            MaGiamGia = "WRONGDISCOUNT",
            PhanTramGiam = percent,
            SoTienGiam = amount,
            NgayBatDau = DateTime.UtcNow.Date,
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(7)
        };

        var result = await _service.CreatePromotionAsync(request);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePromotion_DuplicateCode_MustReturnFailure()
    {
        var request = new CreatePromotionRequestDto
        {
            TenKhuyenMai = "Khuyến mãi trùng code",
            MaGiamGia = "DUPLICATE10",
            PhanTramGiam = 10,
            NgayBatDau = DateTime.UtcNow.Date,
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(7)
        };

        _mockRepo.Setup(r => r.ExistsCodeAsync("DUPLICATE10", null)).ReturnsAsync(true);

        var result = await _service.CreatePromotionAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã tồn tại");
    }

    #endregion

    #region VÒNG 2: CHUỖI VÒNG ĐỜI NGHIỆP VỤ (STATE MACHINE PIPELINE: CREATE -> GET -> UPDATE -> DELETE)

    [Fact]
    public async Task CreatePromotion_ValidData_MustSaveAndReturnId()
    {
        var request = new CreatePromotionRequestDto
        {
            TenKhuyenMai = "Siêu Sale Mùa Thu",
            MaGiamGia = "AUTUMN20",
            PhanTramGiam = 20,
            SoTienGiam = 100000,
            NgayBatDau = DateTime.UtcNow.Date,
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(30),
            MuaToiThieu = 300000,
            SoLuongDung = 500
        };

        _mockRepo.Setup(r => r.ExistsCodeAsync("AUTUMN20", null)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.CreatePromotionAsync(It.IsAny<PromotionEntity>())).ReturnsAsync(99);

        var result = await _service.CreatePromotionAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
    }

    [Fact]
    public async Task UpdatePromotion_ValidData_MustSucceed()
    {
        var request = new UpdatePromotionRequestDto
        {
            MaKhuyenMai = 99,
            TenKhuyenMai = "Siêu Sale Mùa Thu - Cập nhật",
            MaGiamGia = "AUTUMN25",
            PhanTramGiam = 25,
            NgayBatDau = DateTime.UtcNow.Date,
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(45)
        };

        _mockRepo.Setup(r => r.ExistsCodeAsync("AUTUMN25", 99)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.UpdatePromotionAsync(It.IsAny<PromotionEntity>())).ReturnsAsync(true);

        var result = await _service.UpdatePromotionAsync(request);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeletePromotion_ValidId_MustSucceed()
    {
        _mockRepo.Setup(r => r.DeletePromotionAsync(99)).ReturnsAsync(true);

        var result = await _service.DeletePromotionAsync(99);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllPromotions_MustReturnAllWithStatusFlag()
    {
        var entities = new List<PromotionEntity>
        {
            new() { MaKhuyenMai = 1, TenKhuyenMai = "Active", MaGiamGia = "ACT1", PhanTramGiam = 10, NgayKetThuc = DateTime.UtcNow.Date.AddDays(5), SoLuongDung = 10 },
            new() { MaKhuyenMai = 2, TenKhuyenMai = "Expired", MaGiamGia = "EXP1", PhanTramGiam = 10, NgayKetThuc = DateTime.UtcNow.Date.AddDays(-2), SoLuongDung = 10 }
        };

        _mockRepo.Setup(r => r.GetAllPromotionsAsync()).ReturnsAsync(entities);

        var result = await _service.GetAllPromotionsAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.First(p => p.MaGiamGia == "ACT1").ConHieuLuc.Should().BeTrue();
        result.Data.First(p => p.MaGiamGia == "EXP1").ConHieuLuc.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateVoucher_WhenCustomerAlreadyUsed_MustReturnFailure()
    {
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 5,
            TenKhuyenMai = "Ưu đãi hè",
            MaGiamGia = "SUMMER20",
            PhanTramGiam = 20,
            NgayBatDau = DateTime.UtcNow.Date.AddDays(-1),
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(10),
            MuaToiThieu = 100000,
            SoLuongDung = 50
        };

        _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER20")).ReturnsAsync(promo);
        _mockRepo.Setup(r => r.HasCustomerUsedPromotionAsync(101, 5)).ReturnsAsync(true);

        var request = new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER20",
            TongTienDonHang = 200000,
            MaKhachHang = 101
        };

        var response = await _service.ValidateVoucherAsync(request);

        response.Data!.Valid.Should().BeFalse();
        response.Data.Message.Should().Contain("Tài khoản của bạn đã sử dụng mã giảm giá này");
    }

    [Fact]
    public async Task ValidateVoucher_WhenCustomerHasNotUsed_MustReturnSuccess()
    {
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 5,
            TenKhuyenMai = "Ưu đãi hè",
            MaGiamGia = "SUMMER20",
            PhanTramGiam = 20,
            NgayBatDau = DateTime.UtcNow.Date.AddDays(-1),
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(10),
            MuaToiThieu = 100000,
            SoLuongDung = 50
        };

        _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER20")).ReturnsAsync(promo);
        _mockRepo.Setup(r => r.HasCustomerUsedPromotionAsync(102, 5)).ReturnsAsync(false);

        var request = new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER20",
            TongTienDonHang = 200000,
            MaKhachHang = 102
        };

        var response = await _service.ValidateVoucherAsync(request);

        response.Data!.Valid.Should().BeTrue();
        response.Data.DiscountAmount.Should().Be(40000);
        response.Data.FinalAmount.Should().Be(160000);
    }

    [Fact]
    public async Task ValidateVoucher_WhenVoucherOutOfStock_MustReturnFailure()
    {
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 6,
            TenKhuyenMai = "Mã hết lượt",
            MaGiamGia = "EMPTY0",
            PhanTramGiam = 10,
            NgayBatDau = DateTime.UtcNow.Date.AddDays(-1),
            NgayKetThuc = DateTime.UtcNow.Date.AddDays(10),
            MuaToiThieu = 100000,
            SoLuongDung = 0
        };

        _mockRepo.Setup(r => r.GetByCodeAsync("EMPTY0")).ReturnsAsync(promo);

        var request = new ValidateVoucherRequestDto
        {
            MaGiamGia = "EMPTY0",
            TongTienDonHang = 200000,
            MaKhachHang = 102
        };

        var response = await _service.ValidateVoucherAsync(request);

        response.Data!.Valid.Should().BeFalse();
        response.Data.Message.Should().Contain("hết lượt sử dụng");
    }

    #endregion
}

