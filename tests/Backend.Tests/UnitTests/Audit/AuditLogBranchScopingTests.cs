using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Controllers;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.Constants;
using Shared.DTOs.Audit;
using Shared.DTOs.Common;
using Xunit;

namespace Backend.Tests.UnitTests.Audit;

public class AuditLogBranchScopingTests
{
    private readonly Mock<IAuditLogService> _auditServiceMock;
    private readonly AuditLogsController _controller;

    public AuditLogBranchScopingTests()
    {
        _auditServiceMock = new Mock<IAuditLogService>();
        _controller = new AuditLogsController(_auditServiceMock.Object);
    }

    private void SetUserContext(string role, string? branchId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test_user"),
            new(ClaimTypes.Role, role)
        };

        if (branchId != null)
        {
            claims.Add(new Claim("branchId", branchId));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetAuditLogs_BranchManager_ForcesEffectiveBranchId_ToOwnBranch()
    {
        // Arrange: Quản lý chi nhánh 2 cố tình truyền query maChiNhanh = 9
        SetUserContext(RoleConstants.QuanLyChiNhanh, branchId: "2");

        int? capturedBranchId = null;
        _auditServiceMock.Setup(s => s.GetLogsAsync(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>()))
            .Callback<string?, int?, int>((loai, branch, limit) => capturedBranchId = branch)
            .ReturnsAsync(ApiResponse<IEnumerable<AuditLogDto>>.Ok(new List<AuditLogDto>()));

        // Act
        var result = await _controller.GetAuditLogs(loaiThaoTac: "ALL", maChiNhanh: 9, limit: 50);

        // Assert: Bắt buộc gọi service với branchId = 2 của quản lý, KHÔNG phải 9
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult);
        Assert.Equal(2, capturedBranchId);
    }

    [Fact]
    public async Task GetAuditLogs_ChainAdmin_CanQueryAnyBranchOrAll()
    {
        // Arrange: Admin chuỗi không bị ép branchId
        SetUserContext(RoleConstants.AdminChuoi, branchId: "1");

        int? capturedBranchId = null;
        _auditServiceMock.Setup(s => s.GetLogsAsync(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>()))
            .Callback<string?, int?, int>((loai, branch, limit) => capturedBranchId = branch)
            .ReturnsAsync(ApiResponse<IEnumerable<AuditLogDto>>.Ok(new List<AuditLogDto>()));

        // Act 1: Admin xem toàn chuỗi (maChiNhanh = null)
        await _controller.GetAuditLogs(loaiThaoTac: "ALL", maChiNhanh: null, limit: 50);
        Assert.Null(capturedBranchId);

        // Act 2: Admin lọc chi nhánh 5
        await _controller.GetAuditLogs(loaiThaoTac: "ALL", maChiNhanh: 5, limit: 50);
        Assert.Equal(5, capturedBranchId);
    }

    [Fact]
    public async Task GetAuditLogs_BranchManager_MissingBranchClaim_ReturnsForbid()
    {
        // Arrange: Quản lý chi nhánh nhưng token bị lỗi thiếu claim branchId
        SetUserContext(RoleConstants.QuanLyChiNhanh, branchId: null);

        // Act
        var result = await _controller.GetAuditLogs(loaiThaoTac: "ALL", maChiNhanh: null, limit: 50);

        // Assert: Bị từ chối Forbid 403
        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "VÒNG 1: Controller [Authorize] attribute bắt buộc phải chứa RoleConstants.ThuKho")]
    public void AuditLogsController_AuthorizeAttribute_MustIncludeThuKhoRole()
    {
        var authAttr = typeof(AuditLogsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.Contains(RoleConstants.ThuKho, authAttr.Roles);
    }

    [Theory(DisplayName = "VÒNG 2 & 3: Thủ kho truy cập nhật ký nhập hàng được scoped chính xác theo kho quản lý")]
    [InlineData(RoleConstants.ThuKho, "9", 9)]
    [InlineData(RoleConstants.ThuKho, "2", 2)]
    [InlineData(RoleConstants.ThuKho, "1", 1)]
    public async Task GetAuditLogs_WarehouseKeeper_AuthorizedAndScopedToOwnBranch(string role, string branchClaim, int expectedBranchId)
    {
        // Arrange
        SetUserContext(role, branchId: branchClaim);
        int? capturedBranchId = null;
        _auditServiceMock.Setup(s => s.GetLogsAsync(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>()))
            .Callback<string?, int?, int>((loai, branch, limit) => capturedBranchId = branch)
            .ReturnsAsync(ApiResponse<IEnumerable<AuditLogDto>>.Ok(new List<AuditLogDto>()));

        // Act: Thủ kho xem log
        var result = await _controller.GetAuditLogs(loaiThaoTac: "NHAP_HANG", maChiNhanh: null, limit: 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult);
        Assert.Equal(expectedBranchId, capturedBranchId);
    }
}

