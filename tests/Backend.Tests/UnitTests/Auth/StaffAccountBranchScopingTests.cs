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

    // =========================================================================
    // VÒNG 4: DANH SÁCH & THAO TÁC KHÓA/MỞ KHÓA TÀI KHOẢN THEO CHI NHÁNH
    // =========================================================================
    [Fact]
    public async Task GetAccounts_BranchManager_OnlyReturnsStaffInOwnBranch()
    {
        // Arrange
        var mockAccounts = new System.Collections.Generic.List<AccountSummaryDto>
        {
            new() { MaTaiKhoan = 1, TenDangNhap = "staff_cn2", VaiTro = RoleConstants.NhanVien, MaChiNhanh = 2 },
            new() { MaTaiKhoan = 2, TenDangNhap = "staff_cn3", VaiTro = RoleConstants.NhanVien, MaChiNhanh = 3 },
            new() { MaTaiKhoan = 3, TenDangNhap = "thukho_cn2", VaiTro = RoleConstants.ThuKho, MaChiNhanh = 2 },
            new() { MaTaiKhoan = 4, TenDangNhap = "admin_master", VaiTro = RoleConstants.AdminChuoi, MaChiNhanh = null }
        };

        _authRepoMock.Setup(r => r.GetAllAccountsAsync(It.IsAny<string?>()))
            .ReturnsAsync(mockAccounts);

        // Act: Quản lý chi nhánh 2 gọi danh sách tài khoản
        var result = await _authService.GetAccountsAsync(RoleConstants.QuanLyChiNhanh, currentBranchId: 2);

        // Assert: Chỉ trả về 2 tài khoản thuộc chi nhánh 2 (staff_cn2, thukho_cn2)
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, a => Assert.Equal(2, a.MaChiNhanh));
    }

    [Theory]
    [InlineData(2, 3, false)] // Quản lý CN 2 cố toggle tài khoản CN 3 -> FAIL
    [InlineData(2, 4, false)] // Quản lý CN 2 cố toggle tài khoản CN 4 -> FAIL
    [InlineData(2, 2, true)]  // Quản lý CN 2 toggle tài khoản CN 2 -> PASS
    public async Task ToggleAccount_BranchManager_IsolationMatrix_EnforcesSameBranch(
        int managerBranchId, int targetAccountBranchId, bool shouldSucceed)
    {
        // Arrange
        var targetAccount = new AccountSummaryDto
        {
            MaTaiKhoan = 10,
            TenDangNhap = "staff_test",
            VaiTro = RoleConstants.NhanVien,
            MaChiNhanh = targetAccountBranchId,
            DangHoatDong = true
        };

        _authRepoMock.Setup(r => r.GetAccountByIdAsync(10))
            .ReturnsAsync(targetAccount);
        _authRepoMock.Setup(r => r.ToggleAccountStatusAsync(10))
            .ReturnsAsync(true);

        // Act
        var result = await _authService.ToggleAccountStatusAsync(10, RoleConstants.QuanLyChiNhanh, managerBranchId);

        // Assert
        Assert.Equal(shouldSucceed, result.Success);
        if (!shouldSucceed)
        {
            Assert.Contains("chi nhánh", result.Message, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ToggleAccount_BranchManager_TargetingAdmin_IsStrictlyForbidden()
    {
        // Arrange: Quản lý chi nhánh cố toggle tài khoản Admin
        var adminAccount = new AccountSummaryDto
        {
            MaTaiKhoan = 1,
            TenDangNhap = "admin",
            VaiTro = RoleConstants.AdminChuoi,
            MaChiNhanh = 2, // Dù có gán chi nhánh nào đi nữa
            DangHoatDong = true
        };

        _authRepoMock.Setup(r => r.GetAccountByIdAsync(1))
            .ReturnsAsync(adminAccount);

        // Act
        var result = await _authService.ToggleAccountStatusAsync(1, RoleConstants.QuanLyChiNhanh, currentBranchId: 2);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Quản trị", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToggleAccount_ChainAdmin_CanToggleAnyBranch()
    {
        // Arrange: Admin chuỗi toggle tài khoản bất kỳ chi nhánh
        var staffAccount = new AccountSummaryDto
        {
            MaTaiKhoan = 20,
            TenDangNhap = "staff_any_branch",
            VaiTro = RoleConstants.NhanVien,
            MaChiNhanh = 5,
            DangHoatDong = true
        };

        _authRepoMock.Setup(r => r.GetAccountByIdAsync(20))
            .ReturnsAsync(staffAccount);
        _authRepoMock.Setup(r => r.ToggleAccountStatusAsync(20))
            .ReturnsAsync(true);

        // Act
        var result = await _authService.ToggleAccountStatusAsync(20, RoleConstants.AdminChuoi, currentBranchId: null);

        // Assert
        Assert.True(result.Success);
    }
}
