using System;
using System.Threading.Tasks;

namespace Frontend.Services;

/// <summary>
/// Quản lý và đồng bộ trạng thái đăng nhập tập trung cho toàn bộ ứng dụng Blazor WebAssembly.
/// Ngăn chặn triệt để hiện tượng mất trạng thái đăng nhập khi chuyển trang hoặc nhấn logo nhãn hiệu về trang chủ.
/// </summary>
public interface IAuthStateService
{
    event Action? OnAuthStateChanged;

    bool IsAuthenticated { get; }
    string? CurrentUser { get; }
    string? UserRole { get; }
    string? StaffName { get; }
    string? StaffId { get; }
    string? AuthToken { get; }

    Task InitializeAsync();
    Task SetAuthAsync(string token, string username, string role, string? staffName = null, string? staffId = null);
    Task ClearAuthAsync();
}
