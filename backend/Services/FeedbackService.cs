using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Repositories;
using Backend.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Shared.DTOs.Announcement;
using Shared.DTOs.Common;
using Shared.DTOs.Feedback;

namespace Backend.Services;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _feedbackRepository;
    private readonly IHubContext<NotificationHub>? _notificationHub;
    private readonly IAnnouncementRepository? _announcementRepository;

    public FeedbackService(
        IFeedbackRepository feedbackRepository,
        IHubContext<NotificationHub>? notificationHub = null,
        IAnnouncementRepository? announcementRepository = null)
    {
        _feedbackRepository = feedbackRepository;
        _notificationHub = notificationHub;
        _announcementRepository = announcementRepository;
    }


    public async Task<ApiResponse<int>> CreateFeedbackAsync(CreateFeedbackRequestDto request)
    {
        if (request == null)
        {
            return ApiResponse<int>.Fail("Dữ liệu phiếu khiếu nại, góp ý không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.HoTen))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập họ và tên.");
        }

        if (string.IsNullOrWhiteSpace(request.DienThoai))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập số điện thoại liên hệ.");
        }

        if (string.IsNullOrWhiteSpace(request.TieuDe))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập tiêu đề góp ý hoặc khiếu nại.");
        }

        if (string.IsNullOrWhiteSpace(request.NoiDung))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập nội dung chi tiết phản hồi.");
        }

        try
        {
            var feedbackId = await _feedbackRepository.CreateFeedbackAsync(request);

            // Bắn tín hiệu SignalR tức thời tới chuông thông báo Topbar của Admin & CSKH
            if (_notificationHub != null)
            {
                try
                {
                    await _notificationHub.Clients.All.SendAsync(
                        "CustomerFeedbackReceived", 
                        feedbackId, 
                        request.HoTen.Trim(), 
                        request.LoaiPhanHoi, 
                        request.TieuDe.Trim()
                    );
                }
                catch { }
            }

            return ApiResponse<int>.Ok(feedbackId, "Gửi phiếu khiếu nại, góp ý thành công. Bộ phận CSKH sẽ liên hệ hỗ trợ bạn sớm nhất!");
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi tiếp nhận phiếu phản hồi: {ex.Message}");
        }
    }

    public async Task<ApiResponse<IEnumerable<FeedbackDto>>> GetAllFeedbacksAsync()
    {
        var list = await _feedbackRepository.GetAllFeedbacksAsync();
        return ApiResponse<IEnumerable<FeedbackDto>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> UpdateFeedbackStatusAsync(int id, string status, int staffId, string? notes)
    {
        if (id <= 0) return ApiResponse<bool>.Fail("Mã phiếu phản hồi không hợp lệ.");
        var success = await _feedbackRepository.UpdateFeedbackStatusAsync(id, status, staffId, notes);
        return success 
            ? ApiResponse<bool>.Ok(true, "Cập nhật trạng thái phiếu phản hồi thành công.")
            : ApiResponse<bool>.Fail("Không tìm thấy phiếu phản hồi cần cập nhật.");
    }

    public async Task<ApiResponse<int>> CreateReturnRequestAsync(CreateReturnRequestDto request, int? customerId)
    {
        if (request == null)
        {
            return ApiResponse<int>.Fail("Dữ liệu yêu cầu hoàn trả hàng không được để trống.");
        }

        if (request.SoLuongTra <= 0)
        {
            return ApiResponse<int>.Fail("Số lượng hoàn trả phải lớn hơn 0.");
        }

        if (request.MaHoaDon <= 0)
        {
            return ApiResponse<int>.Fail("Mã hóa đơn không hợp lệ.");
        }

        if (request.MaBienThe <= 0)
        {
            return ApiResponse<int>.Fail("Mã biến thể sản phẩm không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.HoTen))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập họ và tên.");
        }

        if (string.IsNullOrWhiteSpace(request.DienThoai))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập số điện thoại liên hệ.");
        }

        if (string.IsNullOrWhiteSpace(request.LyDoTra))
        {
            return ApiResponse<int>.Fail("Vui lòng nhập lý do hoàn trả hàng.");
        }

        string hinhThuc = string.IsNullOrWhiteSpace(request.HinhThucHoan) ? "Thu hồi tại nhà" : request.HinhThucHoan.Trim();
        if (!hinhThuc.Equals("Thu hồi tại nhà", StringComparison.OrdinalIgnoreCase) && 
            !hinhThuc.Equals("Đến chi nhánh", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<int>.Fail("Hình thức hoàn trả không hợp lệ. Các hình thức được chấp nhận: 'Thu hồi tại nhà' hoặc 'Đến chi nhánh'.");
        }

        if (hinhThuc.Equals("Thu hồi tại nhà", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.DiaChiThuHoi))
            {
                return ApiResponse<int>.Fail("Vui lòng cung cấp địa chỉ thu hồi hàng tận nơi.");
            }
        }
        else if (hinhThuc.Equals("Đến chi nhánh", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.MaChiNhanhHen.HasValue || request.MaChiNhanhHen.Value <= 0)
            {
                return ApiResponse<int>.Fail("Vui lòng chọn chi nhánh bạn muốn đến đổi trả hàng.");
            }
        }

        try
        {
            int feedbackId = await _feedbackRepository.CreateReturnRequestAsync(request, customerId);

            // Tự động sinh thông báo nội bộ cho chi nhánh được hẹn trước (Fail-Safe)
            if (_announcementRepository != null && hinhThuc.Equals("Đến chi nhánh", StringComparison.OrdinalIgnoreCase) && request.MaChiNhanhHen.HasValue)
            {
                try
                {
                    var announcement = new CreateAnnouncementRequestDto
                    {
                        TieuDe = $"[Đổi trả hàng] Khách hẹn tại chi nhánh - Đơn #{request.MaHoaDon}",
                        NoiDung = $"Khách hàng {request.HoTen.Trim()} (SĐT: {request.DienThoai.Trim()}) hẹn mang sản phẩm (Biến thể #{request.MaBienThe}, SL: {request.SoLuongTra}) đến chi nhánh để làm thủ tục đổi trả. Lý do: {request.LyDoTra.Trim()}",
                        MucDoUuTien = "Quan trọng",
                        PhamViGui = "Nội bộ chi nhánh",
                        MaChiNhanh = request.MaChiNhanhHen.Value,
                        MaNguoiGui = 1
                    };
                    await _announcementRepository.CreateAnnouncementAsync(announcement);
                }
                catch
                {
                    // Fail-safe: lỗi hệ thống thông báo phụ không làm gián đoạn việc đăng ký của khách
                }
            }

            // Bắn tín hiệu SignalR tức thời tới chuông thông báo Topbar của Admin & CSKH
            if (_notificationHub != null)
            {
                try
                {
                    await _notificationHub.Clients.All.SendAsync(
                        "CustomerFeedbackReceived", 
                        feedbackId, 
                        request.HoTen.Trim(), 
                        "Yêu cầu hoàn trả hàng", 
                        $"Khách hàng gửi yêu cầu hoàn trả cho đơn #DH-{request.MaHoaDon}"
                    );
                }
                catch { }
            }

            return ApiResponse<int>.Ok(feedbackId, "Gửi yêu cầu hoàn trả hàng thành công. Bộ phận CSKH sẽ xem xét và phản hồi sớm nhất!");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResponse<int>.Fail(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<int>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResponse<int>.Fail($"Lỗi khi đăng ký hoàn trả hàng: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> ProcessReturnRequestAsync(int feedbackId, ProcessReturnRequestDto request, int staffId)
    {
        if (feedbackId <= 0)
        {
            return ApiResponse<bool>.Fail("Mã phiếu phản hồi không hợp lệ.");
        }

        if (request == null)
        {
            return ApiResponse<bool>.Fail("Dữ liệu xử lý không được để trống.");
        }

        try
        {
            // Trường hợp 1: Xác nhận đã nhận được hàng hoàn -> chính thức nhập vào kho hàng lỗi
            if (string.Equals(request.Action, "RECEIVE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.Action, "DA_NHAN_HANG", StringComparison.OrdinalIgnoreCase))
            {
                var success = await _feedbackRepository.ProcessReturnReceiptAsync(feedbackId, staffId, request.Notes);
                if (!success)
                {
                    return ApiResponse<bool>.Fail("Không tìm thấy phiếu yêu cầu hoàn trả hoặc trạng thái không hợp lệ để nhận hàng.");
                }

                return ApiResponse<bool>.Ok(true, "Đã nhận bưu phẩm hoàn trả và chính thức nhập sản phẩm vào kho hàng lỗi.");
            }

            // Trường hợp 2: Từ chối yêu cầu hoàn trả
            if (!request.IsApproved || 
                string.Equals(request.Action, "REJECT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.Action, "TU_CHOI", StringComparison.OrdinalIgnoreCase))
            {
                var success = await _feedbackRepository.ProcessReturnRejectionAsync(feedbackId, staffId, request.Notes);
                if (!success)
                {
                    return ApiResponse<bool>.Fail("Không tìm thấy phiếu yêu cầu hoàn trả cần xử lý.");
                }

                return ApiResponse<bool>.Ok(true, "Yêu cầu hoàn trả đã bị từ chối.");
            }

            // Trường hợp 3: Phê duyệt yêu cầu đổi trả (thông báo khách chú ý shipper, chưa cộng kho lỗi)
            var defaultNote = "Yêu cầu hoàn trả của quý khách đã được xác nhận, vui lòng chú ý đến thông báo điện thoại bên ship.";
            var noteToSend = string.IsNullOrWhiteSpace(request.Notes) ? defaultNote : request.Notes;
            var ok = await _feedbackRepository.ProcessReturnApprovalAsync(feedbackId, staffId, noteToSend);
            if (!ok)
            {
                return ApiResponse<bool>.Fail("Không tìm thấy phiếu yêu cầu hoàn trả cần xử lý.");
            }

            return ApiResponse<bool>.Ok(true, "Yêu cầu hoàn trả đã được phê duyệt. Thông báo đã được gửi tới khách hàng.");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResponse<bool>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.Fail($"Lỗi trong quá trình xử lý hoàn trả: {ex.Message}");
        }
    }


    public async Task<ApiResponse<IEnumerable<EligibleReturnOrderDto>>> GetEligibleOrdersForReturnAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return ApiResponse<IEnumerable<EligibleReturnOrderDto>>.Fail("Mã khách hàng không hợp lệ.");
        }

        try
        {
            var list = await _feedbackRepository.GetEligibleOrdersForReturnAsync(customerId);
            return ApiResponse<IEnumerable<EligibleReturnOrderDto>>.Ok(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<EligibleReturnOrderDto>>.Fail($"Lỗi khi lấy danh sách đơn hoàn trả: {ex.Message}");
        }
    }
}
