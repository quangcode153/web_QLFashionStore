using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.IntegrationTests.Catalog;

/// <summary>
/// Integration Tests cho riêng BrandsController (FEAT-CAT-02):
/// - GET /api/brands (Lấy danh sách thương hiệu & nhãn hàng)
/// </summary>
public class BrandsApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BrandsApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "TS-BRD-01: GET /api/brands trả về 200 OK và danh sách thương hiệu")]
    public async Task GetBrands_Returns200WithNonEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/brands");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<BrandDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data.Should().NotBeEmpty("Hệ thống phải có thương hiệu từ seed data");
    }

    [Fact(DisplayName = "TS-BRD-02: Kiểm tra cấu trúc bản ghi thương hiệu có đầy đủ ID và Tên")]
    public async Task GetBrands_RecordsHaveValidProperties()
    {
        // Act
        var response = await _client.GetAsync("/api/brands");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<BrandDto>>>();
        body!.Data.Should().AllSatisfy(brand =>
        {
            brand.MaThuongHieu.Should().BeGreaterThan(0);
            brand.TenThuongHieu.Should().NotBeNullOrWhiteSpace();
        });
    }
}
