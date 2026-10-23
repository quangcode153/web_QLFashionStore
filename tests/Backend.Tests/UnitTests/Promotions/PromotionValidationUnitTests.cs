using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Services;
using FluentAssertions;
using Moq;
using Shared.DTOs.Promotion;
using Xunit;

namespace Backend.Tests.UnitTests.Promotions;

/// <summary>
/// Unit Tests phân hệ Khuyến mãi & Voucher (P1-1 / TS-PRO-01..06)
/// Tuân thủ nghiêm ngặt Tiêu chuẩn Kiểm thử Đa Vòng (Multi-Round Testing Standard - Rule 7)
/// </summary>
public class PromotionValidationUnitTests
{
    private readonly Mock<IPromotionRepository> _mockRepo;
    private readonly PromotionService _service;

    public PromotionValidationUnitTests()
    {
        _mockRepo = new Mock<IPromotionRepository>();
        _service = new PromotionService(_mockRepo.Object);
    }

    #region VÒNG 1: MA TRẬN THAM SỐ & GIÁ TRỊ BIÊN (PARAMETERIZED BOUNDARY MATRIX)

    [Theory]
    [InlineData(200000, 20000, 180000)]     // Chạm đúng ngưỡng tối thiểu 200k, giảm 10% = 20k
    [InlineData(300000, 30000, 270000)]     // Trên ngưỡng tối thiểu, giảm 10% = 30k
    [InlineData(400000, 40000, 360000)]     // Kịch bản chuẩn TS-PRO-01: 400k, giảm 10% = 40k, còn 360k
    [InlineData(499999, 49999.9, 449999.1)] // Ngay dưới trần 50k
    public async Task TS_PRO_01_Validate_ValidPercentVoucher_UnderCap_CalculatesCorrectDiscount(
        decimal orderTotal, decimal expectedDiscount, decimal expectedFinal)
    {
        // Arrange: Voucher SUMMER10 (10%, trần 50k, tối thiểu 200k)
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 1,
            TenKhuyenMai = "Ưu Đãi Chào Hè 2026",
            MaGiamGia = "SUMMER10",
            PhanTramGiam = 10,
            SoTienGiam = 50000, // Trần tối đa 50k
            MuaToiThieu = 200000,
            NgayBatDau = DateTime.UtcNow.AddDays(-10),
            NgayKetThuc = DateTime.UtcNow.AddDays(30),
            SoLuongDung = 100
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER10")).ReturnsAsync(promo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER10",
            TongTienDonHang = orderTotal
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Valid.Should().BeTrue();
        result.Data.DiscountAmount.Should().Be(expectedDiscount);
        result.Data.FinalAmount.Should().Be(expectedFinal);
        result.Data.MaGiamGia.Should().Be("SUMMER10");
        result.Data.Message.Should().Contain("thành công");
    }

    [Theory]
    [InlineData(500000, 50000, 450000)]     // Đúng mức trần 500k * 10% = 50k
    [InlineData(600000, 50000, 550000)]     // Vượt trần: 60k -> bị khống chế còn 50k
    [InlineData(1000000, 50000, 950000)]   // Kịch bản chuẩn TS-PRO-02: 1 triệu * 10% = 100k -> giảm 50k, còn 950k
    [InlineData(10000000, 50000, 9950000)] // Đơn cực lớn 10 triệu -> giảm đúng 50k
    public async Task TS_PRO_02_Validate_ValidPercentVoucher_HitsMaxCap_ClampsToMaxDiscount(
        decimal orderTotal, decimal expectedDiscount, decimal expectedFinal)
    {
        // Arrange: Voucher SUMMER10 (10%, trần tối đa 50k)
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 1,
            TenKhuyenMai = "Ưu Đãi Chào Hè 2026",
            MaGiamGia = "SUMMER10",
            PhanTramGiam = 10,
            SoTienGiam = 50000,
            MuaToiThieu = 200000,
            NgayBatDau = DateTime.UtcNow.AddDays(-5),
            NgayKetThuc = DateTime.UtcNow.AddDays(15),
            SoLuongDung = 50
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER10")).ReturnsAsync(promo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER10",
            TongTienDonHang = orderTotal
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Valid.Should().BeTrue();
        result.Data.DiscountAmount.Should().Be(expectedDiscount);
        result.Data.FinalAmount.Should().Be(expectedFinal);
    }

