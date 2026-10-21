using System.Net.Http.Json;
using Shared.DTOs.Catalog;

namespace Frontend.Services;

public class PolicyApiClient : IPolicyApiClient
{
    private readonly HttpClient _http;

    public PolicyApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<PolicyDto>> GetActivePoliciesAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<IEnumerable<PolicyDto>>("api/policies");
            return resp ?? Enumerable.Empty<PolicyDto>();
        }
        catch
        {
            return Enumerable.Empty<PolicyDto>();
        }
    }

    public async Task<PolicyDto?> GetPolicyBySlugAsync(string slug)
    {
        try
        {
            return await _http.GetFromJsonAsync<PolicyDto>($"api/policies/{slug}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<PolicyDto>> GetAllPoliciesAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<IEnumerable<PolicyDto>>("api/policies/all");
            return resp ?? Enumerable.Empty<PolicyDto>();
        }
        catch
        {
            return Enumerable.Empty<PolicyDto>();
        }
    }

    public async Task<PolicyDto?> GetPolicyByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<PolicyDto>($"api/policies/id/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CreatePolicyAsync(CreateUpdatePolicyDto dto)
    {
        var resp = await _http.PostAsJsonAsync("api/policies", dto);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePolicyAsync(int id, CreateUpdatePolicyDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"api/policies/{id}", dto);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePolicyAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/policies/{id}");
        return resp.IsSuccessStatusCode;
    }
}
