using Backend.Repositories;
using Shared.DTOs.Catalog;

namespace Backend.Services;

public class PolicyService : IPolicyService
{
    private readonly IPolicyRepository _policyRepository;

    public PolicyService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<IEnumerable<PolicyDto>> GetActivePoliciesAsync()
    {
        return await _policyRepository.GetActivePoliciesAsync();
    }

    public async Task<IEnumerable<PolicyDto>> GetAllPoliciesAsync()
    {
        return await _policyRepository.GetAllPoliciesAsync();
    }

    public async Task<PolicyDto?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        return await _policyRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant());
    }

    public async Task<PolicyDto?> GetByIdAsync(int id)
    {
        if (id <= 0) return null;
        return await _policyRepository.GetByIdAsync(id);
    }

    public async Task<PolicyDto> CreateAsync(CreateUpdatePolicyDto dto)
    {
        ValidatePolicyDto(dto);

        dto.Slug = dto.Slug.Trim().ToLowerInvariant();
        dto.TieuDe = dto.TieuDe.Trim();
        dto.NoiDung = dto.NoiDung.Trim();
        if (dto.TomTat != null) dto.TomTat = dto.TomTat.Trim();

        var newId = await _policyRepository.CreateAsync(dto);
        var created = await _policyRepository.GetByIdAsync(newId);
        return created ?? throw new InvalidOperationException("Không thể lấy bản ghi chính sách vừa tạo.");
    }

    public async Task<bool> UpdateAsync(int id, CreateUpdatePolicyDto dto)
    {
        if (id <= 0) return false;
        ValidatePolicyDto(dto);

        dto.Slug = dto.Slug.Trim().ToLowerInvariant();
        dto.TieuDe = dto.TieuDe.Trim();
        dto.NoiDung = dto.NoiDung.Trim();
        if (dto.TomTat != null) dto.TomTat = dto.TomTat.Trim();

        return await _policyRepository.UpdateAsync(id, dto);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (id <= 0) return false;
        return await _policyRepository.DeleteAsync(id);
    }

    private static void ValidatePolicyDto(CreateUpdatePolicyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Slug))
            throw new ArgumentException("Định danh đường dẫn (slug) không được để trống.", nameof(dto.Slug));

        if (string.IsNullOrWhiteSpace(dto.TieuDe))
            throw new ArgumentException("Tiêu đề chính sách không được để trống.", nameof(dto.TieuDe));

        if (string.IsNullOrWhiteSpace(dto.NoiDung))
            throw new ArgumentException("Nội dung chi tiết chính sách không được để trống.", nameof(dto.NoiDung));
    }
}
