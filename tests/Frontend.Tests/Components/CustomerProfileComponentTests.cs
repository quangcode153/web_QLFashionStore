using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Customer;
using Shared.DTOs.Order;
using Xunit;
using Frontend.Pages.Customer;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class CustomerProfileComponentTests : BunitContext
{
    private readonly Mock<ICustomerApiClient> _mockCustomerApi;
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<IToastService> _mockToastService;

    public CustomerProfileComponentTests()
    {
        _mockCustomerApi = new Mock<ICustomerApiClient>();
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<ICustomerApiClient>(_mockCustomerApi.Object);
        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
    }

    private CustomerProfileDto CreateSampleProfile(string name = "Nguyễn Văn Nam", string rank = "Vàng", decimal discount = 10m, int points = 150, decimal spending = 12000000m)
    {
        return new CustomerProfileDto
        {
            MaKhachHang = 1,
            TenKhachHang = name,
            DienThoai = "0909123456",
            Email = "nam.nguyen@example.com",
            DiemThuong = points,
            MaCapDo = rank == "Vàng" ? 3 : (rank == "Bạc" ? 2 : 1),
            TenCapDo = rank,
            PhanTramGiamGia = discount,
            HeSoTichDiem = rank == "Vàng" ? 1.5m : (rank == "Bạc" ? 1.2m : 1.0m),
            TongChiTieu = spending
        };
    }

    #region 1. Customer Profile Tests (SCR-06)

    [Fact]
    public void ProfilePage_InitialRender_ShowsCustomerVipCardAndDetails()
    {
        var profile = CreateSampleProfile("Nguyễn Văn Nam", "Vàng", 10m, 150, 12000000m);
        _mockCustomerApi.Setup(api => api.GetProfileAsync()).ReturnsAsync(profile);

        var cut = Render<Profile>();

        cut.Markup.Should().Contain("Nguyễn Văn Nam");
        cut.Markup.Should().Contain("0909123456");
        cut.Markup.Should().Contain("THÀNH VIÊN VÀNG");
        cut.Markup.Should().Contain("150");
        cut.Markup.Should().Contain("10%");
        cut.Markup.Should().Contain("12.000.000");
    }

    [Fact]
    public void ProfilePage_ProgressToNextRank_CalculatesRemainingAmount()
    {
        // Khách hàng hạng Thường (đã tiêu 3.000.000đ, ngưỡng Bạc là 5.000.000đ -> còn thiếu 2.000.000đ)
        var profile = CreateSampleProfile("Trần Thị B", "Thường", 0m, 20, 3000000m);
        _mockCustomerApi.Setup(api => api.GetProfileAsync()).ReturnsAsync(profile);

        var cut = Render<Profile>();

        cut.Markup.Should().Contain("2.000.000");
        cut.Markup.Should().Contain("THÀNH VIÊN BẠC");
    }

    [Fact]
    public async Task ProfilePage_ClickEvaluateRank_CallsApiAndShowsSuccessToast()
    {
        var profile = CreateSampleProfile("Lê Hoàng C", "Bạc", 5m, 80, 11000000m);
        _mockCustomerApi.Setup(api => api.GetProfileAsync()).ReturnsAsync(profile);
        _mockCustomerApi.Setup(api => api.EvaluateRankAsync())
            .ReturnsAsync(new RankEvaluationResultDto
            {
                MaKhachHang = 1,
                TenKhachHang = "Lê Hoàng C",
                TongChiTieu = 11000000m,
                CapDoMoi = "Vàng"
            });

        var cut = Render<Profile>();

        var evalBtn = cut.Find(".btn-evaluate-rank");
        await evalBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _mockCustomerApi.Verify(api => api.EvaluateRankAsync(), Times.Once);
        _mockToastService.Verify(t => t.ShowSuccess(It.Is<string>(s => s.Contains("Vàng"))), Times.Once);
    }

    [Fact]
    public void ProfilePage_WhenApiReturnsNull_RendersErrorMessageOrEmpty()
    {
        _mockCustomerApi.Setup(api => api.GetProfileAsync()).ReturnsAsync((CustomerProfileDto?)null);

        var cut = Render<Profile>();

        cut.Markup.Should().Match(m => m.Contains("Không tìm thấy thông tin") || m.Contains("Đang tải"));
    }

    #endregion

    #region 2. Order History & Details Modal Tests (SCR-07)

    [Fact]
    public void OrderHistoryPage_WhenEmpty_RendersEmptyPromptAndExploreButton()
    {
        _mockCustomerApi.Setup(api => api.GetOrderHistoryAsync()).ReturnsAsync(new List<CustomerOrderHistoryItemDto>());

        var cut = Render<OrderHistory>();

        cut.Markup.Should().Contain("Bạn chưa có đơn hàng nào");
        cut.Find(".btn-explore-shop").Should().NotBeNull();
    }

    [Fact]
    public void OrderHistoryPage_WhenHasOrders_RendersTableWithStatusBadges()
    {
        var orders = new List<CustomerOrderHistoryItemDto>
        {
            new() { MaHoaDon = 101, NgayLapHoaDon = DateTime.Now, TienHang = 500000m, TienGiamGia = 50000m, TongThanhToan = 450000m, TrangThai = "Đã hoàn thành", SoMatHang = 2 },
            new() { MaHoaDon = 102, NgayLapHoaDon = DateTime.Now, TienHang = 300000m, TienGiamGia = 0m, TongThanhToan = 300000m, TrangThai = "Chờ xử lý", SoMatHang = 1 }
        };
        _mockCustomerApi.Setup(api => api.GetOrderHistoryAsync()).ReturnsAsync(orders);

        var cut = Render<OrderHistory>();

        cut.Markup.Should().Contain("#DH-101");
        cut.Markup.Should().Contain("#DH-102");
        cut.Markup.Should().Contain("Đã hoàn thành");
        cut.Markup.Should().Contain("Chờ xử lý");
        cut.Markup.Should().Contain("450.000");
    }

    [Fact]
    public async Task OrderHistoryPage_ClickViewDetails_OpensModalWithLineItems()
    {
        var orders = new List<CustomerOrderHistoryItemDto>
        {
            new() { MaHoaDon = 200, NgayLapHoaDon = DateTime.Now, TienHang = 400000m, TienGiamGia = 0m, TongThanhToan = 400000m, TrangThai = "Đã hoàn thành", SoMatHang = 1 }
        };
        var details = new List<OrderDetailItemDto>
        {
            new() { TenSanPham = "Áo Thun YaMe Vintage", MauSac = "Trắng", KichCo = "XL", SoLuong = 2, DonGia = 200000m, ThanhTien = 400000m }
        };

        _mockCustomerApi.Setup(api => api.GetOrderHistoryAsync()).ReturnsAsync(orders);
        _mockCustomerApi.Setup(api => api.GetOrderDetailsAsync(200)).ReturnsAsync(details);

        var cut = Render<OrderHistory>();

        var viewBtn = cut.Find(".btn-view-order-details");
        await viewBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _mockCustomerApi.Verify(api => api.GetOrderDetailsAsync(200), Times.Once);
        cut.Markup.Should().Contain("Áo Thun YaMe Vintage");
        cut.Markup.Should().Contain("Trắng / XL");
        cut.Markup.Should().Contain("400.000");
    }

    [Fact]
    public async Task OrderHistoryPage_CloseModal_RemovesModalFromDom()
    {
        var orders = new List<CustomerOrderHistoryItemDto>
        {
            new() { MaHoaDon = 300, NgayLapHoaDon = DateTime.Now, TienHang = 200000m, TienGiamGia = 0m, TongThanhToan = 200000m, TrangThai = "Chờ xử lý", SoMatHang = 1 }
        };
        _mockCustomerApi.Setup(api => api.GetOrderHistoryAsync()).ReturnsAsync(orders);
        _mockCustomerApi.Setup(api => api.GetOrderDetailsAsync(300)).ReturnsAsync(new List<OrderDetailItemDto>());

        var cut = Render<OrderHistory>();

        // Open modal
        await cut.Find(".btn-view-order-details").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Find(".order-details-modal").Should().NotBeNull();

        // Close modal
        await cut.Find(".btn-close-modal").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.FindAll(".order-details-modal").Should().BeEmpty();
    }

    #endregion

    #region 3. VIP Tier Progress & Boundary Calculation Matrix (Multi-Round Testing)

    [Theory]
    [InlineData("Thường", 0, "5.000.000", "THÀNH VIÊN BẠC")]
    [InlineData("Thường", 3000000, "2.000.000", "THÀNH VIÊN BẠC")]
    [InlineData("Thường", 4500000, "500.000", "THÀNH VIÊN BẠC")]
    [InlineData("Bạc", 6000000, "4.000.000", "THÀNH VIÊN VÀNG")]
    [InlineData("Bạc", 9500000, "500.000", "THÀNH VIÊN VÀNG")]
    [InlineData("Vàng", 15000000, "", "")]
    public void ProfilePage_VipRankProgressMatrix_CalculatesAccurately(string currentRank, decimal spending, string expectedRemainingStr, string expectedNextRankName)
    {
        // Arrange
        var profile = CreateSampleProfile("VIP Matrix User", currentRank, 0m, 10, spending);
        _mockCustomerApi.Setup(api => api.GetProfileAsync()).ReturnsAsync(profile);

        // Act
        var cut = Render<Profile>();

        // Assert
        if (!string.IsNullOrEmpty(expectedNextRankName))
        {
            cut.Markup.Should().Contain(expectedNextRankName);
        }
        else
        {
            cut.Markup.Should().NotContain("đặc quyền cao nhất");
        }

        if (!string.IsNullOrEmpty(expectedRemainingStr))
        {
            cut.Markup.Should().Match(m => m.Contains(expectedRemainingStr) || m.Contains(expectedRemainingStr.Replace(".", ",")));
        }
    }

    #endregion
}
