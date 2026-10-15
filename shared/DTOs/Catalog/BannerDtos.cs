namespace Shared.DTOs.Catalog
{
    public class BannerDto
    {
        public int MaBanner { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string? PhuDe { get; set; }
        public string? GiaNoiBat { get; set; }
        public string? NhanBadge { get; set; }
        public string HinhAnh { get; set; } = string.Empty;
        public string? NutChinhText { get; set; }
        public string? NutChinhLink { get; set; }
        public string? NutPhuText { get; set; }
        public string? NutPhuLink { get; set; }
        public int ThuTu { get; set; } = 1;
        public bool KichHoat { get; set; } = true;
    }

    public class CreateUpdateBannerDto
    {
        public string TieuDe { get; set; } = string.Empty;
        public string? PhuDe { get; set; }
        public string? GiaNoiBat { get; set; }
        public string? NhanBadge { get; set; }
        public string HinhAnh { get; set; } = string.Empty;
        public string? NutChinhText { get; set; }
        public string? NutChinhLink { get; set; }
        public string? NutPhuText { get; set; }
        public string? NutPhuLink { get; set; }
        public int ThuTu { get; set; } = 1;
        public bool KichHoat { get; set; } = true;
    }
}
