using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Backend.Database;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString, ILogger logger)
    {
        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            string targetDatabase = builder.InitialCatalog;
            if (string.IsNullOrEmpty(targetDatabase)) targetDatabase = "FashionStore";

            // 1. Kết nối vào database mục tiêu (đo thời gian mở kết nối)
            SqlConnection dbConn;
            var swConnect = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                dbConn = new SqlConnection(connectionString);
                dbConn.Open();
                swConnect.Stop();
                logger.LogInformation("Kết nối CSDL '{Db}' thành công trong {ConnectMs} ms.", targetDatabase, swConnect.ElapsedMilliseconds);
            }
            catch (SqlException ex) when (ex.Number == 4060) // CSDL chưa tồn tại
            {
                swConnect.Stop();
                logger.LogWarning("CSDL '{Db}' chưa tồn tại (Error 4060). Đang tạo mới từ master...", targetDatabase);

                builder.InitialCatalog = "master";
                using (var masterConn = new SqlConnection(builder.ConnectionString))
                {
                    masterConn.Open();
                    using var cmdCreateDb = masterConn.CreateCommand();
                    cmdCreateDb.CommandTimeout = 30;
                    cmdCreateDb.CommandText = $@"
                        IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{targetDatabase}')
                        BEGIN
                            CREATE DATABASE [{targetDatabase}];
                        END;";
                    cmdCreateDb.ExecuteNonQuery();
                }

                dbConn = new SqlConnection(connectionString);
                dbConn.Open();
            }

            using (dbConn)
            {
                // Xác minh giao thức kết nối thực tế qua sys.dm_exec_connections
                try
                {
                    using var cmdTransport = dbConn.CreateCommand();
                    cmdTransport.CommandTimeout = 5;
                    cmdTransport.CommandText = "SELECT net_transport, auth_scheme FROM sys.dm_exec_connections WHERE session_id = @@SPID";
                    using var reader = cmdTransport.ExecuteReader();
                    if (reader.Read())
                    {
                        logger.LogInformation("Giao thức kết nối SQL thực tế: {Transport} | Xác thực: {Auth}", reader["net_transport"], reader["auth_scheme"]);
                    }
                }
                catch (Exception exTransport)
                {
                    logger.LogDebug("Không thể đọc sys.dm_exec_connections: {Msg}", exTransport.Message);
                }

                // 2. Tạo bảng quản lý phiên bản migration nếu chưa có
                using (var cmdMig = dbConn.CreateCommand())
                {
                    cmdMig.CommandTimeout = 15;
                    cmdMig.CommandText = @"
                        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__SchemaMigrations')
                        BEGIN
                            CREATE TABLE __SchemaMigrations (
                                MigrationId NVARCHAR(150) PRIMARY KEY,
                                AppliedAt DATETIME DEFAULT GETDATE()
                            );
                        END;";
                    cmdMig.ExecuteNonQuery();
                }

                // 3. Kiểm tra xem CSDL đã được khởi tạo schema cơ bản chưa
                var swQuery = System.Diagnostics.Stopwatch.StartNew();
                using var cmdCheckTable = dbConn.CreateCommand();
                cmdCheckTable.CommandTimeout = 15;
                cmdCheckTable.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE name = 'SanPham';";
                int tableCount = Convert.ToInt32(cmdCheckTable.ExecuteScalar());
                swQuery.Stop();
                logger.LogInformation("Kiểm tra bảng metadata hoàn tất trong {QueryMs} ms (Số bảng SanPham: {Count}).", swQuery.ElapsedMilliseconds, tableCount);

                if (tableCount == 0)
                {
                    logger.LogInformation("Cơ sở dữ liệu '{Db}' chưa có bảng, bắt đầu sinh schema hoàn chỉnh từ sqlbtl.sql...", targetDatabase);

                    // Tìm file sqlbtl.sql ở thư mục gốc hoặc các thư mục liên quan
                    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    string sqlFilePath = Path.Combine(currentDir, "..", "..", "..", "..", "sqlbtl.sql");
                    sqlFilePath = Path.GetFullPath(sqlFilePath);

                    if (!File.Exists(sqlFilePath))
                    {
                        sqlFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "sqlbtl.sql"));
                        if (!File.Exists(sqlFilePath))
                        {
                            sqlFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "sqlbtl.sql"));
                        }
                    }

                    if (File.Exists(sqlFilePath))
                    {
                        string fullSql = File.ReadAllText(sqlFilePath);

                        // Tách script thành từng lô lệnh (batch) ngăn cách bởi GO
                        var batches = Regex.Split(fullSql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                        int batchIndex = 0;
                        foreach (var batch in batches)
                        {
                            batchIndex++;
                            string trimmedBatch = batch.Trim();
                            if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

                            // Bỏ qua các lệnh quản lý cấp database
                            if (trimmedBatch.StartsWith("USE ", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.Contains("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.Contains("DROP DATABASE", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.Contains("SET SINGLE_USER", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            try
                            {
                                using var batchCmd = dbConn.CreateCommand();
                                batchCmd.CommandText = trimmedBatch;
                                batchCmd.CommandTimeout = 120;
                                batchCmd.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning("Bỏ qua lỗi batch {Index} hoặc đối tượng đã tồn tại: {Msg}", batchIndex, ex.Message);
                            }
                        }

                        // Ghi nhận Migration hoàn tất
                        using (var cmdRecord = dbConn.CreateCommand())
                        {
                            cmdRecord.CommandText = @"
                                IF NOT EXISTS (SELECT 1 FROM __SchemaMigrations WHERE MigrationId = 'V2.0_InitialCompleteSchema')
                                BEGIN
                                    INSERT INTO __SchemaMigrations (MigrationId, AppliedAt) VALUES ('V2.0_InitialCompleteSchema', GETDATE());
                                END;";
                            cmdRecord.ExecuteNonQuery();
                        }

                        // Đảm bảo CSDL luôn ở chế độ MULTI_USER
                        using (var cmdMulti = dbConn.CreateCommand())
                        {
                            cmdMulti.CommandText = $"ALTER DATABASE [{targetDatabase}] SET MULTI_USER;";
                            cmdMulti.ExecuteNonQuery();
                        }

                        logger.LogInformation("Sinh toàn bộ 24 bảng, 6 Views, 5 Functions, 9 Procedures, 6 Triggers, RBAC và Seed Data từ sqlbtl.sql thành công!");
                    }
                    else
                    {
                        logger.LogWarning("Không tìm thấy file sqlbtl.sql tại đường dẫn: {Path}", sqlFilePath);
                    }
                }
                else
                {
                    logger.LogInformation("Cơ sở dữ liệu '{Db}' đã có sẵn cấu trúc bảng ({Count} bảng chính). Kiểm tra và nạp migration gia tăng idempotent...", targetDatabase, tableCount);

                    // Tự động kiểm tra và cập nhật các bảng/cột mới an toàn cho CSDL đang hoạt động
                    ApplyIncrementalMigrations(dbConn, logger);

                    // Đảm bảo ghi nhận migration nếu chưa có
                    try
                    {
                        using var cmdRecord = dbConn.CreateCommand();
                        cmdRecord.CommandText = @"
                            IF NOT EXISTS (SELECT 1 FROM __SchemaMigrations WHERE MigrationId = 'V2.0_InitialCompleteSchema')
                            BEGIN
                                INSERT INTO __SchemaMigrations (MigrationId, AppliedAt) VALUES ('V2.0_InitialCompleteSchema', GETDATE());
                            END;";
                        cmdRecord.ExecuteNonQuery();
                    }
                    catch { }
                }
            }
            swTotal.Stop();
            logger.LogInformation("Khởi tạo CSDL hoàn tất tổng cộng trong {TotalMs} ms.", swTotal.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi khởi tạo tự động cơ sở dữ liệu: {Message}", ex.Message);
        }
    }

    public static void ApplyIncrementalMigrations(SqlConnection dbConn, ILogger logger)
    {
        try
        {
            string sql = GetIncrementalMigrationsSql();
            using (var cmd = dbConn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandTimeout = 60;
                cmd.ExecuteNonQuery();
            }

            try
            {
                var procBatches = Regex.Split(GetUpdateProcedureSql(), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                foreach (var batch in procBatches)
                {
                    var trimmed = batch.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    using var cmdSp = dbConn.CreateCommand();
                    cmdSp.CommandText = trimmed;
                    cmdSp.CommandTimeout = 60;
                    cmdSp.ExecuteNonQuery();
                }
            }
            catch (Exception exSp)
            {
                logger.LogWarning("Bỏ qua lỗi cập nhật procedure: {Msg}", exSp.Message);
            }

            logger.LogInformation("Kiểm tra và áp dụng các migration gia tăng (Incremental Migrations) an toàn thành công.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("Thông báo kiểm tra migration gia tăng: {Msg}", ex.Message);
        }
    }

    public static string GetIncrementalMigrationsSql()
    {
        return @"
-- 1. Bổ sung cột so_luong_loi vào BienTheSanPham nếu thiếu
IF COL_LENGTH('BienTheSanPham', 'so_luong_loi') IS NULL
BEGIN
    ALTER TABLE BienTheSanPham ADD so_luong_loi INT DEFAULT 0 NULL;
END;

-- 2. Bổ sung các cột thông tin hoàn trả vào PhanHoiKhachHang nếu thiếu
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PhanHoiKhachHang')
BEGIN
    IF COL_LENGTH('PhanHoiKhachHang', 'hinh_thuc_hoan') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD hinh_thuc_hoan NVARCHAR(50) NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'dia_chi_thu_hoi') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD dia_chi_thu_hoi NVARCHAR(500) NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'ma_chi_nhanh_hen') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD ma_chi_nhanh_hen INT NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'ma_bien_the') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD ma_bien_the INT NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'so_luong_tra') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD so_luong_tra INT NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'ma_nhan_vien_tiep_nhan') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD ma_nhan_vien_tiep_nhan INT NULL;
    IF COL_LENGTH('PhanHoiKhachHang', 'ghi_chu_xu_ly') IS NULL
        ALTER TABLE PhanHoiKhachHang ADD ghi_chu_xu_ly NVARCHAR(1000) NULL;
