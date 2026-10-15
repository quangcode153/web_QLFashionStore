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

                // 2. Kiểm tra bảng 'SanPham' mà không dùng NOLOCK trên metadata
                var swQuery = System.Diagnostics.Stopwatch.StartNew();
                using var cmdCheckTable = dbConn.CreateCommand();
                cmdCheckTable.CommandTimeout = 15;
                cmdCheckTable.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SELECT COUNT(1) FROM sys.tables WHERE name = 'SanPham';";
                int tableCount = Convert.ToInt32(cmdCheckTable.ExecuteScalar());
                swQuery.Stop();
                logger.LogInformation("Kiểm tra bảng metadata hoàn tất trong {QueryMs} ms (Số bảng SanPham: {Count}).", swQuery.ElapsedMilliseconds, tableCount);

                if (tableCount == 0)
                {
                    logger.LogInformation("Cơ sở dữ liệu '{Db}' chưa có bảng, bắt đầu sinh schema từ sqlbtl.sql...", targetDatabase);

                    // Tìm file sqlbtl.sql ở thư mục gốc
                    string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                    string sqlFilePath = Path.Combine(currentDir, "..", "..", "..", "..", "sqlbtl.sql");
                    sqlFilePath = Path.GetFullPath(sqlFilePath);

                    if (!File.Exists(sqlFilePath))
                    {
                        // Thử tìm ở thư mục cha trực tiếp
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

                        foreach (var batch in batches)
                        {
                            string trimmedBatch = batch.Trim();
                            if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

                            // Bỏ qua các lệnh quản lý cấp database (USE, CREATE, DROP, ALTER DATABASE, SINGLE_USER)
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
                                logger.LogWarning("Bỏ qua lỗi batch hoặc đối tượng đã tồn tại: {Msg}", ex.Message);
                            }
                        }

                        // Đảm bảo CSDL luôn ở chế độ MULTI_USER
                        using (var cmdMulti = dbConn.CreateCommand())
                        {
                            cmdMulti.CommandText = $"ALTER DATABASE [{targetDatabase}] SET MULTI_USER;";
                            cmdMulti.ExecuteNonQuery();
                        }

                        logger.LogInformation("Sinh toàn bộ bảng, Views, Functions, Procedures, Triggers từ code thành công!");
                    }
                    else
                    {
                        logger.LogWarning("Không tìm thấy file sqlbtl.sql tại đường dẫn: {Path}", sqlFilePath);
                    }
                }
                else
                {
                    logger.LogInformation("Cơ sở dữ liệu '{Db}' đã có sẵn cấu trúc bảng ({Count} bảng chính).", targetDatabase, tableCount);
                }

                // 3. Đảm bảo bảng ThongBao_NguoiDoc luôn tồn tại để quản lý trạng thái đã đọc theo tài khoản
                try
                {
                    using var cmdEnsure = dbConn.CreateCommand();
                    cmdEnsure.CommandText = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ThongBao_NguoiDoc')
                        BEGIN
                            CREATE TABLE ThongBao_NguoiDoc (
                                ma_thong_bao INT NOT NULL,
                                ma_tai_khoan INT NOT NULL,
                                ngay_doc DATETIME DEFAULT GETDATE(),
                                CONSTRAINT PK_ThongBao_NguoiDoc PRIMARY KEY (ma_thong_bao, ma_tai_khoan),
                                CONSTRAINT FK_ThongBao_NguoiDoc_ThongBao FOREIGN KEY (ma_thong_bao) REFERENCES ThongBaoNoiBo(ma_thong_bao) ON DELETE CASCADE,
                                CONSTRAINT FK_ThongBao_NguoiDoc_TaiKhoan FOREIGN KEY (ma_tai_khoan) REFERENCES TaiKhoan(ma_tai_khoan) ON DELETE CASCADE
                            );
                        END";
                    cmdEnsure.ExecuteNonQuery();
                }
                catch (Exception exTable)
                {
                    logger.LogDebug("Kiểm tra bảng ThongBao_NguoiDoc: {Msg}", exTable.Message);
                }

                // 4. Đảm bảo cấu trúc Đa Chi Nhánh (ChiNhanh & TonKhoChiNhanh) luôn toàn vẹn
                try
                {
                    using var cmdBranch = dbConn.CreateCommand();
                    cmdBranch.CommandTimeout = 30;
                    cmdBranch.CommandText = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChiNhanh')
                        BEGIN
                            CREATE TABLE ChiNhanh (
                                ma_chi_nhanh INT PRIMARY KEY IDENTITY(1,1),
                                ten_chi_nhanh NVARCHAR(150) NOT NULL,
                                dia_chi NVARCHAR(255) NOT NULL,
                                dien_thoai VARCHAR(20) NULL,
                                loai_chi_nhanh NVARCHAR(50) DEFAULT N'Cửa hàng bán lẻ',
                                dang_hoat_dong BIT DEFAULT 1,
                                ngay_tao DATETIME DEFAULT GETDATE()
                            );
                        END;

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TonKhoChiNhanh')
                        BEGIN
                            CREATE TABLE TonKhoChiNhanh (
                                ma_chi_nhanh INT NOT NULL,
                                ma_bien_the INT NOT NULL,
                                so_luong_ton INT NOT NULL DEFAULT 0,
                                muc_canh_bao INT NOT NULL DEFAULT 10,
                                ngay_cap_nhat DATETIME DEFAULT GETDATE(),
                                CONSTRAINT PK_TonKhoChiNhanh PRIMARY KEY (ma_chi_nhanh, ma_bien_the),
                                CONSTRAINT FK_TonKhoChiNhanh_ChiNhanh FOREIGN KEY (ma_chi_nhanh) REFERENCES ChiNhanh(ma_chi_nhanh),
                                CONSTRAINT FK_TonKhoChiNhanh_BienThe FOREIGN KEY (ma_bien_the) REFERENCES BienTheSanPham(ma_bien_the)
                            );
                        END;
                    ";
                    cmdBranch.ExecuteNonQuery();
                }
                catch (Exception exBranch)
                {
                    logger.LogDebug("Kiểm tra cấu trúc Đa Chi Nhánh: {Msg}", exBranch.Message);
                }

                // 5. Đảm bảo cấu trúc Nâng cấp: Trạng thái Đang giao hàng, Kho hàng lỗi, Đổi trả & Vai trò Order Coordinator
                try
                {
                    using var cmdUpgrade = dbConn.CreateCommand();
                    cmdUpgrade.CommandTimeout = 30;
                    cmdUpgrade.CommandText = @"
                        -- 5.1 Cập nhật ràng buộc trạng thái đơn hàng (HoaDon)
                        IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TrangThaiHoaDon')
                        BEGIN
                            ALTER TABLE HoaDon DROP CONSTRAINT CK_TrangThaiHoaDon;
                        END;
                        ALTER TABLE HoaDon ADD CONSTRAINT CK_TrangThaiHoaDon 
                            CHECK (trang_thai IN (N'Chờ xử lý', N'Đang giao hàng', N'Đã hoàn thành', N'Đã hủy'));

                        -- 5.2 Bổ sung cột loai_don_hang và dia_chi_giao_hang cho HoaDon
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('HoaDon') AND name = 'loai_don_hang')
                        BEGIN
                            ALTER TABLE HoaDon ADD loai_don_hang NVARCHAR(20) DEFAULT 'ONLINE';
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('HoaDon') AND name = 'dia_chi_giao_hang')
                        BEGIN
                            ALTER TABLE HoaDon ADD dia_chi_giao_hang NVARCHAR(255) NULL;
                        END;

                        -- 5.3 Bổ sung cột so_luong_loi cho BienTheSanPham
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BienTheSanPham') AND name = 'so_luong_loi')
                        BEGIN
                            ALTER TABLE BienTheSanPham ADD so_luong_loi INT DEFAULT 0;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SoLuongLoi')
                        BEGIN
                            ALTER TABLE BienTheSanPham ADD CONSTRAINT CK_SoLuongLoi CHECK (so_luong_loi >= 0);
                        END;

                        -- 5.4 Cập nhật ràng buộc lý do nhật ký kho (NhatKyKho)
                        IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LyDoNhatKyKho')
                        BEGIN
                            ALTER TABLE NhatKyKho DROP CONSTRAINT CK_LyDoNhatKyKho;
                        END;
                        ALTER TABLE NhatKyKho ADD CONSTRAINT CK_LyDoNhatKyKho 
                            CHECK (ly_do IN ('BAN_HANG', 'NHAP_HANG', 'TRA_HANG', 'TRA_HANG_LOI', 'DIEU_CHINH'));

                        -- 5.5 Bổ sung thông tin đổi trả cho PhanHoiKhachHang
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PhanHoiKhachHang') AND name = 'hinh_thuc_hoan')
                        BEGIN
                            ALTER TABLE PhanHoiKhachHang ADD hinh_thuc_hoan NVARCHAR(30) DEFAULT N'Thu hồi tại nhà';
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PhanHoiKhachHang') AND name = 'dia_chi_thu_hoi')
                        BEGIN
                            ALTER TABLE PhanHoiKhachHang ADD dia_chi_thu_hoi NVARCHAR(255) NULL;
                        END;

                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PhanHoiKhachHang') AND name = 'ma_chi_nhanh_hen')
                        BEGIN
                            ALTER TABLE PhanHoiKhachHang ADD ma_chi_nhanh_hen INT NULL;
                        END;

                        -- 5.6 Đảm bảo nhân viên và tài khoản mẫu cho Order Coordinator
                        IF NOT EXISTS (SELECT 1 FROM NhanVien WHERE vai_tro = N'Order Coordinator')
                        BEGIN
                            INSERT INTO NhanVien (ten_nhan_vien, dien_thoai, vai_tro, da_xoa)
                            VALUES (N'Nguyễn Điều Phối', '0908889999', N'Order Coordinator', 0);
                            
                            DECLARE @NewStaffId INT = SCOPE_IDENTITY();
                            IF NOT EXISTS (SELECT 1 FROM TaiKhoan WHERE ten_dang_nhap = 'coordinator')
                            BEGIN
                                INSERT INTO TaiKhoan (ten_dang_nhap, mat_khau_hash, ma_nhan_vien, dang_hoat_dong)
                                VALUES ('coordinator', 'Password123!', @NewStaffId, 1);
                            END;
                        END;

                        -- 5.7 Cập nhật ràng buộc trạng thái cho PhanHoiKhachHang & DonTraHang
                        IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TrangThaiPhanHoi')
                        BEGIN
                            ALTER TABLE PhanHoiKhachHang DROP CONSTRAINT CK_TrangThaiPhanHoi;
                        END;
                        ALTER TABLE PhanHoiKhachHang WITH CHECK ADD CONSTRAINT CK_TrangThaiPhanHoi 
                            CHECK (trang_thai IN (N'Chưa xử lý', N'Cần duyệt', N'Đang xử lý', N'Đã duyệt', N'Đã nhận hàng hoàn', N'Đã giải quyết', N'Đã đóng', N'Từ chối'));
                        
                        ALTER TABLE DonTraHang ALTER COLUMN trang_thai NVARCHAR(50);
                    ";
                    cmdUpgrade.ExecuteNonQuery();

                    logger.LogInformation("Nâng cấp Schema (Đang giao hàng, Kho hàng lỗi, Đổi trả, Order Coordinator) thành công!");
                }
                catch (Exception exUpgrade)
                {
                    logger.LogWarning("Kiểm tra/Nâng cấp Schema Order Coordinator: {Msg}", exUpgrade.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi khởi tạo tự động cơ sở dữ liệu: {Message}", ex.Message);
        }
    }
}
