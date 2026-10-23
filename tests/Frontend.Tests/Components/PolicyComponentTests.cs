using Bunit;
using FluentAssertions;
using Frontend.Layout;
using Frontend.Pages;
using Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Catalog;
using Xunit;

namespace Frontend.Tests.Components;

public class PolicyComponentTests : BunitContext
{
    private readonly Mock<IPolicyApiClient> _mockPolicyApi;
    private readonly Mock<ICustomerApiClient> _mockCustomerApi;
    private readonly Mock<IToastService> _mockToast;
    private readonly Mock<IAuthStateService> _mockAuthState;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;

    public PolicyComponentTests()
    {
        _mockPolicyApi = new Mock<IPolicyApiClient>();
        _mockCustomerApi = new Mock<ICustomerApiClient>();
        _mockToast = new Mock<IToastService>();
        _mockAuthState = new Mock<IAuthStateService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();

        Services.AddSingleton<IPolicyApiClient>(_mockPolicyApi.Object);
        Services.AddSingleton<ICustomerApiClient>(_mockCustomerApi.Object);
        Services.AddSingleton<IToastService>(_mockToast.Object);
        Services.AddSingleton<IAuthStateService>(_mockAuthState.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);
    }

    [Fact(DisplayName = "POL-01: PolicyDetail hiển thị đúng nội dung HTML được lấy động từ CSDL")]
    public void PolicyDetail_RendersDynamicPolicyContent_FromApi()
    {
        // Arrange
        var mockPolicies = new List<PolicyDto>
        {
            new()
            {
                MaChinhSach = 1,
                Slug = "chinh-sach-giao-hang",
                TieuDe = "Chính sách giao hàng toàn quốc",
                NoiDung = "<p>Giao hàng miễn phí toàn quốc từ 50k</p>",
                ThuTu = 1,
                KichHoat = true
            },
            new()
            {
                MaChinhSach = 2,
                Slug = "chinh-sach-doi-tra",
                TieuDe = "Chính sách đổi trả hàng",
                NoiDung = "<p>Đổi trả 14 ngày</p>",
                ThuTu = 2,
                KichHoat = true
            }
        };

        _mockPolicyApi.Setup(x => x.GetActivePoliciesAsync())
            .ReturnsAsync(mockPolicies);

        // Act
        var cut = Render<PolicyDetail>(parameters => parameters.Add(p => p.slug, "chinh-sach-giao-hang"));

        // Assert
        cut.Markup.Should().Contain("Chính sách giao hàng toàn quốc");
        cut.Markup.Should().Contain("Giao hàng miễn phí toàn quốc từ 50k");
        cut.FindAll(".policy-tab-item").Should().HaveCount(2);
    }

    [Fact(DisplayName = "POL-02: AdminLayout hiển thị menu Quản lý Chính sách cho vai trò ChainAdmin")]
    public void AdminLayout_WhenChainAdmin_ContainsPolicyMenu()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "staffName").SetResult("Quản trị viên");

        // Act
        var cut = Render<AdminLayout>();

        // Assert
        cut.FindAll("a[href='/admin/policies']").Should().NotBeEmpty();
        cut.Markup.Should().Contain("Chính sách bán hàng");
    }
}
