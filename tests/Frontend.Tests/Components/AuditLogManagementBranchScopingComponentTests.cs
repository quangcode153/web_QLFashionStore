using System;
using System.Collections.Generic;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Constants;
using Shared.DTOs.Audit;
using Xunit;
using Frontend.Pages.Admin;
using Frontend.Services;

namespace Frontend.Tests.Components;

public class AuditLogManagementBranchScopingComponentTests : BunitContext
{
    private readonly Mock<IAdminApiClient> _mockAdminApi;
    private readonly Mock<IToastService> _mockToastService;

    public AuditLogManagementBranchScopingComponentTests()
    {
        _mockAdminApi = new Mock<IAdminApiClient>();
        _mockToastService = new Mock<IToastService>();

        Services.AddSingleton<IAdminApiClient>(_mockAdminApi.Object);
        Services.AddSingleton<IToastService>(_mockToastService.Object);

        _mockAdminApi.Setup(api => api.GetAuditLogsAsync(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(new List<AuditLogDto>
            {
                new AuditLogDto
                {
                    MaNhatKy = 1,
                    LoaiThaoTac = "NHAP_HANG",
                    TenNhanVien = "Trần Kho Q10",
                    ChucVu = "Thủ kho",
                    MaChiNhanh = 1,
                    TenChiNhanh = "QF Sư Vạn Hạnh",
                    DoiTuong = "PN-001",
                    NoiDungChiTiet = "Nhập 50 Áo polo",
                    ThoiGian = DateTime.UtcNow,
                    IpDiaChi = "192.168.1.10"
                }
            });
    }

    [Fact(DisplayName = "Admin chuỗi xem nhật ký: Tiêu đề toàn hệ thống và gọi API không bị gò bó chi nhánh")]
    public void AuditLogManagement_WhenAdminLoads_ShowsFullSystemScope()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.AdminChuoi);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult(null);
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult(null);

        // Act
        var cut = Render<AuditLogManagement>();

        // Assert
        cut.Markup.Should().Contain("NHẬT KÝ HỆ THỐNG & BIẾN ĐỘNG VẬN HÀNH");
        cut.Markup.Should().Contain("Toàn hệ thống chuỗi");
        _mockAdminApi.Verify(api => api.GetAuditLogsAsync(null, null, 200), Times.Once);
    }

    [Fact(DisplayName = "Quản lý chi nhánh xem nhật ký: Tiêu đề hiển thị chi nhánh và gọi API truyền maChiNhanh")]
    public void AuditLogManagement_WhenBranchManagerLoads_ShowsBranchScope()
    {
        // Arrange
        JSInterop.Setup<string?>("localStorage.getItem", "userRole").SetResult(RoleConstants.QuanLyChiNhanh);
        JSInterop.Setup<string?>("localStorage.getItem", "branchId").SetResult("1");
        JSInterop.Setup<string?>("localStorage.getItem", "branchName").SetResult("QF Sư Vạn Hạnh (Q.10, TP.HCM)");

        // Act
        var cut = Render<AuditLogManagement>();

        // Assert
        cut.Markup.Should().Contain("NHẬT KÝ BIẾN ĐỘNG CHI NHÁNH");
        cut.Markup.Should().Contain("QF Sư Vạn Hạnh (Q.10, TP.HCM)");
        _mockAdminApi.Verify(api => api.GetAuditLogsAsync(null, 1, 200), Times.Once);
    }
}
