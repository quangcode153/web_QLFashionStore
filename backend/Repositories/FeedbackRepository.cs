using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Shared.DTOs.Feedback;

namespace Backend.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FeedbackRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateFeedbackAsync(CreateFeedbackRequestDto request)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO PhanHoiKhachHang (ho_ten, dien_thoai, email, loai_phan_hoi, tieu_de, noi_dung, trang_thai, ngay_gui, ngay_cap_nhat)
            VALUES (@HoTen, @DienThoai, @Email, @LoaiPhanHoi, @TieuDe, @NoiDung, N'Chưa xử lý', GETDATE(), GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
        ";
        return await connection.ExecuteScalarAsync<int>(sql, request);
    }

    public async Task<IEnumerable<FeedbackDto>> GetAllFeedbacksAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                p.ma_phan_hoi AS MaPhanHoi,
                p.ho_ten AS HoTen,
                p.dien_thoai AS DienThoai,
                p.email AS Email,
                p.loai_phan_hoi AS LoaiPhanHoi,
                p.tieu_de AS TieuDe,
                p.noi_dung AS NoiDung,
                p.trang_thai AS TrangThai,
                p.ma_nhan_vien_tiep_nhan AS MaNhanVienTiepNhan,
                p.ghi_chu_xu_ly AS GhiChuXuLy,
                p.ma_hoa_don AS MaHoaDon,
                p.ma_bien_the AS MaBienThe,
                p.so_luong_tra AS SoLuongTra,
                p.hinh_thuc_hoan AS HinhThucHoan,
                p.dia_chi_thu_hoi AS DiaChiThuHoi,
                p.ma_chi_nhanh_hen AS MaChiNhanhHen,
                c.ten_chi_nhanh AS TenChiNhanhHen,
                c.dia_chi AS DiaChiChiNhanhHen,
                p.ngay_gui AS NgayGui,
                p.ngay_cap_nhat AS NgayCapNhat
            FROM PhanHoiKhachHang p
            LEFT JOIN ChiNhanh c ON p.ma_chi_nhanh_hen = c.ma_chi_nhanh
            ORDER BY p.ngay_gui DESC;
        ";
        var list = (await connection.QueryAsync<FeedbackDto>(sql)).ToList();
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.DiaChiChiNhanhHen))
            {
                item.DiaChiNhanHangHoanVe = $"{item.DiaChiChiNhanhHen} ({item.TenChiNhanhHen})";
            }
            else if (string.Equals(item.HinhThucHoan?.Trim(), "Thu hồi tại nhà", StringComparison.OrdinalIgnoreCase))
            {
                item.DiaChiNhanHangHoanVe = "Kho Tổng QF Tân Bình (456 Trường Chinh, Q. Tân Bình, TP.HCM)";
            }
        }
        return list;
    }

    public async Task<bool> UpdateFeedbackStatusAsync(int id, string status, int staffId, string? notes)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE PhanHoiKhachHang
            SET trang_thai = @Status,
                ma_nhan_vien_tiep_nhan = @StaffId,
                ghi_chu_xu_ly = @Notes,
                ngay_cap_nhat = GETDATE()
            WHERE ma_phan_hoi = @Id;
        ";
        var rows = await connection.ExecuteAsync(sql, new { Id = id, Status = status, StaffId = staffId, Notes = notes });
        return rows > 0;
    }

    public async Task<FeedbackDto?> GetFeedbackByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                p.ma_phan_hoi AS MaPhanHoi,
                p.ho_ten AS HoTen,
                p.dien_thoai AS DienThoai,
                p.email AS Email,
                p.loai_phan_hoi AS LoaiPhanHoi,
                p.tieu_de AS TieuDe,
                p.noi_dung AS NoiDung,
                p.trang_thai AS TrangThai,
                p.ma_nhan_vien_tiep_nhan AS MaNhanVienTiepNhan,
                p.ghi_chu_xu_ly AS GhiChuXuLy,
                p.ma_hoa_don AS MaHoaDon,
                p.ma_bien_the AS MaBienThe,
                p.so_luong_tra AS SoLuongTra,
                p.hinh_thuc_hoan AS HinhThucHoan,
                p.dia_chi_thu_hoi AS DiaChiThuHoi,
                p.ma_chi_nhanh_hen AS MaChiNhanhHen,
                c.ten_chi_nhanh AS TenChiNhanhHen,
                p.ngay_gui AS NgayGui,
                p.ngay_cap_nhat AS NgayCapNhat
            FROM PhanHoiKhachHang p
            LEFT JOIN ChiNhanh c ON p.ma_chi_nhanh_hen = c.ma_chi_nhanh
            WHERE p.ma_phan_hoi = @Id;
        ";
        return await connection.QueryFirstOrDefaultAsync<FeedbackDto>(sql, new { Id = id });
    }

    public async Task<int> CreateReturnRequestAsync(CreateReturnRequestDto request, int? customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var tran = connection.BeginTransaction();
        try
        {
            // 1. Kiểm tra đơn hàng phải tồn tại và có trạng thái 'Đã hoàn thành'
            const string checkOrderSql = @"
                SELECT ma_hoa_don, ma_khach_hang, trang_thai 
                FROM HoaDon WITH (UPDLOCK)
                WHERE ma_hoa_don = @MaHoaDon;
            ";
            var order = await connection.QueryFirstOrDefaultAsync<dynamic>(checkOrderSql, new { request.MaHoaDon }, tran);
            if (order == null)
            {
                throw new InvalidOperationException($"Không tìm thấy đơn hàng #{request.MaHoaDon}.");
            }

            string orderStatus = (string)order.trang_thai;
            if (!string.Equals(orderStatus, "Đã hoàn thành", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Chỉ những đơn hàng ở trạng thái 'Đã hoàn thành' mới có thể yêu cầu hoàn trả (Trạng thái hiện tại: {orderStatus}).");
            }

            // Chống IDOR: Nếu có CustomerId, đơn hàng phải thuộc về chính khách hàng đó
            if (customerId.HasValue && customerId.Value > 0)
            {
                int? orderCustomerId = (int?)order.ma_khach_hang;
                if (!orderCustomerId.HasValue || orderCustomerId.Value != customerId.Value)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền yêu cầu hoàn trả cho đơn hàng của khách hàng khác.");
                }
            }

            // 2. Kiểm tra biến thể có nằm trong đơn hàng và số lượng trả hợp lệ
            const string checkItemSql = @"
                SELECT so_luong, don_gia 
                FROM ChiTietHoaDon 
                WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe;
            ";
            var item = await connection.QueryFirstOrDefaultAsync<dynamic>(checkItemSql, new { request.MaHoaDon, request.MaBienThe }, tran);
            if (item == null)
            {
                throw new InvalidOperationException($"Mặt hàng (Mã biến thể #{request.MaBienThe}) không tồn tại trong đơn hàng #{request.MaHoaDon}.");
            }

            int purchasedQty = (int)item.so_luong;
            if (request.SoLuongTra > purchasedQty)
            {
                throw new InvalidOperationException($"Số lượng yêu cầu hoàn trả ({request.SoLuongTra}) không được vượt quá số lượng đã mua ({purchasedQty}).");
            }

            // 3. Tạo bản ghi PhanHoiKhachHang với loại 'Yêu cầu hoàn trả hàng'
            const string insertFeedbackSql = @"
                INSERT INTO PhanHoiKhachHang (
                    ho_ten, dien_thoai, email, loai_phan_hoi, tieu_de, noi_dung, 
                    trang_thai, ma_hoa_don, ma_bien_the, so_luong_tra, 
                    hinh_thuc_hoan, dia_chi_thu_hoi, ma_chi_nhanh_hen,
                    ngay_gui, ngay_cap_nhat
                )
                VALUES (
                    @HoTen, @DienThoai, @Email, N'Yêu cầu hoàn trả hàng', 
                    CONCAT(N'Yêu cầu hoàn trả hàng cho đơn #DH-', @MaHoaDon), 
                    @LyDoTra, N'Cần duyệt', @MaHoaDon, @MaBienThe, @SoLuongTra, 
                    @HinhThucHoan, @DiaChiThuHoi, @MaChiNhanhHen,
                    GETDATE(), GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            int feedbackId = await connection.ExecuteScalarAsync<int>(insertFeedbackSql, request, tran);

            // 4. Tạo bản ghi trong DonTraHang ở trạng thái 'Cần duyệt'
            const string insertReturnSql = @"
                INSERT INTO DonTraHang (ma_hoa_don, ma_bien_the, so_luong, ly_do_tra, trang_thai)
                VALUES (@MaHoaDon, @MaBienThe, @SoLuongTra, @LyDoTra, N'Cần duyệt');
            ";
            await connection.ExecuteAsync(insertReturnSql, request, tran);

            tran.Commit();
            return feedbackId;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task<bool> ProcessReturnApprovalAsync(int feedbackId, int staffId, string? notes)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var tran = connection.BeginTransaction();
        try
        {
            // 1. Lấy thông tin phản hồi hoàn trả
            const string getSql = @"
                SELECT ma_phan_hoi, ma_hoa_don, ma_bien_the, so_luong_tra, trang_thai
                FROM PhanHoiKhachHang WITH (UPDLOCK)
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            var fb = await connection.QueryFirstOrDefaultAsync<dynamic>(getSql, new { FeedbackId = feedbackId }, tran);
            if (fb == null) return false;

            int? maHoaDon = (int?)fb.ma_hoa_don;
            int? maBienThe = (int?)fb.ma_bien_the;

            // 2. Cập nhật phiếu phản hồi sang 'Đã duyệt' kèm thông báo chú ý ship
            const string updateFbSql = @"
                UPDATE PhanHoiKhachHang
                SET trang_thai = N'Đã duyệt',
                    ma_nhan_vien_tiep_nhan = @StaffId,
                    ghi_chu_xu_ly = @Notes,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            var defaultNote = "Yêu cầu hoàn trả của quý khách đã được xác nhận, vui lòng chú ý đến thông báo điện thoại bên ship.";
            await connection.ExecuteAsync(updateFbSql, new { FeedbackId = feedbackId, StaffId = staffId, Notes = notes ?? defaultNote }, tran);

            // 3. Cập nhật đơn trả hàng sang 'Đã duyệt' (Chưa cộng kho lỗi)
            if (maHoaDon.HasValue && maBienThe.HasValue)
            {
                const string updateReturnSql = @"
                    UPDATE DonTraHang
                    SET trang_thai = N'Đã duyệt'
                    WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe;
                ";
                await connection.ExecuteAsync(updateReturnSql, new { MaHoaDon = maHoaDon.Value, MaBienThe = maBienThe.Value }, tran);
            }

            tran.Commit();
            return true;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task<bool> ProcessReturnReceiptAsync(int feedbackId, int staffId, string? notes)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var tran = connection.BeginTransaction();
        try
        {
            // 1. Lấy thông tin phản hồi hoàn trả
            const string getSql = @"
                SELECT ma_phan_hoi, ma_hoa_don, ma_bien_the, so_luong_tra, trang_thai
                FROM PhanHoiKhachHang WITH (UPDLOCK)
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            var fb = await connection.QueryFirstOrDefaultAsync<dynamic>(getSql, new { FeedbackId = feedbackId }, tran);
            if (fb == null) return false;

            int? maHoaDon = (int?)fb.ma_hoa_don;
            int? maBienThe = (int?)fb.ma_bien_the;
            int? soLuongTra = (int?)fb.so_luong_tra;

            if (!maHoaDon.HasValue || !maBienThe.HasValue || !soLuongTra.HasValue || soLuongTra.Value <= 0)
            {
                throw new InvalidOperationException("Thông tin hoàn trả của phiếu khiếu nại không hợp lệ hoặc thiếu dữ liệu mặt hàng.");
            }

            // 2. Cập nhật phiếu phản hồi sang 'Đã nhận hàng hoàn'
            const string updateFbSql = @"
                UPDATE PhanHoiKhachHang
                SET trang_thai = N'Đã nhận hàng hoàn',
                    ma_nhan_vien_tiep_nhan = @StaffId,
                    ghi_chu_xu_ly = @Notes,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            await connection.ExecuteAsync(updateFbSql, new { FeedbackId = feedbackId, StaffId = staffId, Notes = notes ?? "Đã nhận bưu phẩm hoàn trả và chính thức nhập vào kho hàng lỗi." }, tran);

            // 3. Cập nhật đơn trả hàng sang 'Đã nhận hàng hoàn'
            const string updateReturnSql = @"
                UPDATE DonTraHang
                SET trang_thai = N'Đã nhận hàng hoàn'
                WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe;
            ";
            await connection.ExecuteAsync(updateReturnSql, new { MaHoaDon = maHoaDon.Value, MaBienThe = maBienThe.Value }, tran);

            // 4. CHÍNH THỨC CỘNG VÀO KHO HÀNG LỖI (so_luong_loi)
            const string restoreStockSql = @"
                UPDATE BienTheSanPham
                SET so_luong_loi = ISNULL(so_luong_loi, 0) + @SoLuongTra,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_bien_the = @MaBienThe;
            ";
            await connection.ExecuteAsync(restoreStockSql, new { SoLuongTra = soLuongTra.Value, MaBienThe = maBienThe.Value }, tran);

            // 5. GHI NHẬT KÝ KHO (TRA_HANG_LOI)
            const string logStockSql = @"
                INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi, thoi_gian)
                VALUES (@MaBienThe, @SoLuongTra, 'TRA_HANG_LOI', @StaffId, 'NHAN_VIEN', GETDATE());
            ";
            await connection.ExecuteAsync(logStockSql, new { MaBienThe = maBienThe.Value, SoLuongTra = soLuongTra.Value, StaffId = staffId }, tran);

            tran.Commit();
            return true;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }


    public async Task<bool> ProcessReturnRejectionAsync(int feedbackId, int staffId, string? notes)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var tran = connection.BeginTransaction();
        try
        {
            const string getSql = @"
                SELECT ma_hoa_don, ma_bien_the
                FROM PhanHoiKhachHang WITH (UPDLOCK)
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            var fb = await connection.QueryFirstOrDefaultAsync<dynamic>(getSql, new { FeedbackId = feedbackId }, tran);
            if (fb == null) return false;

            int? maHoaDon = (int?)fb.ma_hoa_don;
            int? maBienThe = (int?)fb.ma_bien_the;

            // Cập nhật phản hồi sang 'Đã đóng'
            const string updateFbSql = @"
                UPDATE PhanHoiKhachHang
                SET trang_thai = N'Đã đóng',
                    ma_nhan_vien_tiep_nhan = @StaffId,
                    ghi_chu_xu_ly = @Notes,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            await connection.ExecuteAsync(updateFbSql, new { FeedbackId = feedbackId, StaffId = staffId, Notes = notes ?? "Yêu cầu hoàn trả bị từ chối." }, tran);

            // Cập nhật đơn trả hàng sang 'Từ chối' (không tác động tồn kho)
            if (maHoaDon.HasValue && maBienThe.HasValue)
            {
                const string updateReturnSql = @"
                    UPDATE DonTraHang
                    SET trang_thai = N'Từ chối'
                    WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe;
                ";
                await connection.ExecuteAsync(updateReturnSql, new { MaHoaDon = maHoaDon.Value, MaBienThe = maBienThe.Value }, tran);
            }

            tran.Commit();
            return true;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<EligibleReturnOrderDto>> GetEligibleOrdersForReturnAsync(int customerId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string ordersSql = @"
            SELECT 
                ma_hoa_don AS MaHoaDon,
                ngay_lap_hoa_don AS NgayLapHoaDon,
                tong_thanh_toan AS TongThanhToan,
                trang_thai AS TrangThai
            FROM HoaDon
            WHERE ma_khach_hang = @CustomerId AND trang_thai = N'Đã hoàn thành'
            ORDER BY ngay_lap_hoa_don DESC;
        ";

        var orders = (await connection.QueryAsync<EligibleReturnOrderDto>(ordersSql, new { CustomerId = customerId })).ToList();
        if (!orders.Any())
        {
            return orders;
        }

        const string itemsSql = @"
            SELECT 
                ct.ma_hoa_don AS MaHoaDon,
                ct.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                bt.ma_sku AS MaSku,
                ct.so_luong AS SoLuongDaMua,
                ct.don_gia AS DonGia
            FROM ChiTietHoaDon ct
            JOIN BienTheSanPham bt ON ct.ma_bien_the = bt.ma_bien_the
            JOIN SanPham sp ON bt.ma_san_pham = sp.ma_san_pham
            WHERE ct.ma_hoa_don IN @OrderIds;
        ";

        var orderIds = orders.Select(o => o.MaHoaDon).ToList();
        var items = await connection.QueryAsync<dynamic>(itemsSql, new { OrderIds = orderIds });

        var itemsByOrder = items.GroupBy(i => (int)i.MaHoaDon).ToDictionary(
            g => g.Key,
            g => g.Select(i => new EligibleReturnItemDto
            {
                MaBienThe = (int)i.MaBienThe,
                TenSanPham = (string)i.TenSanPham,
                MauSac = (string)i.MauSac,
                KichCo = (string)i.KichCo,
                MaSku = (string)i.MaSku,
                SoLuongDaMua = (int)i.SoLuongDaMua,
                DonGia = (decimal)i.DonGia
            }).ToList()
        );

        foreach (var order in orders)
        {
            if (itemsByOrder.TryGetValue(order.MaHoaDon, out var orderItems))
            {
                order.Items = orderItems;
            }
        }

        return orders;
    }
}
