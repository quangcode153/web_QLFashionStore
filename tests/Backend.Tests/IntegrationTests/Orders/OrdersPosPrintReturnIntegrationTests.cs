using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Backend.Tests.IntegrationTests.Infrastructure;
using FluentAssertions;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Order;
using Xunit;

namespace Backend.Tests.IntegrationTests.Orders;

/// <summary>
/// Kịch bản kiểm thử tích hợp P1-4 (TDD Test-First):
/// - FEAT-ORD-07: In hóa đơn nhiệt K80 (GET /api/orders/{id}/print)
/// - FEAT-ORD-08: Tiếp nhận đổi trả hàng (POST /api/orders/{id}/return)
/// </summary>
public class OrdersPosPrintReturnIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrdersPosPrintReturnIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string GetStaffToken() =>
        TestTokenHelper.GenerateToken(2, "thungan_test", RoleConstants.ThuNgan);

    private string GetCustomerToken() =>
        TestTokenHelper.GenerateToken(1, "customer_test", RoleConstants.KhachHang);

    #region KỊCH BẢN IN HÓA ĐƠN K80 (FEAT-ORD-07)

    [Fact(DisplayName = "TS-ORD-PRINT-01: Thu ngân gọi GET /api/orders/{id}/print trả về 200 OK và đầy đủ chi tiết K80")]
    public async Task PrintReceipt_WithExistingOrderId_Returns200WithFullReceiptDetails()
    {
        // Arrange: Hóa đơn 1 trong seed data đã tồn tại
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/1/print");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetStaffToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReceiptPrintDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.MaHoaDon.Should().Be(1);
        body.Data.Items.Should().NotBeNull();
        body.Data.Items.Should().NotBeEmpty("Hóa đơn phải có ít nhất 1 dòng sản phẩm");
    }

    [Fact(DisplayName = "TS-ORD-PRINT-02: Gọi GET /api/orders/99999/print không tồn tại phải trả về 404 Not Found")]
    public async Task PrintReceipt_WithNonExistentOrderId_Returns404NotFound()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/99999/print");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetStaffToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region KỊCH BẢN ĐỔI TRẢ HÀNG (FEAT-ORD-08)

    [Fact(DisplayName = "TS-ORD-RET-01: Thu ngân trả hàng hợp lệ trên đơn đã hoàn thành trả về 200 OK")]
    public async Task ReturnOrder_ValidItemOnCompletedOrder_Returns200Ok()
    {
        // Arrange: Hóa đơn 1 trong seed data ở trạng thái 'Đã hoàn thành'
        // Chi tiết hóa đơn 1 có biến thể (ma_bien_the = 1, so_luong = 2)
        var returnReq = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Khách đổi size"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/1/return");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetStaffToken());
        request.Content = JsonContent.Create(returnReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReturnOrderResultDto>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.SoLuongTra.Should().Be(1);
    }

    [Fact(DisplayName = "TS-ORD-RET-02: Trả hàng trên đơn không ở trạng thái 'Đã hoàn thành' phải trả về 400 Bad Request")]
    public async Task ReturnOrder_OnNonCompletedOrder_Returns400BadRequest()
    {
        // Arrange: Tạo đơn mới ở trạng thái 'Chờ xử lý'
        var createOrderReq = new CreateOrderRequestDto
        {
            MaKhachHang = 1,
            MaNhanVien = 2,
            Items = new List<CreateOrderItemDto>
            {
                new() { MaBienThe = 1, SoLuong = 1 }
            }
        };

        var createRes = await _client.PostAsJsonAsync("/api/orders", createOrderReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBody = await createRes.Content.ReadFromJsonAsync<ApiResponse<int>>();
        var pendingOrderId = createdBody!.Data;

        // Act: Cố gắng trả hàng trên đơn vừa tạo (trạng thái Chờ xử lý)
        var returnReq = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Thử trả hàng khi đơn chưa hoàn thành"
        };
        var returnMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{pendingOrderId}/return");
        returnMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetStaffToken());
        returnMessage.Content = JsonContent.Create(returnReq);

        var response = await _client.SendAsync(returnMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Chỉ được phép đổi trả hàng cho hóa đơn đã hoàn thành");
    }

    [Fact(DisplayName = "TS-ORD-RET-03: Trả hàng với số lượng vượt quá số lượng đã mua phải trả về 400 Bad Request")]
    public async Task ReturnOrder_WithQuantityExceedingPurchased_Returns400BadRequest()
    {
        // Arrange: Hóa đơn 1 chỉ mua số lượng = 2 chiếc của biến thể 1
        var returnReq = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 999, // Số lượng quá lớn
            LyDoTra = "Cố tình trả lố số lượng"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/1/return");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetStaffToken());
        request.Content = JsonContent.Create(returnReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Không thể trả nhiều hơn số lượng đã mua");
    }

    [Fact(DisplayName = "TS-ORD-RET-04: Khách hàng thường không có quyền Staff gọi trả hàng phải bị 403 Forbidden")]
    public async Task ReturnOrder_CalledByCustomer_Returns403Forbidden()
    {
        // Arrange
        var returnReq = new ReturnOrderRequestDto
        {
            MaBienThe = 1,
            SoLuongTra = 1,
            LyDoTra = "Khách tự gọi API quầy"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/1/return");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetCustomerToken());
        request.Content = JsonContent.Create(returnReq);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Chỉ nhân viên thu ngân / quản lý mới có quyền lập phiếu đổi trả hàng");
    }

    #endregion
}
