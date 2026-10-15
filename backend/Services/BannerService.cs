using Backend.Repositories;
using Shared.DTOs.Catalog;

namespace Backend.Services;

public class BannerService : IBannerService
{
    private readonly IBannerRepository _bannerRepository;

    public BannerService(IBannerRepository bannerRepository)
    {
        _bannerRepository = bannerRepository;
    }

    public async Task<IEnumerable<BannerDto>> GetActiveBannersAsync()
    {
        return await _bannerRepository.GetActiveBannersAsync();
    }

    public async Task<IEnumerable<BannerDto>> GetAllBannersAsync()
    {
        return await _bannerRepository.GetAllBannersAsync();
    }

    public async Task<BannerDto?> GetByIdAsync(int id)
    {
        if (id <= 0) return null;
        return await _bannerRepository.GetByIdAsync(id);
    }

    public async Task<BannerDto> CreateAsync(CreateUpdateBannerDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.TieuDe))
            throw new ArgumentException("Vui lòng nhập tiêu đề cho banner.", nameof(dto.TieuDe));
        if (string.IsNullOrWhiteSpace(dto.HinhAnh))
            throw new ArgumentException("Vui lòng cung cấp đường dẫn hình ảnh cho banner.", nameof(dto.HinhAnh));

        var id = await _bannerRepository.CreateAsync(dto);
        var created = await _bannerRepository.GetByIdAsync(id);
        return created ?? new BannerDto
        {
            MaBanner = id,
            TieuDe = dto.TieuDe,
            PhuDe = dto.PhuDe,
            GiaNoiBat = dto.GiaNoiBat,
            NhanBadge = dto.NhanBadge,
            HinhAnh = dto.HinhAnh,
            NutChinhText = dto.NutChinhText,
            NutChinhLink = dto.NutChinhLink,
            NutPhuText = dto.NutPhuText,
            NutPhuLink = dto.NutPhuLink,
            ThuTu = dto.ThuTu,
            KichHoat = dto.KichHoat
        };
    }

    public async Task<bool> UpdateAsync(int id, CreateUpdateBannerDto dto)
    {
        if (id <= 0)
            throw new ArgumentException("Mã ID banner không hợp lệ.", nameof(id));
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.TieuDe))
            throw new ArgumentException("Vui lòng nhập tiêu đề cho banner.", nameof(dto.TieuDe));
        if (string.IsNullOrWhiteSpace(dto.HinhAnh))
            throw new ArgumentException("Vui lòng cung cấp đường dẫn hình ảnh cho banner.", nameof(dto.HinhAnh));

        return await _bannerRepository.UpdateAsync(id, dto);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (id <= 0) return false;
        return await _bannerRepository.DeleteAsync(id);
    }
}
