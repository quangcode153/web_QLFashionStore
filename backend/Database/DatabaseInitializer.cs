using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Backend.Database;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString, ILogger logger)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            string targetDatabase = builder.InitialCatalog;
            if (string.IsNullOrEmpty(targetDatabase)) targetDatabase = "FashionStore";

            // 1. Kết nối vào 'master' để kiểm tra và tạo database nếu chưa có
            builder.InitialCatalog = "master";
            using (var masterConn = new SqlConnection(builder.ConnectionString))
            {
                masterConn.Open();
                using var cmdCheckDb = masterConn.CreateCommand();
                cmdCheckDb.CommandText = $@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{targetDatabase}')
                    BEGIN
                        CREATE DATABASE [{targetDatabase}];
                    END;";
                cmdCheckDb.ExecuteNonQuery();
            }

            // 2. Kết nối vào database mục tiêu và kiểm tra các bảng đã tồn tại chưa
            using (var dbConn = new SqlConnection(connectionString))
            {
                dbConn.Open();
                using var cmdCheckTable = dbConn.CreateCommand();
                cmdCheckTable.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE name = 'SanPham'";
                int tableCount = Convert.ToInt32(cmdCheckTable.ExecuteScalar());

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

                            // Bỏ qua lệnh USE master hoặc CREATE DATABASE vì đã tạo ở trên
                            if (trimmedBatch.StartsWith("USE master", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.StartsWith("DROP DATABASE", StringComparison.OrdinalIgnoreCase) ||
                                trimmedBatch.StartsWith("ALTER DATABASE", StringComparison.OrdinalIgnoreCase))
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
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi khởi tạo tự động cơ sở dữ liệu: {Message}", ex.Message);
        }
    }
}
