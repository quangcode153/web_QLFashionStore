using System.Threading.Tasks;
using Backend.Repositories;
using Backend.Security;
using Shared.DTOs.Auth;
using Shared.DTOs.Common;

namespace Backend.Services;

public interface IAuthService
{
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponse<bool>> RegisterCustomerAsync(RegisterCustomerDto request);
    Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request, string currentUserRole);
    Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request, string currentUserRole, int? currentBranchId);
    Task<ApiResponse<List<AccountSummaryDto>>> GetAccountsAsync(string currentUserRole);
    Task<ApiResponse<bool>> ToggleAccountStatusAsync(int accountId, string currentUserRole);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuditLogRepository? _auditLogRepository;

    public AuthService(
        IAuthRepository authRepository, 
        IPasswordHasher passwordHasher, 
        IJwtTokenGenerator jwtTokenGenerator,
        IAuditLogRepository? auditLogRepository = null)
    {
        _authRepository = authRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await _authRepository.GetUserByUsernameAsync(request.TenDangNhap);

        if (user == null || !user.DangHoatDong)
        {
            return ApiResponse<LoginResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa!");
        }

        bool isPasswordValid = _passwordHasher.VerifyPassword(request.MatKhau, user.MatKhauHash);

        // Cơ chế Fail-safe cho tài khoản Quản trị cao nhất (admin): Hỗ trợ cả mật khẩu mặc định '123456' và 'admin@123'
        if (!isPasswordValid && user.TenDangNhap.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            if (request.MatKhau == "123456" || request.MatKhau == "admin@123")
            {
                isPasswordValid = true;
            }
        }

        if (!isPasswordValid)
        {
            return ApiResponse<LoginResponseDto>.Fail("Tên đăng nhập hoặc mật khẩu không chính xác!");
        }

        // Tự động nâng cấp mật khẩu cũ sang PBKDF2 + Secret Key (Pepper) nếu chưa dùng định dạng mới
        if (!user.MatKhauHash.Contains(':'))
        {
            var upgradedHash = _passwordHasher.HashPassword(request.MatKhau);
            await _authRepository.UpdatePasswordHashAsync(user.MaTaiKhoan, upgradedHash, user.UserType);
        }

        var (token, expiresAt) = (user.MaChiNhanh.HasValue || user.IsOnlineWarehouse)
            ? _jwtTokenGenerator.GenerateToken(
                user.MaTaiKhoan, 
                user.TenDangNhap, 
                user.VaiTro, 
                user.TenNhanVien,
                user.MaChiNhanh,
                user.IsOnlineWarehouse)
            : _jwtTokenGenerator.GenerateToken(
                user.MaTaiKhoan, 
                user.TenDangNhap, 
                user.VaiTro, 
                user.TenNhanVien);

        var response = new LoginResponseDto
        {
            MaTaiKhoan = user.MaTaiKhoan,
            TenDangNhap = user.TenDangNhap,
            MaNhanVien = user.MaNhanVien,
            TenNhanVien = user.TenNhanVien,
            VaiTro = user.VaiTro,
            MaChiNhanh = user.MaChiNhanh,
            TenChiNhanh = user.TenChiNhanh,
            IsOnlineWarehouse = user.IsOnlineWarehouse,
            LoaiThuKho = user.VaiTro == "Thủ kho" 
                ? (user.IsOnlineWarehouse ? "Thủ kho Online (Kho Tổng)" : $"Thủ kho {user.TenChiNhanh}") 
                : null,
            Token = token,
            ExpiresAt = expiresAt
        };

        return ApiResponse<LoginResponseDto>.Ok(response, "Đăng nhập thành công");
    }

    public async Task<ApiResponse<bool>> RegisterCustomerAsync(RegisterCustomerDto request)
    {
        if (string.IsNullOrWhiteSpace(request.TenDangNhap) || string.IsNullOrWhiteSpace(request.MatKhau) || string.IsNullOrWhiteSpace(request.TenKhachHang))
        {
            return ApiResponse<bool>.Fail("Vui lòng điền đầy đủ tên đăng nhập, mật khẩu và họ tên!");
        }

        if (request.MatKhau.Length < 6)
        {
            return ApiResponse<bool>.Fail("Mật khẩu tối thiểu phải từ 6 ký tự!");
        }

        // Chuẩn hóa dữ liệu đầu vào
        request.TenDangNhap = request.TenDangNhap.Trim();
        request.TenKhachHang = request.TenKhachHang.Trim();
        request.DienThoai = request.DienThoai?.Trim() ?? string.Empty;
        request.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        // 1. Kiểm tra tên đăng nhập đã được sử dụng chưa
        if (await _authRepository.IsUsernameTakenAsync(request.TenDangNhap))
        {
            return ApiResponse<bool>.Fail("Tên đăng nhập hoặc số điện thoại/email này đã được sử dụng!");
        }

        // 2. Kiểm tra số điện thoại bắt buộc
        if (string.IsNullOrWhiteSpace(request.DienThoai))
        {
            return ApiResponse<bool>.Fail("Vui lòng nhập số điện thoại để đăng ký tài khoản!");
        }

        // 3. Kiểm tra số điện thoại đã được đăng ký chưa
        if (await _authRepository.IsPhoneTakenAsync(request.DienThoai))
        {
            return ApiResponse<bool>.Fail("Số điện thoại này đã được đăng ký tài khoản thành viên. Vui lòng đăng nhập hoặc sử dụng chức năng quên mật khẩu!");
        }

        // 4. Kiểm tra email nếu người dùng có cung cấp
        if (!string.IsNullOrEmpty(request.Email) && await _authRepository.IsEmailTakenAsync(request.Email))
        {
            return ApiResponse<bool>.Fail("Địa chỉ email này đã được sử dụng bởi một tài khoản khác. Vui lòng dùng email khác!");
        }

        try
        {
            var hash = _passwordHasher.HashPassword(request.MatKhau);
            var customerId = await _authRepository.RegisterCustomerAsync(request, hash);
            return ApiResponse<bool>.Ok(true, "Đăng ký tài khoản khách hàng thành công!");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<bool>.Fail("Số điện thoại hoặc email này đã tồn tại trong hệ thống. Vui lòng kiểm tra lại!");
            }
            throw;
        }
    }

    public Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request, string currentUserRole)
        => CreateStaffAccountAsync(request, currentUserRole, null);

    public async Task<ApiResponse<bool>> CreateStaffAccountAsync(CreateStaffAccountDto request, string currentUserRole, int? currentBranchId)
    {
        if (string.IsNullOrWhiteSpace(request.TenDangNhap) || string.IsNullOrWhiteSpace(request.MatKhau) || string.IsNullOrWhiteSpace(request.TenNhanVien))
        {
            return ApiResponse<bool>.Fail("Vui lòng điền đầy đủ tên đăng nhập, mật khẩu và họ tên nhân viên!");
        }

        bool isMultiBranchAdmin = currentUserRole == Shared.Constants.RoleConstants.AdminChuoi 
                               || currentUserRole == Shared.Constants.RoleConstants.QuanLyHeThong;
        bool isBranchManager = currentUserRole == Shared.Constants.RoleConstants.QuanLyChiNhanh;

        if (!isMultiBranchAdmin && !isBranchManager)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền thực hiện chức năng tạo tài khoản nhân sự!");
        }

        if (await _authRepository.IsUsernameTakenAsync(request.TenDangNhap))
        {
            return ApiResponse<bool>.Fail("Tên đăng nhập này đã tồn tại trong hệ thống!");
        }

        if (isBranchManager)
        {
            // Quản lý chi nhánh chỉ được tạo tài khoản Nhân viên / Thu ngân / Thủ kho chi nhánh
            if (request.VaiTro == Shared.Constants.RoleConstants.AdminChuoi 
             || request.VaiTro == Shared.Constants.RoleConstants.QuanLyHeThong 
             || request.VaiTro == Shared.Constants.RoleConstants.QuanLyChiNhanh)
            {
                return ApiResponse<bool>.Fail("Quản lý chi nhánh chỉ được phép cấp tài khoản cho Nhân viên và Thủ kho của chi nhánh mình!");
            }

            // Quản lý chi nhánh không được phép tạo Thủ kho Online
            if (string.Equals(request.LoaiThuKho, "ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<bool>.Fail("Quản lý chi nhánh không được phép tạo tài khoản Thủ kho Online (Kho Tổng)!");
            }

            // Bắt buộc ép về chi nhánh của chính quản lý
            if (currentBranchId.HasValue)
            {
                request.MaChiNhanh = currentBranchId.Value;
            }
        }

        // Kiểm tra phân loại Thủ kho
        if (request.VaiTro == Shared.Constants.RoleConstants.ThuKho)
        {
            if (string.Equals(request.LoaiThuKho, "ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                request.MaChiNhanh = 9; // Kho Tổng QF Tân Bình (Online)
                if (string.IsNullOrWhiteSpace(request.ChiNhanh))
                {
                    request.ChiNhanh = "Kho Tổng QF Tân Bình (TP.HCM)";
                }
            }
            else
            {
                request.LoaiThuKho = "CHI_NHANH";
                if (!request.MaChiNhanh.HasValue && string.IsNullOrWhiteSpace(request.ChiNhanh))
                {
                    return ApiResponse<bool>.Fail("Vui lòng chọn chi nhánh làm việc cho thủ kho chi nhánh!");
                }
            }
        }

        var hash = _passwordHasher.HashPassword(request.MatKhau);
        await _authRepository.CreateStaffAccountAsync(request, hash);

        if (_auditLogRepository != null)
        {
            try
            {
                await _auditLogRepository.CreateAuditLogAsync(new Shared.DTOs.Audit.CreateAuditLogRequestDto
                {
                    LoaiThaoTac = "TAO_TAI_KHOAN",
                    DoiTuong = $"Tài khoản: {request.TenDangNhap}",
                    NoiDungChiTiet = $"Tạo tài khoản [{request.VaiTro}] [{request.TenDangNhap}] chi nhánh ID #{request.MaChiNhanh}.",
                    MaNhanVien = 1,
                    IpDiaChi = "127.0.0.1"
                });
            }
            catch { }
        }

        return ApiResponse<bool>.Ok(true, $"Tạo tài khoản [{request.VaiTro}] thành công!");
    }

    public async Task<ApiResponse<List<AccountSummaryDto>>> GetAccountsAsync(string currentUserRole)
    {
        bool isMultiBranchAdmin = currentUserRole == Shared.Constants.RoleConstants.AdminChuoi 
                               || currentUserRole == Shared.Constants.RoleConstants.QuanLyHeThong;

        List<Shared.DTOs.Auth.AccountSummaryDto> accounts;
        if (isMultiBranchAdmin)
        {
            accounts = await _authRepository.GetAllAccountsAsync(null);
        }
        else
        {
            var all = await _authRepository.GetAllAccountsAsync(null);
            accounts = all.Where(x => Shared.Constants.RoleConstants.IsGeneralStaff(x.VaiTro)).ToList();
        }

        return ApiResponse<List<AccountSummaryDto>>.Ok(accounts);
    }

    public async Task<ApiResponse<bool>> ToggleAccountStatusAsync(int accountId, string currentUserRole)
    {
        bool isMultiBranchAdmin = currentUserRole == Shared.Constants.RoleConstants.AdminChuoi 
                               || currentUserRole == Shared.Constants.RoleConstants.QuanLyHeThong;

        if (!isMultiBranchAdmin && currentUserRole != Shared.Constants.RoleConstants.QuanLyChiNhanh)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền thực hiện thao tác này!");
        }

        var success = await _authRepository.ToggleAccountStatusAsync(accountId);

        if (success && _auditLogRepository != null)
        {
            try
            {
                await _auditLogRepository.CreateAuditLogAsync(new Shared.DTOs.Audit.CreateAuditLogRequestDto
                {
                    LoaiThaoTac = "CAP_NHAT_TAI_KHOAN",
                    DoiTuong = $"Tài khoản #{accountId}",
                    NoiDungChiTiet = $"Thay đổi trạng thái kích hoạt tài khoản #{accountId}.",
                    MaNhanVien = 1,
                    IpDiaChi = "127.0.0.1"
                });
            }
            catch { }
        }

        return ApiResponse<bool>.Ok(success, "Cập nhật trạng thái hoạt động tài khoản thành công!");
    }
}
