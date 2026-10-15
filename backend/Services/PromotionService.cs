using Backend.Repositories;
using Shared.DTOs.Common;
using Shared.DTOs.Promotion;

namespace Backend.Services;

public interface IPromotionService
{
    Task<ApiResponse<ValidateVoucherResponseDto>> ValidateVoucherAsync(ValidateVoucherRequestDto request);
    Task<ApiResponse<IEnumerable<PromotionDto>>> GetActivePromotionsAsync();
}

public class PromotionService : IPromotionService
{
    private readonly IPromotionRepository _promotionRepository;

    public PromotionService(IPromotionRepository promotionRepository)
    {
        _promotionRepository = promotionRepository;
    }

    public async Task<ApiResponse<ValidateVoucherResponseDto>> ValidateVoucherAsync(ValidateVoucherRequestDto request)
    {
        var rawCode = request.MaGiamGia?.Trim();
        var total = request.TongTienDonHang;

        var response = new ValidateVoucherResponseDto
        {
            Valid = false,
            MaGiamGia = rawCode ?? string.Empty,
            DiscountAmount = 0,
            FinalAmount = total
        };

        if (string.IsNullOrWhiteSpace(rawCode))
        {
            response.Message = "Mã giảm giá không hợp lệ hoặc để trống";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        if (total < 0)
        {
            response.Message = "Tổng tiền đơn hàng không hợp lệ (phải >= 0)";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        var normalizedCode = rawCode.ToUpperInvariant();
        var promo = await _promotionRepository.GetByCodeAsync(normalizedCode);
        if (promo == null)
        {
            response.Message = "Mã giảm giá không tồn tại";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        response.MaKhuyenMai = promo.MaKhuyenMai;
        response.TenKhuyenMai = promo.TenKhuyenMai;

        var today = DateTime.UtcNow.Date;
        if (promo.NgayBatDau.HasValue && today < promo.NgayBatDau.Value.Date)
        {
            response.Message = "Chương trình khuyến mãi chưa bắt đầu";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        if (promo.NgayKetThuc.HasValue && today > promo.NgayKetThuc.Value.Date)
        {
            response.Message = "Mã giảm giá đã hết hạn sử dụng";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        if (promo.SoLuongDung.HasValue && promo.SoLuongDung.Value <= 0)
        {
            response.Message = "Mã giảm giá đã hết lượt sử dụng";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        if (total < promo.MuaToiThieu)
        {
            response.Message = "Đơn hàng chưa đạt giá trị tối thiểu";
            return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
        }

        // Tính tiền giảm
        decimal discount = 0;
        if (promo.PhanTramGiam > 0)
        {
            discount = Math.Round((total * promo.PhanTramGiam) / 100m, 2);
            // Giới hạn trần tối đa nếu so_tien_giam > 0
            if (promo.SoTienGiam > 0 && discount > promo.SoTienGiam)
            {
                discount = promo.SoTienGiam;
            }
        }
        else if (promo.SoTienGiam > 0)
        {
            discount = promo.SoTienGiam;
        }

        // Ràng buộc tiền giảm không vượt quá tổng tiền hàng
        if (discount > total)
        {
            discount = total;
        }

        response.Valid = true;
        response.DiscountAmount = discount;
        response.FinalAmount = total - discount;
        response.Message = "Áp dụng mã giảm giá thành công";

        return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<IEnumerable<PromotionDto>>> GetActivePromotionsAsync()
    {
        var promos = await _promotionRepository.GetActivePromotionsAsync();
        var today = DateTime.UtcNow.Date;

        var dtos = promos.Select(p => new PromotionDto
        {
            MaKhuyenMai = p.MaKhuyenMai,
            TenKhuyenMai = p.TenKhuyenMai,
            MaGiamGia = p.MaGiamGia,
            PhanTramGiam = p.PhanTramGiam,
            SoTienGiam = p.SoTienGiam,
            GiamToiDa = p.PhanTramGiam > 0 && p.SoTienGiam > 0 ? p.SoTienGiam : null,
            NgayBatDau = p.NgayBatDau,
            NgayKetThuc = p.NgayKetThuc,
            MuaToiThieu = p.MuaToiThieu,
            SoLuongDung = p.SoLuongDung,
            ConHieuLuc = (!p.NgayKetThuc.HasValue || p.NgayKetThuc.Value.Date >= today) &&
                         (!p.SoLuongDung.HasValue || p.SoLuongDung.Value > 0)
        });

        return ApiResponse<IEnumerable<PromotionDto>>.Ok(dtos);
    }
}
