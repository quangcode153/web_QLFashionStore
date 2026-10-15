using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Shared.DTOs.Announcement;
using Shared.DTOs.Common;

namespace Backend.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IHubContext<NotificationHub>? _notificationHub;

    public AnnouncementService(
        IAnnouncementRepository announcementRepository,
        IHubContext<NotificationHub>? notificationHub = null)
    {
        _announcementRepository = announcementRepository;
        _notificationHub = notificationHub;
    }

    public async Task<ApiResponse<int>> CreateAnnouncementAsync(CreateAnnouncementRequestDto request)
    {
        if (request == null)
        {
            return ApiResponse<int>.Fail("Dữ liệu thông báo không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.TieuDe))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập tiêu đề thông báo.");
        }

        if (string.IsNullOrWhiteSpace(request.NoiDung))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập nội dung thông báo.");
        }

        try
        {
            if (request.PhamViGui != null && request.PhamViGui.Contains("chi nhánh", StringComparison.OrdinalIgnoreCase) && !request.MaChiNhanh.HasValue)
            {
                request.MaChiNhanh = 1;
            }

            var id = await _announcementRepository.CreateAnnouncementAsync(request);

            // Bắn tín hiệu SignalR tức thời đến tất cả nhân viên / chi nhánh
            if (_notificationHub != null)
            {
                try
                {
                    await _notificationHub.Clients.All.SendAsync(
                        "InternalAnnouncementReceived",
                        id,
                        request.TieuDe.Trim(),
                        request.MucDoUuTien,
                        request.PhamViGui,
                        request.MaChiNhanh,
                        request.NoiDung?.Trim() ?? string.Empty
                    );
                }
                catch { }
            }

            return ApiResponse<int>.Ok(id, "Phát thông báo nội bộ thành công.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi phát thông báo: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<InternalAnnouncementDto>>> GetAnnouncementsAsync(string? phamVi = null, int? maChiNhanh = null, int? maTaiKhoan = null)
    {
        try
        {
            var list = await _announcementRepository.GetAnnouncementsAsync(phamVi, maChiNhanh, maTaiKhoan);
            return ApiResponse<IEnumerable<InternalAnnouncementDto>>.Ok(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<InternalAnnouncementDto>>.Fail($"Lỗi tải danh sách thông báo: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(int maThongBao, int maTaiKhoan)
    {
        if (maThongBao <= 0)
        {
            return ApiResponse<bool>.Fail("Mã thông báo không hợp lệ.");
        }

        if (maTaiKhoan <= 0)
        {
            return ApiResponse<bool>.Fail("Mã tài khoản không hợp lệ.");
        }

        try
        {
            var ok = await _announcementRepository.MarkAsReadAsync(maThongBao, maTaiKhoan);
            return ApiResponse<bool>.Ok(ok, "Đã đánh dấu thông báo là đã đọc.");
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi khi đánh dấu đã đọc: {ex.Message}");
        }
    }

    public async Task<ApiResponse<int>> MarkAllAsReadAsync(int maTaiKhoan, string? phamVi = null, int? maChiNhanh = null)
    {
        if (maTaiKhoan <= 0)
        {
            return ApiResponse<int>.Fail("Mã tài khoản không hợp lệ.");
        }

        try
        {
            var count = await _announcementRepository.MarkAllAsReadAsync(maTaiKhoan, phamVi, maChiNhanh);
            return ApiResponse<int>.Ok(count, $"Đã đánh dấu đã đọc {count} thông báo.");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi đánh dấu đã đọc tất cả: {ex.Message}");
        }
    }
}
