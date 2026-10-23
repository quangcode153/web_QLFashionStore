using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Frontend.Services;
using Frontend.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Xunit;

namespace Frontend.Tests.Components;

public class DesignSystemComponentTests : BunitContext
{
    private readonly Mock<ICustomerApiClient> _customerApiMock = new();
    private readonly Mock<IAuthStateService> _authStateMock = new();
    private readonly Mock<IPolicyApiClient> _policyApiMock = new();

    public DesignSystemComponentTests()
    {
        // Đăng ký dịch vụ Toast vào BunitContext
        Services.AddSingleton<IToastService, ToastService>();
        var catalogMock = new Mock<ICatalogApiClient>();
        Services.AddSingleton<ICatalogApiClient>(catalogMock.Object);
        Services.AddSingleton<ICustomerApiClient>(_customerApiMock.Object);
        Services.AddSingleton<IAuthStateService>(_authStateMock.Object);

        _policyApiMock.Setup(p => p.GetActivePoliciesAsync())
            .ReturnsAsync(new List<global::Shared.DTOs.Catalog.PolicyDto>
            {
                new() { Slug = "chinh-sach-giao-hang", TieuDe = "Chính sách giao hàng toàn quốc", ThuTu = 1 },
                new() { Slug = "khieu-nai-gop-y", TieuDe = "Khiếu nại & Góp ý", ThuTu = 2, NoiDung = "<p>QF Fashion luôn coi trọng từng phản ánh</p>" }
            });
        Services.AddSingleton<IPolicyApiClient>(_policyApiMock.Object);
    }

    #region VÒNG 1: RENDER & MA TRẬN THAM SỐ BIÊN (BOUNDARY & RENDER MATRIX)

    [Fact]
    public void Footer_RendersFeedbackLinkInPolicyColumn()
    {
        var cut = Render<Footer>();
        cut.Markup.Should().Contain("Khiếu nại & Góp ý");
        cut.Markup.Should().Contain("/policies/khieu-nai-gop-y");
    }

    [Fact(DisplayName = "VÒNG 1: Footer tự động cập nhật danh sách chính sách bán hàng động từ API")]
    public void Footer_RendersDynamicPolicies_WhenLoadedFromApi()
    {
        // Arrange
        _policyApiMock.Setup(p => p.GetActivePoliciesAsync())
            .ReturnsAsync(new List<global::Shared.DTOs.Catalog.PolicyDto>
            {
                new() { Slug = "chinh-sach-giao-hang", TieuDe = "Chính sách giao hàng toàn quốc", ThuTu = 1 },
                new() { Slug = "chinh-sach-kiem-hang-dong-kiem", TieuDe = "Chính sách kiểm hàng đồng kiểm mới", ThuTu = 2 },
                new() { Slug = "chinh-sach-bao-hanh-vip", TieuDe = "Chính sách bảo hành VIP mở rộng", ThuTu = 3 }
            });

        // Act
        var cut = Render<Footer>();

        // Assert
        cut.Markup.Should().Contain("Chính sách kiểm hàng đồng kiểm mới");
        cut.Markup.Should().Contain("/policies/chinh-sach-kiem-hang-dong-kiem");
        cut.Markup.Should().Contain("Chính sách bảo hành VIP mở rộng");
        cut.Markup.Should().Contain("/policies/chinh-sach-bao-hanh-vip");
    }

    [Fact]
    public void PolicyDetail_KhieuNaiGopY_WhenLoggedIn_SubmitsFeedbackSuccessfully()
    {
        _authStateMock.SetupGet(a => a.IsAuthenticated).Returns(true);
        _customerApiMock.Setup(api => api.SubmitFeedbackAsync(It.IsAny<global::Shared.DTOs.Feedback.CreateFeedbackRequestDto>()))
            .ReturnsAsync(global::Shared.DTOs.Common.ApiResponse<int>.Ok(1, "Gửi phiếu thành công!"));

        var cut = Render<Frontend.Pages.PolicyDetail>(parameters => parameters
            .Add(p => p.slug, "khieu-nai-gop-y")
        );

        cut.Markup.Should().Contain("PHIẾU TIẾP NHẬN KHIẾU NẠI & GÓP Ý TRỰC TUYẾN");
        cut.Markup.Should().Contain("QF Fashion luôn coi trọng từng phản ánh");

        // Nhập form
        cut.Find("input[placeholder*='Họ và tên']").Change("Nguyễn Văn Khách");
        cut.Find("input[placeholder*='Số điện thoại']").Change("0912345678");
        cut.Find("input[placeholder*='Tóm tắt nội dung']").Change("Góp ý chất lượng phục vụ");
        cut.Find("textarea").Change("Nhân viên tư vấn rất nhiệt tình, rất hài lòng!");

        cut.Find("button.btn-submit-feedback").Click();

        _customerApiMock.Verify(api => api.SubmitFeedbackAsync(It.Is<global::Shared.DTOs.Feedback.CreateFeedbackRequestDto>(r =>
            r.HoTen == "Nguyễn Văn Khách" && r.DienThoai == "0912345678")), Times.Once);
    }

