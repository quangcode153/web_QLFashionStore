using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Frontend.Services;

/// <summary>
/// Triển khai dịch vụ lưu trữ và phát thông báo đồng bộ trạng thái đăng nhập trong bộ nhớ và localStorage.
/// </summary>
public class AuthStateService : IAuthStateService
{
    private readonly IJSRuntime? _jsRuntime;

    public event Action? OnAuthStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken) && !string.IsNullOrEmpty(CurrentUser);
    public string? CurrentUser { get; private set; }
    public string? UserRole { get; private set; }
    public string? StaffName { get; private set; }
    public string? StaffId { get; private set; }
    public string? AuthToken { get; private set; }

    public AuthStateService(IJSRuntime? jsRuntime = null)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_jsRuntime == null) return;

        try
        {
            AuthToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            CurrentUser = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "currentUser");
            UserRole = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userRole");
            StaffName = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "staffName");
            StaffId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "staffId");

            if (string.IsNullOrEmpty(StaffName) && !string.IsNullOrEmpty(CurrentUser))
            {
                StaffName = CurrentUser;
            }

            OnAuthStateChanged?.Invoke();
        }
        catch
        {
            // Fail-safe khi JS interop chưa sẵn sàng (prerendering hoặc unit test context)
        }
    }

    public async Task SetAuthAsync(string token, string username, string role, string? staffName = null, string? staffId = null)
    {
        AuthToken = token;
        CurrentUser = username;
        UserRole = role;
        StaffName = !string.IsNullOrEmpty(staffName) ? staffName : username;
        StaffId = staffId;

        if (_jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "currentUser", username);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userRole", role);
                if (!string.IsNullOrEmpty(staffName))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "staffName", staffName);
                }
                if (!string.IsNullOrEmpty(staffId))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "staffId", staffId);
                }
            }
            catch { }
        }

        OnAuthStateChanged?.Invoke();
    }

    public async Task ClearAuthAsync()
    {
        AuthToken = null;
        CurrentUser = null;
        UserRole = null;
        StaffName = null;
        StaffId = null;

        if (_jsRuntime != null)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "currentUser");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userRole");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "staffName");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "staffId");
            }
            catch { }
        }

        OnAuthStateChanged?.Invoke();
    }
}
