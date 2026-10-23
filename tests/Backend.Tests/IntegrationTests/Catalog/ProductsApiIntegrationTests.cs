using System.Collections.Generic;
using System.Linq;
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
/// Integration Tests cho riêng ProductsController (FEAT-CAT-03, FEAT-CAT-05, FEAT-CAT-06):
/// - GET /api/products (Phân trang, Lọc đa tiêu chí, Tìm kiếm, Sắp xếp)
/// - GET /api/products/{id}/variants (Danh sách biến thể SKU theo sản phẩm)
/// </summary>
public class ProductsApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region PHÂN TRANG & CATALOG CƠ BẢN

    [Fact(DisplayName = "TS-PROD-01: GET /api/products mặc định trả về 200 OK với phân trang trang 1")]
    public async Task GetProducts_DefaultParameters_Returns200WithPagedResult()
    {
        // Act
        var response = await _client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.PageNumber.Should().Be(1);
        body.Data.PageSize.Should().Be(10);
        body.Data.Items.Should().NotBeNull();
    }

    [Fact(DisplayName = "TS-PROD-02: GET /api/products phân trang trang 1 và trang 2 không trùng lặp (OFFSET...FETCH)")]
    public async Task GetProducts_Pagination_Page1AndPage2_DoNotOverlap()
    {
        // Act: Lấy trang 1 kích thước 2
        var res1 = await _client.GetAsync("/api/products?pageNumber=1&pageSize=2");
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>();
        body1!.Data!.Items.Should().HaveCountLessThanOrEqualTo(2);

        // Act: Lấy trang 2 kích thước 2
        var res2 = await _client.GetAsync("/api/products?pageNumber=2&pageSize=2");
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>();

        // Assert: Nếu có dữ liệu, các sản phẩm ở trang 1 không được trùng với trang 2
        var page1Ids = body1.Data.Items.Select(x => x.MaSanPham).ToList();
        var page2Ids = body2!.Data!.Items.Select(x => x.MaSanPham).ToList();
        page1Ids.Intersect(page2Ids).Should().BeEmpty("Phân trang SQL Server OFFSET...FETCH không được trùng bản ghi giữa các trang");
    }

    [Theory(DisplayName = "TS-PROD-03: Boundary Matrix - Ép biên PageNumber và PageSize an toàn")]
    [InlineData(-1, 10, 1, 10)]
    [InlineData(0, 10, 1, 10)]
    [InlineData(1, 0, 1, 10)]
    [InlineData(1, -5, 1, 10)]
    [InlineData(1, 500, 1, 100)] // Vượt trần 100 bị ép về 100
    public async Task GetProducts_BoundaryParameters_ClampsProperly(
        int inputPage, int inputSize, int expectedPage, int expectedSize)
    {
        // Act
        var response = await _client.GetAsync($"/api/products?pageNumber={inputPage}&pageSize={inputSize}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProductListItemDto>>>();
        body!.Data!.PageNumber.Should().Be(expectedPage);
        body.Data.PageSize.Should().Be(expectedSize);
    }

    #endregion

    #region BIẾN THỂ SẢN PHẨM & SKU

    [Fact(DisplayName = "TS-PROD-04: Lấy danh sách biến thể theo ID sản phẩm hợp lệ trả về 200 OK và thông tin SKU")]
    public async Task GetVariants_ShouldReturn200Ok_WhenProductIdIsValid()
    {
        // Act - Sản phẩm ID = 1 trong seed data
        var response = await _client.GetAsync("/api/products/1/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<ProductVariantDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Should().NotBeEmpty();
    }

    [Theory(DisplayName = "TS-PROD-05: Truy vấn biến thể với ID không tồn tại hoặc ID âm/0 trả về danh sách rỗng an toàn")]
    [InlineData(9999)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetVariants_ShouldReturnEmptyListSafely_WhenProductIdDoesNotExist(int invalidProductId)
    {
        // Act
        var response = await _client.GetAsync($"/api/products/{invalidProductId}/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<ProductVariantDto>>>();
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Data.Should().BeEmpty("Sản phẩm không tồn tại không được sinh biến thể giả tạo");
    }

    [Fact(DisplayName = "TS-PROD-06: ID sản phẩm không phải kiểu số nguyên phải trả về 400 Bad Request")]
    public async Task GetVariants_ShouldReturn400BadRequest_WhenIdIsNotInteger()
    {
        // Act
        var response = await _client.GetAsync("/api/products/not-an-integer/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
            "Tham số ID không hợp lệ trên route phải bị từ chối với mã 400 Bad Request");
    }

    #endregion

    #region TRA CỨU BIẾN THỂ THEO SKU (POS API)

    [Fact(DisplayName = "TS-PROD-07: Tra cứu SKU hợp lệ trả về 200 OK và thông tin chi tiết biến thể")]
    public async Task GetVariantBySku_ValidSku_Returns200WithVariantData()
    {
        // Act
        var response = await _client.GetAsync("/api/products/variant-by-sku?sku=AP-NAM-BLK-M");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductVariantDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.MaSku.Should().Be("AP-NAM-BLK-M");
        body.Data.GiaBan.Should().Be(250000);
        body.Data.MauSac.Should().Be("Đen");
    }

    [Fact(DisplayName = "TS-PROD-08: Tra cứu SKU không tồn tại trả về 404 Not Found")]
    public async Task GetVariantBySku_NonExistentSku_Returns404NotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/products/variant-by-sku?sku=SKU-NOT-EXIST-999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductVariantDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("Không tìm thấy");
    }

    [Theory(DisplayName = "TS-PROD-09: Tra cứu SKU với chuỗi rỗng/space/null trả về 400 Bad Request")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetVariantBySku_EmptyOrWhitespaceSku_Returns400BadRequest(string invalidSku)
    {
        // Act
        var response = await _client.GetAsync($"/api/products/variant-by-sku?sku={invalidSku}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
