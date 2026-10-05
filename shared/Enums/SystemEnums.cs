namespace Shared.Enums;

public enum UserRole
{
    AdminChuoi,
    QuanLyChiNhanh,
    NhanVienBanHang,
    KhachHang
}

public enum OrderStatus
{
    ChoXuLy,
    DaHoanThanh,
    DaHuy
}

public static class OrderStatusHelper
{
    private static readonly Dictionary<string, string> ValidStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ChoXuLy", "Chờ xử lý" },
        { "Chờ xử lý", "Chờ xử lý" },
        { "Cho_Xu_Ly", "Chờ xử lý" },
        { "DaHoanThanh", "Đã hoàn thành" },
        { "Đã hoàn thành", "Đã hoàn thành" },
        { "Da_Hoan_Thanh", "Đã hoàn thành" },
        { "Completed", "Đã hoàn thành" },
        { "DaHuy", "Đã hủy" },
        { "Đã hủy", "Đã hủy" },
        { "Da_Huy", "Đã hủy" },
        { "Cancelled", "Đã hủy" }
    };

    public static bool TryNormalizeStatus(string? input, out string normalizedDbStatus)
    {
        normalizedDbStatus = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return ValidStatusMap.TryGetValue(input.Trim(), out normalizedDbStatus!);
    }

    public static string GetAllowedStatusString() => "Chờ xử lý (ChoXuLy), Đã hoàn thành (DaHoanThanh), Đã hủy (DaHuy)";
}

public enum InventoryChangeReason
{
    BAN_HANG,
    NHAP_HANG,
    TRA_HANG,
    DIEU_CHINH
}
