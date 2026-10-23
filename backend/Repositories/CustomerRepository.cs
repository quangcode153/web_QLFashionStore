using System.Data;
using Dapper;
using Shared.DTOs.Customer;

namespace Backend.Repositories;

public interface ICustomerRepository
{
    Task<CustomerProfileDto?> GetProfileByIdAsync(int customerId);
    Task<CustomerProfileDto?> GetProfileByPhoneAsync(string phone);
    Task<IEnumerable<CustomerOrderHistoryItemDto>> GetOrderHistoryAsync(int customerId);
    Task<IEnumerable<RankEvaluationResultDto>> EvaluateRanksAsync();
    Task<IEnumerable<CustomerAccountDto>> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null);
    Task<bool> ToggleCustomerStatusAsync(int customerId);
    Task<bool> UpdateCustomerProfileAsync(UpdateCustomerAccountDto dto);
    Task<int> CreateCustomerAsync(CreateCustomerAccountDto dto, string passwordHash);
}

public class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CustomerRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CustomerProfileDto?> GetProfileByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                kh.dien_thoai AS DienThoai,
                kh.email AS Email,
                kh.diem_thuong AS DiemThuong,
                kh.ma_cap_do AS MaCapDo,
                cd.ten_cap_do AS TenCapDo,
                cd.phan_tram_giam_gia AS PhanTramGiamGia,
                cd.he_so_tich_diem AS HeSoTichDiem,
                dbo.fn_TinhTongChiTieuKhachHang(kh.ma_khach_hang) AS TongChiTieu
            FROM KhachHang kh
            JOIN CapDoThanhVien cd ON kh.ma_cap_do = cd.ma_cap_do
            WHERE kh.dien_thoai = @Phone AND kh.da_xoa = 0;";

        return await connection.QueryFirstOrDefaultAsync<CustomerProfileDto>(sql, new { Phone = phone.Trim() });
    }

    public async Task<CustomerProfileDto?> GetProfileByIdAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                kh.dien_thoai AS DienThoai,
                kh.email AS Email,
                kh.diem_thuong AS DiemThuong,
                kh.ma_cap_do AS MaCapDo,
                cd.ten_cap_do AS TenCapDo,
                cd.phan_tram_giam_gia AS PhanTramGiamGia,
                cd.he_so_tich_diem AS HeSoTichDiem,
                dbo.fn_TinhTongChiTieuKhachHang(kh.ma_khach_hang) AS TongChiTieu
            FROM KhachHang kh
            JOIN CapDoThanhVien cd ON kh.ma_cap_do = cd.ma_cap_do
            WHERE kh.ma_khach_hang = @CustomerId AND kh.da_xoa = 0;";

        return await connection.QueryFirstOrDefaultAsync<CustomerProfileDto>(sql, new { CustomerId = customerId });
    }

    public async Task<IEnumerable<CustomerOrderHistoryItemDto>> GetOrderHistoryAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                hd.ma_hoa_don AS MaHoaDon,
                hd.ngay_lap_hoa_don AS NgayLapHoaDon,
                hd.tien_hang AS TienHang,
                hd.tien_giam_gia AS TienGiamGia,
                hd.tong_thanh_toan AS TongThanhToan,
                hd.trang_thai AS TrangThai,
                hd.so_mat_hang AS SoMatHang,
                actual_hd.dia_chi_giao_hang AS DiaChiGiaoHang,
                actual_hd.loai_don_hang AS LoaiDonHang,
                (
                    SELECT TOP 1 dt.trang_thai
                    FROM DonTraHang dt
                    WHERE dt.ma_hoa_don = hd.ma_hoa_don
                    ORDER BY 
                        CASE 
                            WHEN dt.trang_thai IN (N'Cần duyệt', N'Đang xử lý') THEN 1 
                            ELSE 2 
                        END,
                        dt.ma_don_tra DESC
                ) AS TrangThaiHoanTra,
                ISNULL((SELECT SUM(ct.so_luong) FROM ChiTietHoaDon ct WHERE ct.ma_hoa_don = hd.ma_hoa_don), 0) AS TongSoLuongDaMua,
                ISNULL((SELECT SUM(dt.so_luong) FROM DonTraHang dt WHERE dt.ma_hoa_don = hd.ma_hoa_don AND dt.trang_thai <> N'Từ chối'), 0) AS TongSoLuongDaTra
            FROM dbo.fn_LichSuMuaHangKhachHang(@CustomerId) hd
            LEFT JOIN HoaDon actual_hd ON hd.ma_hoa_don = actual_hd.ma_hoa_don
            ORDER BY hd.ngay_lap_hoa_don DESC;";

        return await connection.QueryAsync<CustomerOrderHistoryItemDto>(sql, new { CustomerId = customerId });
    }

    public async Task<IEnumerable<RankEvaluationResultDto>> EvaluateRanksAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<RankEvaluationResultDto>(
            "dbo.sp_TuDongXetHangThanhVien",
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<CustomerAccountDto>> GetAllCustomersAsync(string? search = null, int? capDo = null, bool? daXoa = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                kh.ma_khach_hang AS MaKhachHang,
                kh.ten_khach_hang AS TenKhachHang,
                kh.ten_dang_nhap AS TenDangNhap,
                kh.dien_thoai AS DienThoai,
                kh.email AS Email,
                kh.diem_thuong AS DiemThuong,
                kh.ma_cap_do AS MaCapDo,
                ISNULL(cd.ten_cap_do, N'Thường') AS TenCapDo,
                ISNULL(cd.phan_tram_giam_gia, 0) AS PhanTramGiamGia,
                ISNULL(dbo.fn_TinhTongChiTieuKhachHang(kh.ma_khach_hang), 0) AS TongChiTieu,
                ISNULL(kh.da_xoa, 0) AS DaXoa,
                (SELECT COUNT(1) FROM HoaDon hd WHERE hd.ma_khach_hang = kh.ma_khach_hang) AS TongSoDonHang
            FROM KhachHang kh
            LEFT JOIN CapDoThanhVien cd ON kh.ma_cap_do = cd.ma_cap_do
            WHERE (@DaXoa IS NULL OR ISNULL(kh.da_xoa, 0) = @DaXoa)
              AND (@CapDo IS NULL OR kh.ma_cap_do = @CapDo)
              AND (
                  @Search IS NULL OR @Search = ''
                  OR kh.ten_khach_hang LIKE '%' + @Search + '%'
                  OR kh.dien_thoai LIKE '%' + @Search + '%'
                  OR kh.email LIKE '%' + @Search + '%'
                  OR kh.ten_dang_nhap LIKE '%' + @Search + '%'
              )
            ORDER BY kh.ma_khach_hang DESC;";

        return await connection.QueryAsync<CustomerAccountDto>(sql, new { Search = search?.Trim(), CapDo = capDo, DaXoa = daXoa });
    }

    public async Task<bool> ToggleCustomerStatusAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE KhachHang
            SET da_xoa = CASE WHEN ISNULL(da_xoa, 0) = 1 THEN 0 ELSE 1 END
            WHERE ma_khach_hang = @CustomerId;";

        var rows = await connection.ExecuteAsync(sql, new { CustomerId = customerId });
        return rows > 0;
    }

    public async Task<bool> UpdateCustomerProfileAsync(UpdateCustomerAccountDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE KhachHang
            SET ten_khach_hang = @TenKhachHang,
                dien_thoai = @DienThoai,
                email = @Email,
                ma_cap_do = @MaCapDo,
                diem_thuong = @DiemThuong,
                da_xoa = ISNULL(@DaXoa, da_xoa)
            WHERE ma_khach_hang = @MaKhachHang;";

        var rows = await connection.ExecuteAsync(sql, dto);
        return rows > 0;
    }

    public async Task<int> CreateCustomerAsync(CreateCustomerAccountDto dto, string passwordHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO KhachHang (ten_khach_hang, ten_dang_nhap, dien_thoai, email, mat_khau_hash, ma_cap_do, diem_thuong, da_xoa)
            VALUES (@TenKhachHang, @TenDangNhap, @DienThoai, @Email, @PasswordHash, @MaCapDo, @DiemThuong, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            dto.TenKhachHang,
            dto.TenDangNhap,
            dto.DienThoai,
            dto.Email,
            PasswordHash = passwordHash,
            dto.MaCapDo,
            dto.DiemThuong
        });
    }
}
