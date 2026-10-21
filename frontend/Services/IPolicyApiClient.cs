using Shared.DTOs.Catalog;

namespace Frontend.Services;

public interface IPolicyApiClient
{
    Task<IEnumerable<PolicyDto>> GetActivePoliciesAsync();
    Task<PolicyDto?> GetPolicyBySlugAsync(string slug);
    Task<IEnumerable<PolicyDto>> GetAllPoliciesAsync();
    Task<PolicyDto?> GetPolicyByIdAsync(int id);
    Task<bool> CreatePolicyAsync(CreateUpdatePolicyDto dto);
    Task<bool> UpdatePolicyAsync(int id, CreateUpdatePolicyDto dto);
    Task<bool> DeletePolicyAsync(int id);
}