    [Theory]
    [InlineData(0)]       // Đơn 0đ
    [InlineData(100000)]  // Đơn 100k
    [InlineData(300000)]  // Đơn 300k (Kịch bản TS-PRO-03)
    [InlineData(499999)]  // Sát ngưỡng 500k thiếu 1đ
    public async Task TS_PRO_03_Validate_BelowMinimumOrderThreshold_RejectsWithFriendlyMessage(decimal orderTotal)
    {
        // Arrange: Voucher yêu cầu tối thiểu 500,000đ
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 3,
            TenKhuyenMai = "Voucher Đơn Tối Thiểu 500K",
            MaGiamGia = "MIN500K",
            PhanTramGiam = 15,
            SoTienGiam = 100000,
            MuaToiThieu = 500000,
            NgayBatDau = DateTime.UtcNow.AddDays(-5),
            NgayKetThuc = DateTime.UtcNow.AddDays(15),
            SoLuongDung = 20
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("MIN500K")).ReturnsAsync(promo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "MIN500K",
            TongTienDonHang = orderTotal
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.DiscountAmount.Should().Be(0);
        result.Data.FinalAmount.Should().Be(orderTotal);
        result.Data.Message.Should().Contain("tối thiểu");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("'; DROP TABLE ChuongTrinhKhuyenMai; --")]
    public async Task MalformedInput_InvalidVoucherCode_RejectsSafely(string? malformedCode)
    {
        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = malformedCode,
            TongTienDonHang = 300000
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.DiscountAmount.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-500000)]
    public async Task MalformedInput_NegativeOrderTotal_RejectsSafely(decimal negativeTotal)
    {
        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER10",
            TongTienDonHang = negativeTotal
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.Message.Should().Contain("không hợp lệ");
    }

    [Theory]
    [InlineData("summer10")]
    [InlineData("SUMMER10")]
    [InlineData("Summer10")]
    [InlineData("  SUMMER10  ")]
    public async Task CaseInsensitiveAndTrim_MatchesVoucherCodeCorrectly(string inputCode)
    {
        // Arrange
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 1,
            MaGiamGia = "SUMMER10",
            PhanTramGiam = 10,
            MuaToiThieu = 200000,
            NgayBatDau = DateTime.UtcNow.AddDays(-1),
            NgayKetThuc = DateTime.UtcNow.AddDays(10),
            SoLuongDung = 10
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("SUMMER10")).ReturnsAsync(promo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = inputCode,
            TongTienDonHang = 300000
        });

        // Assert
        result.Data!.Valid.Should().BeTrue();
        result.Data.DiscountAmount.Should().Be(30000);
    }

    [Theory]
    [InlineData(400000, 50000, 350000)] // Tiền mặt 50k, đơn 400k -> giảm 50k, còn 350k
    [InlineData(50000, 50000, 0)]       // Tiền giảm bằng tổng tiền hàng -> còn 0đ
    [InlineData(30000, 30000, 0)]       // Tiền giảm lớn hơn tiền hàng (50k > 30k) -> clamp tối đa bằng tiền hàng (giảm 30k, còn 0đ)
    public async Task FixedAmountVoucher_CalculatesAndClampsCorrectly(
        decimal orderTotal, decimal expectedDiscount, decimal expectedFinal)
    {
        // Arrange: Voucher SALE50K giảm cố định 50,000đ (mua tối thiểu 0đ để test biên clamp)
        var promo = new PromotionEntity
        {
            MaKhuyenMai = 2,
            MaGiamGia = "SALE50K",
            PhanTramGiam = 0,
            SoTienGiam = 50000,
            MuaToiThieu = 0,
            NgayBatDau = DateTime.UtcNow.AddDays(-5),
            NgayKetThuc = DateTime.UtcNow.AddDays(15),
            SoLuongDung = 10
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("SALE50K")).ReturnsAsync(promo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "SALE50K",
            TongTienDonHang = orderTotal
        });

        // Assert
        result.Data!.Valid.Should().BeTrue();
        result.Data.DiscountAmount.Should().Be(expectedDiscount);
        result.Data.FinalAmount.Should().Be(expectedFinal);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI (STATE MACHINE PIPELINE)

    [Fact]
    public async Task TS_PRO_04_Validate_ExpiredVoucher_RejectsWithExpiredMessage()
    {
        // Arrange: Voucher đã hết hạn vào ngày hôm qua
        var expiredPromo = new PromotionEntity
        {
            MaKhuyenMai = 4,
            MaGiamGia = "EXPIRED2025",
            PhanTramGiam = 20,
            MuaToiThieu = 100000,
            NgayBatDau = DateTime.UtcNow.AddDays(-60),
            NgayKetThuc = DateTime.UtcNow.AddDays(-1), // Đã hết hạn
            SoLuongDung = 10
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("EXPIRED2025")).ReturnsAsync(expiredPromo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "EXPIRED2025",
            TongTienDonHang = 200000
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.DiscountAmount.Should().Be(0);
        result.Data.Message.Should().Contain("hết hạn sử dụng");
    }

    [Fact]
    public async Task TS_PRO_05_Validate_OutOfStockVoucher_RejectsWithLimitReachedMessage()
    {
        // Arrange: Voucher còn hạn nhưng số lượng dùng đã về 0
        var outOfStockPromo = new PromotionEntity
        {
            MaKhuyenMai = 5,
            MaGiamGia = "OUTOFSTOCK",
            PhanTramGiam = 10,
            MuaToiThieu = 100000,
            NgayBatDau = DateTime.UtcNow.AddDays(-5),
            NgayKetThuc = DateTime.UtcNow.AddDays(30),
            SoLuongDung = 0 // Đã hết lượt dùng
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("OUTOFSTOCK")).ReturnsAsync(outOfStockPromo);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "OUTOFSTOCK",
            TongTienDonHang = 200000
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.DiscountAmount.Should().Be(0);
        result.Data.Message.Should().Contain("hết lượt sử dụng");
    }

    [Fact]
    public async Task TS_PRO_06_Validate_NonExistentVoucher_RejectsWithNotFoundMessage()
    {
        // Arrange: Mã không tồn tại trong CSDL
        _mockRepo.Setup(r => r.GetByCodeAsync("VOUCHER_FAKE_999"))
            .ReturnsAsync((PromotionEntity?)null);

        // Act
        var result = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "VOUCHER_FAKE_999",
            TongTienDonHang = 400000
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Valid.Should().BeFalse();
        result.Data.DiscountAmount.Should().Be(0);
        result.Data.FinalAmount.Should().Be(400000);
        result.Data.Message.Should().Contain("không tồn tại");
    }

    [Fact]
    public async Task VoucherLifecycle_FromActiveToDepletedToRejection_SimulatesStateMachine()
    {
        // Bước 1: Voucher đang hoạt động bình thường
        var dynamicPromo = new PromotionEntity
        {
            MaKhuyenMai = 10,
            MaGiamGia = "FLASH10",
            PhanTramGiam = 10,
            MuaToiThieu = 100000,
            NgayBatDau = DateTime.UtcNow.AddDays(-1),
            NgayKetThuc = DateTime.UtcNow.AddDays(1),
            SoLuongDung = 1 // Còn đúng 1 lượt
        };
        _mockRepo.Setup(r => r.GetByCodeAsync("FLASH10")).ReturnsAsync(dynamicPromo);

        var firstUse = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "FLASH10",
            TongTienDonHang = 200000
        });
        firstUse.Data!.Valid.Should().BeTrue();

        // Bước 2: Sau khi dùng, lượt dùng về 0
        dynamicPromo.SoLuongDung = 0;

        var secondUse = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "FLASH10",
            TongTienDonHang = 200000
        });
        secondUse.Data!.Valid.Should().BeFalse();
        secondUse.Data.Message.Should().Contain("hết lượt sử dụng");

        // Bước 3: Hết thời gian khuyến mãi
        dynamicPromo.NgayKetThuc = DateTime.UtcNow.AddDays(-1);

        var thirdUse = await _service.ValidateVoucherAsync(new ValidateVoucherRequestDto
        {
            MaGiamGia = "FLASH10",
            TongTienDonHang = 200000
        });
        thirdUse.Data!.Valid.Should().BeFalse();
        thirdUse.Data.Message.Should().Contain("hết hạn sử dụng");
    }

    #endregion
}
