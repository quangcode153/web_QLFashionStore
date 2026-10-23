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
/// Integration Tests cho riêng CategoriesController (FEAT-CAT-01):
/// - GET /api/categories (Lấy danh mục sản phẩm phục vụ điều hướng menu Storefront)
/// </summary>
public class CategoriesApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CategoriesApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "TS-CAT-01: GET /api/categories trả về 200 OK và danh sách danh mục hoạt động")]
    public async Task GetCategories_Returns200WithNonEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<CategoryDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data.Should().NotBeEmpty("Hệ thống phải có danh mục sản phẩm từ seed data");
    }

    [Fact(DisplayName = "TS-CAT-02: Kiểm tra cấu trúc bản ghi danh mục có đầy đủ ID và Tên")]
    public async Task GetCategories_RecordsHaveValidProperties()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<CategoryDto>>>();
        body!.Data.Should().AllSatisfy(cat =>
        {
            cat.MaDanhMuc.Should().BeGreaterThan(0);
            cat.TenDanhMuc.Should().NotBeNullOrWhiteSpace();
        });
    }
}
