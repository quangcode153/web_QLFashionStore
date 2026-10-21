using System.Data;
using Dapper;
using Shared.DTOs.Order;

namespace Backend.Repositories;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode);
    Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode, int? branchId);
    Task AddOrderItemAsync(int orderId, int variantId, int quantity);
    Task UpdateOrderStatusAsync(int orderId, string newStatus);
    Task<IEnumerable<OrderSummaryDto>> GetAllOrdersAsync(int? branchId = null);
    Task<IEnumerable<OrderDetailItemDto>> GetOrderDetailsAsync(int orderId);
    Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId);
    Task<string?> GetOrderStatusAsync(int orderId);
    Task<ReceiptPrintDto?> GetReceiptDetailsAsync(int orderId);
    Task<(bool Success, string Message, ReturnOrderResultDto? Data)> ProcessOrderReturnAsync(int orderId, int variantId, int quantity, string? reason, int staffId);
    Task<OrderReturnLookupDto?> GetOrderForReturnLookupAsync(int orderId);
    Task UpdateOrderShippingAddressAsync(int orderId, string address);
    Task<string?> GetOrderShippingAddressAsync(int orderId);
}

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OrderReturnLookupDto?> GetOrderForReturnLookupAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sqlHeader = @"
            SELECT 
                hd.ma_hoa_don AS MaHoaDon,
                hd.ngay_lap_hoa_don AS NgayLapHoaDon,
                ISNULL(kh.ho_ten, N'Khách vãng lai') AS TenKhachHang,
                kh.dien_thoai AS SdtKhachHang,
                ISNULL(nv.ten_nhan_vien, N'Hội sở / Quầy') AS ThuNgan,
                hd.tong_tien_hang AS TienHang,
                hd.tien_giam_gia AS TienGiamGia,
                hd.tong_thanh_toan AS TongThanhToan,
                hd.trang_thai AS TrangThai,
                hd.dia_chi_giao_hang AS DiaChiGiaoHang,
                hd.ma_chi_nhanh AS MaChiNhanh,
                cn.ten_chi_nhanh AS TenChiNhanh,
                cn.dia_chi AS DiaChiChiNhanh,
                ph.hinh_thuc_hoan AS HinhThucHoan,
                ph.dia_chi_thu_hoi AS DiaChiThuHoi,
                ph.ma_chi_nhanh_hen AS MaChiNhanhHen,
                cn_hen.ten_chi_nhanh AS TenChiNhanhHen,
                cn_hen.dia_chi AS DiaChiChiNhanhHen
            FROM HoaDon hd
            LEFT JOIN KhachHang kh ON hd.ma_khach_hang = kh.ma_khach_hang
            LEFT JOIN NhanVien nv ON hd.ma_nhan_vien = nv.ma_nhan_vien
            LEFT JOIN ChiNhanh cn ON hd.ma_chi_nhanh = cn.ma_chi_nhanh
            OUTER APPLY (
                SELECT TOP 1 hinh_thuc_hoan, dia_chi_thu_hoi, ma_chi_nhanh_hen
                FROM PhanHoiKhachHang
                WHERE ma_hoa_don = hd.ma_hoa_don AND loai_phan_hoi = N'Yêu cầu hoàn trả hàng'
                ORDER BY ngay_gui DESC
            ) ph
            LEFT JOIN ChiNhanh cn_hen ON ph.ma_chi_nhanh_hen = cn_hen.ma_chi_nhanh
            WHERE hd.ma_hoa_don = @orderId;
        ";

        var lookup = await connection.QueryFirstOrDefaultAsync<OrderReturnLookupDto>(sqlHeader, new { orderId });
        if (lookup == null) return null;

        if (!string.IsNullOrWhiteSpace(lookup.DiaChiChiNhanhHen))
        {
            lookup.DiaChiNhanHangHoanVe = $"{lookup.DiaChiChiNhanhHen} ({lookup.TenChiNhanhHen})";
        }
        else if (!string.IsNullOrWhiteSpace(lookup.DiaChiChiNhanh))
        {
            lookup.DiaChiNhanHangHoanVe = $"{lookup.DiaChiChiNhanh} ({lookup.TenChiNhanh})";
        }
        else
        {
            lookup.DiaChiNhanHangHoanVe = "772 Sư Vạn Hạnh, Phường 12, Quận 10, TP.HCM (Kho tiếp nhận QF)";
        }

        const string sqlItems = @"
            SELECT 
                ct.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                ct.don_gia AS DonGia,
                ct.so_luong AS SoLuongMua,
                ISNULL(SUM(dt.so_luong), 0) AS SoLuongDaTra
            FROM ChiTietHoaDon ct
            JOIN BienTheSanPham bt ON ct.ma_bien_the = bt.ma_bien_the
            JOIN SanPham sp ON bt.ma_san_pham = sp.ma_san_pham
            LEFT JOIN DonTraHang dt ON dt.ma_hoa_don = ct.ma_hoa_don AND dt.ma_bien_the = ct.ma_bien_the
            WHERE ct.ma_hoa_don = @orderId
            GROUP BY ct.ma_bien_the, sp.ten_san_pham, bt.mau_sac, bt.kich_co, ct.don_gia, ct.so_luong;
        ";

        var items = (await connection.QueryAsync<OrderReturnLookupItemDto>(sqlItems, new { orderId })).ToList();
        lookup.Items = items;
        return lookup;
    }


    public Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode)
        => CreateOrderAsync(customerId, employeeId, couponCode, null);

    public async Task<int> CreateOrderAsync(int? customerId, int employeeId, string? couponCode, int? branchId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaKhachHang", customerId);
        parameters.Add("@MaNhanVien", employeeId);
        parameters.Add("@MaGiamGia", couponCode);
        parameters.Add("@MaChiNhanh", branchId);
        parameters.Add("@MaHoaDonMoi", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dbo.sp_TaoHoaDonMoi", parameters, commandType: CommandType.StoredProcedure);
        return parameters.Get<int>("@MaHoaDonMoi");
    }

    public async Task AddOrderItemAsync(int orderId, int variantId, int quantity)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);
        parameters.Add("@MaBienThe", variantId);
        parameters.Add("@SoLuong", quantity);

        await connection.ExecuteAsync("dbo.sp_ThemSanPhamVaoHoaDon", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);
        parameters.Add("@TrangThaiMoi", newStatus);

        await connection.ExecuteAsync("dbo.sp_CapNhatTrangThaiHoaDon", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateOrderShippingAddressAsync(int orderId, string address)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE HoaDon SET dia_chi_giao_hang = @address WHERE ma_hoa_don = @orderId",
            new { address, orderId });
    }

    public async Task<string?> GetOrderShippingAddressAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT dia_chi_giao_hang FROM HoaDon WHERE ma_hoa_don = @orderId";
        return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { orderId });
    }

    public async Task<IEnumerable<OrderSummaryDto>> GetAllOrdersAsync(int? branchId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT 
                v.ma_hoa_don AS MaHoaDon,
                v.ngay_lap_hoa_don AS NgayLapHoaDon,
                v.ten_khach_hang AS TenKhachHang,
                v.sdt_khach_hang AS SdtKhachHang,
                v.ten_nhan_vien AS NguoiLapDon,
                v.tien_hang AS TienHang,
                v.tien_giam_gia AS TienGiamGia,
                v.tong_thanh_toan AS TongThanhToan,
                v.trang_thai AS TrangThai,
                v.ma_chi_nhanh AS MaChiNhanh,
                hd.dia_chi_giao_hang AS DiaChiGiaoHang,
                ISNULL(hd.loai_don_hang, 'ONLINE') AS LoaiDonHang,
                ISNULL(cn.ten_chi_nhanh, N'Chi nhánh #' + CAST(v.ma_chi_nhanh AS NVARCHAR(10))) AS TenChiNhanh,
                ISNULL((SELECT SUM(dt.so_tien_hoan) FROM DonTraHang dt WHERE dt.ma_hoa_don = v.ma_hoa_don AND dt.trang_thai IN (N'Đã nhận hàng hoàn', N'Đã hoàn tiền', N'Đã xử lý')), 0) AS SoTienDaHoan
            FROM vw_QuanLyDonHang v
            LEFT JOIN HoaDon hd ON v.ma_hoa_don = hd.ma_hoa_don
            LEFT JOIN ChiNhanh cn ON v.ma_chi_nhanh = cn.ma_chi_nhanh";

        if (branchId.HasValue && branchId.Value > 0)
        {
            sql += " WHERE v.ma_chi_nhanh = @branchId";
        }
        sql += " ORDER BY v.ngay_lap_hoa_don DESC";

        return await connection.QueryAsync<OrderSummaryDto>(sql, new { branchId });
    }

    public async Task<IEnumerable<OrderDetailItemDto>> GetOrderDetailsAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT 
                bt.ma_bien_the AS MaBienThe,
                sp.ten_san_pham AS TenSanPham,
                bt.mau_sac AS MauSac,
                bt.kich_co AS KichCo,
                ct.so_luong AS SoLuong,
                ct.don_gia AS DonGia,
                (ct.so_luong * ct.don_gia) AS ThanhTien,
                ISNULL((
                    SELECT SUM(dt.so_luong)
                    FROM DonTraHang dt
                    WHERE dt.ma_hoa_don = ct.ma_hoa_don 
                      AND dt.ma_bien_the = ct.ma_bien_the 
                      AND dt.trang_thai <> N'Từ chối'
                ), 0) AS SoLuongDaTra
            FROM ChiTietHoaDon ct
            JOIN BienTheSanPham bt ON ct.ma_bien_the = bt.ma_bien_the
            JOIN SanPham sp ON bt.ma_san_pham = sp.ma_san_pham
            WHERE ct.ma_hoa_don = @OrderId;";

        return await connection.QueryAsync<OrderDetailItemDto>(sql, new { OrderId = orderId });
    }

    public async Task<(bool Exists, int? CustomerId)> GetOrderOwnershipAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT ma_hoa_don AS OrderId, ma_khach_hang AS CustomerId FROM HoaDon WHERE ma_hoa_don = @orderId";
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { orderId });
        if (row == null)
        {
            return (false, null);
        }
        return (true, (int?)row.CustomerId);
    }

    public async Task<ReceiptPrintDto?> GetReceiptDetailsAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@MaHoaDon", orderId);

        using var multi = await connection.QueryMultipleAsync("dbo.sp_InChiTietHoaDon", parameters, commandType: CommandType.StoredProcedure);
        
        var header = await multi.ReadFirstOrDefaultAsync<dynamic>();
        if (header == null) return null;

        var items = (await multi.ReadAsync<ReceiptItemDto>()).ToList();

        return new ReceiptPrintDto
        {
            MaHoaDon = (int)header.ma_hoa_don,
            NgayLapHoaDon = (DateTime)header.ngay_lap_hoa_don,
            TenKhachHang = (string)header.ten_khach_hang,
            SdtKhachHang = (string?)header.sdt_khach_hang,
            ThuNgan = (string)header.thu_ngan,
            TenKhuyenMai = (string?)header.ten_khuyen_mai,
            TienHang = (decimal)header.tien_hang,
            TienGiamGia = (decimal)header.tien_giam_gia,
            TongThanhToan = (decimal)header.tong_thanh_toan,
            TrangThai = (string)header.trang_thai,
            Items = items
        };
    }

    public async Task<(bool Success, string Message, ReturnOrderResultDto? Data)> ProcessOrderReturnAsync(
        int orderId, int variantId, int quantity, string? reason, int staffId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Kiểm tra trạng thái đơn hàng
            const string sqlCheckOrder = "SELECT trang_thai FROM HoaDon WHERE ma_hoa_don = @orderId";
            var status = await connection.QueryFirstOrDefaultAsync<string>(sqlCheckOrder, new { orderId }, transaction);
            if (status == null)
            {
                return (false, "Hóa đơn không tồn tại trên hệ thống.", null);
            }

            if (!string.Equals(status.Trim(), "Đã hoàn thành", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Chỉ được phép đổi trả hàng cho hóa đơn đã hoàn thành.", null);
            }

            // 2. Kiểm tra sản phẩm và số lượng trong đơn
            const string sqlCheckItem = "SELECT so_luong FROM ChiTietHoaDon WHERE ma_hoa_don = @orderId AND ma_bien_the = @variantId";
            var purchasedQty = await connection.QueryFirstOrDefaultAsync<int?>(sqlCheckItem, new { orderId, variantId }, transaction);
            if (!purchasedQty.HasValue)
            {
                return (false, "Sản phẩm không có trong chi tiết hóa đơn này.", null);
            }

            if (quantity <= 0)
            {
                return (false, "Số lượng trả hàng phải lớn hơn 0.", null);
            }

            // Kiểm tra số lượng đã trả trước đó để chống trả thừa
            const string sqlCheckReturned = "SELECT ISNULL(SUM(so_luong), 0) FROM DonTraHang WHERE ma_hoa_don = @orderId AND ma_bien_the = @variantId";
            var alreadyReturned = await connection.QuerySingleAsync<int>(sqlCheckReturned, new { orderId, variantId }, transaction);
            var remainingReturnable = purchasedQty.Value - alreadyReturned;

            if (quantity > remainingReturnable)
            {
                return (false, $"Số lượng trả ({quantity}) vượt quá số lượng còn lại có thể trả ({remainingReturnable}) trong đơn hàng.", null);
            }

            // 3. Tạo bản ghi DonTraHang
            const string sqlInsertReturn = @"
                INSERT INTO DonTraHang (ma_hoa_don, ma_bien_the, so_luong, ly_do_tra, trang_thai)
                VALUES (@orderId, @variantId, @quantity, @reason, N'Đã xử lý');
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";
            var returnId = await connection.QuerySingleAsync<int>(sqlInsertReturn, new
            {
                orderId,
                variantId,
                quantity,
                reason = reason ?? "Khách đổi trả hàng"
            }, transaction);

            // 4. Cộng vào kho hàng lỗi (so_luong_loi) - Tuyệt đối không cộng vào tồn kho bán lẻ (so_luong_ton)
            const string sqlUpdateDefectiveStock = @"
                UPDATE BienTheSanPham
                SET so_luong_loi = ISNULL(so_luong_loi, 0) + @quantity,
                    ngay_cap_nhat = GETDATE()
                WHERE ma_bien_the = @variantId;
            ";
            await connection.ExecuteAsync(sqlUpdateDefectiveStock, new { quantity, variantId }, transaction);

            // 5. Ghi nhật ký kho với lý do TRA_HANG_LOI
            const string sqlInsertLog = @"
                INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi)
                VALUES (@variantId, @quantity, 'TRA_HANG_LOI', @staffId, 'NHAN_VIEN');
            ";
            await connection.ExecuteAsync(sqlInsertLog, new { variantId, quantity, staffId }, transaction);

            transaction.Commit();

            var result = new ReturnOrderResultDto
            {
                MaDonTra = returnId,
                MaHoaDon = orderId,
                MaBienThe = variantId,
                SoLuongTra = quantity,
                LyDoTra = reason ?? "Khách đổi trả hàng",
                TrangThai = "Đã xử lý"
            };

            return (true, "Đổi trả hàng thành công và đã nhập vào kho hàng lỗi.", result);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return (false, $"Lỗi xử lý đổi trả hàng: {ex.Message}", null);
        }
    }

    public async Task<string?> GetOrderStatusAsync(int orderId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT trang_thai FROM HoaDon WHERE ma_hoa_don = @OrderId;";
        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { OrderId = orderId });
    }
}
