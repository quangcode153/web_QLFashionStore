# Script tự động bật TCP/IP và cổng 1433 cho SQL Server Express
# Yêu cầu: Chạy bằng quyền Administrator (Run as Administrator)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " BẬT GIAO THỨC TCP/IP VÀ CỔNG 1433 CHO SQL SERVER EXPRESS " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$regPath = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQLServer\SuperSocketNetLib\Tcp"
$ipAllPath = "$regPath\IPAll"

try {
    # 1. Bật giao thức TCP/IP
    Set-ItemProperty -Path $regPath -Name "Enabled" -Value 1
    Write-Host "[1/3] Đã bật TCP/IP (Enabled = 1)" -ForegroundColor Green

    # 2. Đặt cổng cố định 1433 cho IPAll và xóa dynamic port
    Set-ItemProperty -Path $ipAllPath -Name "TcpPort" -Value "1433"
    Set-ItemProperty -Path $ipAllPath -Name "TcpDynamicPorts" -Value ""
    Write-Host "[2/3] Đã cấu hình cổng TCPPort = 1433" -ForegroundColor Green

    # 3. Khởi động lại dịch vụ SQL Server để áp dụng
    Write-Host "[3/3] Đang khởi động lại dịch vụ SQL Server (SQLEXPRESS)..." -ForegroundColor Yellow
    Restart-Service "MSSQL`$SQLEXPRESS" -Force
    Write-Host ">>> THÀNH CÔNG! SQL Server đã sẵn sàng nhận kết nối TCP/IP qua cổng 1433." -ForegroundColor Green
} catch {
    Write-Host "LỖI: Vui lòng click chuột phải vào file này và chọn 'Run with PowerShell' hoặc chạy PowerShell dưới quyền Administrator!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host "`nBấm phím bất kỳ để đóng cửa sổ..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