    [Fact]
    public void PolicyDetail_KhieuNaiGopY_WhenNotLoggedIn_BlocksSubmitAndPromptsLogin()
    {
        _authStateMock.SetupGet(a => a.IsAuthenticated).Returns(false);

        var cut = Render<Frontend.Pages.PolicyDetail>(parameters => parameters
            .Add(p => p.slug, "khieu-nai-gop-y")
        );

        cut.Markup.Should().Contain("PHIẾU TIẾP NHẬN KHIẾU NẠI & GÓP Ý TRỰC TUYẾN");
        cut.Markup.Should().Contain("Yêu cầu đăng nhập tài khoản");

        // Nhập form khi chưa đăng nhập
        cut.Find("input[placeholder*='Họ và tên']").Change("Spammer");
        cut.Find("input[placeholder*='Số điện thoại']").Change("0999999999");
        cut.Find("input[placeholder*='Tóm tắt nội dung']").Change("Spam test");
        cut.Find("textarea").Change("Nội dung spam phá hoại");

        cut.Find("button.btn-submit-feedback").Click();

        // Không được phép gọi API gửi phiếu
        _customerApiMock.Verify(api => api.SubmitFeedbackAsync(It.IsAny<global::Shared.DTOs.Feedback.CreateFeedbackRequestDto>()), Times.Never);
        // Hiển thị modal hoặc thông báo yêu cầu đăng nhập
        cut.Markup.Should().Contain("Yêu Cầu Đăng Nhập");
    }

    [Fact]
    public void TopBar_RendersAllFourTrustCommitments()
    {
        // Act
        var cut = Render<TopBar>();

        // Assert: 4 cam kết uy tín chuẩn YaMe.vn
        cut.Markup.Should().Contain("Miễn phí vận chuyển");
        cut.Markup.Should().Contain("14 ngày");
        cut.Markup.Should().Contain("365 ngày");
        cut.Markup.Should().Contain("VIP");
    }

    [Fact]
    public void Navbar_RendersEssentialFashionNavigationLinks()
    {
        // Act
        var cut = Render<Navbar>();

        // Assert: Menu điều hướng thời trang chuẩn
        cut.Markup.Should().Contain("SALE");
        cut.Markup.Should().Contain("ÁO");
        cut.Markup.Should().Contain("QUẦN");
        cut.Markup.Should().Contain("JEAN");
        cut.Markup.Should().Contain("PHỤ KIỆN");
    }

    [Theory]
    [InlineData(0, false)] // 0 món: Không hiển thị badge hoặc không có class active
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(99, true)]
    public void Navbar_CartBadge_ReflectsItemCountAccurately(int cartCount, bool shouldShowBadge)
    {
        // Act
        var cut = Render<Navbar>(parameters => parameters
            .Add(p => p.CartCount, cartCount)
        );

        // Assert
        if (shouldShowBadge)
        {
            var badge = cut.Find(".cart-badge");
            badge.TextContent.Trim().Should().Be(cartCount.ToString());
        }
        else
        {
            cut.FindAll(".cart-badge").Should().BeEmpty();
        }
    }

    [Fact]
    public void Footer_RendersCoreInformationAndPhilosophy()
    {
        // Act
        var cut = Render<Footer>();

        // Assert: Triết lý thương hiệu và thông tin chính sách
        cut.Markup.Should().Contain("Mặc Bền");
        cut.Markup.Should().Contain("Mặc Mềm");
        cut.Markup.Should().Contain("Chính sách");
    }

    #endregion

    #region VÒNG 2: CHUỖI TƯƠNG TÁC & STATE MACHINE (STATE MACHINE & INTERACTIONS)

    [Fact]
    public void ToastNotification_DisplaysCorrectLevelAndMessage_WhenTriggered()
    {
        // Arrange
        var toastService = Services.GetRequiredService<IToastService>();
        var cut = Render<ToastNotification>();

        // Ban đầu chưa có toast
        cut.FindAll(".yame-toast").Should().BeEmpty();

        // Act: Kích hoạt Toast Success
        toastService.ShowSuccess("Áp dụng mã giảm giá thành công!");

        // Assert: Toast phải xuất hiện trong DOM với class và message chính xác
        var toast = cut.Find(".yame-toast");
        toast.ClassList.Should().Contain("toast-success");
        toast.TextContent.Should().Contain("Áp dụng mã giảm giá thành công!");
    }

    [Fact]
    public async Task ConfirmModal_TriggersCallbacks_OnConfirmAndCancel()
    {
        // Arrange
        var confirmed = false;
        var cancelled = false;

        var cut = Render<ConfirmModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Xác nhận xóa món hàng")
            .Add(p => p.Message, "Bạn có chắc chắn muốn xóa sản phẩm này khỏi giỏ?")
            .Add(p => p.OnConfirm, () => { confirmed = true; })
            .Add(p => p.OnCancel, () => { cancelled = true; })
        );

        // Assert Render
        cut.Find(".modal-title").TextContent.Should().Be("Xác nhận xóa món hàng");
        cut.Find(".modal-body").TextContent.Should().Contain("Bạn có chắc chắn");

        // Act 1: Click Xác nhận
        await cut.Find(".btn-confirm").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        confirmed.Should().BeTrue();

        // Act 2: Click Hủy
        await cut.Find(".btn-cancel").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cancelled.Should().BeTrue();
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN RBAC (RBAC ROLE ISOLATION MATRIX)

    [Theory]
    [InlineData(null, "Đăng nhập")]
    [InlineData(RoleConstants.AdminChuoi, "Quản trị")]
    [InlineData(RoleConstants.ThuNgan, "Thu ngân POS")]
    public void Navbar_RendersRoleSpecificLinks(string? role, string expectedText)
    {
        // Act
        var cut = Render<Navbar>(parameters => parameters
            .Add(p => p.CurrentUserRole, role)
        );

        // Assert
        cut.Markup.Should().Contain(expectedText);
    }

    #endregion
}
