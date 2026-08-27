$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailySettlements') AND name = 'CardSales')
    ALTER TABLE DailySettlements ADD CardSales DECIMAL(18,2) NOT NULL DEFAULT 0.00;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DailySettlements') AND name = 'QRSales')
    ALTER TABLE DailySettlements ADD QRSales DECIMAL(18,2) NOT NULL DEFAULT 0.00;
"@
$cmd.ExecuteNonQuery()
Write-Host "DailySettlements CardSales and QRSales columns added/verified successfully!"
$conn.Close()
