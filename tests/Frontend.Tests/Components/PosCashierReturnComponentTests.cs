using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;
using Xunit;
using Frontend.Pages.Pos;
using Frontend.Components;
using Frontend.Services;

namespace Frontend.Tests.Components;

/// <summary>
/// Component Tests cho Bước 5: Giao diện Thu ngân POS - Nhập hàng trả tại quầy (PosCashier.razor & PosReturnModal).
/// Tuân thủ quy chuẩn Multi-Round Testing Standard (Rule 6, Rule 8, Rule 9):
/// - Vòng 1: Boundary & UI Render Matrix (Nút F3, modal tra cứu, validation số lượng trả)
/// - Vòng 2: State Machine & Return Workflow (Tra cứu -> Hiển thị mặt hàng -> Chọn số lượng/lý do -> Hoàn tiền & Gọi API nhập kho lỗi)
/// - Vòng 3: Business Validation & Ineligible Defense (Đơn chưa hoàn thành, trả vượt quá số lượng mua)
/// </summary>
public class PosCashierReturnComponentTests : BunitContext
{
    private readonly Mock<IPosApiClient> _mockPosApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public PosCashierReturnComponentTests()
    {
        _mockPosApi = new Mock<IPosApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();
        _mockAuthState = new Mock<IAuthStateService>();

        _mockPosApi.Setup(x => x.GetFastSaleProductsAsync())
            .ReturnsAsync(new List<ProductListItemDto>());

        Services.AddSingleton<IPosApiClient>(_mockPosApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private OrderReturnLookupDto CreateSampleReturnLookupDto(int orderId = 101, bool eligible = true)
    {
        return new OrderReturnLookupDto
        {
            MaHoaDon = orderId,
            NgayLapHoaDon = DateTime.Now.AddDays(-2),
            TenKhachHang = "Nguyễn Văn An",
            SdtKhachHang = "0909123456",
            ThuNgan = "Lê Văn Cường",
            TienHang = 500000m,
            TienGiamGia = 50000m,
            TongThanhToan = 450000m,
            TrangThai = eligible ? "Đã hoàn thành" : "Đang xử lý",
            IneligibilityReason = eligible ? null : "Chỉ những đơn hàng ở trạng thái 'Đã hoàn thành' mới có thể đổi trả.",
            DiaChiChiNhanhHen = "772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM",
            TenChiNhanhHen = "QF Sư Vạn Hạnh",
            DiaChiNhanHangHoanVe = "772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM (QF Sư Vạn Hạnh)",
            DiaChiThuHoi = "123 Cách Mạng Tháng 8, P.11, Q.3, TP.HCM",
            HinhThucHoan = "Đến chi nhánh",
            Items = new List<OrderReturnLookupItemDto>
            {
                new()
                {
                    MaBienThe = 201,
                    TenSanPham = "Áo Polo Pima Cotton",
                    MauSac = "Xanh Navy",
                    KichCo = "L",
                    DonGia = 250000m,
                    SoLuongMua = 2,
                    SoLuongDaTra = 0
                },
                new()
                {
                    MaBienThe = 202,
                    TenSanPham = "Quần Short Kaki",
                    MauSac = "Be",
                    KichCo = "XL",
                    DonGia = 250000m,
                    SoLuongMua = 1,
                    SoLuongDaTra = 1 // Đã trả hết
                }
            }
        };
    }

    #region VÒNG 1: UI RENDER & NÚT THAO TÁC F3 (BOUNDARY & RENDER MATRIX)

    [Fact(DisplayName = "VÒNG 1: Giao diện POS hiển thị nút 'Nhập hàng trả (F3)'")]
    public void PosCashier_ShouldRenderReturnButton()
    {
        // Act
        var cut = Render<PosCashier>();

        // Assert
        var returnBtn = cut.Find("button.btn-pos-return");
        returnBtn.Should().NotBeNull();
        returnBtn.TextContent.Should().Contain("Nhập hàng trả");
    }

    [Fact(DisplayName = "VÒNG 1: Nhấn nút 'Nhập hàng trả (F3)' sẽ mở Modal Nhập Hàng Trả")]
    public void PosCashier_ClickReturnButton_OpensReturnModal()
    {
        // Arrange
        var cut = Render<PosCashier>();

        // Act
        var returnBtn = cut.Find("button.btn-pos-return");
        returnBtn.Click();

        // Assert
        cut.Find(".pos-return-modal").Should().NotBeNull();
        cut.Markup.Should().Contain("NHẬP HÀNG TRẢ & ĐỔI TRẢ TẠI QUẦY");
    }

    #endregion

    #region VÒNG 2: STATE MACHINE TRA CỨU & XỬ LÝ ĐỔI TRẢ (RETURN PIPELINE)

    [Fact(DisplayName = "VÒNG 2: Tra cứu mã hóa đơn hợp lệ hiển thị thông tin hóa đơn và các sản phẩm còn có thể trả")]
    public async Task PosReturnModal_LookupValidOrder_DisplaysItemsAndRemainingQuantities()
    {
        // Arrange
        var sampleOrder = CreateSampleReturnLookupDto(101, eligible: true);
        _mockPosApi.Setup(x => x.GetOrderForReturnLookupAsync(101))
            .ReturnsAsync(sampleOrder);

        var cut = Render<PosCashier>();
        cut.Find("button.btn-pos-return").Click();

        // Act - Nhập mã hóa đơn và bấm Tra cứu
        var inputOrderId = cut.Find("input.input-lookup-order-id");
        inputOrderId.Change("101");

        var btnLookup = cut.Find("button.btn-lookup-order");
        await cut.InvokeAsync(() => btnLookup.Click());

        // Assert
        cut.Markup.Should().Contain("#HD-101");
        cut.Markup.Should().Contain("Nguyễn Văn An");
        cut.Markup.Should().Contain("Áo Polo Pima Cotton");
        cut.Markup.Should().Contain("Xanh Navy");
        cut.Markup.Should().Contain("Quần Short Kaki");

        // Mặt hàng 201 có SoLuongConLai = 2 -> có checkbox chọn trả
        cut.Find("input[data-variant-id='201']").Should().NotBeNull();

        // Mặt hàng 202 đã trả hết (SoLuongConLai = 0) -> hiển thị badge đã trả đủ
        cut.Markup.Should().Contain("Đã trả đủ");

        // Kiểm tra thông tin địa chỉ nhận hàng lỗi hoàn về hiển thị đầy đủ
        cut.Markup.Should().Contain("ĐỊA CHỈ TIẾP NHẬN HÀNG LỖI HOÀN VỀ");
        cut.Markup.Should().Contain("772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM (QF Sư Vạn Hạnh)");
        cut.Markup.Should().Contain("123 Cách Mạng Tháng 8, P.11, Q.3, TP.HCM");
        cut.Markup.Should().Contain("Đến chi nhánh");
    }

    [Fact(DisplayName = "VÒNG 2: Chọn sản phẩm, nhập số lượng & lý do, nhấn xác nhận sẽ gọi API trả hàng và hiển thị Toast")]
    public async Task PosReturnModal_SubmitReturn_CallsApiAndShowsSuccessToast()
    {
        // Arrange
        var sampleOrder = CreateSampleReturnLookupDto(101, eligible: true);
        _mockPosApi.Setup(x => x.GetOrderForReturnLookupAsync(101))
            .ReturnsAsync(sampleOrder);

        _mockPosApi.Setup(x => x.ProcessReturnAsync(101, It.IsAny<ReturnOrderRequestDto>()))
            .ReturnsAsync(ApiResponse<int>.Ok(999, "Trả hàng thành công"));

        var cut = Render<PosCashier>();
        cut.Find("button.btn-pos-return").Click();

        cut.Find("input.input-lookup-order-id").Change("101");
        await cut.InvokeAsync(() => cut.Find("button.btn-lookup-order").Click());

        // Act - Chọn mặt hàng 201
        var chkItem = cut.Find("input[data-variant-id='201']");
        chkItem.Change(true);

        // Nhập số lượng trả = 1
        var qtyInput = cut.Find("input.input-return-qty[data-variant-id='201']");
        qtyInput.Change("1");

        // Nhập lý do trả
        var reasonInput = cut.Find("input.input-return-reason[data-variant-id='201']");
        reasonInput.Change("Khách thử bị chật vai");

        // Bấm Xác nhận trả hàng
        var btnConfirm = cut.Find("button.btn-confirm-return");
        await cut.InvokeAsync(() => btnConfirm.Click());

        // Assert
        _mockPosApi.Verify(x => x.ProcessReturnAsync(101, It.Is<ReturnOrderRequestDto>(dto =>
            dto.MaBienThe == 201 &&
            dto.SoLuongTra == 1 &&
            dto.LyDoTra != null && dto.LyDoTra.Contains("chật vai")
        )), Times.Once);

        _mockToastService.Verify(x => x.ShowSuccess(
            It.Is<string>(msg => msg.Contains("thành công"))
        ), Times.Once);
    }

    #endregion

    #region VÒNG 3: VALIDATION BIÊN & CHỐNG TRẢ HÓA ĐƠN KHÔNG HỢP LỆ (DEFENSE MATRIX)

    [Fact(DisplayName = "VÒNG 3: Đơn hàng chưa 'Đã hoàn thành' sẽ hiển thị cảnh báo từ chối đổi trả")]
    public async Task PosReturnModal_IneligibleOrder_ShowsIneligibleAlert()
    {
        // Arrange
        var sampleOrder = CreateSampleReturnLookupDto(102, eligible: false);
        _mockPosApi.Setup(x => x.GetOrderForReturnLookupAsync(102))
            .ReturnsAsync(sampleOrder);

        var cut = Render<PosCashier>();
        cut.Find("button.btn-pos-return").Click();

        cut.Find("input.input-lookup-order-id").Change("102");
        await cut.InvokeAsync(() => cut.Find("button.btn-lookup-order").Click());

        // Assert
        cut.Markup.Should().Contain("không đủ điều kiện đổi trả");
        cut.FindAll("button.btn-confirm-return").Should().BeEmpty();
    }

    [Fact(DisplayName = "VÒNG 3: Chặn trả hàng khi chưa chọn sản phẩm nào")]
    public async Task PosReturnModal_NoItemSelected_BlocksSubmit()
    {
        // Arrange
        var sampleOrder = CreateSampleReturnLookupDto(101, eligible: true);
        _mockPosApi.Setup(x => x.GetOrderForReturnLookupAsync(101))
            .ReturnsAsync(sampleOrder);

        var cut = Render<PosCashier>();
        cut.Find("button.btn-pos-return").Click();

        cut.Find("input.input-lookup-order-id").Change("101");
        await cut.InvokeAsync(() => cut.Find("button.btn-lookup-order").Click());

        // Không tick chọn checkbox nào -> nút Xác nhận bị disabled
        var btnConfirm = cut.Find("button.btn-confirm-return");
        btnConfirm.HasAttribute("disabled").Should().BeTrue();
    }

    #endregion
}
