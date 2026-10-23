using System;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Frontend.Services;
using Frontend.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Xunit;

namespace Frontend.Tests.Components;

public class AuthStateAndBrandNavigationTests : BunitContext
{
    private readonly Mock<ICatalogApiClient> _catalogApiMock = new();

    public AuthStateAndBrandNavigationTests()
    {
        Services.AddSingleton<IToastService, ToastService>();
        Services.AddSingleton<ICatalogApiClient>(_catalogApiMock.Object);
        Services.AddScoped<IAuthStateService, AuthStateService>();
    }

    #region VÒNG 1: MA TRẬN THAM SỐ BIÊN VAI TRÒ (PARAMETERIZED BOUNDARY MATRIX)

    [Theory]
    [InlineData(null, null, "Đăng nhập", "/login")]
    [InlineData("", "", "Đăng nhập", "/login")]
    [InlineData(RoleConstants.KhachHang, "Anh Minh", "Anh Minh", "/profile")]
    [InlineData(RoleConstants.KhachHang, null, "Khách hàng", "/profile")]
    [InlineData(RoleConstants.ThuKho, "Trần Văn Kho", "Thủ kho (Kho hàng)", "/admin/warehouse")]
    [InlineData(RoleConstants.ThuNgan, "Lê Thu Ngân", "Thu ngân POS", "/pos")]
    [InlineData(RoleConstants.NhanVien, "Nguyễn Nhân Viên", "Nhân viên POS", "/pos")]
    [InlineData(RoleConstants.AdminChuoi, "Võ Quản Trị", "Quản trị (Đa CN)", "/admin/dashboard")]
    public void Navbar_RendersCorrectBrandRoleAndTargetLink(string? role, string? name, string expectedBadgeText, string expectedTargetUrl)
    {
        // Act
        var cut = Render<Navbar>(parameters => parameters
            .Add(p => p.CurrentUserRole, role)
            .Add(p => p.CurrentUserName, name)
        );

        // Assert
        cut.Markup.Should().Contain(expectedBadgeText);
        cut.Markup.Should().Contain(expectedTargetUrl);
    }

    #endregion

    #region VÒNG 2: CHUỖI CHUYỂN TRẠNG THÁI (STATE MACHINE PIPELINE)

    [Fact]
    public async Task AuthStateService_PreservesLoginSession_ThroughPipeline()
    {
        // Arrange
        var authService = new AuthStateService(JSInterop.JSRuntime);
        authService.IsAuthenticated.Should().BeFalse();

        var stateChangedCount = 0;
        authService.OnAuthStateChanged += () => { stateChangedCount++; };

        // Act 1: Giả lập đăng nhập thành công vai trò Thủ kho
        await authService.SetAuthAsync("mock_token_123", "thukho_user", RoleConstants.ThuKho, "Nguyễn Văn Kho", "5");

        // Assert 1: Trạng thái cập nhật và thông báo event
        authService.IsAuthenticated.Should().BeTrue();
        authService.CurrentUser.Should().Be("thukho_user");
        authService.UserRole.Should().Be(RoleConstants.ThuKho);
        authService.StaffName.Should().Be("Nguyễn Văn Kho");
        stateChangedCount.Should().Be(1);

        // Act 2: Render Navbar với AuthState đã đăng nhập
        var cut = Render<Navbar>(parameters => parameters
            .Add(p => p.CurrentUserRole, authService.UserRole)
            .Add(p => p.CurrentUserName, authService.StaffName)
        );

        cut.Markup.Should().Contain("Thủ kho (Kho hàng)");
        cut.Markup.Should().NotContain("Đăng nhập");

        // Act 3: Click vào Brand Logo để quay lại trang chủ
        var brandLogo = cut.Find("a.header-logo.qf-brand-lockup");
        brandLogo.Should().NotBeNull();
        await brandLogo.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert 3: URL được điều hướng về "/" bằng client routing
        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.Uri.Should().EndWith("/");

        // Đảm bảo sau khi về trang chủ, AuthState của Thủ kho vẫn nguyên vẹn
        authService.IsAuthenticated.Should().BeTrue();
        authService.UserRole.Should().Be(RoleConstants.ThuKho);

        // Act 4: Đăng xuất
        await authService.ClearAuthAsync();
        authService.IsAuthenticated.Should().BeFalse();
        authService.UserRole.Should().BeNull();
        stateChangedCount.Should().Be(2);
    }

    #endregion

    #region VÒNG 3: MA TRẬN PHÂN QUYỀN VÀ BẢO TOÀN ROLE (RBAC ISOLATION MATRIX)

    [Theory]
    [InlineData(RoleConstants.KhachHang)]
    [InlineData(RoleConstants.ThuKho)]
    [InlineData(RoleConstants.ThuNgan)]
    public async Task BrandLogo_Click_DoesNotLogout_ForMultipleRoles(string role)
    {
        // Arrange
        var authService = new AuthStateService(JSInterop.JSRuntime);
        await authService.SetAuthAsync("token", "user1", role, "Người Dùng", "1");

        var cut = Render<Navbar>(parameters => parameters
            .Add(p => p.CurrentUserRole, authService.UserRole)
            .Add(p => p.CurrentUserName, authService.StaffName)
        );

        // Act: Click Brand Logo
        var logo = cut.Find("a.header-logo.qf-brand-lockup");
        await logo.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert: Không bị out khỏi tài khoản
        authService.IsAuthenticated.Should().BeTrue();
        authService.UserRole.Should().Be(role);
    }

    [Theory]
    [InlineData(RoleConstants.KhachHang, "Test Name", "/profile")]
    [InlineData(RoleConstants.ThuKho, "Thủ kho (Kho hàng)", "/admin/warehouse")]
    [InlineData(RoleConstants.ThuNgan, "Thu ngân POS", "/pos")]
    public async Task MainLayout_MaintainsUserRole_WhenNavigatingToHome_AfterLogin(string role, string expectedBadgeText, string expectedTargetLink)
    {
        // Arrange
        Services.AddSingleton<ICartService, CartService>();
        var authService = Services.GetRequiredService<IAuthStateService>();

        // Giả lập ban đầu chưa đăng nhập
        JSInterop.Setup<string?>("localStorage.getItem", "currentUser").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "authToken").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "fashion_cart_guest").SetResult(null);

        var cut = Render<Frontend.Layout.MainLayout>();
        cut.Markup.Should().Contain("Đăng nhập");

        // Act 1: User đăng nhập vào hệ thống
        JSInterop.Setup<string?>("localStorage.getItem", "currentUser").SetResult("testuser");
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(role);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Test Name");
        JSInterop.Setup<string?>("localStorage.getItem", "authToken").SetResult("mock_token");

        await authService.SetAuthAsync("mock_token", "testuser", role, "Test Name");

        // Act 2: Điều hướng SPA về trang chủ "/"
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/");

        // Assert: MainLayout và Navbar phải hiển thị đúng vai trò của người dùng, KHÔNG bị out thành "Đăng nhập"
        cut.Markup.Should().Contain(expectedBadgeText);
        cut.Markup.Should().Contain(expectedTargetLink);
        cut.Markup.Should().NotContain("auth-link");
    }

    #endregion
}