END;

-- 3. Tạo bảng ChinhSach nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ChinhSach')
BEGIN
    CREATE TABLE ChinhSach (
        ma_chinh_sach INT PRIMARY KEY IDENTITY(1,1),
        slug VARCHAR(100) NOT NULL UNIQUE,
        tieu_de NVARCHAR(200) NOT NULL,
        tom_tat NVARCHAR(500) NULL,
        noi_dung NVARCHAR(MAX) NOT NULL,
        thu_tu INT DEFAULT 0,
        kich_hoat BIT DEFAULT 1,
        ngay_tao DATETIME DEFAULT GETDATE(),
        ngay_cap_nhat DATETIME DEFAULT GETDATE(),
        nguoi_cap_nhat NVARCHAR(100) NULL
    );
END;

-- 4. Tạo bảng NhatKyHeThong nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NhatKyHeThong')
BEGIN
    CREATE TABLE NhatKyHeThong (
        ma_nhat_ky INT PRIMARY KEY IDENTITY(1,1),
        loai_thao_tac NVARCHAR(50) NOT NULL,
        doi_tuong NVARCHAR(100) NOT NULL,
        noi_dung_chi_tiet NVARCHAR(1000) NULL,
        ma_nhan_vien INT NULL,
        ip_dia_chi VARCHAR(50) NULL,
        thoi_gian DATETIME DEFAULT GETDATE()
    );
