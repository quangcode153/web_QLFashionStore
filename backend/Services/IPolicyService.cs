using Shared.DTOs.Catalog;

namespace Backend.Services;

public interface IPolicyService
{
    Task<IEnumerable<PolicyDto>> GetActivePoliciesAsync();
    Task<IEnumerable<PolicyDto>> GetAllPoliciesAsync();
    Task<PolicyDto?> GetBySlugAsync(string slug);
    Task<PolicyDto?> GetByIdAsync(int id);
    Task<PolicyDto> CreateAsync(CreateUpdatePolicyDto dto);
    Task<bool> UpdateAsync(int id, CreateUpdatePolicyDto dto);
    Task<bool> DeleteAsync(int id);
}
