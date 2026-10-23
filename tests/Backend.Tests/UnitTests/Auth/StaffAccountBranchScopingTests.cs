using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Security;
using Backend.Services;
using Moq;
using Shared.Constants;
using Shared.DTOs.Auth;
using Xunit;

namespace Backend.Tests.UnitTests.Auth;

public class StaffAccountBranchScopingTests
{
    private readonly Mock<IAuthRepository> _authRepoMock;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtMock;
    private readonly IAuthService _authService;

    public StaffAccountBranchScopingTests()
    {
        _authRepoMock = new Mock<IAuthRepository>();
        _hasherMock = new Mock<IPasswordHasher>();
        _jwtMock = new Mock<IJwtTokenGenerator>();

        _hasherMock.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed_secret_password");

        _authService = new AuthService(
            _authRepoMock.Object,
            _hasherMock.Object,
            _jwtMock.Object
        );
    }

    // =========================================================================
    // VÒNG 1: MA TRẬN THAM SỐ & PHÂN LOẠI THỦ KHO (ONLINE VS CHI NHÁNH)
    // =========================================================================
    [Theory]
    [InlineData("ONLINE", null, true)] // Admin tạo thủ kho Online -> tự gán online warehouse
    [InlineData("CHI_NHANH", 1, true)] // Admin tạo thủ kho chi nhánh có branch id -> thành công
    [InlineData("CHI_NHANH", null, false)] // Admin tạo thủ kho chi nhánh nhưng thiếu branch -> reject
    public async Task CreateStaffAccount_WarehouseKeeper_ClassificationMatrix_BehavesCorrectly(
        string loaiThuKho, int? maChiNhanh, bool expectedSuccess)
    {
        // Arrange
        var req = new CreateStaffAccountDto
        {
            TenDangNhap = "thukho_test",
            MatKhau = "123456",
            TenNhanVien = "Nguyễn Văn Kho",
            DienThoai = "0901234567",
            VaiTro = RoleConstants.ThuKho,
            LoaiThuKho = loaiThuKho,
            MaChiNhanh = maChiNhanh
        };

        _authRepoMock.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _authRepoMock.Setup(r => r.CreateStaffAccountAsync(It.IsAny<CreateStaffAccountDto>(), It.IsAny<string>()))
            .ReturnsAsync(101);

        // Act
        var result = await _authService.CreateStaffAccountAsync(req, RoleConstants.AdminChuoi);

        // Assert
        Assert.Equal(expectedSuccess, result.Success);
        if (!expectedSuccess)
        {
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }
    }

    [Fact]
    public async Task BranchManager_CannotCreate_OnlineWarehouseKeeper()
    {
        // Arrange: Quản lý chi nhánh cố tạo thủ kho online
        var req = new CreateStaffAccountDto
        {
            TenDangNhap = "thukho_online_fake",
            MatKhau = "123456",
            TenNhanVien = "Trần Kho",
            VaiTro = RoleConstants.ThuKho,
            LoaiThuKho = "ONLINE",
            MaChiNhanh = 9
        };

        // Act
        var result = await _authService.CreateStaffAccountAsync(req, RoleConstants.QuanLyChiNhanh);

        // Assert: Bị từ chối
        Assert.False(result.Success);
        Assert.Contains("online", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // VÒNG 2: CÔ LẬP CHI NHÁNH CỦA QUẢN LÝ (BRANCH SCOPING & STATE ENFORCEMENT)
    // =========================================================================
    [Fact]
    public async Task BranchManager_CreatingStaff_OverridesBranchToOwnBranch()
    {
        // Arrange: Quản lý chi nhánh 2 cố gắng set MaChiNhanh = 5
        var req = new CreateStaffAccountDto
        {
            TenDangNhap = "staff_branch_2",
            MatKhau = "123456",
            TenNhanVien = "Nhân viên cơ sở 2",
            VaiTro = RoleConstants.NhanVien,
            MaChiNhanh = 5 // Cố tình truyền sai
        };

        CreateStaffAccountDto? capturedDto = null;
        _authRepoMock.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _authRepoMock.Setup(r => r.CreateStaffAccountAsync(It.IsAny<CreateStaffAccountDto>(), It.IsAny<string>()))
            .Callback<CreateStaffAccountDto, string>((dto, hash) => capturedDto = dto)
            .ReturnsAsync(102);

        // Giả lập Quản lý chi nhánh có branch id là 2
        var currentManagerBranchId = 2;

        // Act: Service được truyền thêm currentBranchId của người gọi
        var result = await _authService.CreateStaffAccountAsync(req, RoleConstants.QuanLyChiNhanh, currentManagerBranchId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedDto);
        Assert.Equal(2, capturedDto.MaChiNhanh); // Phải bị ép về chi nhánh 2 của người quản lý
    }

    // =========================================================================
    // VÒNG 3: MA TRẬN PHÂN QUYỀN RBAC (RBAC SECURITY MATRIX)
    // =========================================================================
    [Theory]
    [InlineData(RoleConstants.AdminChuoi, RoleConstants.QuanLyChiNhanh, true)]
    [InlineData(RoleConstants.AdminChuoi, RoleConstants.ThuKho, true)]
    [InlineData(RoleConstants.AdminChuoi, RoleConstants.OrderCoordinator, true)]
    [InlineData(RoleConstants.QuanLyChiNhanh, RoleConstants.NhanVien, true)]
    [InlineData(RoleConstants.QuanLyChiNhanh, RoleConstants.ThuKho, true)]
    [InlineData(RoleConstants.QuanLyChiNhanh, RoleConstants.OrderCoordinator, true)]
    [InlineData(RoleConstants.QuanLyChiNhanh, RoleConstants.QuanLyChiNhanh, false)] // Không được tạo quản lý khác
    [InlineData(RoleConstants.QuanLyChiNhanh, RoleConstants.AdminChuoi, false)] // Không được tạo admin
    [InlineData(RoleConstants.NhanVien, RoleConstants.NhanVien, false)] // Nhân viên thường không được tạo tài khoản
    public async Task RolePermissionMatrix_StaffAccountCreation_EnforcesRBAC(
        string actorRole, string targetRole, bool shouldSucceed)
    {
        // Arrange
        var req = new CreateStaffAccountDto
        {
            TenDangNhap = "user_rbac_test",
            MatKhau = "123456",
            TenNhanVien = "User RBAC",
            VaiTro = targetRole,
            MaChiNhanh = 1,
            LoaiThuKho = "CHI_NHANH"
        };

        _authRepoMock.Setup(r => r.IsUsernameTakenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _authRepoMock.Setup(r => r.CreateStaffAccountAsync(It.IsAny<CreateStaffAccountDto>(), It.IsAny<string>()))
            .ReturnsAsync(103);

        // Act
        var result = await _authService.CreateStaffAccountAsync(req, actorRole, actorRole == RoleConstants.QuanLyChiNhanh ? 1 : null);

        // Assert
        Assert.Equal(shouldSucceed, result.Success);
    }
}
