using Backend.Repositories;
using Shared.DTOs.Common;
using Shared.DTOs.Promotion;

namespace Backend.Services;

public interface IPromotionService
{
    Task<ApiResponse<ValidateVoucherResponseDto>> ValidateVoucherAsync(ValidateVoucherRequestDto request);
    Task<ApiResponse<IEnumerable<PromotionDto>>> GetActivePromotionsAsync();
    Task<ApiResponse<IEnumerable<PromotionDto>>> GetAllPromotionsAsync();
    Task<ApiResponse<int>> CreatePromotionAsync(CreatePromotionRequestDto request);
    Task<ApiResponse<bool>> UpdatePromotionAsync(UpdatePromotionRequestDto request);
    Task<ApiResponse<bool>> DeletePromotionAsync(int id);
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

        if (request.MaKhachHang.HasValue && request.MaKhachHang.Value > 0)
        {
            var alreadyUsed = await _promotionRepository.HasCustomerUsedPromotionAsync(request.MaKhachHang.Value, promo.MaKhuyenMai);
            if (alreadyUsed)
            {
                response.Message = "Tài khoản của bạn đã sử dụng mã giảm giá này. Mỗi tài khoản chỉ được áp dụng 1 lần duy nhất.";
                return ApiResponse<ValidateVoucherResponseDto>.Ok(response);
            }
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

    public async Task<ApiResponse<IEnumerable<PromotionDto>>> GetAllPromotionsAsync()
    {
        var promos = await _promotionRepository.GetAllPromotionsAsync();
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

    public async Task<ApiResponse<int>> CreatePromotionAsync(CreatePromotionRequestDto request)
    {
        if (request == null)
            return ApiResponse<int>.Fail("Dữ liệu tạo mã giảm giá không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.MaGiamGia))
            return ApiResponse<int>.Fail("Mã giảm giá không được để trống.");

        if (string.IsNullOrWhiteSpace(request.TenKhuyenMai))
            return ApiResponse<int>.Fail("Tên chương trình khuyến mãi không được để trống.");

        if (request.NgayBatDau.HasValue && request.NgayKetThuc.HasValue && request.NgayBatDau.Value.Date > request.NgayKetThuc.Value.Date)
            return ApiResponse<int>.Fail("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

        if (request.PhanTramGiam < 0 || request.PhanTramGiam > 100)
            return ApiResponse<int>.Fail("Phần trăm giảm giá phải nằm trong khoảng từ 0% đến 100%.");

        if (request.SoTienGiam < 0)
            return ApiResponse<int>.Fail("Số tiền giảm giá không được nhỏ hơn 0.");

        if (request.PhanTramGiam <= 0 && request.SoTienGiam <= 0)
            return ApiResponse<int>.Fail("Vui lòng thiết lập mức giảm theo % hoặc số tiền cụ thể (> 0).");

        if (request.MuaToiThieu < 0)
            return ApiResponse<int>.Fail("Đơn hàng tối thiểu không được nhỏ hơn 0.");

        if (request.SoLuongDung.HasValue && request.SoLuongDung.Value < 0)
            return ApiResponse<int>.Fail("Số lượt sử dụng không được nhỏ hơn 0.");

        var cleanCode = request.MaGiamGia.Trim().ToUpperInvariant();
        var exists = await _promotionRepository.ExistsCodeAsync(cleanCode);
        if (exists)
            return ApiResponse<int>.Fail($"Mã giảm giá '{cleanCode}' đã tồn tại trong hệ thống.");

        var entity = new PromotionEntity
        {
            TenKhuyenMai = request.TenKhuyenMai.Trim(),
            MaGiamGia = cleanCode,
            PhanTramGiam = request.PhanTramGiam,
            SoTienGiam = request.SoTienGiam,
            NgayBatDau = request.NgayBatDau?.Date,
            NgayKetThuc = request.NgayKetThuc?.Date,
            MuaToiThieu = request.MuaToiThieu,
            SoLuongDung = request.SoLuongDung
        };

        var id = await _promotionRepository.CreatePromotionAsync(entity);
        return ApiResponse<int>.Ok(id, "Tạo mã giảm giá thành công.");
    }

    public async Task<ApiResponse<bool>> UpdatePromotionAsync(UpdatePromotionRequestDto request)
    {
        if (request == null || request.MaKhuyenMai <= 0)
            return ApiResponse<bool>.Fail("Mã định danh chương trình khuyến mãi không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.MaGiamGia))
            return ApiResponse<bool>.Fail("Mã giảm giá không được để trống.");

        if (string.IsNullOrWhiteSpace(request.TenKhuyenMai))
            return ApiResponse<bool>.Fail("Tên chương trình khuyến mãi không được để trống.");

        if (request.NgayBatDau.HasValue && request.NgayKetThuc.HasValue && request.NgayBatDau.Value.Date > request.NgayKetThuc.Value.Date)
            return ApiResponse<bool>.Fail("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

        if (request.PhanTramGiam < 0 || request.PhanTramGiam > 100)
            return ApiResponse<bool>.Fail("Phần trăm giảm giá phải nằm trong khoảng từ 0% đến 100%.");

        if (request.SoTienGiam < 0)
            return ApiResponse<bool>.Fail("Số tiền giảm giá không được nhỏ hơn 0.");

        if (request.PhanTramGiam <= 0 && request.SoTienGiam <= 0)
            return ApiResponse<bool>.Fail("Vui lòng thiết lập mức giảm theo % hoặc số tiền cụ thể (> 0).");

        if (request.MuaToiThieu < 0)
            return ApiResponse<bool>.Fail("Đơn hàng tối thiểu không được nhỏ hơn 0.");

        if (request.SoLuongDung.HasValue && request.SoLuongDung.Value < 0)
            return ApiResponse<bool>.Fail("Số lượt sử dụng không được nhỏ hơn 0.");

        var cleanCode = request.MaGiamGia.Trim().ToUpperInvariant();
        var exists = await _promotionRepository.ExistsCodeAsync(cleanCode, request.MaKhuyenMai);
        if (exists)
            return ApiResponse<bool>.Fail($"Mã giảm giá '{cleanCode}' đã được sử dụng bởi chương trình khác.");

        var entity = new PromotionEntity
        {
            MaKhuyenMai = request.MaKhuyenMai,
            TenKhuyenMai = request.TenKhuyenMai.Trim(),
            MaGiamGia = cleanCode,
            PhanTramGiam = request.PhanTramGiam,
            SoTienGiam = request.SoTienGiam,
            NgayBatDau = request.NgayBatDau?.Date,
            NgayKetThuc = request.NgayKetThuc?.Date,
            MuaToiThieu = request.MuaToiThieu,
            SoLuongDung = request.SoLuongDung
        };

        var ok = await _promotionRepository.UpdatePromotionAsync(entity);
        if (!ok)
            return ApiResponse<bool>.Fail("Không tìm thấy chương trình khuyến mãi để cập nhật.");

        return ApiResponse<bool>.Ok(true, "Cập nhật mã giảm giá thành công.");
    }

    public async Task<ApiResponse<bool>> DeletePromotionAsync(int id)
    {
        if (id <= 0)
            return ApiResponse<bool>.Fail("ID mã giảm giá không hợp lệ.");

        var ok = await _promotionRepository.DeletePromotionAsync(id);
        if (!ok)
            return ApiResponse<bool>.Fail("Không tìm thấy mã giảm giá để xóa.");

        return ApiResponse<bool>.Ok(true, "Xóa mã giảm giá thành công.");
    }
}