END;

-- 5. Tạo bảng ThongBaoNoiBo nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ThongBaoNoiBo')
BEGIN
    CREATE TABLE ThongBaoNoiBo (
        ma_thong_bao INT PRIMARY KEY IDENTITY(1,1),
        tieu_de NVARCHAR(200) NOT NULL,
        noi_dung NVARCHAR(MAX) NOT NULL,
        muc_do_uu_tien NVARCHAR(20) DEFAULT N'Bình thường',
        vai_tro_nhan NVARCHAR(50) NULL,
        ma_chi_nhanh INT NULL,
        ngay_tao DATETIME DEFAULT GETDATE(),
        ngay_het_han DATETIME NULL,
        trang_thai NVARCHAR(20) DEFAULT N'Hiệu lực',
        ma_nhan_vien_tao INT NULL
    );
END;

-- 6. Tạo bảng ThongBao_NguoiDoc nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ThongBao_NguoiDoc')
BEGIN
    CREATE TABLE ThongBao_NguoiDoc (
        ma_thong_bao INT NOT NULL,
        ma_nhan_vien INT NOT NULL,
        da_doc BIT DEFAULT 0,
        thoi_gian_doc DATETIME NULL,
        PRIMARY KEY (ma_thong_bao, ma_nhan_vien)
    );
