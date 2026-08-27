$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanDB;Integrated Security=True;Encrypt=False")
$conn.Open()

$fromDate = "2026-05-01"
$toDate = "2026-07-06"

Write-Host "--- REVENUES & REFUNDS FOR PERIOD $fromDate TO $toDate ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE CAST(SaleDate as DATE) BETWEEN @from AND @to"
$cmd.Parameters.AddWithValue("@from", $fromDate) | Out-Null
$cmd.Parameters.AddWithValue("@to", $toDate) | Out-Null
$salesRevenue = [decimal]$cmd.ExecuteScalar()

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "SELECT ISNULL(SUM(TotalRefund), 0) FROM SalesReturns WHERE CAST(ReturnDate as DATE) BETWEEN @from AND @to"
$cmd2.Parameters.AddWithValue("@from", $fromDate) | Out-Null
$cmd2.Parameters.AddWithValue("@to", $toDate) | Out-Null
$returnedRefund = [decimal]$cmd2.ExecuteScalar()

$netRevenue = $salesRevenue - $returnedRefund

Write-Host "Gross Sales Revenue: Rs. $salesRevenue"
Write-Host "Returns Refunds: Rs. $returnedRefund"
Write-Host "Net Revenue: Rs. $netRevenue"

Write-Host "`n--- COGS FOR PERIOD $fromDate TO $toDate ---"
$cmd3 = $conn.CreateCommand()
$cmd3.CommandText = @"
    SELECT ISNULL(SUM(sd.Quantity * sd.PurchaseCostAtSale), 0)
    FROM SaleDetails sd
    INNER JOIN Sales s ON sd.SaleId = s.Id
    WHERE CAST(s.SaleDate as DATE) BETWEEN @from AND @to
"@
$cmd3.Parameters.AddWithValue("@from", $fromDate) | Out-Null
$cmd3.Parameters.AddWithValue("@to", $toDate) | Out-Null
$grossCogs = [decimal]$cmd3.ExecuteScalar()

$cmd4 = $conn.CreateCommand()
$cmd4.CommandText = @"
    SELECT ISNULL(SUM(srd.Quantity * sd.PurchaseCostAtSale), 0)
    FROM SalesReturnDetails srd
    INNER JOIN SalesReturns sr ON srd.ReturnId = sr.Id
    INNER JOIN SaleDetails sd ON sr.SaleId = sd.SaleId AND srd.ProductId = sd.ProductId
    WHERE srd.ItemCondition = 'Resellable' 
      AND CAST(sr.ReturnDate as DATE) BETWEEN @from AND @to
"@
$cmd4.Parameters.AddWithValue("@from", $fromDate) | Out-Null
$cmd4.Parameters.AddWithValue("@to", $toDate) | Out-Null
$resellableReturnCost = [decimal]$cmd4.ExecuteScalar()

$netCogs = $grossCogs - $resellableReturnCost
$netMargin = $netRevenue - $netCogs

Write-Host "Gross COGS: Rs. $grossCogs"
Write-Host "Resellable Return Cost: Rs. $resellableReturnCost"
Write-Host "Net COGS: Rs. $netCogs"
Write-Host "Net Margin: Rs. $netMargin"

$conn.Close()
