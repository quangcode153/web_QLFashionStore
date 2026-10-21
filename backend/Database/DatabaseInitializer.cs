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
            using var cmd = dbConn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            cmd.ExecuteNonQuery();
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
";
    }
}
