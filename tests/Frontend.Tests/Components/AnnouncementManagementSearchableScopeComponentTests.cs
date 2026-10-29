using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Announcement;
using Shared.DTOs.Warehouse;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class AnnouncementManagementSearchableScopeComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public AnnouncementManagementSearchableScopeComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        _mockAdminApi.Setup(api => api.GetBranchesAsync())
            .ReturnsAsync(new List<BranchDto>
            {
                new BranchDto { MaChiNhanh = 1, TenChiNhanh = "QF Sư Vạn Hạnh", DiaChi = "Q.10, TP.HCM" },
                new BranchDto { MaChiNhanh = 2, TenChiNhanh = "QF Quang Trung", DiaChi = "Q. Gò Vấp, TP.HCM" },
                new BranchDto { MaChiNhanh = 3, TenChiNhanh = "QF Cầu Giấy", DiaChi = "Q. Cầu Giấy, Hà Nội" }
            });

        _mockAdminApi.Setup(api => api.GetAnnouncementsAsync(It.IsAny<string?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<InternalAnnouncementDto>());
    }

    [Fact]
    public async Task AnnouncementManagement_SearchableScopeCombobox_RendersAndFiltersBranchesCorrectly()
    {
        // Arrange
        var cut = Render<AnnouncementManagement>();
        cut.WaitForElement("button.scope-combobox-trigger");

        var trigger = cut.Find("button.scope-combobox-trigger");

        // Act 1: Click open scope dropdown
        await cut.InvokeAsync(() => trigger.Click());

        // Assert 1: Search box and list are visible
        cut.WaitForElement("input.scope-search-input");
        cut.Markup.Should().Contain("QF Sư Vạn Hạnh");
        cut.Markup.Should().Contain("QF Quang Trung");

        // Act 2: Type search term "Quang Trung"
        var searchInput = cut.Find("input.scope-search-input");
        searchInput.Input("Quang Trung");

        // Assert 2: Only QF Quang Trung matches, Sư Vạn Hạnh is filtered out
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("QF Quang Trung");
            cut.Markup.Should().NotContain("QF Sư Vạn Hạnh");
        });

        // Act 3: Select QF Quang Trung
        var matchedItem = cut.Find(".scope-combobox-item[data-branch-id='2']");
        await cut.InvokeAsync(() => matchedItem.Click());

        // Assert 3: Scope is updated to QF Quang Trung and dropdown closed
        cut.WaitForAssertion(() =>
        {
            cut.Find("button.scope-combobox-trigger").InnerHtml.Should().Contain("QF Quang Trung");
            cut.FindAll(".scope-combobox-popover").Should().BeEmpty();
        });
    }
}

