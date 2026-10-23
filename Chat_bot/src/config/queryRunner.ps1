param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "FashionStore",
    [string]$User = "sa",
    [string]$Password = "",
    [string]$Query = ""
)

$connString = "Server=$Server;Database=$Database;User Id=$User;Password=$Password;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)

try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SET NOCOUNT ON; SELECT ($Query FOR JSON PATH) AS json_data"
    $reader = $cmd.ExecuteReader()
    $sb = New-Object System.Text.StringBuilder

    while ($reader.Read()) {
        if (-not $reader.IsDBNull(0)) {
            $null = $sb.Append($reader.GetValue(0))
        }
    }
    
    $result = $sb.ToString()
    if ([string]::IsNullOrWhiteSpace($result)) {
        Write-Output "[]"
    } else {
        Write-Output $result
    }
} catch {
    Write-Error $_.Exception.Message
    exit 1
} finally {
    if ($conn.State -eq [System.Data.ConnectionState]::Open) {
        $conn.Close()
    }
}
