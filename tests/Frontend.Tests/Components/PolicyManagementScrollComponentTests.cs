using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Catalog;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class PolicyManagementScrollComponentTests : BunitContext
{
    private readonly Mock<IPolicyApiClient> _mockPolicyApi;
    private readonly Mock<IToastService> _mockToastService;

    public PolicyManagementScrollComponentTests()
    {
        _mockPolicyApi = new Mock<IPolicyApiClient>();
        _mockToastService = new Mock<IToastService>();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IPolicyApiClient>(_mockPolicyApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        _mockPolicyApi.Setup(api => api.GetAllPoliciesAsync())
            .ReturnsAsync(new List<PolicyDto>
            {
                new PolicyDto
                {
                    MaChinhSach = 1,
                    TieuDe = "Chính sách đổi trả trong 15 ngày",
                    Slug = "chinh-sach-doi-tra",
                    TomTat = "Hỗ trợ đổi trả miễn phí toàn quốc",
                    KichHoat = true,
                    ThuTu = 1
                }
            });
    }

    [Fact]
    public void PolicyManagement_RendersScrollableContainer_AndStickyTable()
    {
        // Act
        var cut = Render<PolicyManagement>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var scrollContainer = cut.Find(".table-scroll-container");
            scrollContainer.Should().NotBeNull();

            var stickyTable = cut.Find("table.sticky-header-table");
            stickyTable.Should().NotBeNull();
        });
    }
}
