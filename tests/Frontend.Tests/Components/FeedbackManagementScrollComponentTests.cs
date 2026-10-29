using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.DTOs.Feedback;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class FeedbackManagementScrollComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;
    private readonly Mock<ISignalRNotificationService> _mockSignalR;

    public FeedbackManagementScrollComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();
        _mockSignalR = new Mock<ISignalRNotificationService>();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);
        Services.AddSingleton<ISignalRNotificationService>(_mockSignalR.Object);

        _mockAdminApi.Setup(api => api.GetFeedbacksAsync())
            .ReturnsAsync(new List<FeedbackDto>
            {
                new FeedbackDto
                {
                    MaPhanHoi = 1,
                    HoTen = "Nguyễn Văn Test",
                    DienThoai = "0901234567",
                    LoaiPhanHoi = "Góp ý chất lượng phục vụ",
                    TieuDe = "Góp ý quầy thanh toán",
                    NoiDung = "Nhân viên rất chu đáo và tận tình",
                    TrangThai = "Chưa xử lý"
                }
            });
    }

    [Fact]
    public void FeedbackManagement_RendersScrollableContainer_ToPreventLongLayoutShift()
    {
        // Act
        var cut = Render<FeedbackManagement>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var scrollContainer = cut.Find(".feedback-scroll-container");
            scrollContainer.Should().NotBeNull();
            cut.Markup.Should().Contain("tickets-grid");
        });
    }
}

