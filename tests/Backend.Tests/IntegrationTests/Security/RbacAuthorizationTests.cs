using System.Net;
using System.Net.Http.Headers;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Xunit;

namespace Backend.Tests.IntegrationTests.Security;

/// <summary>
/// Kịch bản kiểm thử phân quyền RBAC dựa trên TEST_SPECIFICATION.md:
/// - TS-SEC-06: Phân quyền RBAC chặn quyền truy cập trái phép
/// </summary>
public class RbacAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RbacAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory(DisplayName = "TS-SEC-06-OrdersMatrix: Kiểm tra quyền xem toàn bộ đơn hàng quản trị qua ma trận toàn bộ vai trò")]
    [InlineData(RoleConstants.AdminChuoi, HttpStatusCode.OK)]
    [InlineData(RoleConstants.QuanLyChiNhanh, HttpStatusCode.OK)]
    [InlineData(RoleConstants.ThuNgan, HttpStatusCode.OK)]
    [InlineData(RoleConstants.KhachHang, HttpStatusCode.Forbidden)]
    [InlineData("HackerRole", HttpStatusCode.Forbidden)]
    public async Task GetAllOrders_ShouldEnforceRolePermissions_AcrossFullRoleMatrix(string role, HttpStatusCode expectedStatus)
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = TestTokenHelper.GenerateToken(10, "test_user", role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(expectedStatus, 
            $"Vai trò '{role}' truy cập /api/orders phải nhận mã phản hồi {expectedStatus}");
    }

    [Fact(DisplayName = "TS-SEC-06-Anonymous: Người dùng chưa đăng nhập gọi endpoint quản trị bị chặn 401 Unauthorized")]
    public async Task GetAllOrders_ShouldReturn401Unauthorized_WhenCallerIsAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient();
        // Không gửi token

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
            "Chưa đăng nhập bắt buộc phải bị từ chối với 401 Unauthorized");
    }

    [Theory(DisplayName = "TS-SEC-06-WarehouseMatrix: Kiểm tra quyền nhập kho qua ma trận toàn bộ vai trò")]
    [InlineData(RoleConstants.AdminChuoi, HttpStatusCode.OK)]
    [InlineData(RoleConstants.QuanLyChiNhanh, HttpStatusCode.OK)]
    [InlineData(RoleConstants.ThuKho, HttpStatusCode.OK)]
    [InlineData(RoleConstants.ThuNgan, HttpStatusCode.Forbidden)]
    [InlineData(RoleConstants.KhachHang, HttpStatusCode.Forbidden)]
    [InlineData("UnknownRole", HttpStatusCode.Forbidden)]
    public async Task ImportStock_ShouldEnforceRolePermissions_AcrossFullRoleMatrix(string role, HttpStatusCode expectedStatus)
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = TestTokenHelper.GenerateToken(10, "test_user", role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Gửi JSON body hợp lệ để kiểm tra quyền hạn RBAC
        var content = System.Net.Http.Json.JsonContent.Create(new 
        { 
            MaChiNhanh = 1, 
            GhiChu = "Nhập kho thử nghiệm qua RBAC Matrix" 
        });
        var response = await client.PostAsync("/api/warehouse/import", content);

        // Assert
        response.StatusCode.Should().Be(expectedStatus, 
            $"Vai trò '{role}' gọi /api/warehouse/import phải nhận mã phản hồi {expectedStatus}");
    }
}
