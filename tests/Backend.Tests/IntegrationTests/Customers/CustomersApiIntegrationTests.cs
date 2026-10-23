using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;
using Xunit;

namespace Backend.Tests.IntegrationTests.Customers;

/// <summary>
/// Integration Tests cho toàn bộ API Hồ sơ Khách hàng & Lịch sử mua sắm (P1-2 / TS-CUS-01..03)
/// Kiểm thử HTTP Request Pipeline, Token Claim Extraction, Authorization và RBAC Protection
/// </summary>
public class CustomersApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CustomersApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region KỊCH BẢN KIỂM THỬ ĐẶC TẢ TS-CUS-01 ĐẾN TS-CUS-03

    [Fact]
    public async Task TS_CUS_01_GetMyOrders_WithValidCustomerToken_Returns200WithOrderHistory()
    {
        // Arrange: Tạo token cho khách hàng ID = 1
        var token = TestTokenHelper.GenerateToken(1, "quan_customer", "Khách hàng");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/me/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CustomerOrderHistoryItemDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().Be(2); // Đúng 2 đơn hàng của khách hàng 1
        body.Data![0].MaHoaDon.Should().Be(101);
    }

    [Fact]
    public async Task TS_CUS_02_GetMyOrders_WithoutToken_Returns401Unauthorized()
    {
        // Act: Gọi endpoint không kèm Authorization Bearer
        var response = await _client.GetAsync("/api/customers/me/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyProfile_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/customers/me/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TS_CUS_03_GetMyProfile_SilverTierCustomer_Returns200With5PercentDiscountAndPointMultiplier()
    {
        // Arrange: Token Khách hàng ID = 2 (Hạng Bạc, chi tiêu 5.5tr)
        var token = TestTokenHelper.GenerateToken(2, "vy_customer", "Khách hàng");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TenCapDo.Should().Be("Bạc");
        body.Data.PhanTramGiamGia.Should().Be(5);
        body.Data.HeSoTichDiem.Should().Be(1.2m);
        body.Data.TongChiTieu.Should().Be(5500000);
    }

    [Fact]
    public async Task TS_CUS_03_GetMyProfile_GoldTierCustomer_Returns200With10PercentDiscountAndMultiplier()
    {
        // Arrange: Token Khách hàng ID = 3 (Hạng Vàng, chi tiêu 22tr)
        var token = TestTokenHelper.GenerateToken(3, "nam_customer", "Khách hàng");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TenCapDo.Should().Be("Vàng");
        body.Data.PhanTramGiamGia.Should().Be(10);
        body.Data.HeSoTichDiem.Should().Be(1.5m);
        body.Data.TongChiTieu.Should().Be(22000000);
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN & CHỐNG LEO QUYỀN (RBAC & PRIVILEGE ESCALATION)

    [Fact]
    public async Task EvalRanks_CalledByCustomerRole_Returns403Forbidden()
    {
        // Arrange: Khách hàng thường cố tình gọi API xét hạng
        var token = TestTokenHelper.GenerateToken(1, "normal_cust", "Khách hàng");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/eval-ranks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EvalRanks_CalledByCashierRole_Returns403Forbidden()
    {
        // Arrange: Thu ngân không được phép tự ý kích hoạt xét hạng chuỗi
        var token = TestTokenHelper.GenerateToken(2, "cashier_user", RoleConstants.ThuNgan);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/eval-ranks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EvalRanks_CalledByAdminRole_Returns200OkWithSummary()
    {
        // Arrange: Admin chuỗi có toàn quyền gọi API xét hạng
        var token = TestTokenHelper.GenerateToken(1, "admin_user", RoleConstants.AdminChuoi);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/eval-ranks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RankEvaluationResultDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().Be(3);
    }

    #endregion

    #region TRA CỨU KHÁCH HÀNG THEO SỐ ĐIỆN THOẠI (POS & CSKH)

    [Fact(DisplayName = "TS-CUS-04: Tra cứu theo SĐT hợp lệ trả về 200 OK và hồ sơ khách hàng")]
    public async Task GetByPhone_ValidPhone_Returns200WithCustomerProfile()
    {
        // Act
        var response = await _client.GetAsync("/api/customers/by-phone?phone=0911111111");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TenKhachHang.Should().Be("Phạm Minh Quân");
        body.Data.DienThoai.Should().Be("0911111111");
    }

    [Fact(DisplayName = "TS-CUS-05: Tra cứu theo SĐT không tồn tại trả về 404 Not Found")]
    public async Task GetByPhone_NonExistentPhone_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/customers/by-phone?phone=0999999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("Không tìm thấy");
    }

    [Theory(DisplayName = "TS-CUS-06: Tra cứu theo SĐT rỗng/khoảng trắng trả về 400 Bad Request")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByPhone_EmptyOrWhitespacePhone_Returns400BadRequest(string invalidPhone)
    {
        // Act
        var response = await _client.GetAsync($"/api/customers/by-phone?phone={invalidPhone}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
