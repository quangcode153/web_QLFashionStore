using System;
using System.Collections.Generic;

namespace Shared.DTOs.Feedback;

public class ReturnItemRequestDto
{
    public int MaBienThe { get; set; }
    public int SoLuongTra { get; set; }
    public string? TenSanPham { get; set; }
    public string? MauSac { get; set; }
    public string? KichCo { get; set; }
}

public class CreateReturnRequestDto
{
    public int MaHoaDon { get; set; }
    public int MaBienThe { get; set; }
    public int SoLuongTra { get; set; }
    public List<ReturnItemRequestDto> Items { get; set; } = new();
    public string LyDoTra { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string DienThoai { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string HinhThucHoan { get; set; } = "Thu hồi tại nhà";
    public string? DiaChiThuHoi { get; set; }
    public int? MaChiNhanhHen { get; set; }
}

public class ProcessReturnRequestDto
{
    public string Action { get; set; } = "APPROVE";
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }
}


public class EligibleReturnItemDto
{
    public int MaBienThe { get; set; }
    public string TenSanPham { get; set; } = string.Empty;
    public string MauSac { get; set; } = string.Empty;
    public string KichCo { get; set; } = string.Empty;
    public string MaSku { get; set; } = string.Empty;
    public int SoLuongDaMua { get; set; }
    public decimal DonGia { get; set; }
}

public class EligibleReturnOrderDto
{
    public int MaHoaDon { get; set; }
    public DateTime NgayLapHoaDon { get; set; }
    public decimal TongThanhToan { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public List<EligibleReturnItemDto> Items { get; set; } = new();
}
