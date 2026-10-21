namespace Shared.Enums;

public enum UserRole
{
    AdminChuoi,
    QuanLyChiNhanh,
    NhanVienBanHang,
    KhachHang,
    OrderCoordinator
}

public enum OrderStatus
{
    ChoXuLy,
    DangGiaoHang,
    DaHoanThanh,
    DaHuy,
    DaHoanTra
}

public static class OrderStatusHelper
{
    private static readonly Dictionary<string, string> ValidStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ChoXuLy", "Chờ xử lý" },
        { "Chờ xử lý", "Chờ xử lý" },
        { "Cho_Xu_Ly", "Chờ xử lý" },
        { "DangGiaoHang", "Đang giao hàng" },
        { "Đang giao hàng", "Đang giao hàng" },
        { "Dang_Giao_Hang", "Đang giao hàng" },
        { "DangGiao", "Đang giao hàng" },
        { "Đang giao", "Đang giao hàng" },
        { "Shipping", "Đang giao hàng" },
        { "Delivering", "Đang giao hàng" },
        { "DaHoanThanh", "Đã hoàn thành" },
        { "Đã hoàn thành", "Đã hoàn thành" },
        { "Da_Hoan_Thanh", "Đã hoàn thành" },
        { "Completed", "Đã hoàn thành" },
        { "DaHuy", "Đã hủy" },
        { "Đã hủy", "Đã hủy" },
        { "Da_Huy", "Đã hủy" },
        { "Cancelled", "Đã hủy" },
        { "DaHoanTra", "Đã hoàn trả" },
        { "Đã hoàn trả", "Đã hoàn trả" },
        { "Da_Hoan_Tra", "Đã hoàn trả" },
        { "Returned", "Đã hoàn trả" }
    };

    public static bool TryNormalizeStatus(string? input, out string normalizedDbStatus)
    {
        normalizedDbStatus = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (ValidStatusMap.TryGetValue(input.Trim(), out var matched))
        {
            normalizedDbStatus = matched;
            return true;
        }

        normalizedDbStatus = string.Empty;
        return false;
    }

    public static string GetAllowedStatusString() => "Chờ xử lý (ChoXuLy), Đang giao hàng (DangGiaoHang), Đã hoàn thành (DaHoanThanh), Đã hoàn trả (DaHoanTra), Đã hủy (DaHuy)";
}

public enum InventoryChangeReason
{
    BAN_HANG,
    NHAP_HANG,
    TRA_HANG,
    TRA_HANG_LOI,
    DIEU_CHINH
}
