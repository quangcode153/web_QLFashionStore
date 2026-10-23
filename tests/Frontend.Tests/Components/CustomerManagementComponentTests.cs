using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Common;
using Shared.DTOs.Customer;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class CustomerManagementComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public CustomerManagementComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        _mockAdminApi.Setup(api => api.GetAllCustomersAsync(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<bool?>()))
            .ReturnsAsync(CreateSampleCustomers());
    }

    private List<CustomerAccountDto> CreateSampleCustomers()
    {
        return new List<CustomerAccountDto>
        {
            new CustomerAccountDto
            {
                MaKhachHang = 1,
                TenKhachHang = "Nguyễn Văn Tuấn",
                TenDangNhap = "tuan.nguyen",
                DienThoai = "0901234567",
                Email = "tuan@example.com",
                DiemThuong = 250,
                MaCapDo = 3,
                TenCapDo = "Vàng",
                PhanTramGiamGia = 10m,
                TongChiTieu = 15000000m,
                TongSoDonHang = 8,
                DaXoa = false
            },
            new CustomerAccountDto
            {
                MaKhachHang = 2,
                TenKhachHang = "Lê Thị Hồng",
                TenDangNhap = "hong.le",
                DienThoai = "0912345678",
                Email = "hong@example.com",
                DiemThuong = 50,
                MaCapDo = 1,
                TenCapDo = "Đồng",
                PhanTramGiamGia = 0m,
                TongChiTieu = 1200000m,
                TongSoDonHang = 2,
                DaXoa = true
            }
        };
    }

    [Fact]
    public void CustomerManagement_InitialRender_DisplaysKpiCardsAndCustomerList()
    {
        // Act
        var cut = Render<CustomerManagement>();

        // Wait for async load to finish
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Nguyễn Văn Tuấn");
            cut.Markup.Should().Contain("0901234567");
            cut.Markup.Should().Contain("Lê Thị Hồng");
            cut.Markup.Should().Contain("0912345678");
            cut.Markup.Should().Contain("Vàng");
            cut.Markup.Should().Contain("Đồng");
        });
    }

    [Fact]
    public async Task CustomerManagement_ToggleLockStatus_CallsApiAndShowsToast()
    {
        // Arrange
        _mockAdminApi.Setup(api => api.ToggleCustomerStatusAsync(1))
            .ReturnsAsync(true);

        var cut = Render<CustomerManagement>();

        // Wait until table is loaded with rows
        cut.WaitForElement("button.btn-lock");

        // Act - Click lock button on first active customer
        var toggleBtn = cut.Find("button.btn-lock");
        await cut.InvokeAsync(() => toggleBtn.Click());

        // Assert
        _mockAdminApi.Verify(api => api.ToggleCustomerStatusAsync(1), Times.Once);
        _mockToastService.Verify(t => t.ShowSuccess(It.Is<string>(s => s.Contains("khóa"))), Times.Once);
    }

    [Fact]
    public async Task CustomerManagement_OpenCreateModal_AndSubmit_CallsApi()
    {
        // Arrange
        _mockAdminApi.Setup(api => api.CreateCustomerAccountAsync(It.IsAny<CreateCustomerAccountDto>()))
            .ReturnsAsync(ApiResponse<int>.Ok(3, "Tạo khách hàng thành công"));

        var cut = Render<CustomerManagement>();

        // Wait for page ready
        cut.WaitForElement("button.btn-action-primary");

        // Act - Open create modal
        var openBtn = cut.Find("button.btn-action-primary");
        await cut.InvokeAsync(() => openBtn.Click());

        // Fill form fields
        var nameInput = cut.Find("input[placeholder*='Nguyễn Văn A']");
        var phoneInput = cut.Find("input[placeholder*='0912345678']");

        nameInput.Change("Hoàng Minh");
        phoneInput.Change("0933445566");

        var submitBtn = cut.Find("button.btn-modal-submit");
        await cut.InvokeAsync(() => submitBtn.Click());

        // Assert
        _mockAdminApi.Verify(api => api.CreateCustomerAccountAsync(It.Is<CreateCustomerAccountDto>(
            c => c.TenKhachHang == "Hoàng Minh" && c.DienThoai == "0933445566")), Times.Once);
        _mockToastService.Verify(t => t.ShowSuccess(It.Is<string>(s => s.Contains("thành công"))), Times.Once);
    }

    [Fact]
    public async Task CustomerManagement_OpenOrderHistoryModal_LoadsCustomerOrders()
    {
        // Arrange
        var sampleOrders = new List<CustomerOrderHistoryItemDto>
        {
            new CustomerOrderHistoryItemDto
            {
                MaHoaDon = 501,
                NgayLapHoaDon = DateTime.Now,
                TongThanhToan = 650000m,
                TrangThai = "Đã hoàn thành",
                SoMatHang = 2
            }
        };

        _mockAdminApi.Setup(api => api.GetCustomerOrdersForAdminAsync(1))
            .ReturnsAsync(sampleOrders);

        var cut = Render<CustomerManagement>();

        // Wait until table is loaded
        cut.WaitForElement("button[title*='lịch sử đơn hàng']");

        // Act - Click history button on first customer
        var historyBtn = cut.Find("button[title*='lịch sử đơn hàng']");
        await cut.InvokeAsync(() => historyBtn.Click());

        // Assert modal content loaded
        cut.WaitForAssertion(() =>
        {
            _mockAdminApi.Verify(api => api.GetCustomerOrdersForAdminAsync(1), Times.Once);
            cut.Markup.Should().Contain("#501");
            cut.Markup.Should().Contain("650.000");
        });
    }

    [Theory]
    [InlineData("Tuấn", 1)]
    [InlineData("0912345678", 1)]
    [InlineData("KhôngTồnTại", 0)]
    public void CustomerManagement_SearchKeyword_FiltersClientSide(string keyword, int expectedCount)
    {
        // Arrange
        var cut = Render<CustomerManagement>();

        cut.WaitForElement("input.search-input");

        // Act - Enter search text
        var searchInput = cut.Find("input.search-input");
        searchInput.Input(keyword);

        // Assert
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            if (expectedCount == 0)
            {
                cut.Markup.Should().Contain("Không tìm thấy tài khoản");
            }
            else
            {
                rows.Count.Should().Be(expectedCount);
            }
        });
    }

    [Fact]
    public async Task CustomerManagement_OpenEditModal_DisplaysDiamondTierOption()
    {
        // Arrange
        var cut = Render<CustomerManagement>();
        cut.WaitForElement("button[title*='Chỉnh sửa']");

        // Act - Click edit button on first customer
        var editBtn = cut.Find("button[title*='Chỉnh sửa']");
        await cut.InvokeAsync(() => editBtn.Click());

        // Assert - Edit modal is open and select contains Kim Cương option
        cut.WaitForElement("select.form-control");
        var select = cut.Find("select.form-control");
        select.InnerHtml.Should().Contain("Kim Cương");
        select.InnerHtml.Should().Contain("value=\"4\"");
    }

    [Fact]
    public void CustomerManagement_ScrollContainerAndPagination_RendersProperly()
    {
        // Act
        var cut = Render<CustomerManagement>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var scrollContainer = cut.Find(".table-scroll-container");
            scrollContainer.Should().NotBeNull();

            var stickyTable = cut.Find("table.sticky-header-table");
            stickyTable.Should().NotBeNull();

            var pagination = cut.Find(".pagination-footer");
            pagination.Should().NotBeNull();
            pagination.InnerHtml.Should().Contain("Hiển thị");
            pagination.InnerHtml.Should().Contain("trong tổng số");

            var sizeSelector = cut.Find(".page-size-selector select");
            sizeSelector.Should().NotBeNull();
        });
    }
}

