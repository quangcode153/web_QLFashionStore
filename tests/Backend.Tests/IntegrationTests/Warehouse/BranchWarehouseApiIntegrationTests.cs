using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Warehouse;
using Xunit;

namespace Backend.Tests.IntegrationTests.Warehouse;

public class BranchWarehouseApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BranchWarehouseApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string GetAdminToken() =>
        TestTokenHelper.GenerateToken(1, "admin_user", RoleConstants.AdminChuoi);

    private string GetWarehouseToken() =>
        TestTokenHelper.GenerateToken(3, "warehouse_staff", RoleConstants.ThuKho);

    private string GetCustomerToken() =>
        TestTokenHelper.GenerateToken(1, "customer_test", RoleConstants.KhachHang);

    #region VÒNG 1: TRUY VẤN DANH SÁCH CHI NHÁNH & KHO ONLINE

    [Fact(DisplayName = "TS-BRANCH-01: Public API GET /api/branches trả về danh sách 9 chi nhánh hợp lệ")]
    public async Task GetAllBranches_PublicAccess_Returns200WithBranchList()
    {
        // Act
        var response = await _client.GetAsync("/api/branches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<BranchDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count().Should().BeGreaterThanOrEqualTo(9);

        // Có chi nhánh Kho Tổng Online
        body.Data.Should().Contain(b => b.LoaiChiNhanh == "Kho tổng online" || b.TenChiNhanh.Contains("Kho Tổng"));
    }

    [Fact(DisplayName = "TS-BRANCH-02: GET /api/branches/1/inventory trả về tồn kho của chi nhánh Quận 10")]
    public async Task GetBranchInventory_WithStaffToken_Returns200WithInventory()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/branches/1/inventory");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<BranchInventoryDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().NotBeEmpty();
    }

    #endregion

    #region VÒNG 2: PHÂN QUYỀN RBAC ĐIỀU CHỈNH TỒN KHO & TẠO CHI NHÁNH

    [Fact(DisplayName = "TS-BRANCH-03: Khách hàng gọi POST /api/branches bị chặn 403 Forbidden")]
    public async Task CreateBranch_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/branches");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetCustomerToken());
        request.Content = JsonContent.Create(new CreateBranchDto
        {
            TenChiNhanh = "QF Hack Branch",
            DiaChi = "Không hợp lệ"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TS-BRANCH-04: Admin tạo chi nhánh mới tự động seed toàn bộ biến thể với tồn kho = 0")]
    public async Task CreateBranch_WithAdminToken_SucceedsAndAutoSeedsInventory()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/branches");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());
        var uniqueName = $"QF Test Branch {System.Guid.NewGuid().ToString().Substring(0, 8)}";
        request.Content = JsonContent.Create(new CreateBranchDto
        {
            TenChiNhanh = uniqueName,
            DiaChi = "456 Đường Thử Nghiệm, TP.HCM",
            LoaiChiNhanh = "Cửa hàng bán lẻ"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        var createdBranchId = body.Data;
        createdBranchId.Should().BeGreaterThan(0);

        // Kiểm tra tồn kho của chi nhánh mới: toàn bộ tồn kho phải = 0
        var invReq = new HttpRequestMessage(HttpMethod.Get, $"/api/branches/{createdBranchId}/inventory");
        invReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());
        var invResp = await _client.SendAsync(invReq);
        invResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var invBody = await invResp.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<BranchInventoryDto>>>();
        invBody.Should().NotBeNull();
        invBody!.Data.Should().NotBeNull();
        invBody.Data!.Should().NotBeEmpty();
        invBody.Data!.All(x => x.SoLuongTon == 0).Should().BeTrue();
    }

    #endregion
}
