using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Promotion;
using Xunit;

namespace Backend.Tests.IntegrationTests.Promotions;

/// <summary>
/// Integration Tests cho toàn bộ API Khuyến mãi & Voucher (P1-1 / TS-PRO-01..06)
/// Kiểm thử HTTP Request Pipeline, Controller, Routing, Model Binding và RBAC Matrix
/// </summary>
public class PromotionsApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PromotionsApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region KỊCH BẢN KIỂM THỬ ĐẶC TẢ TS-PRO-01 ĐẾN TS-PRO-06

    [Fact]
    public async Task TS_PRO_01_ValidateEndpoint_ValidPercentVoucher_Returns200WithDiscount()
    {
        // Act: Gửi đơn 400,000đ áp dụng SUMMER10 (10%, trần 50k, đơn tối thiểu 200k)
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER10",
            TongTienDonHang = 400000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeTrue();
        body.DiscountAmount.Should().Be(40000);
        body.FinalAmount.Should().Be(360000);
        body.MaGiamGia.Should().Be("SUMMER10");
        body.Message.Should().Contain("thành công");
    }

    [Fact]
    public async Task TS_PRO_02_ValidateEndpoint_ValidPercentVoucher_HitsCap_ClampsToMaxDiscount()
    {
        // Act: Gửi đơn 1,000,000đ áp dụng SUMMER10 (10% = 100k > trần 50k -> giảm 50k)
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "SUMMER10",
            TongTienDonHang = 1000000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeTrue();
        body.DiscountAmount.Should().Be(50000); // Khống chế tại trần 50k
        body.FinalAmount.Should().Be(950000);
    }

    [Fact]
    public async Task TS_PRO_03_ValidateEndpoint_BelowMinimumOrder_Returns200WithFriendlyRejection()
    {
        // Act: Gửi đơn 300,000đ áp dụng MIN500K (yêu cầu tối thiểu 500k)
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "MIN500K",
            TongTienDonHang = 300000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeFalse();
        body.DiscountAmount.Should().Be(0);
        body.FinalAmount.Should().Be(300000);
        body.Message.Should().Contain("tối thiểu");
    }

    [Fact]
    public async Task TS_PRO_04_ValidateEndpoint_ExpiredVoucher_Returns200WithExpiredMessage()
    {
        // Act: Áp dụng mã EXPIRED2025 đã hết hạn
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "EXPIRED2025",
            TongTienDonHang = 200000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeFalse();
        body.DiscountAmount.Should().Be(0);
        body.Message.Should().Contain("hết hạn sử dụng");
    }

    [Fact]
    public async Task TS_PRO_05_ValidateEndpoint_OutOfStockVoucher_Returns200WithOutOfStockMessage()
    {
        // Act: Áp dụng mã OUTOFSTOCK đã hết lượt dùng
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "OUTOFSTOCK",
            TongTienDonHang = 200000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeFalse();
        body.DiscountAmount.Should().Be(0);
        body.Message.Should().Contain("hết lượt sử dụng");
    }

    [Fact]
    public async Task TS_PRO_06_ValidateEndpoint_NonExistentVoucher_Returns200WithNotFoundMessage()
    {
        // Act: Áp dụng mã giả không tồn tại
        var response = await _client.PostAsJsonAsync("/api/promotions/validate", new ValidateVoucherRequestDto
        {
            MaGiamGia = "VOUCHER_FAKE_999",
            TongTienDonHang = 400000
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body.Should().NotBeNull();
        body!.Valid.Should().BeFalse();
        body.DiscountAmount.Should().Be(0);
        body.FinalAmount.Should().Be(400000);
        body.Message.Should().Contain("không tồn tại");
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & CÔ LẬP VAI TRÒ (RBAC ROLE ISOLATION MATRIX)

    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Khách hàng")]
    [InlineData("Thu ngân")]
    [InlineData("Admin chuỗi")]
    public async Task ValidateEndpoint_AccessibleByAllRoles_Returns200(string roleName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/promotions/validate")
        {
            Content = JsonContent.Create(new ValidateVoucherRequestDto
            {
                MaGiamGia = "SUMMER10",
                TongTienDonHang = 300000
            })
        };

        if (roleName != "Anonymous")
        {
            var token = TestTokenHelper.GenerateToken(99, "role_tester", roleName);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateVoucherResponseDto>();
        body!.Valid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Khách hàng")]
    [InlineData("Thu ngân")]
    [InlineData("Admin chuỗi")]
    public async Task GetActivePromotions_AccessibleByAllRoles_OnlyReturnsValidPromotions(string roleName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/promotions");

        if (roleName != "Anonymous")
        {
            var token = TestTokenHelper.GenerateToken(88, "role_reader", roleName);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<PromotionDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();

        // Không chứa mã đã hết hạn hoặc hết lượt dùng
        body.Data!.Should().NotContain(p => p.MaGiamGia == "EXPIRED2025");
        body.Data.Should().NotContain(p => p.MaGiamGia == "OUTOFSTOCK");

        // Chứa các mã hợp lệ đang diễn ra
        body.Data.Should().Contain(p => p.MaGiamGia == "SUMMER10");
        body.Data.Should().Contain(p => p.MaGiamGia == "SALE50K");
    }

    #endregion
}
