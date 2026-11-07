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

/// <summary>
/// RBAC & Multi-Role Isolation Matrix for Warehouse Supplier Creation (POST /api/warehouse/suppliers)
/// Rule: Chỉ có Admin (Admin chuỗi, Quản lý hệ thống) mới được quyền tạo Nhà cung cấp (nguồn hàng) mới.
/// Thủ kho, Quản lý chi nhánh, Nhân viên, Khách hàng đều bị CHẶN (403 Forbidden).
/// </summary>
public class WarehouseSupplierRoleRbacTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WarehouseSupplierRoleRbacTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string GenerateToken(int id, string username, string role) =>
        TestTokenHelper.GenerateToken(id, username, role);

    #region Vòng 3: RBAC & Multi-Role Isolation Matrix (Cấm Thủ Kho & Quản Lý Chi Nhánh tạo NCC)

    [Theory(DisplayName = "TS-WH-RBAC-01: Các role không phải Admin (Thủ kho, Quản lý chi nhánh, NV, KH) gọi POST suppliers phải bị chặn 403")]
    [InlineData(RoleConstants.ThuKho, "thukho_01")]
    [InlineData(RoleConstants.QuanLyChiNhanh, "qlcn_01")]
    [InlineData(RoleConstants.NhanVien, "nhanvien_01")]
    [InlineData(RoleConstants.KhachHang, "khachhang_01")]
    public async Task CreateSupplier_WithNonAdminRoles_MustReturn403Forbidden(string role, string username)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(10, username, role));
        request.Content = JsonContent.Create(new CreateSupplierRequestDto
        {
            TenNhaCungCap = "Nguồn Hàng Trái Phép",
            DienThoaiLienHe = "0999888777"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, 
            $"Role '{role}' không có thẩm quyền thêm nguồn hàng mới vào hệ thống.");
    }

    [Fact(DisplayName = "TS-WH-RBAC-02: Chưa đăng nhập (Anonymous) gọi POST suppliers phải trả về 401 Unauthorized")]
    public async Task CreateSupplier_Anonymous_MustReturn401Unauthorized()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        request.Content = JsonContent.Create(new CreateSupplierRequestDto
        {
            TenNhaCungCap = "Nguồn Hàng Ẩn Danh",
            DienThoaiLienHe = "0999888777"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory(DisplayName = "TS-WH-RBAC-03: Chỉ Admin chuỗi hoặc Quản lý hệ thống mới được phép tạo NCC (200 OK)")]
    [InlineData(RoleConstants.AdminChuoi, "admin_chuoi")]
    [InlineData(RoleConstants.QuanLyHeThong, "ql_hethong")]
    public async Task CreateSupplier_WithAdminRoles_MustReturn200Ok(string adminRole, string username)
    {
        // Arrange
        var uniqueName = $"Nguồn Hàng Hợp Lệ {System.Guid.NewGuid().ToString().Substring(0, 8)}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(1, username, adminRole));
        request.Content = JsonContent.Create(new CreateSupplierRequestDto
        {
            TenNhaCungCap = uniqueName,
            DienThoaiLienHe = "0912345678"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SupplierDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.TenNhaCungCap.Should().Be(uniqueName);
    }

    #endregion
}

