using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;
using Xunit;
using Frontend.Pages.Pos;
using Frontend.Components;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class PosCashierComponentTests : BunitContext
{
    private readonly Mock<IPosApiClient> _mockPosApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public PosCashierComponentTests()
    {
        _mockPosApi = new Mock<IPosApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();
        _mockAuthState = new Mock<IAuthStateService>();

        Services.AddSingleton<IPosApiClient>(_mockPosApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);
    }

    private List<ProductListItemDto> CreateSampleFastSaleProducts()
    {
        return new List<ProductListItemDto>
        {
            new() { MaSanPham = 1, TenSanPham = "Áo Thun Cotton Basic", GiaThapNhat = 150000m, GiaCaoNhat = 150000m, TongTonKho = 50 },
            new() { MaSanPham = 2, TenSanPham = "Quần Jean Slimfit", GiaThapNhat = 350000m, GiaCaoNhat = 350000m, TongTonKho = 30 }
        };
    }

    private ProductVariantDto CreateSampleVariant(string sku = "AT-COT-01", decimal price = 150000m)
    {
        return new ProductVariantDto
        {
            MaBienThe = 101,
            MaSanPham = 1,
            TenSanPham = "Áo Thun Cotton Basic",
            MaSku = sku,
            MauSac = "Đen",
            KichCo = "L",
            GiaBan = price,
            SoLuongTon = 20,
            TinhTrangHang = "Còn hàng"
        };
    }

    private CustomerProfileDto CreateSampleCustomer(string phone = "0987654321")
    {
        return new CustomerProfileDto
        {
            MaKhachHang = 10,
            TenKhachHang = "Trần Thị VIP",
            DienThoai = phone,
            Email = "vip@example.com",
            DiemThuong = 250,
            MaCapDo = 2,
            TenCapDo = "Bạc",
            PhanTramGiamGia = 5m,
            HeSoTichDiem = 1.2m,
            TongChiTieu = 5500000m
        };
    }

    private ReceiptPrintDto CreateSampleReceipt(int orderId = 1001)
    {
        return new ReceiptPrintDto
        {
            MaHoaDon = orderId,
            NgayLapHoaDon = new DateTime(2026, 10, 8, 14, 30, 0),
            TenKhachHang = "Trần Thị VIP",
            SdtKhachHang = "0987654321",
            ThuNgan = "Thu Ngân 01",
            TenKhuyenMai = "VIP 5%",
            TienHang = 300000m,
            TienGiamGia = 15000m,
            TongThanhToan = 285000m,
            TrangThai = "Đã thanh toán",
            Items = new List<ReceiptItemDto>
            {
                new() { TenSanPham = "Áo Thun Cotton Basic", MauSac = "Đen", KichCo = "L", SoLuong = 2, DonGia = 150000m, ThanhTien = 300000m }
            }
        };
    }

    #region Vòng 1 & 2: PosCashier Component Tests (SCR-08)

    [Fact]
    public void PosCashier_InitialRender_ShowsBarcodeSearchAndFastSaleSection()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(CreateSampleFastSaleProducts());

        var cut = Render<PosCashier>();

        cut.Markup.Should().Contain("THU NGÂN BÁN LẺ");
        cut.Find("input[placeholder*='mã vạch']").Should().NotBeNull();
        cut.Markup.Should().Contain("Áo Thun Cotton Basic");
        cut.Markup.Should().Contain("Quần Jean Slimfit");
    }

    [Fact]
    public void PosCashier_SearchSku_AddsProductVariantToCart()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-COT-01")).ReturnsAsync(CreateSampleVariant("AT-COT-01", 150000m));

        var cut = Render<PosCashier>();

        var skuInput = cut.Find("input[placeholder*='mã vạch']");
        skuInput.Change("AT-COT-01");
        var searchBtn = cut.Find("button.btn-search-sku");
        searchBtn.Click();

        cut.Markup.Should().Contain("AT-COT-01");
        cut.Markup.Should().Contain("Áo Thun Cotton Basic");
        cut.Markup.Should().Contain("150.000");
    }

    [Fact]
    public void PosCashier_AdjustQuantity_UpdatesSubtotalAndTotal()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-COT-01")).ReturnsAsync(CreateSampleVariant("AT-COT-01", 150000m));

        var cut = Render<PosCashier>();

        var skuInput = cut.Find("input[placeholder*='mã vạch']");
        skuInput.Change("AT-COT-01");
        cut.Find("button.btn-search-sku").Click();

        // Tăng số lượng lên 2
        var btnPlus = cut.Find("button.btn-qty-plus");
        btnPlus.Click();

        cut.Markup.Should().Contain("300.000"); // 150.000 x 2
    }

    [Fact]
    public void PosCashier_LookupCustomerByPhone_ShowsCustomerInfoAndDiscount()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.LookupCustomerByPhoneAsync("0987654321")).ReturnsAsync(CreateSampleCustomer("0987654321"));

        var cut = Render<PosCashier>();

        var phoneInput = cut.Find("input[placeholder*='số điện thoại']");
        phoneInput.Change("0987654321");
        cut.Find("button.btn-lookup-customer").Click();

        cut.Markup.Should().Contain("Trần Thị VIP");
        cut.Markup.Should().Contain("Bạc");
        cut.Markup.Should().Contain("5%");
    }

    [Theory]
    [InlineData(200000, 200000, 0)]
    [InlineData(200000, 300000, 100000)]
    [InlineData(200000, 150000, 0)] // Thiếu tiền, tiền thừa là 0
    public void PosCashier_CalculateChange_CalculatesChangeGivenByCashCustomer(decimal totalPayable, decimal cashGiven, decimal expectedChange)
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-01")).ReturnsAsync(CreateSampleVariant("AT-01", totalPayable));

        var cut = Render<PosCashier>();

        // Thêm 1 item với giá totalPayable
        cut.Find("input[placeholder*='mã vạch']").Change("AT-01");
        cut.Find("button.btn-search-sku").Click();

        // Nhập tiền khách đưa
        var cashInput = cut.Find("input.input-cash-given");
        cashInput.Change(cashGiven.ToString());

        var changeElement = cut.Find(".pos-change-amount");
        var formattedExpected = expectedChange.ToString("N0").Replace(",", ".");
        changeElement.TextContent.Should().Contain(formattedExpected);
    }

    [Fact]
    public void PosCashier_EmptyCart_PaymentButtonDisabled()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());

        var cut = Render<PosCashier>();

        var payBtn = cut.Find("button.btn-pos-pay");
        payBtn.Attributes.GetNamedItem("disabled").Should().NotBeNull();
    }

    [Fact]
    public void PosCashier_CheckoutSuccess_OpensPosPrintModalWithReceiptData()
    {
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-COT-01")).ReturnsAsync(CreateSampleVariant("AT-COT-01", 150000m));
        _mockPosApi.Setup(api => api.CreatePosOrderAsync(It.IsAny<CreateOrderRequestDto>()))
            .ReturnsAsync(new OrderSummaryDto { MaHoaDon = 1001, TongThanhToan = 150000m });
        _mockPosApi.Setup(api => api.GetReceiptPrintAsync(1001)).ReturnsAsync(CreateSampleReceipt(1001));

        var cut = Render<PosCashier>();

        // Thêm món vào đơn
        cut.Find("input[placeholder*='mã vạch']").Change("AT-COT-01");
        cut.Find("button.btn-search-sku").Click();

        // Khách đưa tiền
        cut.Find("input.input-cash-given").Change("200000");

        // Bấm thanh toán
        var payBtn = cut.Find("button.btn-pos-pay");
        payBtn.Click();

        // Kiểm tra modal in hóa đơn xuất hiện
        cut.Find(".pos-receipt-k80").Should().NotBeNull();
        cut.Markup.Should().Contain("HÓA ĐƠN THANH TOÁN");
        cut.Markup.Should().Contain("1001");
    }

    #endregion

    #region Vòng 3: PosPrintModal Component Tests (CMP-05)

    [Fact]
    public void PosPrintModal_WhenVisible_RendersK80ReceiptLayout()
    {
        var receipt = CreateSampleReceipt(2002);

        var cut = Render<PosPrintModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Receipt, receipt));

        cut.Markup.Should().Contain("FASHION STORE");
        cut.Markup.Should().Contain("HÓA ĐƠN THANH TOÁN");
        cut.Markup.Should().Contain("2002");
        cut.Markup.Should().Contain("Áo Thun Cotton Basic");
        cut.Markup.Should().Contain("285.000"); // Tổng thanh toán
        cut.Markup.Should().Contain("IN HÓA ĐƠN");
    }

    [Fact]
    public void PosPrintModal_WhenNotVisible_DoesNotRenderReceipt()
    {
        var cut = Render<PosPrintModal>(parameters => parameters
            .Add(p => p.IsVisible, false)
            .Add(p => p.Receipt, null));

        cut.FindAll(".pos-receipt-k80").Should().BeEmpty();
    }

    [Fact]
    public void PosPrintModal_ClickClose_InvokesOnCloseCallback()
    {
        var receipt = CreateSampleReceipt(3003);
        var wasClosed = false;

        var cut = Render<PosPrintModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Receipt, receipt)
            .Add(p => p.OnClose, () => { wasClosed = true; }));

        var closeBtn = cut.Find("button.btn-modal-close");
        closeBtn.Click();

        wasClosed.Should().BeTrue();
    }

    #endregion

    #region Vòng 4 & 5: SKU Aggregation & Advanced Financial Calculations (Multi-Round Testing)

    [Fact]
    public void PosCashier_ScanSameSkuMultipleTimes_AggregatesQuantityIntoSingleRow()
    {
        // Arrange
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-COT-01")).ReturnsAsync(CreateSampleVariant("AT-COT-01", 100000m));

        var cut = Render<PosCashier>();

        // Act: Quét 3 lần liên tiếp mã AT-COT-01
        for (int i = 0; i < 3; i++)
        {
            var skuInput = cut.Find("input[placeholder*='mã vạch']");
            skuInput.Change("AT-COT-01");
            cut.Find("button.btn-search-sku").Click();
        }

        // Assert: Chỉ có 1 hàng dữ liệu nhưng số lượng là 3, thành tiền 300.000đ
        var rows = cut.FindAll("tr.cart-item-row");
        rows.Should().HaveCount(1);
        cut.Find(".qty-val").TextContent.Trim().Should().Be("3");
        cut.Find(".item-total-col").TextContent.Should().Contain("300.000");
    }

    [Fact]
    public void PosCashier_ScanNonExistentSku_ShowsToastWarningAndDoesNotAddToCart()
    {
        // Arrange
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("UNKNOWN-SKU")).ReturnsAsync((ProductVariantDto?)null);

        var cut = Render<PosCashier>();

        // Act
        cut.Find("input[placeholder*='mã vạch']").Change("UNKNOWN-SKU");
        cut.Find("button.btn-search-sku").Click();

        // Assert
        cut.FindAll("tr.cart-item-row").Should().BeEmpty();
        _mockToastService.Verify(t => t.ShowWarning(It.Is<string>(s => s.Contains("UNKNOWN-SKU"))), Times.Once);
    }

    [Theory]
    [InlineData("Vàng", 10, 500000, 50000, 450000, "500000", 50000)]
    [InlineData("Bạc", 5, 200000, 10000, 190000, "200000", 10000)]
    [InlineData("Thường", 0, 300000, 0, 300000, "250000", 0)] // Khách trả thiếu -> Tiền thừa = 0
    [InlineData("Thường", 0, 300000, 0, 300000, "invalid_text", 0)] // Parse lỗi -> Tiền thừa = 0
    public void PosCashier_VipDiscountAndChangeMatrix_CalculatesAccurately(
        string rank, decimal discountPct, decimal subtotal, decimal expectedDiscount, decimal expectedGrandTotal, string cashGivenStr, decimal expectedChange)
    {
        // Arrange
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("SKU-1")).ReturnsAsync(CreateSampleVariant("SKU-1", subtotal));
        _mockPosApi.Setup(api => api.LookupCustomerByPhoneAsync("0987654321")).ReturnsAsync(new CustomerProfileDto
        {
            MaKhachHang = 1,
            TenKhachHang = "VIP Test",
            TenCapDo = rank,
            PhanTramGiamGia = discountPct
        });

        var cut = Render<PosCashier>();

        // Thêm sản phẩm
        cut.Find("input[placeholder*='mã vạch']").Change("SKU-1");
        cut.Find("button.btn-search-sku").Click();

        // Tra cứu VIP
        cut.Find("input[placeholder*='số điện thoại']").Change("0987654321");
        cut.Find("button.btn-lookup-customer").Click();

        // Khách đưa tiền
        cut.Find("input.input-cash-given").Change(cashGivenStr);

        // Assert: Tiền thừa, chiết khấu và tổng thanh toán đúng theo ma trận
        var changeText = cut.Find(".pos-change-amount").TextContent;
        var formattedExpectedChange = expectedChange.ToString("N0").Replace(",", ".");
        changeText.Should().Contain(formattedExpectedChange);

        if (expectedDiscount > 0)
        {
            var formattedDiscount = expectedDiscount.ToString("N0").Replace(",", ".");
            cut.Markup.Should().Contain(formattedDiscount);
        }

        var formattedGrandTotal = expectedGrandTotal.ToString("N0").Replace(",", ".");
        cut.Markup.Should().Contain(formattedGrandTotal);
    }

    [Fact]
    public void PosCashier_DeleteCartItem_UpdatesCartAndDisablesPayButtonWhenEmpty()
    {
        // Arrange
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(new List<ProductListItemDto>());
        _mockPosApi.Setup(api => api.SearchSkuAsync("AT-COT-01")).ReturnsAsync(CreateSampleVariant("AT-COT-01", 150000m));

        var cut = Render<PosCashier>();

        cut.Find("input[placeholder*='mã vạch']").Change("AT-COT-01");
        cut.Find("button.btn-search-sku").Click();

        cut.FindAll("tr.cart-item-row").Should().HaveCount(1);
        cut.Find("button.btn-pos-pay").Attributes.GetNamedItem("disabled").Should().BeNull();

        // Act: Xóa món
        cut.Find("button.btn-del-item").Click();

        // Assert: Giỏ hàng rỗng, nút thanh toán disabled trở lại
        cut.FindAll("tr.cart-item-row").Should().BeEmpty();
        cut.Find("button.btn-pos-pay").Attributes.GetNamedItem("disabled").Should().NotBeNull();
    }

    [Fact(DisplayName = "VÒNG 2: Click sản phẩm bán nhanh phải tra cứu và lấy đúng MaBienThe thực tế (101), không được gán MaSanPham (1)")]
    public void PosCashier_FastSaleSelect_AssignsRealVariantId_NotProductId()
    {
        // Arrange
        var fastSaleProducts = CreateSampleFastSaleProducts(); // Product 1: MaSanPham = 1
        var sampleVariant = CreateSampleVariant("AT-COT-01", 150000m); // MaBienThe = 101, MaSanPham = 1
        _mockPosApi.Setup(api => api.GetFastSaleProductsAsync()).ReturnsAsync(fastSaleProducts);
        _mockPosApi.Setup(api => api.GetProductVariantsAsync(1)).ReturnsAsync(new List<ProductVariantDto> { sampleVariant });

        CreateOrderRequestDto? capturedOrderReq = null;
        _mockPosApi.Setup(api => api.CreatePosOrderAsync(It.IsAny<CreateOrderRequestDto>()))
            .Callback<CreateOrderRequestDto>(req => capturedOrderReq = req)
            .ReturnsAsync(new OrderSummaryDto { MaHoaDon = 999, TongThanhToan = 150000m });
        _mockPosApi.Setup(api => api.GetReceiptPrintAsync(999)).ReturnsAsync(new ReceiptPrintDto { MaHoaDon = 999 });

        var cut = Render<PosCashier>();

        // Act: Click thẻ sản phẩm bán nhanh đầu tiên (Áo Thun Cotton Basic, MaSanPham = 1)
        var firstTile = cut.Find("div.fast-sale-tile");
        firstTile.Click();

        // Nhập tiền khách đưa đủ và thanh toán
        cut.Find("input.input-cash-given").Change("200000");
        cut.Find("button.btn-pos-pay").Click();

        // Assert: Đơn hàng gửi xuống Backend PHẢI có MaBienThe == 101 (chứ KHÔNG PHẢI 1)
        capturedOrderReq.Should().NotBeNull();
        capturedOrderReq!.Items.Should().HaveCount(1);
        capturedOrderReq.Items[0].MaBienThe.Should().Be(101, "MaBienThe phải là ID biến thể thực tế (101), tuyệt đối không được gán MaSanPham (1)");
    }

    #endregion
}
