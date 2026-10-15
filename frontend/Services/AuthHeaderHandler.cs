using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Frontend.Services;

/// <summary>
/// HTTP Message Handler tự động đính kèm JWT Bearer Token từ localStorage vào mọi request ra ngoài.
/// Ngăn chặn toàn diện lỗi 401 Unauthorized do quên gắn header thủ công ở từng hàm API Client.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;

    public AuthHeaderHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch
        {
            // Fail-safe khi JS interop chưa sẵn sàng trong môi trường prerender hoặc unit test
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
