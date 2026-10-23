using System;

namespace Shared.DTOs.Promotion;

public class ValidateVoucherRequestDto
{
    public string? MaGiamGia { get; set; }
    public decimal TongTienDonHang { get; set; }
    public int? MaKhachHang { get; set; }

    // Hỗ trợ alias linh hoạt theo đặc tả FEAT-PRO-02
    public string? Code
    {
        get => MaGiamGia;
        set => MaGiamGia ??= value;
    }

    public decimal? OrderTotal
    {
        get => TongTienDonHang;
        set
        {
            if (value.HasValue && TongTienDonHang == 0)
            {
                TongTienDonHang = value.Value;
            }
        }
    }

    public int? CustomerId
    {
        get => MaKhachHang;
        set => MaKhachHang ??= value;
    }
}

public class ValidateVoucherResponseDto
{
    public bool Valid { get; set; }
    public bool IsValid => Valid;

    public string MaGiamGia { get; set; } = string.Empty;
    public int? MaKhuyenMai { get; set; }
    public string? TenKhuyenMai { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal FinalTotal => FinalAmount;
    public string Message { get; set; } = string.Empty;
    public string Reason => Message;
}

public class PromotionDto
{
    public int MaKhuyenMai { get; set; }
    public string TenKhuyenMai { get; set; } = string.Empty;
    public string MaGiamGia { get; set; } = string.Empty;
    public decimal PhanTramGiam { get; set; }
    public decimal SoTienGiam { get; set; }
    public decimal? GiamToiDa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public decimal MuaToiThieu { get; set; }
    public int? SoLuongDung { get; set; }
    public bool ConHieuLuc { get; set; }
}

public class CreatePromotionRequestDto
{
    public string TenKhuyenMai { get; set; } = string.Empty;
    public string MaGiamGia { get; set; } = string.Empty;
    public decimal PhanTramGiam { get; set; }
    public decimal SoTienGiam { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public decimal MuaToiThieu { get; set; }
    public int? SoLuongDung { get; set; }
}

public class UpdatePromotionRequestDto : CreatePromotionRequestDto
{
    public int MaKhuyenMai { get; set; }
}

