using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Common;
using Shared.DTOs.Feedback;
using Shared.DTOs.Order;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Staff;
using Frontend.Services;

namespace Frontend.Tests.Components;

/// <summary>
/// Component Tests cho Bước 6: Cổng Điều phối Đơn hàng & Đổi trả Online chuyên biệt (OrderSupportPortal.razor - /staff/order-support).
/// Tuân thủ quy chuẩn Multi-Round Testing Standard (Rule 6, Rule 8, Rule 9):
/// - Vòng 1: Boundary & UI Layout Matrix (Header thương hiệu, 4 thẻ KPI, 2 Tabs nghiệp vụ)
/// - Vòng 2: Order Dispatch & Shipping Pipeline (Chờ xử lý -> Đang giao hàng -> Đã hoàn thành)
/// - Vòng 3: Return Tickets & Address Display Matrix (Phê duyệt thu hồi tại nhà, Xác nhận nhận hàng hoàn nhập kho lỗi, Địa chỉ nhận hàng hoàn)
/// </summary>
public class OrderSupportPortalComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;
    private readonly Mock<IAuthStateService> _mockAuthState;

    public OrderSupportPortalComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();
        _mockAuthState = new Mock<IAuthStateService>();

        _mockAdminApi.Setup(x => x.GetAllOrdersAsync(It.IsAny<int?>()))
            .ReturnsAsync(CreateSampleOrders());

        _mockAdminApi.Setup(x => x.GetFeedbacksAsync())
            .ReturnsAsync(CreateSampleFeedbacks());

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private List<OrderSummaryDto> CreateSampleOrders()
    {
        return new List<OrderSummaryDto>
        {
            new()
            {
                MaHoaDon = 1001,
                TenKhachHang = "Lê Văn Hùng",
                SdtKhachHang = "0901112233",
                NgayLapHoaDon = DateTime.Now.AddHours(-2),
                TongThanhToan = 450000m,
                TrangThai = "Chờ xử lý",
                LoaiDonHang = "ONLINE"
            },
            new()
            {
                MaHoaDon = 1002,
                TenKhachHang = "Trần Thị Mai",
                SdtKhachHang = "0902223344",
                NgayLapHoaDon = DateTime.Now.AddHours(-5),
                TongThanhToan = 650000m,
                TrangThai = "Đang giao hàng",
                LoaiDonHang = "ONLINE"
            },
            new()
            {
                MaHoaDon = 1003,
                TenKhachHang = "Nguyễn Văn Tuấn",
                SdtKhachHang = "0903334455",
                NgayLapHoaDon = DateTime.Now.AddDays(-1),
                TongThanhToan = 320000m,
                TrangThai = "Đã hoàn thành",
                LoaiDonHang = "ONLINE"
            }
        };
    }

    private List<FeedbackDto> CreateSampleFeedbacks()
    {
        return new List<FeedbackDto>
        {
            new()
            {
                MaPhanHoi = 501,
                MaHoaDon = 1003,
                HoTen = "Nguyễn Văn Tuấn",
                DienThoai = "0903334455",
                LoaiPhanHoi = "Yêu cầu hoàn trả hàng",
                TieuDe = "Yêu cầu hoàn trả hàng cho đơn #DH-1003",
                NoiDung = "Áo bị bung chỉ may ở gấu áo",
                TrangThai = "Cần duyệt",
                HinhThucHoan = "Thu hồi tại nhà",
                DiaChiThuHoi = "456 CMT8, Phường 11, Quận 3, TP.HCM",
                SoLuongTra = 1,
                NgayGui = DateTime.Now.AddHours(-3)
            },
            new()
            {
                MaPhanHoi = 502,
                MaHoaDon = 1000,
                HoTen = "Phạm Thị Loan",
                DienThoai = "0904445566",
                LoaiPhanHoi = "Yêu cầu hoàn trả hàng",
                TieuDe = "Yêu cầu hoàn trả hàng cho đơn #DH-1000",
                NoiDung = "Mặc chật vai",
                TrangThai = "Đã duyệt",
                HinhThucHoan = "Đến chi nhánh",
                MaChiNhanhHen = 2,
                TenChiNhanhHen = "QF Sư Vạn Hạnh",
                DiaChiChiNhanhHen = "772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM",
                DiaChiNhanHangHoanVe = "772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM (QF Sư Vạn Hạnh)",
                SoLuongTra = 2,
                NgayGui = DateTime.Now.AddDays(-1)
            }
        };
    }

    #region VÒNG 1: UI LAYOUT & 4 THẺ KPI ĐỔI TRẢ & HOÀN KHO LỖI (BOUNDARY MATRIX)

    [Fact(DisplayName = "VÒNG 1: Render cổng đổi trả hiển thị tiêu đề, 4 thẻ KPI và danh sách phiếu đổi trả trực tiếp")]
    public void OrderSupportPortal_InitialRender_ShowsHeaderAndReturnKpis()
    {
        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert
        cut.Find("h1.portal-title").TextContent.Should().Contain("QUẢN LÝ ĐỔI TRẢ & HOÀN KHO LỖI");

        // 4 Thẻ KPI Đổi Trả
        cut.Markup.Should().Contain("Đổi trả cần duyệt");
        cut.Markup.Should().Contain("Đang thu hồi / Đến chi nhánh");
        cut.Markup.Should().Contain("Đã hoàn kho lỗi");
        cut.Markup.Should().Contain("Tổng phiếu đổi trả");

        // Danh sách phiếu đổi trả hiển thị trực tiếp
        cut.Find(".returns-panel").Should().NotBeNull();
        cut.Find(".search-input-box").Should().NotBeNull();
    }

    #endregion

    #region VÒNG 2: QUY TRÌNH DUYỆT ĐỔI TRẢ & TỪ CHỐI (RETURN APPROVAL & REJECTION)

    [Fact(DisplayName = "VÒNG 2: Phiếu 'Cần duyệt' -> Bấm nút 'Từ chối' gọi ProcessReturnRequestAsync với Action REJECT")]
    public async Task OrderSupportPortal_RejectReturn_CallsProcessReturnApi()
    {
        // Arrange
        _mockAdminApi.Setup(x => x.ProcessReturnRequestAsync(501, It.Is<ProcessReturnRequestDto>(r => r.Action == "REJECT")))
            .ReturnsAsync(new ApiResponse<bool> { Success = true, Data = true, Message = "Từ chối thành công" });

        var cut = Render<OrderSupportPortal>();

        // Act
        var btnReject = cut.Find("button.btn-reject-return");
        await cut.InvokeAsync(() => btnReject.Click());

        // Assert
        _mockAdminApi.Verify(x => x.ProcessReturnRequestAsync(501, It.Is<ProcessReturnRequestDto>(r => r.Action == "REJECT")), Times.Once);
        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(msg => msg.Contains("từ chối"))), Times.Once);
    }

    [Fact(DisplayName = "VÒNG 2: Phiếu 'Cần duyệt' -> Bấm nút 'Phê duyệt thu hồi' gọi ProcessReturnRequestAsync")]
    public async Task OrderSupportPortal_ApproveReturn_CallsProcessReturnApi()
    {
        // Arrange
        _mockAdminApi.Setup(x => x.ProcessReturnRequestAsync(501, It.Is<ProcessReturnRequestDto>(r => r.Action == "APPROVE")))
            .ReturnsAsync(new ApiResponse<bool> { Success = true, Data = true, Message = "Phê duyệt thành công" });

        var cut = Render<OrderSupportPortal>();

        // Act
        var btnApprove = cut.Find("button.btn-approve-return");
        await cut.InvokeAsync(() => btnApprove.Click());

        // Assert
        _mockAdminApi.Verify(x => x.ProcessReturnRequestAsync(501, It.Is<ProcessReturnRequestDto>(r => r.Action == "APPROVE")), Times.Once);
        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(msg => msg.Contains("phê duyệt"))), Times.Once);
    }

    #endregion

    #region VÒNG 3: QUY TRÌNH TIẾP NHẬN, ĐỊA CHỈ THU HỒI & PHÂN TRANG ĐỔI TRẢ

    [Fact(DisplayName = "VÒNG 3: Hiển thị đầy đủ Địa chỉ thu hồi tại nhà và Địa chỉ chi nhánh nhận hàng hoàn")]
    public void OrderSupportPortal_ReturnList_DisplaysReturnAddressesCompletely()
    {
        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert
        cut.Markup.Should().Contain("#PH-501");
        cut.Markup.Should().Contain("456 CMT8, Phường 11, Quận 3, TP.HCM", "Phải hiện rõ địa chỉ thu hồi tại nhà của khách");
        cut.Markup.Should().Contain("Thu hồi tại nhà");

        cut.Markup.Should().Contain("#PH-502");
        cut.Markup.Should().Contain("772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM", "Phải hiện rõ địa chỉ chi nhánh nhận hàng lỗi");
        cut.Markup.Should().Contain("Đến chi nhánh");
    }

    [Fact(DisplayName = "VÒNG 3: Phiếu 'Đã duyệt' -> Bấm nút 'Đã nhận bưu phẩm hoàn' gọi ProcessReturnRequestAsync với Action RECEIVE")]
    public async Task OrderSupportPortal_ReceiveDefectiveStock_CallsProcessReturnWithReceiveAction()
    {
        // Arrange
        _mockAdminApi.Setup(x => x.ProcessReturnRequestAsync(502, It.IsAny<ProcessReturnRequestDto>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true, "Nhận hàng hoàn thành công"));

        var cut = Render<OrderSupportPortal>();

        // Act: Bấm nút "Nhận hàng & Nhập kho lỗi" của phiếu #PH-502
        var btnReceive = cut.Find("button.btn-receive-return[data-feedback-id='502']");
        await cut.InvokeAsync(() => btnReceive.Click());

        // Assert
        _mockAdminApi.Verify(x => x.ProcessReturnRequestAsync(502, It.Is<ProcessReturnRequestDto>(dto =>
            dto.Action == "RECEIVE"
        )), Times.Once);

        _mockToastService.Verify(x => x.ShowSuccess(It.Is<string>(msg => msg.Contains("nhập kho lỗi"))), Times.Once);
    }

    [Fact(DisplayName = "VÒNG 3: Quản lý chi nhánh truy cập thì hiển thị tiêu đề và huy hiệu khu vực quản lý")]
    public void OrderSupportPortal_WhenBranchManagerLoads_ShowsBranchHeader()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh (Q.10, TP.HCM)");

        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find(".branch-scope-pill").TextContent.Should().Contain("QF Sư Vạn Hạnh");
            cut.Find(".role-badge").TextContent.Should().Contain("QUẢN LÝ CHI NHÁNH");
        });
    }

    [Fact(DisplayName = "VÒNG 1: Phân trang danh sách phiếu đổi trả khi số lượng lớn và chuyển trang")]
    public async Task OrderSupportPortal_Pagination_SplitsLargeListAndNavigatesPages()
    {
        // Arrange: 25 phiếu đổi trả
        var feedbacks = new List<FeedbackDto>();
        for (int i = 1; i <= 25; i++)
        {
            feedbacks.Add(new FeedbackDto
            {
                MaPhanHoi = 600 + i,
                MaHoaDon = 1000 + i,
                HoTen = $"Khách hàng {i}",
                DienThoai = $"09000000{i:D2}",
                LoaiPhanHoi = "Yêu cầu hoàn trả hàng",
                TieuDe = $"Đổi trả hàng cho đơn #{1000 + i}",
                NoiDung = $"Hàng bị lỗi {i}",
                TrangThai = "Cần duyệt",
                HinhThucHoan = "Thu hồi tại nhà",
                DiaChiThuHoi = $"Địa chỉ thu hồi {i}",
                SoLuongTra = 1,
                NgayGui = DateTime.Now.AddMinutes(-i)
            });
        }
        _mockAdminApi.Setup(x => x.GetFeedbacksAsync()).ReturnsAsync(feedbacks);

        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert: Trang 1 chỉ hiển thị 10 phiếu (#PH-601 đến #PH-610), không hiển thị tràn #PH-615
        cut.WaitForAssertion(() =>
        {
            cut.Find(".pagination-info").TextContent.Should().Contain("Hiển thị 1 - 10");
            cut.Markup.Should().Contain("#PH-601");
            cut.Markup.Should().NotContain("#PH-615");
        });

        // Act: Bấm chuyển sang Trang 2
        var btnNext = cut.Find("button.page-nav-next");
        await cut.InvokeAsync(() => btnNext.Click());

        // Assert: Trang 2 hiển thị #PH-611 đến #PH-620
        cut.Find(".pagination-info").TextContent.Should().Contain("Hiển thị 11 - 20");
        cut.Markup.Should().Contain("#PH-615");
        cut.Markup.Should().NotContain("#PH-601");
    }

    [Theory(DisplayName = "VÒNG 3: Điều phối viên chi nhánh chỉ xem đơn hoàn & kho lỗi tại cơ sở mình, không thấy chi nhánh khác")]
    [InlineData("2", "QF Sư Vạn Hạnh", "#PH-502", "#PH-501")]
    [InlineData("9", "Kho Tổng Tân Bình", "#PH-501", "#PH-502")]
    public void OrderSupportPortal_BranchCoordinator_StrictBranchIsolation(string branchId, string branchName, string expectedVisibleTicket, string expectedHiddenTicket)
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.OrderCoordinator);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(branchId);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(branchName);

        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain(expectedVisibleTicket);
            cut.Markup.Should().NotContain(expectedHiddenTicket);
            cut.Find(".portal-header").TextContent.Should().Contain("ĐIỀU PHỐI VIÊN");
            cut.Find(".portal-header").TextContent.Should().Contain(branchName);
        });
    }

    [Fact(DisplayName = "VÒNG 3: Admin chuỗi có thể xem toàn bộ đơn đổi trả và có bộ lọc chi nhánh")]
    public async Task OrderSupportPortal_ChainAdmin_CanViewAllAndFilterByBranch()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        _mockAdminApi.Setup(x => x.GetBranchesAsync()).ReturnsAsync(new List<BranchDto>
        {
            new() { MaChiNhanh = 2, TenChiNhanh = "QF Sư Vạn Hạnh" },
            new() { MaChiNhanh = 9, TenChiNhanh = "Kho Tổng Tân Bình" }
        });

        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert 1: Toàn bộ chuỗi hiển thị cả #PH-501 và #PH-502
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("#PH-501");
            cut.Markup.Should().Contain("#PH-502");
            cut.Find(".admin-branch-select").Should().NotBeNull();
        });

        // Act 2: Admin lọc sang chi nhánh 2 (QF Sư Vạn Hạnh)
        var select = cut.Find(".admin-branch-select");
        await cut.InvokeAsync(() => select.Change("2"));

        // Assert 2: Chỉ còn #PH-502, không còn #PH-501
        cut.Markup.Should().Contain("#PH-502");
        cut.Markup.Should().NotContain("#PH-501");
    }

    [Fact(DisplayName = "VÒNG 3: Cổng Đổi trả cô lập tuyệt đối - Đơn khiếu nại và góp ý không được phép lọt vào")]
    public void OrderSupportPortal_CustomerComplaintAndFeedback_AreStrictlyExcludedFromReturnPortal()
    {
        // Arrange: Giả lập tập dữ liệu lẫn lộn cả đổi trả, khiếu nại có nhắc mã đơn, và góp ý
        var mixedFeedbacks = new List<FeedbackDto>
        {
            new()
            {
                MaPhanHoi = 501,
                MaHoaDon = 1003,
                HoTen = "Nguyễn Văn Tuấn",
                LoaiPhanHoi = "Yêu cầu hoàn trả hàng",
                TieuDe = "Yêu cầu hoàn trả hàng cho đơn #DH-1003",
                TrangThai = "Cần duyệt",
                HinhThucHoan = "Thu hồi tại nhà",
                SoLuongTra = 1,
                NgayGui = DateTime.Now
            },
            new()
            {
                MaPhanHoi = 901,
                MaHoaDon = 1002,
                HoTen = "Khách Hàng Khiếu Nại",
                LoaiPhanHoi = "Khiếu nại dịch vụ",
                TieuDe = "Áo bị bung chỉ, tôi muốn trả hàng hoặc đổi áo khác",
                NoiDung = "Đề nghị hoàn tiền gấp cho đơn này",
                TrangThai = "Chưa xử lý",
                NgayGui = DateTime.Now
            },
            new()
            {
                MaPhanHoi = 902,
                HoTen = "Khách Hàng Góp Ý",
                LoaiPhanHoi = "Góp ý sản phẩm",
                TieuDe = "Form dáng đẹp",
                NoiDung = "Cần thêm nhiều màu sắc hơn",
                TrangThai = "Chưa xử lý",
                NgayGui = DateTime.Now
            }
        };

        _mockAdminApi.Setup(x => x.GetFeedbacksAsync()).ReturnsAsync(mixedFeedbacks);

        // Act
        var cut = Render<OrderSupportPortal>();

        // Assert: Chỉ có phiếu đổi trả #PH-501 xuất hiện, các phiếu #PH-901 và #PH-902 bị loại trừ hoàn toàn
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("#PH-501");
            cut.Markup.Should().NotContain("#PH-901");
            cut.Markup.Should().NotContain("#PH-902");
            cut.Markup.Should().NotContain("Khách Hàng Khiếu Nại");
            cut.Markup.Should().NotContain("Khách Hàng Góp Ý");
        });
    }

    #endregion
}