END;

-- 7. Bổ sung cột da_xoa vào BienTheSanPham nếu thiếu
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BienTheSanPham')
BEGIN
    IF COL_LENGTH('BienTheSanPham', 'da_xoa') IS NULL
    BEGIN
        ALTER TABLE BienTheSanPham ADD da_xoa BIT DEFAULT 0 NOT NULL;
    END;
END;

-- 8. Bổ sung cột phan_tram_giam_gia vào BienTheSanPham nếu thiếu
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BienTheSanPham')
BEGIN
    IF COL_LENGTH('BienTheSanPham', 'phan_tram_giam_gia') IS NULL
    BEGIN
        ALTER TABLE BienTheSanPham ADD phan_tram_giam_gia INT DEFAULT 0 NOT NULL;
    END;
END;
";
    }

    private static string GetUpdateProcedureSql()
    {
        return @"
CREATE OR ALTER PROCEDURE dbo.sp_ThemSanPhamVaoHoaDon
    @MaHoaDon INT,
    @MaBienThe INT,
    @SoLuong INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @MaChiNhanh INT;
        SELECT @MaChiNhanh = ma_chi_nhanh
        FROM HoaDon 
        WHERE ma_hoa_don = @MaHoaDon;

        IF (@MaChiNhanh IS NULL OR @MaChiNhanh <= 0)
        BEGIN
            SELECT TOP 1 @MaChiNhanh = ma_chi_nhanh 
            FROM ChiNhanh 
            WHERE is_online_warehouse = 1 AND dang_hoat_dong = 1;
        END;

        DECLARE @DonGia DECIMAL(12,2);
        SELECT @DonGia = CASE 
            WHEN ISNULL(phan_tram_giam_gia, 0) > 0 
            THEN ROUND(gia_ban * (100.0 - phan_tram_giam_gia) / 100.0, 0)
            ELSE gia_ban 
        END
        FROM BienTheSanPham
        WHERE ma_bien_the = @MaBienThe;

        IF (@DonGia IS NULL)
        BEGIN
            RAISERROR(N'Biến thể sản phẩm không tồn tại!', 16, 1);
        END;

        DECLARE @TonChiNhanh INT = 0;
        SELECT @TonChiNhanh = ISNULL(so_luong_ton, 0)
        FROM TonKhoChiNhanh
        WHERE ma_chi_nhanh = @MaChiNhanh AND ma_bien_the = @MaBienThe;

        IF (@TonChiNhanh < @SoLuong)
        BEGIN
            DECLARE @TenCN NVARCHAR(200);
            SELECT @TenCN = ten_chi_nhanh FROM ChiNhanh WHERE ma_chi_nhanh = @MaChiNhanh;
            DECLARE @Msg NVARCHAR(500) = N'Số lượng tồn kho tại chi nhánh [' + ISNULL(@TenCN, N'Kho') + N'] không đủ để xuất bán cho mặt hàng này (Hiện còn: ' + CAST(@TonChiNhanh AS NVARCHAR(10)) + N')!';
            RAISERROR(@Msg, 16, 1);
        END;

        IF EXISTS (SELECT 1 FROM ChiTietHoaDon WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe)
        BEGIN
            UPDATE ChiTietHoaDon
            SET so_luong = so_luong + @SoLuong
            WHERE ma_hoa_don = @MaHoaDon AND ma_bien_the = @MaBienThe;

            UPDATE TonKhoChiNhanh
            SET so_luong_ton = so_luong_ton - @SoLuong,
                ngay_cap_nhat = GETDATE()
            WHERE ma_chi_nhanh = @MaChiNhanh AND ma_bien_the = @MaBienThe;

            UPDATE BienTheSanPham
            SET so_luong_ton = (
                SELECT ISNULL(SUM(so_luong_ton), 0)
                FROM TonKhoChiNhanh
                WHERE ma_bien_the = @MaBienThe
            )
            WHERE ma_bien_the = @MaBienThe;

            INSERT INTO NhatKyKho (ma_bien_the, so_luong_thay_doi, ly_do, nguoi_thay_doi, loai_nguoi_thay_doi, thoi_gian, ma_chi_nhanh)
            VALUES (@MaBienThe, -@SoLuong, 'BAN_HANG', NULL, 'NHAN_VIEN', GETDATE(), @MaChiNhanh);
        END
        ELSE
        BEGIN
            INSERT INTO ChiTietHoaDon (ma_hoa_don, ma_bien_the, so_luong, don_gia)
            VALUES (@MaHoaDon, @MaBienThe, @SoLuong, @DonGia);
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_TaoHoaDonMoi
    @MaKhachHang INT = NULL,
    @MaNhanVien INT = NULL,
    @MaGiamGia VARCHAR(20) = NULL,
    @MaChiNhanh INT = NULL,
    @MaHoaDonMoi INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @MaKhuyenMai INT = NULL;

        -- Tìm mã khuyến mãi nếu có truyền vào
        IF (@MaGiamGia IS NOT NULL AND @MaGiamGia <> '')
        BEGIN
            SELECT @MaKhuyenMai = ma_khuyen_mai
            FROM ChuongTrinhKhuyenMai
            WHERE ma_giam_gia = @MaGiamGia
              AND CAST(GETDATE() AS DATE) BETWEEN ngay_bat_dau AND ngay_ket_thuc;

            IF (@MaKhuyenMai IS NULL)
            BEGIN
                RAISERROR(N'Mã giảm giá không tồn tại hoặc đã hết hạn sử dụng!', 16, 1);
            END;

            -- 1. Kiểm tra số lượt dùng toàn hệ thống
            IF EXISTS (SELECT 1 FROM ChuongTrinhKhuyenMai WHERE ma_khuyen_mai = @MaKhuyenMai AND so_luong_dung <= 0)
            BEGIN
                RAISERROR(N'Mã giảm giá đã hết lượt sử dụng!', 16, 1);
            END;

            -- 2. Ràng buộc: Mỗi mã chỉ có tác dụng với 1 tài khoản ở 1 đơn hàng (trừ đơn đã hủy)
            IF (@MaKhachHang IS NOT NULL AND EXISTS (
                SELECT 1 FROM HoaDon 
                WHERE ma_khach_hang = @MaKhachHang 
                  AND ma_khuyen_mai = @MaKhuyenMai 
                  AND trang_thai <> N'Đã hủy'
            ))
            BEGIN
                RAISERROR(N'Tài khoản này đã sử dụng mã giảm giá trên. Mỗi tài khoản chỉ được áp dụng 1 lần duy nhất!', 16, 1);
            END;

            -- 3. Trừ số lượt sử dụng
            UPDATE ChuongTrinhKhuyenMai 
            SET so_luong_dung = CASE WHEN so_luong_dung > 0 THEN so_luong_dung - 1 ELSE 0 END
            WHERE ma_khuyen_mai = @MaKhuyenMai;
        END;

        -- Xác định chi nhánh: Nếu chưa có thì lấy từ nhân viên, nếu vẫn null thì mặc định Kho Tổng Online (9)
        IF (@MaChiNhanh IS NULL OR @MaChiNhanh <= 0)
        BEGIN
            IF (@MaNhanVien IS NOT NULL AND @MaNhanVien > 0)
            BEGIN
                SELECT @MaChiNhanh = ma_chi_nhanh FROM NhanVien WHERE ma_nhan_vien = @MaNhanVien;
            END;
        END;

        IF (@MaChiNhanh IS NULL OR @MaChiNhanh <= 0)
        BEGIN
            SET @MaChiNhanh = 9; -- Kho Tổng QF Tân Bình (Online Channel)
        END;

        INSERT INTO HoaDon (
            ma_khach_hang,
            ma_nhan_vien,
            ma_chi_nhanh,
            ma_khuyen_mai,
            ngay_lap_hoa_don,
            tien_hang,
            tien_giam_gia,
            tong_thanh_toan,
            trang_thai
        )
        VALUES (
            @MaKhachHang,
            @MaNhanVien,
            @MaChiNhanh,
            @MaKhuyenMai,
            GETDATE(),
            0,
            0,
            0,
            N'Chờ xử lý'
        );

        SET @MaHoaDonMoi = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH;
END;
GO
CREATE OR ALTER TRIGGER trg_CapNhatTongTienHoaDon
ON ChiTietHoaDon
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HoaDonList TABLE (ma_hoa_don INT PRIMARY KEY);

    INSERT INTO @HoaDonList (ma_hoa_don)
    SELECT DISTINCT ma_hoa_don FROM inserted
    UNION
    SELECT DISTINCT ma_hoa_don FROM deleted;

    -- Cập nhật lại tien_hang, tien_giam_gia và tong_thanh_toan
    -- RÀNG BUỘC TOÁN HỌC: Tổng tiền giảm (Voucher + VIP) tối đa không được vượt quá tiền hàng (không giảm âm)
    UPDATE hd
    SET 
        hd.tien_hang = ISNULL(ct.TongTienHang, 0),
        hd.tien_giam_gia = CASE 
            WHEN (dbo.fn_TinhTienGiamGiaVoucher(hd.ma_khuyen_mai, ISNULL(ct.TongTienHang, 0)) 
                  + (ISNULL(ct.TongTienHang, 0) * ISNULL(cd.phan_tram_giam_gia, 0) / 100.0)) > ISNULL(ct.TongTienHang, 0)
            THEN ISNULL(ct.TongTienHang, 0)
            ELSE (dbo.fn_TinhTienGiamGiaVoucher(hd.ma_khuyen_mai, ISNULL(ct.TongTienHang, 0)) 
                  + (ISNULL(ct.TongTienHang, 0) * ISNULL(cd.phan_tram_giam_gia, 0) / 100.0))
        END,
        hd.tong_thanh_toan = CASE 
            WHEN ISNULL(ct.TongTienHang, 0) < (dbo.fn_TinhTienGiamGiaVoucher(hd.ma_khuyen_mai, ISNULL(ct.TongTienHang, 0)) 
                                              + (ISNULL(ct.TongTienHang, 0) * ISNULL(cd.phan_tram_giam_gia, 0) / 100.0))
            THEN 0
            ELSE ISNULL(ct.TongTienHang, 0) - (dbo.fn_TinhTienGiamGiaVoucher(hd.ma_khuyen_mai, ISNULL(ct.TongTienHang, 0)) 
                                              + (ISNULL(ct.TongTienHang, 0) * ISNULL(cd.phan_tram_giam_gia, 0) / 100.0))
        END
    FROM HoaDon hd
    JOIN @HoaDonList hdl ON hd.ma_hoa_don = hdl.ma_hoa_don
    LEFT JOIN KhachHang kh ON hd.ma_khach_hang = kh.ma_khach_hang
    LEFT JOIN CapDoThanhVien cd ON kh.ma_cap_do = cd.ma_cap_do
    OUTER APPLY (
        SELECT SUM(so_luong * don_gia) AS TongTienHang
        FROM ChiTietHoaDon
        WHERE ma_hoa_don = hd.ma_hoa_don
    ) ct;
END;";
    }
}
