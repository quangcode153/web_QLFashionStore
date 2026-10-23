using System.Collections.Generic;
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
/// Kịch bản kiểm thử tích hợp P1-5 (Warehouse & Supplier API theo TDD Test-First):
/// - FEAT-INV-01: Lập phiếu nhập kho (POST /api/warehouse/import)
/// - FEAT-INV-02: Cảnh báo tồn kho tối thiểu (GET /api/warehouse/low-stock)
/// - FEAT-INV-03: Danh sách Nhà cung cấp (GET /api/warehouse/suppliers)
/// </summary>
public class WarehouseApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WarehouseApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string GetWarehouseStaffToken() =>
        TestTokenHelper.GenerateToken(3, "warehouse_staff", RoleConstants.ThuKho);

    private string GetCustomerToken() =>
        TestTokenHelper.GenerateToken(1, "customer_test", RoleConstants.KhachHang);

    private string GetAdminToken() =>
        TestTokenHelper.GenerateToken(1, "admin_user", RoleConstants.AdminChuoi);

    #region NHÀ CUNG CẤP & CẢNH BÁO TỒN KHO

    [Fact(DisplayName = "TS-WH-01: Thủ kho gọi GET /api/warehouse/suppliers trả về 200 OK và danh sách NCC")]
    public async Task GetSuppliers_WithStaffToken_Returns200WithSupplierList()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/suppliers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<SupplierDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    [Fact(DisplayName = "TS-WH-02: Thủ kho gọi GET /api/warehouse/low-stock trả về 200 OK")]
    public async Task GetLowStockAlerts_WithStaffToken_Returns200WithAlertList()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/low-stock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<LowStockAlertDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    #endregion

    #region NHẬP KHO & PHÂN QUYỀN RBAC

    [Fact(DisplayName = "TS-WH-03: Thủ kho nhập kho hợp lệ trả về 200 OK")]
    public async Task ImportStock_ValidRequest_Returns200Ok()
    {
        // Arrange
        var importReq = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 3,
            MaBienThe = 1,
            SoLuong = 20,
            DonGiaNhap = 100000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/import");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());
        request.Content = JsonContent.Create(importReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    [Theory(DisplayName = "TS-WH-04: Nhập kho với số lượng <= 0 hoặc đơn giá < 0 phải trả về 400 Bad Request")]
    [InlineData(0, 100000)]
    [InlineData(-5, 100000)]
    [InlineData(10, -50000)]
    public async Task ImportStock_InvalidParameters_Returns400BadRequest(int soLuong, decimal donGia)
    {
        // Arrange
        var importReq = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 3,
            MaBienThe = 1,
            SoLuong = soLuong,
            DonGiaNhap = donGia
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/import");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());
        request.Content = JsonContent.Create(importReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "TS-WH-05: Khách hàng thường gọi POST /api/warehouse/import phải bị 403 Forbidden")]
    public async Task ImportStock_CalledByCustomer_Returns403Forbidden()
    {
        // Arrange
        var importReq = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 1,
            MaBienThe = 1,
            SoLuong = 10,
            DonGiaNhap = 100000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/import");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetCustomerToken());
        request.Content = JsonContent.Create(importReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TS-WH-06: Khách hàng thường gọi GET /api/warehouse/low-stock phải bị 403 Forbidden")]
    public async Task GetLowStock_CalledByCustomer_Returns403Forbidden()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/low-stock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetCustomerToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TS-WH-07: Nhập kho khi MaNhanVien = 0 sẽ tự động trích xuất MaNhanVien từ JWT Token claims")]
    public async Task ImportStock_ZeroStaffId_ExtractsStaffIdFromJwtClaims_Returns200Ok()
    {
        // Arrange: Thủ kho token ID = 3, gửi request với MaNhanVien = 0
        var importReq = new ImportStockRequestDto
        {
            MaNhaCungCap = 1,
            MaNhanVien = 0,
            MaBienThe = 1,
            SoLuong = 15,
            DonGiaNhap = 120000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/import");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());
        request.Content = JsonContent.Create(importReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    [Fact(DisplayName = "TS-WH-08: Gửi request body rỗng khi nhập kho phải trả về 400 Bad Request")]
    public async Task ImportStock_NullBody_Returns400BadRequest()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/import");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());
        request.Content = JsonContent.Create<ImportStockRequestDto?>(null);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "TS-WH-09: Admin gọi POST /api/warehouse/suppliers tạo mới NCC trả về 200 OK và được lưu vào DB")]
    public async Task CreateSupplier_WithAdminToken_Returns200AndCreatesSupplier()
    {
        // Arrange
        var newSupplierName = $"NCC Thử Nghiệm {Guid.NewGuid().ToString().Substring(0, 8)}";
        var newSupplierReq = new CreateSupplierRequestDto
        {
            TenNhaCungCap = newSupplierName,
            DienThoaiLienHe = "0911223344"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());
        request.Content = JsonContent.Create(newSupplierReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SupplierDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.MaNhaCungCap.Should().BeGreaterThan(0);
        body.Data.TenNhaCungCap.Should().Be(newSupplierName);
    }

    [Fact(DisplayName = "TS-WH-10: Khách hàng gọi POST /api/warehouse/suppliers bị chặn 403 Forbidden (Đặc quyền Admin)")]
    public async Task CreateSupplier_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var newSupplierReq = new CreateSupplierRequestDto
        {
            TenNhaCungCap = "NCC Không Hợp Lệ",
            DienThoaiLienHe = "0900000000"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetCustomerToken());
        request.Content = JsonContent.Create(newSupplierReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TS-WH-11: Sau khi tạo NCC mới, gọi GET /api/warehouse/suppliers thấy ngay NCC trong danh sách")]
    public async Task CreateSupplier_ThenGetSuppliers_ReturnsNewlyCreatedSupplier()
    {
        // Arrange
        var uniqueName = $"NCC AutoList {Guid.NewGuid().ToString().Substring(0, 8)}";
        var createReq = new CreateSupplierRequestDto
        {
            TenNhaCungCap = uniqueName,
            DienThoaiLienHe = "0987654321"
        };

        var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/warehouse/suppliers");
        postRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAdminToken());
        postRequest.Content = JsonContent.Create(createReq);
        var postResponse = await _client.SendAsync(postRequest);
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/suppliers");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetWarehouseStaffToken());
        var getResponse = await _client.SendAsync(getRequest);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<SupplierDto>>>();
        getBody.Should().NotBeNull();
        getBody!.Data.Should().Contain(s => s.TenNhaCungCap == uniqueName);
    }

    #endregion
}
