using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Shared.DTOs.Promotion;
using global::Shared.DTOs.Catalog;
using Xunit;
using Frontend.Components;
using Frontend.Pages;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class CartAndCheckoutComponentTests : BunitContext
{
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<IOrderApiClient> _mockOrderApiClient;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ICatalogApiClient> _mockCatalogApiClient;

    public CartAndCheckoutComponentTests()
    {
        _mockCartService = new Mock<ICartService>();
        _mockOrderApiClient = new Mock<IOrderApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockCatalogApiClient = new Mock<ICatalogApiClient>();

        _mockCatalogApiClient.Setup(c => c.CheckVariantsAvailabilityAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<VariantAvailabilityDto>());

        Services.AddSingleton<ICartService>(_mockCartService.Object);
        Services.AddSingleton<IOrderApiClient>(_mockOrderApiClient.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApiClient.Object);
    }

    private CartItemDto CreateSampleItem(int id = 1, int bienThe = 101, string name = "Áo Thun Polo YaMe", decimal price = 250000m, int qty = 1, int stock = 10)
    {
        return new CartItemDto
        {
            MaSanPham = id,
            TenSanPham = name,
            MaBienThe = bienThe,
            MaSKU = $"POLO-{bienThe}",
            MauSac = "Đen",
            KichCo = "L",
            DonGia = price,
            SoLuong = qty,
            SoLuongTon = stock
        };
    }

    #region 1. CartDrawer Tests (SCR-03)

    [Fact]
    public void CartDrawer_WhenCartEmpty_RendersEmptyMessage()
    {
        _mockCartService.Setup(c => c.Items).Returns(new List<CartItemDto>());
        _mockCartService.Setup(c => c.TotalCount).Returns(0);
        _mockCartService.Setup(c => c.SubTotal).Returns(0m);
        _mockCartService.Setup(c => c.IsDrawerOpen).Returns(true);

        var cut = Render<CartDrawer>();

        cut.Markup.Should().Contain("Giỏ hàng của bạn đang trống");
        cut.Markup.Should().Contain("Khám phá ngay");
    }

    [Fact]
    public void CartDrawer_WhenHasItems_RendersItemDetailsAndSubtotal()
    {
        var items = new List<CartItemDto>
        {
            CreateSampleItem(1, 101, "Áo Thun Polo YaMe", 250000m, 2, 10)
        };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.TotalCount).Returns(2);
        _mockCartService.Setup(c => c.SubTotal).Returns(500000m);
        _mockCartService.Setup(c => c.IsDrawerOpen).Returns(true);

        var cut = Render<CartDrawer>();

        cut.Markup.Should().Contain("Áo Thun Polo YaMe");
        cut.Markup.Should().Contain("500.000");
        cut.Markup.Should().Contain("Đen / L");
    }

    [Fact]
    public void CartDrawer_FreeshipBanner_CalculatesProgressCorrectly()
    {
        // Don hang 30.000d tren nguong freeship 50.000d -> Con thieu 20.000d
        var items = new List<CartItemDto> { CreateSampleItem(price: 30000m, qty: 1) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(30000m);
        _mockCartService.Setup(c => c.IsDrawerOpen).Returns(true);

        var cut = Render<CartDrawer>();

        cut.Markup.Should().Contain("20.000");
    }

    [Fact]
    public async Task CartDrawer_ClickRemoveItem_CallsCartServiceRemove()
    {
        var items = new List<CartItemDto> { CreateSampleItem(1, 101) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.IsDrawerOpen).Returns(true);

        var cut = Render<CartDrawer>();
        var removeBtn = cut.Find(".cart-item-remove-btn");
        await removeBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _mockCartService.Verify(c => c.RemoveItemAsync(101), Times.Once);
    }

    #endregion

    #region 2. Cart Page Tests (Pages/Cart.razor)

    [Fact]
    public void CartPage_WhenEmpty_RendersEmptyPromptAndShoppingButton()
    {
        _mockCartService.Setup(c => c.Items).Returns(new List<CartItemDto>());
        _mockCartService.Setup(c => c.TotalCount).Returns(0);
        _mockCartService.Setup(c => c.SubTotal).Returns(0m);

        var cut = Render<Cart>();

        cut.Markup.Should().Contain("Giỏ hàng của bạn đang trống");
        cut.Find(".btn-continue-shopping").Should().NotBeNull();
    }

    [Fact]
    public void CartPage_WhenHasItems_RendersTableAndCheckoutCTA()
    {
        var items = new List<CartItemDto>
        {
            CreateSampleItem(1, 101, "Quần Jean YaMe F3", 450000m, 1, 5)
        };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.TotalCount).Returns(1);
        _mockCartService.Setup(c => c.SubTotal).Returns(450000m);

        var cut = Render<Cart>();

        cut.Markup.Should().Contain("Quần Jean YaMe F3");
        cut.Markup.Should().Contain("450.000");
        cut.Find(".btn-proceed-checkout").Should().NotBeNull();
    }

    #endregion

    #region 3. Checkout Page Tests (Pages/Checkout.razor - SCR-04)

    [Fact]
    public void CheckoutPage_RendersCustomerFormFieldsAndPaymentOptions()
    {
        var items = new List<CartItemDto> { CreateSampleItem() };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(250000m);

        var cut = Render<Checkout>();

        cut.Find("input[name='fullName']").Should().NotBeNull();
        cut.Find("input[name='phoneNumber']").Should().NotBeNull();
        cut.Find("textarea[name='address']").Should().NotBeNull();
        cut.Markup.Should().Contain("COD");
        cut.Markup.Should().Contain("Chuyển khoản QR");
    }

    [Fact]
    public async Task CheckoutPage_ApplyVoucher_WhenValid_UpdatesDiscountAndTotal()
    {
        var items = new List<CartItemDto> { CreateSampleItem(price: 300000m) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(300000m);

        _mockOrderApiClient.Setup(api => api.ValidateVoucherAsync(It.IsAny<ValidateVoucherRequestDto>()))
            .ReturnsAsync(new ValidateVoucherResponseDto
            {
                Valid = true,
                MaGiamGia = "YAME50K",
                TenKhuyenMai = "Giảm 50K đơn 200K",
                DiscountAmount = 50000m,
                FinalAmount = 250000m
            });

        var cut = Render<Checkout>();
        var voucherInput = cut.Find("input[name='voucherCode']");
        voucherInput.Change("YAME50K");

        var applyBtn = cut.Find(".btn-apply-voucher");
        await applyBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Giảm 50K");
        cut.Markup.Should().Contain("50.000");
        cut.Markup.Should().Contain("250.000");
    }

    [Fact]
    public async Task CheckoutPage_ApplyVoucher_WhenInvalid_ShowsErrorMessage()
    {
        var items = new List<CartItemDto> { CreateSampleItem(price: 100000m) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(100000m);

        _mockOrderApiClient.Setup(api => api.ValidateVoucherAsync(It.IsAny<ValidateVoucherRequestDto>()))
            .ReturnsAsync(new ValidateVoucherResponseDto
            {
                Valid = false,
                Message = "Đơn hàng chưa đạt giá trị tối thiểu 200.000 ₫"
            });

        var cut = Render<Checkout>();
        var voucherInput = cut.Find("input[name='voucherCode']");
        voucherInput.Change("SAIMA");

        var applyBtn = cut.Find(".btn-apply-voucher");
        await applyBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Đơn hàng chưa đạt giá trị tối thiểu");
    }

    [Fact]
    public async Task CheckoutPage_SubmitOrder_ValidData_CreatesOrderAndNavigates()
    {
        var items = new List<CartItemDto> { CreateSampleItem(bienThe: 202, qty: 1) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(250000m);

        _mockOrderApiClient.Setup(api => api.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()))
            .ReturnsAsync(new OrderSummaryDto
            {
                MaHoaDon = 9999,
                TongThanhToan = 250000m,
                TrangThai = "Chờ xử lý"
            });

        var cut = Render<Checkout>();

        cut.Find("input[name='fullName']").Change("Nguyễn Văn A");
        cut.Find("input[name='phoneNumber']").Change("0901234567");
        cut.Find("textarea[name='address']").Change("123 Lê Duẩn, Quận 1, TP.HCM");

        var submitBtn = cut.Find(".btn-submit-order");
        await submitBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _mockOrderApiClient.Verify(api => api.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Once);
        _mockCartService.Verify(c => c.ClearCartAsync(), Times.Once);
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri.Should().Contain("order-success/9999");
    }

    #endregion

    #region 4. OrderSuccess Page Tests (Pages/OrderSuccess.razor - SCR-05)

    [Fact]
    public void OrderSuccessPage_RendersSuccessBannerAndOrderId()
    {
        var cut = Render<OrderSuccess>(parameters => parameters.Add(p => p.OrderId, 10025));

        cut.Markup.Should().Contain("ĐẶT HÀNG THÀNH CÔNG");
        cut.Markup.Should().Contain("#DH-10025");
        cut.Find(".btn-continue-shopping").Should().NotBeNull();
    }

    #endregion

    #region 5. CartService Logic Unit Tests (State Machine & Clamping)

    [Fact]
    public async Task CartService_AddUpdateRemoveClear_WorkflowMaintainsIntegrity()
    {
        var service = new CartService();
        service.Items.Should().BeEmpty();
        service.TotalCount.Should().Be(0);
        service.SubTotal.Should().Be(0m);

        // Add Item
        var item1 = CreateSampleItem(1, 101, "Áo Polo", 200000m, 1, 5);
        await service.AddItemAsync(item1);

        service.Items.Should().HaveCount(1);
        service.TotalCount.Should().Be(1);
        service.SubTotal.Should().Be(200000m);

        // Add Same Item -> Increments quantity up to stock
        await service.AddItemAsync(CreateSampleItem(1, 101, "Áo Polo", 200000m, 2, 5));
        service.Items.Should().HaveCount(1);
        service.TotalCount.Should().Be(3);
        service.SubTotal.Should().Be(600000m);

        // Update quantity
        await service.UpdateQuantityAsync(101, 4);
        service.TotalCount.Should().Be(4);

        // Clamping to stock
        await service.UpdateQuantityAsync(101, 10); // Stock is 5
        service.Items[0].SoLuong.Should().Be(5);

        // Remove item
        await service.RemoveItemAsync(101);
        service.Items.Should().BeEmpty();
        service.TotalCount.Should().Be(0);
        service.SubTotal.Should().Be(0m);
    }

    #endregion

    #region 6. Checkout Validation & Cart Boundary Matrix (Multi-Round Testing)

    [Theory]
    [InlineData("", "0901234567", "123 Lê Lợi", "Vui lòng điền đầy đủ")]
    [InlineData("   ", "0901234567", "123 Lê Lợi", "Vui lòng điền đầy đủ")]
    [InlineData("Nguyễn Văn A", "", "123 Lê Lợi", "Vui lòng điền đầy đủ")]
    [InlineData("Nguyễn Văn A", "0901234567", "", "Vui lòng điền đầy đủ")]
    [InlineData("Nguyễn Văn A", "12345", "123 Lê Lợi", "Số điện thoại không hợp lệ")]
    [InlineData("Nguyễn Văn A", "09012abcde", "123 Lê Lợi", "Số điện thoại không hợp lệ")]
    [InlineData("Nguyễn Văn A", "9901234567", "123 Lê Lợi", "Số điện thoại không hợp lệ")] // Không bắt đầu bằng 0
    [InlineData("Nguyễn Văn A", "09012345678999", "123 Lê Lợi", "Số điện thoại không hợp lệ")] // Quá 11 số
    public async Task CheckoutPage_FormValidationMatrix_RejectsInvalidInputAndBlocksOrder(string name, string phone, string address, string expectedErrorSubstr)
    {
        // Arrange
        var items = new List<CartItemDto> { CreateSampleItem(1, 101, "Áo Polo", 200000m, 1, 10) };
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(200000m);

        var cut = Render<Checkout>();

        cut.Find("input[name='fullName']").Change(name);
        cut.Find("input[name='phoneNumber']").Change(phone);
        cut.Find("textarea[name='address']").Change(address);

        // Act
        var submitBtn = cut.Find(".btn-submit-order");
        await submitBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert: Không được gọi CreateOrderAsync và hiển thị thông báo lỗi tương ứng
        _mockOrderApiClient.Verify(api => api.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Never);
        cut.Markup.Should().Contain(expectedErrorSubstr);
    }

    [Fact]
    public void CheckoutPage_EmptyCart_SubmissionBlockedWithErrorMessage()
    {
        // Arrange
        _mockCartService.Setup(c => c.Items).Returns(new List<CartItemDto>());
        _mockCartService.Setup(c => c.SubTotal).Returns(0m);

        var cut = Render<Checkout>();

        // Assert: Giỏ hàng rỗng hiển thị thông báo và nút quay về cửa hàng, form submit không được render
        cut.Markup.Should().Contain("Giỏ hàng của bạn đang trống");
        cut.Find(".btn-back-catalog").Should().NotBeNull();
        cut.FindAll(".btn-submit-order").Should().BeEmpty();
        _mockOrderApiClient.Verify(api => api.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Never);
    }

    [Theory]
    [InlineData(0, "50", false)]
    [InlineData(25000, "25", false)]
    [InlineData(50000, "", true)]
    [InlineData(120000, "", true)]
    public void CartDrawer_FreeshipThresholdMatrix_CalculatesAccurately(decimal subtotal, string remainingPrefix, bool isFree)
    {
        var items = subtotal > 0 
            ? new List<CartItemDto> { CreateSampleItem(price: subtotal, qty: 1) } 
            : new List<CartItemDto>();
        _mockCartService.Setup(c => c.Items).Returns(items);
        _mockCartService.Setup(c => c.SubTotal).Returns(subtotal);
        _mockCartService.Setup(c => c.IsDrawerOpen).Returns(true);

        var cut = Render<CartDrawer>();

        if (isFree)
        {
            cut.Markup.Should().Contain("FREESHIP toàn quốc");
        }
        else
        {
            cut.Markup.Should().Match(m => m.Contains(remainingPrefix + ".000") || m.Contains(remainingPrefix + ",000"));
        }
    }

    [Theory]
    [InlineData(0, 0)]    // Update về 0 -> Xóa khỏi giỏ
    [InlineData(-5, 0)]   // Update âm -> Xóa hoặc 0
    [InlineData(1, 1)]    // 1 -> Giữ 1
    [InlineData(5, 5)]    // Đạt stock 5 -> Giữ 5
    [InlineData(20, 5)]   // Vượt quá stock 5 -> Kẹp về 5
    public async Task CartService_BoundaryQuantityClamping_GuaranteesValidStock(int requestedQty, int expectedQty)
    {
        var service = new CartService();
        var item = CreateSampleItem(1, 101, "Áo Polo", 200000m, 1, stock: 5);
        await service.AddItemAsync(item);

        await service.UpdateQuantityAsync(101, requestedQty);

        if (expectedQty == 0)
        {
            service.Items.Should().BeEmpty();
        }
        else
        {
            service.Items[0].SoLuong.Should().Be(expectedQty);
        }
    }

    [Fact]
    public void CartPage_WhenItemsContainHiddenVariant_DisplaysUnavailableBadgeAndDisablesCheckout()
    {
        // Arrange
        var item1 = CreateSampleItem(1, 101, "Áo Thun Polo Đang Bán", 200000m, 1, 10);
        var item2 = CreateSampleItem(2, 102, "Áo Khoác Đã Bị Ẩn", 350000m, 1, 5);
        _mockCartService.Setup(c => c.Items).Returns(new List<CartItemDto> { item1, item2 });
        _mockCartService.Setup(c => c.TotalCount).Returns(2);
        _mockCartService.Setup(c => c.SubTotal).Returns(550000m);

        _mockCatalogApiClient.Setup(c => c.CheckVariantsAvailabilityAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<VariantAvailabilityDto>
            {
                new() { MaBienThe = 101, TenSanPham = "Áo Thun Polo Đang Bán", DaAn = false },
                new() { MaBienThe = 102, TenSanPham = "Áo Khoác Đã Bị Ẩn", DaAn = true, ThongBao = "Sản phẩm tạm thời không cung cấp" }
            });

        // Act
        var cut = Render<Cart>();

        // Assert: Hiển thị cảnh báo sản phẩm tạm thời không cung cấp và vô hiệu hóa nút tiến hành đặt hàng
        cut.Markup.Should().Contain("sản phẩm tạm thời không cung cấp");
        cut.Markup.Should().Contain("cart-unavailable-warning-banner");
        cut.Find(".btn-proceed-checkout.disabled").Should().NotBeNull();
        cut.Find(".btn-proceed-checkout").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void CheckoutPage_WhenItemsContainHiddenVariant_BlocksOrderSubmissionAndShowsAlert()
    {
        // Arrange
        var item = CreateSampleItem(2, 102, "Áo Sơ Mi Đã Bị Ẩn", 300000m, 1, 5);
        _mockCartService.Setup(c => c.Items).Returns(new List<CartItemDto> { item });
        _mockCartService.Setup(c => c.TotalCount).Returns(1);
        _mockCartService.Setup(c => c.SubTotal).Returns(300000m);

        _mockCatalogApiClient.Setup(c => c.CheckVariantsAvailabilityAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new List<VariantAvailabilityDto>
            {
                new() { MaBienThe = 102, TenSanPham = "Áo Sơ Mi Đã Bị Ẩn", DaAn = true, ThongBao = "Sản phẩm tạm thời không cung cấp" }
            });

        // Act
        var cut = Render<Checkout>();

        // Assert: Hiển thị cảnh báo giỏ hàng có món tạm ngừng cung cấp và disable nút xác nhận
        cut.Markup.Should().Contain("tạm thời không cung cấp");
        cut.Find(".btn-submit-order").HasAttribute("disabled").Should().BeTrue();
        _mockOrderApiClient.Verify(api => api.CreateOrderAsync(It.IsAny<CreateOrderRequestDto>()), Times.Never);
    }

    #endregion
}
