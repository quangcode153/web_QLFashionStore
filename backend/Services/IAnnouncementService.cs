using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Announcement;
using Shared.DTOs.Common;

namespace Backend.Services;

public interface IAnnouncementService
{
    Task<ApiResponse<int>> CreateAnnouncementAsync(CreateAnnouncementRequestDto request);
    Task<ApiResponse<IEnumerable<InternalAnnouncementDto>>> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null, int? maTaiKhoan = null);
    Task<ApiResponse<bool>> MarkAsReadAsync(int maThongBao, int maTaiKhoan);
    Task<ApiResponse<int>> MarkAllAsReadAsync(int maTaiKhoan, string? phamVi = null, int? maChiNhanh = null);
}
