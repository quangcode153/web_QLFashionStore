namespace Shared.Constants;

public static class RoleConstants
{
    public const string AdminChuoi = "Admin chuỗi";
    public const string QuanLyHeThong = "Quản lý hệ thống";
    public const string QuanLyChiNhanh = "Quản lý chi nhánh";
    public const string NhanVienBanHang = "Nhân viên bán hàng";
    public const string ThuNgan = "Thu ngân";
    public const string ThuKho = "Thủ kho";
    public const string KhachHang = "Khách hàng";

    public static bool IsStaff(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return role is AdminChuoi or QuanLyHeThong or QuanLyChiNhanh or NhanVienBanHang or ThuNgan or ThuKho;
    }
}

public static class AppConstants
{
    public const string DefaultDbConnectionString = "DefaultConnection";
    public const int DefaultLowStockThreshold = 10;
}
