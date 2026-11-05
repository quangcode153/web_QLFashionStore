using System.Collections.Generic;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Shared.DTOs.Common;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class ProductManagementVariantQrComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public ProductManagementVariantQrComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        var pagedData = new PagedResult<ProductListItemDto>
        {
            Items = new List<ProductListItemDto>
            {
                new()
                {
                    MaSanPham = 1,
                    TenSanPham = "Áo Thun Cotton Compact 220gsm",
                    TenDanhMuc = "Áo Thun",
                    TenThuongHieu = "QF NOIR",
                    GiaThapNhat = 187000m,
                    GiaCaoNhat = 187000m,
                    TongTonKho = 50,
                    DaXoa = false
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 10
        };

        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(pagedData);

        _mockAdminApi.Setup(api => api.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pagedData);

        _mockAdminApi.Setup(api => api.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto> { new() { MaDanhMuc = 1, TenDanhMuc = "Áo Thun" } });

        _mockAdminApi.Setup(api => api.GetBrandsAsync())
            .ReturnsAsync(new List<BrandDto> { new() { MaThuongHieu = 1, TenThuongHieu = "QF NOIR" } });

        _mockAdminApi.Setup(api => api.GetProductVariantsForAdminAsync(1))
            .ReturnsAsync(new List<ProductVariantDto>
            {
                new()
                {
                    MaBienThe = 101,
                    MaSanPham = 1,
                    TenSanPham = "Áo Thun Cotton Compact 220gsm",
                    MaSku = "POLO-DEN-L",
                    MauSac = "Đen",
                    KichCo = "L",
                    GiaBan = 187000m,
                    SoLuongTon = 25,
                    DaXoa = false
                }
            });

        _mockAdminApi.Setup(api => api.GetProductVariantsAsync(1))
            .ReturnsAsync(new List<ProductVariantDto>
            {
                new()
                {
                    MaBienThe = 101,
                    MaSanPham = 1,
                    TenSanPham = "Áo Thun Cotton Compact 220gsm",
                    MaSku = "POLO-DEN-L",
                    MauSac = "Đen",
                    KichCo = "L",
                    GiaBan = 187000m,
                    SoLuongTon = 25,
                    DaXoa = false
                }
            });
    }

    [Fact(DisplayName = "VÒNG 1: Bảng biến thể hiển thị nút xem mã QR và mở modal in tem nhãn chuẩn")]
    public void ProductManagement_ViewVariants_RendersQrButtonAndStickerModal()
    {
        // Arrange: Đăng nhập quyền Admin Chuỗi
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);

        var cut = Render<ProductManagement>();

        // Act 1: Click xem danh sách biến thể của sản phẩm #1
        var viewVariantsBtn = cut.Find("button.btn-action-ghost");
        viewVariantsBtn.Click();

        // Assert 1: Modal danh sách biến thể hiển thị có nút QR
        cut.Find(".variant-table").Should().NotBeNull();
        var qrBtn = cut.Find("button.btn-qr-icon-action");
        qrBtn.Should().NotBeNull();

        // Act 2: Click nút xem mã QR của biến thể POLO-DEN-L
        qrBtn.Click();

        // Assert 2: Modal tem mã QR mở ra với thông tin chi tiết
        var stickerCard = cut.Find(".qr-sticker-card");
        stickerCard.Should().NotBeNull();
        cut.Markup.Should().Contain("QF NOIR APPAREL");
        cut.Markup.Should().Contain("Áo Thun Cotton Compact 220gsm");
        cut.Markup.Should().Contain("POLO-DEN-L");
        cut.Markup.Should().Contain("Đen");
        cut.Markup.Should().Contain("L");
        cut.Markup.Should().Contain("187.000 đ");

        // Ảnh QR có URL trích xuất từ BarcodeParser
        var qrImg = cut.Find(".qr-sticker-card img");
        qrImg.GetAttribute("src").Should().Contain("POLO-DEN-L");

        // Act 3: Click nút đóng modal QR
        var closeBtn = cut.Find(".modal-panel-sm .btn-modal-cancel");
        closeBtn.Click();

        // Assert 3: Modal QR đã đóng
        cut.FindAll(".modal-panel-sm").Should().BeEmpty();
    }
}
