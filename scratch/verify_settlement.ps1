$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanDB;Integrated Security=True;Encrypt=False")
$conn.Open()

$todayStart = "2026-07-06 06:00:00"
$todayEnd = "2026-07-07 06:00:00"

Write-Host "--- DAILY SETTLEMENT METRICS FOR 2026-07-06 ---"

# 1. Cash Sales Today
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT ISNULL(SUM(AmountPaid), 0) FROM Sales WHERE SaleDate >= @start AND SaleDate < @end AND PaymentMethod = 'Cash'"
$cmd.Parameters.AddWithValue("@start", $todayStart) | Out-Null
$cmd.Parameters.AddWithValue("@end", $todayEnd) | Out-Null
$cashSales = [decimal]$cmd.ExecuteScalar()

# 2. Dues Created Today
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "SELECT ISNULL(SUM(DueAmount), 0) FROM Sales WHERE SaleDate >= @start AND SaleDate < @end"
$cmd2.Parameters.AddWithValue("@start", $todayStart) | Out-Null
$cmd2.Parameters.AddWithValue("@end", $todayEnd) | Out-Null
$duesCreated = [decimal]$cmd2.ExecuteScalar()

# 3. Cash Refunds Today
$cmd3 = $conn.CreateCommand()
$cmd3.CommandText = "SELECT ISNULL(SUM(CashRefund), 0) FROM SalesReturns WHERE ReturnDate >= @start AND ReturnDate < @end"
$cmd3.Parameters.AddWithValue("@start", $todayStart) | Out-Null
$cmd3.Parameters.AddWithValue("@end", $todayEnd) | Out-Null
$cashRefunds = [decimal]$cmd3.ExecuteScalar()

# 4. Total Cash Repayments Today
$cmd4 = $conn.CreateCommand()
$cmd4.CommandText = "SELECT ISNULL(SUM(Amount), 0) FROM CustomerPayments WHERE PaymentDate >= @start AND PaymentDate < @end AND PaymentMethod = 'Cash'"
$cmd4.Parameters.AddWithValue("@start", $todayStart) | Out-Null
$cmd4.Parameters.AddWithValue("@end", $todayEnd) | Out-Null
$totalRepayments = [decimal]$cmd4.ExecuteScalar()

# 5. Previous Due Repayments
$cmd5 = $conn.CreateCommand()
$cmd5.CommandText = @"
    SELECT ISNULL(SUM(cp.Amount), 0)
    FROM CustomerPayments cp
    LEFT JOIN Sales s ON cp.SaleId = s.Id
    WHERE cp.PaymentDate >= @start AND cp.PaymentDate < @end 
      AND cp.PaymentMethod = 'Cash'
      AND (s.SaleDate < @start OR cp.SaleId IS NULL)
"@
$cmd5.Parameters.AddWithValue("@start", $todayStart) | Out-Null
$cmd5.Parameters.AddWithValue("@end", $todayEnd) | Out-Null
$prevDueRepayments = [decimal]$cmd5.ExecuteScalar()

$todayDueRepayments = $totalRepayments - $prevDueRepayments
if ($todayDueRepayments -lt 0) { $todayDueRepayments = 0 }

$expectedCash = $cashSales + $totalRepayments - $cashRefunds

Write-Host "Cash Sales (Amount Paid in Cash): Rs. $cashSales"
Write-Host "New Dues Created: Rs. $duesCreated"
Write-Host "Total Customer Repayments (Dues Collected): Rs. $totalRepayments"
Write-Host "Cash Refunds: Rs. $cashRefunds"
Write-Host "Expected Cash in Drawer: Rs. $expectedCash"

$conn.Close()
