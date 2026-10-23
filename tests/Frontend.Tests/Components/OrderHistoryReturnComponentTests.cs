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
using Shared.DTOs.Feedback;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Customer;
using Frontend.Services;

namespace Frontend.Tests.Components;

/// <summary>
/// Component Tests cho Bước 7: Nâng cấp Form Đổi trả phía khách hàng trên OrderHistory.razor
/// Tuân thủ quy chuẩn Multi-Round Testing Standard (Rule 6, Rule 8, Rule 9):
/// - Vòng 1: Boundary & Layout Matrix (2 hình thức: Thu hồi tại nhà vs Đến chi nhánh hẹn trước)
/// - Vòng 2: Dynamic Branch Selection & Address Preview
/// - Vòng 3: Validation & API Submission Pipeline (Gửi đúng HinhThucHoan, DiaChiThuHoi, MaChiNhanhHen)
/// </summary>
public class OrderHistoryReturnComponentTests : BunitContext
{
    private readonly Mock<ICustomerApiClient> _mockCustomerApi;
    private readonly Mock<ICatalogApiClient> _mockCatalogApi;
    private readonly Mock<IToastService> _mockToastService;

    public OrderHistoryReturnComponentTests()
    {
        _mockCustomerApi = new Mock<ICustomerApiClient>();
        _mockCatalogApi = new Mock<ICatalogApiClient>();
        _mockToastService = new Mock<IToastService>();

        _mockCustomerApi.Setup(x => x.GetOrderHistoryAsync())
            .ReturnsAsync(CreateSampleOrders());

        _mockCustomerApi.Setup(x => x.GetProfileAsync())
            .ReturnsAsync(new CustomerProfileDto
            {
                MaKhachHang = 1,
                TenKhachHang = "Nguyễn Văn Khách",
                DienThoai = "0988776655",
                Email = "khach@example.com"
            });

        _mockCustomerApi.Setup(x => x.GetOrderDetailsAsync(1001))
            .ReturnsAsync(CreateSampleOrderItems());

        _mockCustomerApi.Setup(x => x.GetBranchesAsync())
            .ReturnsAsync(CreateSampleBranches());

        Services.AddSingleton<ICustomerApiClient>(_mockCustomerApi.Object);
        Services.AddSingleton<ICatalogApiClient>(_mockCatalogApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private List<CustomerOrderHistoryItemDto> CreateSampleOrders()
    {
        return new List<CustomerOrderHistoryItemDto>
        {
            new()
            {
                MaHoaDon = 1001,
                NgayLapHoaDon = DateTime.Now.AddDays(-2),
                SoMatHang = 1,
                TongThanhToan = 350000m,
                TrangThai = "Đã hoàn thành",
                TongSoLuongDaMua = 2,
                TongSoLuongDaTra = 0
            }
        };
    }

    private List<OrderDetailItemDto> CreateSampleOrderItems()
    {
        return new List<OrderDetailItemDto>
        {
            new()
            {
                MaBienThe = 101,
                TenSanPham = "Áo Thun Premium Cotton",
                MauSac = "Đen",
                KichCo = "L",
                SoLuong = 2,
                DonGia = 350000m,
                ThanhTien = 700000m
            }
        };
    }

    private List<BranchDto> CreateSampleBranches()
    {
        return new List<BranchDto>
        {
            new() { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", DiaChi = "772 Sư Vạn Hạnh, Q.10, TP.HCM" },
            new() { MaChiNhanh = 2, TenChiNhanh = "QF Quang Trung", DiaChi = "128 Quang Trung, Q.Gò Vấp, TP.HCM" }
        };
    }

    #region VÒNG 1: LAYOUT & 2 HÌNH THỨC HOÀN TRẢ (BOUNDARY & LAYOUT MATRIX)

    [Fact(DisplayName = "VÒNG 1: Mở modal đổi trả hiển thị radio chọn 2 hình thức: Thu hồi tại nhà và Đến chi nhánh")]
    public async Task OrderHistory_OpenReturnModal_DisplaysBothReturnMethodRadios()
    {
        // Arrange
        var cut = Render<OrderHistory>();

        // Act - Click nút Hoàn trả
        var btnOpenReturn = cut.Find("button.btn-open-return-modal");
        await cut.InvokeAsync(() => btnOpenReturn.Click());

        // Assert
        cut.Markup.Should().Contain("ĐĂNG KÝ HOÀN TRẢ HÀNG");
        cut.Find("input.radio-return-home").Should().NotBeNull();
        cut.Find("input.radio-return-branch").Should().NotBeNull();
        cut.Find("input.input-pickup-address").Should().NotBeNull();
    }

    #endregion

    #region VÒNG 2: CHUYỂN HÌNH THỨC SANG CHI NHÁNH & PREVIEW ĐỊA CHỈ (STATE MACHINE)

    [Fact(DisplayName = "VÒNG 2: Chọn 'Đến chi nhánh' hiển thị dropdown chọn chi nhánh và địa chỉ chi nhánh")]
    public async Task OrderHistory_SelectBranchMethod_ShowsBranchDropdownAndAddress()
    {
        // Arrange
        var cut = Render<OrderHistory>();
        await cut.InvokeAsync(() => cut.Find("button.btn-open-return-modal").Click());

        // Act - Click chọn radio 'Đến chi nhánh'
        var radioBranch = cut.Find("input.radio-return-branch");
        await cut.InvokeAsync(() => radioBranch.Change(true));

        // Assert
        var branchSelect = cut.Find("select.select-branch-dropoff");
        branchSelect.Should().NotBeNull();
        cut.Markup.Should().Contain("QF Sư Vạn Hạnh");
    }

    #endregion

    #region VÒNG 3: SUBMIT FORM & GỬI CONTRACT CHUẨN XÁC (API VERIFICATION MATRIX)

    [Fact(DisplayName = "VÒNG 3: Gửi yêu cầu 'Thu hồi tại nhà' truyền đúng HinhThucHoan và DiaChiThuHoi")]
    public async Task OrderHistory_SubmitHomeReturn_SendsCorrectPayload()
    {
        // Arrange
        _mockCustomerApi.Setup(x => x.SubmitReturnRequestAsync(It.IsAny<CreateReturnRequestDto>()))
            .ReturnsAsync(ApiResponse<int>.Ok(501, "Thành công"));

        var cut = Render<OrderHistory>();
        await cut.InvokeAsync(() => cut.Find("button.btn-open-return-modal").Click());

        // Act - Điền thông tin
        var inputAddress = cut.Find("input.input-pickup-address");
        await cut.InvokeAsync(() => inputAddress.Change("Số 99 Lê Duẩn, Đà Nẵng"));

        var textareaReason = cut.Find("textarea.return-reason");
        await cut.InvokeAsync(() => textareaReason.Change("Áo bị tuột đường may nách áo"));

        var btnSubmit = cut.Find("button.btn-modal-submit-return");
        await cut.InvokeAsync(() => btnSubmit.Click());

        // Assert
        _mockCustomerApi.Verify(x => x.SubmitReturnRequestAsync(It.Is<CreateReturnRequestDto>(dto =>
            dto.MaHoaDon == 1001 &&
            dto.HinhThucHoan == "Thu hồi tại nhà" &&
            dto.DiaChiThuHoi == "Số 99 Lê Duẩn, Đà Nẵng" &&
            dto.MaChiNhanhHen == null
        )), Times.Once);

        _mockToastService.Verify(x => x.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "VÒNG 3: Gửi yêu cầu 'Đến chi nhánh' truyền đúng HinhThucHoan và MaChiNhanhHen")]
    public async Task OrderHistory_SubmitBranchReturn_SendsCorrectPayload()
    {
        // Arrange
        _mockCustomerApi.Setup(x => x.SubmitReturnRequestAsync(It.IsAny<CreateReturnRequestDto>()))
            .ReturnsAsync(ApiResponse<int>.Ok(502, "Thành công"));

        var cut = Render<OrderHistory>();
        await cut.InvokeAsync(() => cut.Find("button.btn-open-return-modal").Click());

        // Act - Chuyển sang đến chi nhánh
        var radioBranch = cut.Find("input.radio-return-branch");
        await cut.InvokeAsync(() => radioBranch.Change(true));

        var branchSelect = cut.Find("select.select-branch-dropoff");
        await cut.InvokeAsync(() => branchSelect.Change("2"));

        var textareaReason = cut.Find("textarea.return-reason");
        await cut.InvokeAsync(() => textareaReason.Change("Mang đến đổi size nhỏ hơn"));

        var btnSubmit = cut.Find("button.btn-modal-submit-return");
        await cut.InvokeAsync(() => btnSubmit.Click());

        // Assert
        _mockCustomerApi.Verify(x => x.SubmitReturnRequestAsync(It.Is<CreateReturnRequestDto>(dto =>
            dto.MaHoaDon == 1001 &&
            dto.HinhThucHoan == "Đến chi nhánh" &&
            dto.MaChiNhanhHen == 2
        )), Times.Once);

        _mockToastService.Verify(x => x.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "VÒNG 3: Cho phép chọn nhiều sản phẩm trong 1 đơn hàng và gửi yêu cầu hoàn trả cho từng mặt hàng")]
    public async Task OrderHistory_SelectMultipleItems_SubmitsReturnRequestForEachItem()
    {
        // Arrange: Đơn hàng có 2 mặt hàng
        _mockCustomerApi.Setup(x => x.GetOrderDetailsAsync(1001))
            .ReturnsAsync(new List<OrderDetailItemDto>
            {
                new()
                {
                    MaBienThe = 101,
                    TenSanPham = "Áo Thun Premium Cotton",
                    MauSac = "Đen",
                    KichCo = "L",
                    SoLuong = 2,
                    DonGia = 350000m,
                    ThanhTien = 700000m
                },
                new()
                {
                    MaBienThe = 102,
                    TenSanPham = "Quần Short Kaki",
                    MauSac = "Be",
                    KichCo = "M",
                    SoLuong = 1,
                    DonGia = 250000m,
                    ThanhTien = 250000m
                }
            });

        _mockCustomerApi.Setup(x => x.SubmitReturnRequestAsync(It.IsAny<CreateReturnRequestDto>()))
            .ReturnsAsync(ApiResponse<int>.Ok(503, "Thành công"));

        var cut = Render<OrderHistory>();
        await cut.InvokeAsync(() => cut.Find("button.btn-open-return-modal").Click());

        // Assert - Có các checkbox chọn sản phẩm
        var checkboxes = cut.FindAll("input.return-item-checkbox");
        checkboxes.Count.Should().Be(2);

        // Act - Tích chọn thêm sản phẩm thứ 2 (biến thể 102)
        await cut.InvokeAsync(() => checkboxes[1].Change(true));

        var inputAddress = cut.Find("input.input-pickup-address");
        await cut.InvokeAsync(() => inputAddress.Change("Số 10 Lê Duẩn, Q.1"));

        var textareaReason = cut.Find("textarea.return-reason");
        await cut.InvokeAsync(() => textareaReason.Change("Cả 2 sản phẩm đều không vừa kích cỡ"));

        var btnSubmit = cut.Find("button.btn-modal-submit-return");
        await cut.InvokeAsync(() => btnSubmit.Click());

        // Assert - API SubmitReturnRequestAsync được gọi đúng 1 lần (1 phiếu hoàn duy nhất cho cả 2 mặt hàng)
        _mockCustomerApi.Verify(x => x.SubmitReturnRequestAsync(It.Is<CreateReturnRequestDto>(dto =>
            dto.MaHoaDon == 1001 &&
            dto.Items.Count == 2 &&
            dto.Items.Any(i => i.MaBienThe == 101) &&
            dto.Items.Any(i => i.MaBienThe == 102) &&
            dto.SoLuongTra == 2
        )), Times.Once);
    }

    #endregion

    #region VÒNG 4: ĐỒNG BỘ TRẠNG THÁI HOÀN TRẢ & CHỐNG GỬI TRÙNG LẶP (ANTI-DUPLICATE & SYNC)

    [Fact(DisplayName = "VÒNG 4: Đơn hàng Đã hoàn trả hiển thị badge 'Đã hoàn trả' và ẩn nút 'Hoàn trả'")]
    public void OrderHistory_ReturnedOrder_ShowsReturnedBadge_AndHidesReturnButton()
    {
        // Arrange
        _mockCustomerApi.Setup(x => x.GetOrderHistoryAsync())
            .ReturnsAsync(new List<CustomerOrderHistoryItemDto>
            {
                new()
                {
                    MaHoaDon = 1002,
                    NgayLapHoaDon = DateTime.Now.AddDays(-1),
                    SoMatHang = 1,
                    TongThanhToan = 250000m,
                    TrangThai = "Đã hoàn trả"
                }
            });

        // Act
        var cut = Render<OrderHistory>();

        // Assert
        cut.Markup.Should().Contain("Đã hoàn trả");
        cut.FindAll("button.btn-open-return-modal").Should().BeEmpty();
    }

    [Fact(DisplayName = "VÒNG 4: Đơn hàng đang có yêu cầu đổi trả (Cần duyệt) hiển thị badge 'Đang hoàn trả' và không cho bấm Hoàn trả lần 2")]
    public void OrderHistory_PendingReturnOrder_ShowsPendingBadge_AndPreventsDuplicateSubmit()
    {
        // Arrange
        _mockCustomerApi.Setup(x => x.GetOrderHistoryAsync())
            .ReturnsAsync(new List<CustomerOrderHistoryItemDto>
            {
                new()
                {
                    MaHoaDon = 1003,
                    NgayLapHoaDon = DateTime.Now.AddDays(-3),
                    SoMatHang = 1,
                    TongThanhToan = 400000m,
                    TrangThai = "Đã hoàn thành",
                    TrangThaiHoanTra = "Cần duyệt"
                }
            });

        // Act
        var cut = Render<OrderHistory>();

        // Assert
        cut.Markup.Should().Contain("Đang hoàn trả");
        cut.FindAll("button.btn-open-return-modal").Should().BeEmpty();
    }

    [Fact(DisplayName = "VÒNG 4: Tabs lọc trạng thái đơn hàng (Tất cả, Chờ xử lý, Đang giao, Thành công, Đã hoàn trả, Đã hủy) hoạt động chuẩn")]
    public async Task OrderHistory_FilterTabs_FilterOrdersCorrectly()
    {
        // Arrange
        _mockCustomerApi.Setup(x => x.GetOrderHistoryAsync())
            .ReturnsAsync(new List<CustomerOrderHistoryItemDto>
            {
                new() { MaHoaDon = 1, TrangThai = "Chờ xử lý" },
                new() { MaHoaDon = 2, TrangThai = "Đã hoàn thành" },
                new() { MaHoaDon = 3, TrangThai = "Đã hoàn trả" },
                new() { MaHoaDon = 4, TrangThai = "Đã hoàn thành", TrangThaiHoanTra = "Cần duyệt" }
            });

        var cut = Render<OrderHistory>();

        // Act & Assert - Kiểm tra tab Đã hoàn trả
        var returnedTab = cut.Find("button.order-tab-btn[data-tab='Đã hoàn trả']");
        await cut.InvokeAsync(() => returnedTab.Click());

        cut.Markup.Should().Contain("#DH-3");
        cut.Markup.Should().Contain("#DH-4");
        cut.Markup.Should().NotContain("#DH-1");
        cut.Markup.Should().NotContain("#DH-2");
    }

    #endregion
}
