using System.Net;
using System.Net.Http.Headers;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Xunit;

namespace Backend.Tests.IntegrationTests.Orders;

/// <summary>
/// Kịch bản kiểm thử bảo mật IDOR và Whitelist đơn hàng dựa trên TEST_SPECIFICATION.md & FEATURE_SPECIFICATION.md:
/// - TS-ORD-01: Chống lỗ hổng IDOR - Khách hàng xem đơn của người khác (Trả về 403 Forbidden)
/// - TS-ORD-02: Khách hàng xem đơn của chính mình (Trả về 200 OK)
/// - TS-ORD-03: Nhân viên/Quản lý xem chi tiết bất kỳ đơn hàng (Không bị chặn 403)
/// - TS-ORD-05: Chặn cập nhật trạng thái đơn hàng bằng chuỗi bất hợp pháp (Trả về 400 Bad Request)
/// - Negative Cases: Chưa đăng nhập (401), Đơn không tồn tại (404), Khách thường đổi trạng thái (403)
/// </summary>
public class OrderIdorIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OrderIdorIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "TS-ORD-01: Khách hàng xem đơn của người khác phải bị chặn với 403 Forbidden (Chống IDOR)")]
    public async Task GetDetails_ShouldReturn403Forbidden_WhenCustomerTriesToAccessAnotherCustomersOrder()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenCustomerOther = TestTokenHelper.GenerateToken(999, "khach_other", RoleConstants.KhachHang);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenCustomerOther);

        // Act - Khách 999 cố tình truy cập đơn hàng ID = 1 của khách khác
        var response = await client.GetAsync("/api/orders/1/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
            "Khách hàng không sở hữu đơn hàng phải bị chặn ngay lập tức với mã 403 Forbidden để phòng chống IDOR");
    }

    [Fact(DisplayName = "TS-ORD-02: Khách hàng xem đơn của chính mình phải thành công 200 OK")]
    public async Task GetDetails_ShouldReturn200Ok_WhenCustomerAccessesOwnOrder()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenOwner = TestTokenHelper.GenerateToken(1, "khach_chinh_chu", RoleConstants.KhachHang);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwner);

        // Act
        var response = await client.GetAsync("/api/orders/1/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Khách hàng xem đơn thuộc sở hữu của chính mình phải được chấp thuận với mã 200 OK");
    }

    [Fact(DisplayName = "TS-ORD-03: Nhân viên / Quản lý có quyền xem chi tiết đơn hàng của bất kỳ ai mà không bị chặn 403")]
    public async Task GetDetails_ShouldNotReturn403_WhenStaffAccessesAnyOrder()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenStaff = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStaff);

        // Act
        var response = await client.GetAsync("/api/orders/1/details");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, 
            "Nhân sự quản trị (Admin, Quản lý, Thu ngân) phải có quyền xem đơn hàng của khách");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "IDOR-Negative: Chưa đăng nhập (Anonymous) gọi xem chi tiết đơn hàng phải trả về 401 Unauthorized")]
    public async Task GetDetails_ShouldReturn401Unauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Không gắn header Authorization

        // Act
        var response = await client.GetAsync("/api/orders/1/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
            "Truy cập không kèm Bearer Token phải bị từ chối với mã 401 Unauthorized");
    }

    [Fact(DisplayName = "IDOR-Negative: Xem chi tiết đơn hàng không tồn tại phải trả về 404 Not Found")]
    public async Task GetDetails_ShouldReturn404NotFound_WhenOrderDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenStaff = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStaff);

        // Act - Đơn hàng ID = 999 không tồn tại
        var response = await client.GetAsync("/api/orders/999/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, 
            "Đơn hàng không tồn tại trên hệ thống phải trả về mã 404 Not Found");
    }

    [Fact(DisplayName = "TS-ORD-05: Cập nhật trạng thái bằng chuỗi lạ không trong Whitelist phải trả về 400 Bad Request")]
    public async Task UpdateStatus_ShouldReturn400BadRequest_WhenStatusIsNotInWhitelist()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenStaff = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStaff);

        // Act - Gửi chuỗi trạng thái không hợp lệ "TrangThaiHack"
        var response = await client.PutAsync("/api/orders/1/status?status=TrangThaiHack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "Trạng thái ngoài Whitelist phải bị từ chối với 400 Bad Request");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("không hợp lệ", "Thông báo lỗi phải nêu rõ trạng thái không hợp lệ");
    }

    [Fact(DisplayName = "TS-ORD-05-Success: Staff cập nhật trạng thái hợp lệ trong Whitelist phải trả về 200 OK")]
    public async Task UpdateStatus_ShouldReturn200Ok_WhenStatusIsValidAndUserIsStaff()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenStaff = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStaff);

        // Act - Gửi trạng thái hợp lệ "DaHoanThanh"
        var response = await client.PutAsync("/api/orders/1/status?status=DaHoanThanh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, 
            "Cập nhật trạng thái hợp lệ bởi Staff phải thành công với mã 200 OK");
    }

    [Fact(DisplayName = "TS-ORD-05-RBAC: Khách hàng thường cố tình cập nhật trạng thái đơn phải bị chặn với 403 Forbidden")]
    public async Task UpdateStatus_ShouldReturn403Forbidden_WhenUserIsCustomer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenCustomer = TestTokenHelper.GenerateToken(1, "khach", RoleConstants.KhachHang);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenCustomer);

        // Act - Khách hàng thường cố tình gọi API cập nhật trạng thái
        var response = await client.PutAsync("/api/orders/1/status?status=DaHoanThanh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
            "Khách hàng thông thường tuyệt đối không được cấp quyền đổi trạng thái đơn hàng (RBAC chặn 403)");
    }

    [Fact(DisplayName = "TS-ORD-05-Negative: Cập nhật trạng thái cho đơn hàng không tồn tại phải trả về 404 Not Found")]
    public async Task UpdateStatus_ShouldReturn404NotFound_WhenOrderDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tokenStaff = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStaff);

        // Act - Đơn hàng ID = 999 không tồn tại
        var response = await client.PutAsync("/api/orders/999/status?status=DaHoanThanh", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, 
            "Cập nhật trạng thái cho đơn không tồn tại phải trả về mã 404 Not Found");
    }

    [Fact(DisplayName = "TS-ORD-Pipeline: Vòng kiểm thử liên hoàn vòng đời đơn hàng (Chống bypass chuyển trạng thái)")]
    public async Task OrderLifecycle_ShouldFollowStrictTransitionRules_ThroughMultipleRounds()
    {
        // Arrange
        var client = _factory.CreateClient();
        var staffToken = TestTokenHelper.GenerateToken(1, "admin", RoleConstants.AdminChuoi);
        var customerToken = TestTokenHelper.GenerateToken(1, "khach", RoleConstants.KhachHang);

        // Vòng 1: Khách kiểm tra đơn của mình đang ở trạng thái ban đầu
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var res1 = await client.GetAsync("/api/orders/1/details");
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Vòng 2: Khách cố tình tự cập nhật trạng thái đơn -> Phải bị chặn 403
        var res2 = await client.PutAsync("/api/orders/1/status?status=DaHoanThanh", null);
        res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Vòng 3: Staff cố tình cập nhật trạng thái bằng chuỗi rác -> Phải bị chặn 400
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffToken);
        var res3 = await client.PutAsync("/api/orders/1/status?status=TrangThaiRac", null);
        res3.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Vòng 4: Staff cập nhật trạng thái hợp lệ trong Whitelist -> Thành công 200
        var res4 = await client.PutAsync("/api/orders/1/status?status=DaHoanThanh", null);
        res4.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
