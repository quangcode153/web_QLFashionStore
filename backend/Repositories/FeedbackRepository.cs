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
            INSERT INTO PhanHoiKhachHang (
                ho_ten, dien_thoai, email, loai_phan_hoi, tieu_de, noi_dung, 
                trang_thai, ma_hoa_don, ma_bien_the, so_luong_tra, hinh_thuc_hoan, dia_chi_thu_hoi, ma_chi_nhanh_hen,
                ngay_gui, ngay_cap_nhat
            )
            VALUES (
                @HoTen, @DienThoai, @Email, @LoaiPhanHoi, @TieuDe, @NoiDung, 
                N'Chưa xử lý', NULL, NULL, NULL, NULL, NULL, NULL,
                GETDATE(), GETDATE()
            );
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
                hd.ma_chi_nhanh AS MaChiNhanhDonHang,
                c_hd.ten_chi_nhanh AS TenChiNhanhDonHang,
                p.ngay_gui AS NgayGui,
                p.ngay_cap_nhat AS NgayCapNhat
            FROM PhanHoiKhachHang p
            LEFT JOIN ChiNhanh c ON p.ma_chi_nhanh_hen = c.ma_chi_nhanh
            LEFT JOIN HoaDon hd ON p.ma_hoa_don = hd.ma_hoa_don
            LEFT JOIN ChiNhanh c_hd ON hd.ma_chi_nhanh = c_hd.ma_chi_nhanh
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
                if (!item.MaChiNhanhHen.HasValue)
                {
                    item.MaChiNhanhHen = 9;
                    item.TenChiNhanhHen = "Kho Tổng QF Tân Bình";
                }
            }
            else if (item.MaChiNhanhHen.HasValue)
            {
                item.DiaChiNhanHangHoanVe = item.TenChiNhanhHen ?? "Chi nhánh tiếp nhận";
            }
            else if (item.MaChiNhanhDonHang.HasValue)
            {
                item.MaChiNhanhHen = item.MaChiNhanhDonHang;
                item.TenChiNhanhHen = item.TenChiNhanhDonHang;
                item.DiaChiNhanHangHoanVe = item.TenChiNhanhDonHang ?? "Chi nhánh đặt đơn";
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

            // Chống gửi trùng lặp yêu cầu hoàn trả khi đang có đợt chờ duyệt/xử lý (Anti-Concurrent Duplicate Return Request)
            const string checkExistingReturnSql = @"
                SELECT COUNT(1) 
                FROM DonTraHang 
                WHERE ma_hoa_don = @MaHoaDon 
                  AND trang_thai IN (N'Cần duyệt', N'Đang xử lý');
            ";
            int existingReturnCount = await connection.ExecuteScalarAsync<int>(checkExistingReturnSql, new { request.MaHoaDon }, tran);
            if (existingReturnCount > 0)
            {
                throw new InvalidOperationException($"Đơn hàng #{request.MaHoaDon} đang có yêu cầu hoàn trả đang được xử lý. Quý khách vui lòng chờ CSKH xử lý xong đợt này trước khi gửi thêm yêu cầu.");
            }

            var returnItems = (request.Items != null && request.Items.Any())
                ? request.Items
                : new List<ReturnItemRequestDto> { new() { MaBienThe = request.MaBienThe, SoLuongTra = request.SoLuongTra } };

            // 2. Kiểm tra tất cả biến thể có nằm trong đơn hàng và số lượng trả không vượt quá số lượng còn lại có thể hoàn
            const string checkItemSql = @"
                SELECT 
                    ct.ma_bien_the, 
                    ct.so_luong, 
                    ct.don_gia,
                    ISNULL((
                        SELECT SUM(dt.so_luong)
                        FROM DonTraHang dt
                        WHERE dt.ma_hoa_don = ct.ma_hoa_don 
                          AND dt.ma_bien_the = ct.ma_bien_the 
                          AND dt.trang_thai <> N'Từ chối'
                    ), 0) AS so_luong_da_tra
                FROM ChiTietHoaDon ct 
                WHERE ct.ma_hoa_don = @MaHoaDon AND ct.ma_bien_the = @MaBienThe;
            ";
            foreach (var rItem in returnItems)
            {
                var item = await connection.QueryFirstOrDefaultAsync<dynamic>(checkItemSql, new { request.MaHoaDon, rItem.MaBienThe }, tran);
                if (item == null)
                {
                    throw new InvalidOperationException($"Mặt hàng (Mã biến thể #{rItem.MaBienThe}) không tồn tại trong đơn hàng #{request.MaHoaDon}.");
                }

                int purchasedQty = (int)item.so_luong;
                int alreadyReturnedQty = (int)item.so_luong_da_tra;
                int remainingQty = Math.Max(0, purchasedQty - alreadyReturnedQty);

                if (remainingQty <= 0)
                {
                    throw new InvalidOperationException($"Mặt hàng #{rItem.MaBienThe} trong đơn hàng #{request.MaHoaDon} đã được hoàn trả hết số lượng.");
                }

                if (rItem.SoLuongTra > remainingQty)
                {
                    throw new InvalidOperationException($"Số lượng yêu cầu hoàn trả ({rItem.SoLuongTra}) vượt quá số lượng còn lại có thể hoàn ({remainingQty}) của sản phẩm.");
                }
            }

            // 3. Tạo đúng 1 bản ghi PhanHoiKhachHang đại diện cho toàn bộ đơn đổi trả
            int totalQty = returnItems.Sum(x => x.SoLuongTra);
            int primaryVariantId = returnItems.First().MaBienThe;

            var itemsSummaryLines = returnItems.Select(x =>
                $"- Mặt hàng #{x.MaBienThe}{(string.IsNullOrEmpty(x.TenSanPham) ? "" : $" {x.TenSanPham}")}{(string.IsNullOrEmpty(x.MauSac) ? "" : $" ({x.MauSac}/{x.KichCo})")}: {x.SoLuongTra} sp");
            var combinedContent = returnItems.Count > 1
                ? $"{request.LyDoTra.Trim()}\n\n[Chi tiết hoàn trả {returnItems.Count} mặt hàng]:\n{string.Join("\n", itemsSummaryLines)}"
                : request.LyDoTra.Trim();

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
                    @NoiDung, N'Cần duyệt', @MaHoaDon, @PrimaryVariantId, @TotalQty, 
                    @HinhThucHoan, @DiaChiThuHoi, @MaChiNhanhHen,
                    GETDATE(), GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            int feedbackId = await connection.ExecuteScalarAsync<int>(insertFeedbackSql, new
            {
                request.HoTen,
                request.DienThoai,
                request.Email,
                request.MaHoaDon,
                PrimaryVariantId = primaryVariantId,
                TotalQty = totalQty,
                NoiDung = combinedContent,
                request.HinhThucHoan,
                request.DiaChiThuHoi,
                request.MaChiNhanhHen
            }, tran);

            // 4. Tạo các bản ghi chi tiết trong DonTraHang ở trạng thái 'Cần duyệt' cho từng mặt hàng
            const string insertReturnSql = @"
                INSERT INTO DonTraHang (ma_hoa_don, ma_bien_the, so_luong, ly_do_tra, trang_thai, hinh_thuc_hoan, ngay_tao)
                VALUES (@MaHoaDon, @MaBienThe, @SoLuongTra, @LyDoTra, N'Cần duyệt', @HinhThucHoan, GETDATE());
            ";
            foreach (var rItem in returnItems)
            {
                await connection.ExecuteAsync(insertReturnSql, new
                {
                    request.MaHoaDon,
                    rItem.MaBienThe,
                    rItem.SoLuongTra,
                    LyDoTra = request.LyDoTra.Trim(),
                    HinhThucHoan = request.HinhThucHoan
                }, tran);
            }

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

            // 3. Cập nhật đơn trả hàng sang 'Đã duyệt'
            if (maHoaDon.HasValue)
            {
                const string updateReturnSql = @"
                    UPDATE DonTraHang
                    SET trang_thai = N'Đã duyệt'
                    WHERE ma_hoa_don = @MaHoaDon AND trang_thai = N'Cần duyệt';
                ";
                await connection.ExecuteAsync(updateReturnSql, new { MaHoaDon = maHoaDon.Value }, tran);
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

            if (!maHoaDon.HasValue)
            {
                throw new InvalidOperationException("Thông tin hoàn trả của phiếu khiếu nại không hợp lệ hoặc thiếu dữ liệu mặt hàng.");
            }

            // 2. Xác định chi nhánh lưu giữ kho lỗi
            int targetBranchId;
            if (fb.ma_chi_nhanh_hen != null)
            {
                targetBranchId = (int)fb.ma_chi_nhanh_hen;
            }
            else
            {
                const string getStaffBranchSql = "SELECT ma_chi_nhanh FROM NhanVien WHERE ma_nhan_vien = @StaffId;";
                int? staffBranch = await connection.ExecuteScalarAsync<int?>(getStaffBranchSql, new { StaffId = staffId }, tran);
                targetBranchId = staffBranch ?? 9;
            }

            // Cập nhật phiếu phản hồi sang 'Đã nhận hàng hoàn' kèm chi nhánh tiếp nhận kho lỗi
            const string updateFbSql = @"
                UPDATE PhanHoiKhachHang
                SET trang_thai = N'Đã nhận hàng hoàn',
                    ma_nhan_vien_tiep_nhan = @StaffId,
                    ma_chi_nhanh_hen = @TargetBranchId,
                    ghi_chu_xu_ly = @Notes,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_phan_hoi = @FeedbackId;
            ";
            await connection.ExecuteAsync(updateFbSql, new { FeedbackId = feedbackId, StaffId = staffId, TargetBranchId = targetBranchId, Notes = notes ?? "Đã nhận bưu phẩm hoàn trả và chính thức nhập vào kho hàng lỗi của chi nhánh." }, tran);

            // 3. Lấy thông tin hóa đơn để xác định tỷ lệ chiết khấu thực trả của đơn hàng
            const string getOrderDiscountSql = @"
                SELECT tien_hang, tong_thanh_toan 
                FROM HoaDon 
                WHERE ma_hoa_don = @MaHoaDon;
            ";
            var orderInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(getOrderDiscountSql, new { MaHoaDon = maHoaDon.Value }, tran);
            decimal tienHang = orderInfo != null ? (decimal)orderInfo.tien_hang : 0m;
            decimal tongThanhToan = orderInfo != null ? (decimal)orderInfo.tong_thanh_toan : 0m;
            decimal discountRatio = (tienHang > 0 && tongThanhToan > 0) ? (tongThanhToan / tienHang) : 1.0m;

            // Lấy tất cả mặt hàng cần nhận hoàn trả của đơn này trong DonTraHang kèm ma_don_tra
            const string getReturnItemsSql = @"
                SELECT ma_don_tra, ma_bien_the, so_luong
                FROM DonTraHang
                WHERE ma_hoa_don = @MaHoaDon AND trang_thai IN (N'Cần duyệt', N'Đã duyệt');
            ";
            var returnItemsToReceive = (await connection.QueryAsync<dynamic>(getReturnItemsSql, new { MaHoaDon = maHoaDon.Value }, tran)).ToList();

            if (!returnItemsToReceive.Any() && maBienThe.HasValue && soLuongTra.HasValue && soLuongTra.Value > 0)
            {
                returnItemsToReceive.Add(new { ma_don_tra = (int?)null, ma_bien_the = maBienThe.Value, so_luong = soLuongTra.Value });
            }

            foreach (var rItem in returnItemsToReceive)
            {
                int? donTraId = (int?)rItem.ma_don_tra;
                int btId = (int)rItem.ma_bien_the;
                int qty = (int)rItem.so_luong;

                // Cập nhật DonTraHang: Chỉ cập nhật đúng dòng ma_don_tra đang xử lý, tính theo số tiền thực trả sau chiết khấu
                const string updateReturnSql = @"
                    UPDATE dt
                    SET dt.trang_thai = N'Đã nhận hàng hoàn',
                        dt.so_tien_hoan = ROUND(ISNULL(ct.don_gia * @DiscountRatio * @SoLuongTra, 0), 0),
                        dt.ngay_hoan_tien = GETDATE(),
                        dt.ma_nhan_vien_duyet = @StaffId,
                        dt.ma_chi_nhanh_nhan = @TargetBranchId
                    FROM DonTraHang dt
                    LEFT JOIN ChiTietHoaDon ct ON dt.ma_hoa_don = ct.ma_hoa_don AND dt.ma_bien_the = ct.ma_bien_the
                    WHERE (@DonTraId IS NOT NULL AND dt.ma_don_tra = @DonTraId)
                       OR (@DonTraId IS NULL AND dt.ma_hoa_don = @MaHoaDon AND dt.ma_bien_the = @MaBienThe AND dt.trang_thai IN (N'Cần duyệt', N'Đã duyệt'));
                ";
                await connection.ExecuteAsync(updateReturnSql, new { DonTraId = donTraId, MaHoaDon = maHoaDon.Value, MaBienThe = btId, SoLuongTra = qty, DiscountRatio = discountRatio, StaffId = staffId, TargetBranchId = targetBranchId }, tran);

                // CHÍNH THỨC CỘNG VÀO KHO HÀNG LỖI (so_luong_loi)
                const string restoreStockSql = @"
                    UPDATE BienTheSanPham
                    SET so_luong_loi = ISNULL(so_luong_loi, 0) + @SoLuongTra,
                        ngay_cap_nhat = GETDATE()
                    WHERE ma_bien_the = @MaBienThe;
                ";
                await connection.ExecuteAsync(restoreStockSql, new { SoLuongTra = qty, MaBienThe = btId }, tran);

                // GHI NHẬT KÝ KHO (TRA_HANG_LOI) KÈM CHI NHÁNH NHẬP HÀNG LỖI
                const string logStockSql = @"
                    INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi, thoi_gian, ma_chi_nhanh)
                    VALUES (@MaBienThe, @SoLuongTra, 'TRA_HANG_LOI', @StaffId, 'NHAN_VIEN', GETDATE(), @MaChiNhanh);
                ";
                await connection.ExecuteAsync(logStockSql, new { MaBienThe = btId, SoLuongTra = qty, StaffId = staffId, MaChiNhanh = targetBranchId }, tran);
            }

            // 4. Bảo toàn Hóa đơn gốc (HoaDon): Giữ nguyên trạng thái 'Đã hoàn thành' để bảo toàn tính toàn vẹn chứng từ kế toán.
            // Sổ sách tài chính và tiền hoàn được quản lý độc lập minh bạch qua DonTraHang (Doanh thu thuần = Doanh thu gộp - Tiền hoàn).

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
