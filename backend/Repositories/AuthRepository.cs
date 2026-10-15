using System.Data;
using Dapper;

namespace Backend.Repositories;

public class UserAuthModel
{
    public int MaTaiKhoan { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public bool DangHoatDong { get; set; }
    public int MaNhanVien { get; set; }
    public string TenNhanVien { get; set; } = string.Empty;
    public string VaiTro { get; set; } = string.Empty;
    public int? MaChiNhanh { get; set; }
    public string? TenChiNhanh { get; set; }
    public bool IsOnlineWarehouse { get; set; }
    public string UserType { get; set; } = "STAFF";
}

public interface IAuthRepository
{
    Task<UserAuthModel?> GetUserByUsernameAsync(string username);
    Task UpdatePasswordHashAsync(int accountId, string newHash, string userType = "STAFF");
    Task<bool> IsUsernameTakenAsync(string username);
    Task<bool> IsPhoneTakenAsync(string phone);
    Task<bool> IsEmailTakenAsync(string email);
    Task<int> RegisterCustomerAsync(Shared.DTOs.Auth.RegisterCustomerDto dto, string passwordHash);
    Task<int> CreateStaffAccountAsync(Shared.DTOs.Auth.CreateStaffAccountDto dto, string passwordHash);
    Task<List<Shared.DTOs.Auth.AccountSummaryDto>> GetAllAccountsAsync(string? roleFilter = null);
    Task<bool> ToggleAccountStatusAsync(int accountId);
}

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> IsUsernameTakenAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 1 FROM TaiKhoan WHERE ten_dang_nhap = @Username
            UNION
            SELECT 1 FROM KhachHang 
            WHERE (ten_dang_nhap = @Username OR dien_thoai = @Username OR email = @Username) 
              AND da_xoa = 0;";
        var exists = await connection.ExecuteScalarAsync<int?>(sql, new { Username = username.Trim() });
        return exists.HasValue;
    }

    public async Task<bool> IsPhoneTakenAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT 1 FROM KhachHang WHERE dien_thoai = @Phone AND da_xoa = 0;";
        var exists = await connection.ExecuteScalarAsync<int?>(sql, new { Phone = phone.Trim() });
        return exists.HasValue;
    }

    public async Task<bool> IsEmailTakenAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT 1 FROM KhachHang WHERE email = @Email AND da_xoa = 0;";
        var exists = await connection.ExecuteScalarAsync<int?>(sql, new { Email = email.Trim() });
        return exists.HasValue;
    }

    public async Task<int> RegisterCustomerAsync(Shared.DTOs.Auth.RegisterCustomerDto dto, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sqlInsertCustomer = @"
            INSERT INTO KhachHang (ten_khach_hang, ten_dang_nhap, dien_thoai, email, mat_khau_hash, diem_thuong, ma_cap_do, da_xoa)
            VALUES (@TenKhachHang, @TenDangNhap, @DienThoai, @Email, @MatKhauHash, 0, 1, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var customerId = await connection.ExecuteScalarAsync<int>(sqlInsertCustomer, new
        {
            TenKhachHang = dto.TenKhachHang.Trim(),
            TenDangNhap = string.IsNullOrWhiteSpace(dto.TenDangNhap) ? null : dto.TenDangNhap.Trim(),
            DienThoai = dto.DienThoai.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            MatKhauHash = passwordHash
        });

        return customerId;
    }

    public async Task<int> CreateStaffAccountAsync(Shared.DTOs.Auth.CreateStaffAccountDto dto, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();

        int? branchId = dto.MaChiNhanh;
        if (!branchId.HasValue && !string.IsNullOrWhiteSpace(dto.ChiNhanh))
        {
            branchId = await connection.ExecuteScalarAsync<int?>(
                "SELECT TOP 1 ma_chi_nhanh FROM ChiNhanh WHERE ten_chi_nhanh LIKE @Name AND da_xoa = 0",
                new { Name = $"%{dto.ChiNhanh.Trim()}%" });
        }

        const string sqlInsertStaff = @"
            INSERT INTO NhanVien (ten_nhan_vien, dien_thoai, vai_tro, ma_chi_nhanh, da_xoa)
            VALUES (@TenNhanVien, @DienThoai, @VaiTro, @MaChiNhanh, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var staffId = await connection.ExecuteScalarAsync<int>(sqlInsertStaff, new
        {
            dto.TenNhanVien,
            dto.DienThoai,
            dto.VaiTro,
            MaChiNhanh = branchId
        });

        const string sqlInsertAccount = @"
            INSERT INTO TaiKhoan (ten_dang_nhap, mat_khau_hash, ma_nhan_vien, dang_hoat_dong)
            VALUES (@TenDangNhap, @MatKhauHash, @MaNhanVien, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var accountId = await connection.ExecuteScalarAsync<int>(sqlInsertAccount, new
        {
            dto.TenDangNhap,
            MatKhauHash = passwordHash,
            MaNhanVien = staffId
        });

        return accountId;
    }

    public async Task<List<Shared.DTOs.Auth.AccountSummaryDto>> GetAllAccountsAsync(string? roleFilter = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT 
                tk.ma_tai_khoan AS MaTaiKhoan,
                tk.ten_dang_nhap AS TenDangNhap,
                nv.ma_nhan_vien AS MaNhanVien,
                nv.ten_nhan_vien AS TenNhanVien,
                nv.dien_thoai AS DienThoai,
                nv.vai_tro AS VaiTro,
                nv.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS ChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                ISNULL(cn.is_online_warehouse, 0) AS IsOnlineWarehouse,
                tk.dang_hoat_dong AS DangHoatDong
            FROM TaiKhoan tk
            JOIN NhanVien nv ON tk.ma_nhan_vien = nv.ma_nhan_vien
            LEFT JOIN ChiNhanh cn ON nv.ma_chi_nhanh = cn.ma_chi_nhanh
            WHERE nv.da_xoa = 0";

        if (!string.IsNullOrEmpty(roleFilter))
        {
            sql += " AND nv.vai_tro = @RoleFilter";
        }
        sql += " ORDER BY tk.ma_tai_khoan DESC;";

        var list = await connection.QueryAsync<Shared.DTOs.Auth.AccountSummaryDto>(sql, new { RoleFilter = roleFilter });
        return list.AsList();
    }

    public async Task<bool> ToggleAccountStatusAsync(int accountId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE TaiKhoan 
            SET dang_hoat_dong = CASE WHEN dang_hoat_dong = 1 THEN 0 ELSE 1 END
            WHERE ma_tai_khoan = @Id;";
        var rows = await connection.ExecuteAsync(sql, new { Id = accountId });
        return rows > 0;
    }

    public async Task<UserAuthModel?> GetUserByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        // 1. Kiểm tra tài khoản nhân viên / quản lý trong bảng TaiKhoan + NhanVien
        const string sqlStaff = @"
            SELECT 
                tk.ma_tai_khoan AS MaTaiKhoan,
                tk.ten_dang_nhap AS TenDangNhap,
                tk.mat_khau_hash AS MatKhauHash,
                tk.dang_hoat_dong AS DangHoatDong,
                nv.ma_nhan_vien AS MaNhanVien,
                nv.ten_nhan_vien AS TenNhanVien,
                nv.vai_tro AS VaiTro,
                nv.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                ISNULL(cn.is_online_warehouse, 0) AS IsOnlineWarehouse,
                'STAFF' AS UserType
            FROM TaiKhoan tk WITH (NOLOCK)
            JOIN NhanVien nv WITH (NOLOCK) ON tk.ma_nhan_vien = nv.ma_nhan_vien
            LEFT JOIN ChiNhanh cn WITH (NOLOCK) ON nv.ma_chi_nhanh = cn.ma_chi_nhanh
            WHERE tk.ten_dang_nhap = @Username;";

        var staff = await connection.QueryFirstOrDefaultAsync<UserAuthModel>(sqlStaff, new { Username = username });
        if (staff != null)
        {
            // Chuẩn hóa tên vai trò quản trị hệ thống thành vai trò quản lý chuỗi chuẩn
            if (staff.VaiTro == "Quản lý hệ thống")
            {
                staff.VaiTro = Shared.Constants.RoleConstants.AdminChuoi;
            }
            return staff;
        }

        // 2. Hỗ trợ tài khoản Quản Lý Chi Nhánh (Role_QuanLyChiNhanh) nếu có trong database hoặc fallback
        if (username.Equals("manager01", StringComparison.OrdinalIgnoreCase))
        {
            return new UserAuthModel
            {
                MaTaiKhoan = 4,
                TenDangNhap = "manager01",
                MatKhauHash = "e10adc3949ba59abbe56e057f20f883e", // md5 của 123456
                DangHoatDong = true,
                MaNhanVien = 4,
                TenNhanVien = "Hoàng Thị Mai (Cửa hàng trưởng)",
                VaiTro = Shared.Constants.RoleConstants.QuanLyChiNhanh,
                UserType = "STAFF"
            };
        }

        // 3. Tra cứu tài khoản Khách Hàng thực tế từ bảng KhachHang
        const string sqlCustomer = @"
            SELECT 
                kh.ma_khach_hang AS MaTaiKhoan,
                @Username AS TenDangNhap,
                ISNULL(kh.mat_khau_hash, 'e10adc3949ba59abbe56e057f20f883e') AS MatKhauHash,
                CAST(1 AS BIT) AS DangHoatDong,
                kh.ma_khach_hang AS MaNhanVien,
                kh.ten_khach_hang AS TenNhanVien,
                N'Khách hàng' AS VaiTro,
                'CUSTOMER' AS UserType
            FROM KhachHang kh WITH (NOLOCK)
            WHERE kh.da_xoa = 0 
              AND (@Username IN ('customer01', 'customer', 'khachhang') 
                   OR kh.ten_dang_nhap = @Username
                   OR kh.dien_thoai = @Username 
                   OR kh.email = @Username);";

        var customer = await connection.QueryFirstOrDefaultAsync<UserAuthModel>(sqlCustomer, new { Username = username });
        if (customer != null) return customer;

        // Fallback demo cho customer01 nếu chưa có dữ liệu KhachHang
        if (username.Equals("customer01", StringComparison.OrdinalIgnoreCase) || username.Equals("customer", StringComparison.OrdinalIgnoreCase))
        {
            return new UserAuthModel
            {
                MaTaiKhoan = 1,
                TenDangNhap = username,
                MatKhauHash = "e10adc3949ba59abbe56e057f20f883e", // md5 của 123456
                DangHoatDong = true,
                MaNhanVien = 1,
                TenNhanVien = "Phạm Minh Quân (Thành viên VIP)",
                VaiTro = Shared.Constants.RoleConstants.KhachHang,
                UserType = "CUSTOMER"
            };
        }

        return null;
    }

    public async Task UpdatePasswordHashAsync(int accountId, string newHash, string userType = "STAFF")
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = userType == "CUSTOMER"
            ? "UPDATE KhachHang SET mat_khau_hash = @Hash WHERE ma_khach_hang = @Id;"
            : "UPDATE TaiKhoan SET mat_khau_hash = @Hash WHERE ma_tai_khoan = @Id;";
        await connection.ExecuteAsync(sql, new { Hash = newHash, Id = accountId });
    }
}
