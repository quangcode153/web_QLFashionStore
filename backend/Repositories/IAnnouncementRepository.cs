using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTOs.Announcement;

namespace Backend.Repositories;

public interface IAnnouncementRepository
{
    Task<int> CreateAnnouncementAsync(CreateAnnouncementRequestDto dto);
    Task<IEnumerable<InternalAnnouncementDto>> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null, int? maTaiKhoan = null);
    Task<bool> MarkAsReadAsync(int maThongBao, int maTaiKhoan);
    Task<int> MarkAllAsReadAsync(int maTaiKhoan, string? phamVi = null, int? maChiNhanh = null);
}
