using Shared.DTOs.Catalog;

namespace Backend.Services;

public interface IBannerService
{
    Task<IEnumerable<BannerDto>> GetActiveBannersAsync();
    Task<IEnumerable<BannerDto>> GetAllBannersAsync();
    Task<BannerDto?> GetByIdAsync(int id);
    Task<BannerDto> CreateAsync(CreateUpdateBannerDto dto);
    Task<bool> UpdateAsync(int id, CreateUpdateBannerDto dto);
    Task<bool> DeleteAsync(int id);
}
