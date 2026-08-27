$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanDB;Integrated Security=True;Encrypt=False")
$conn.Open()

Write-Host "`n--- LATEST SALES ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 5 Id, InvoiceNumber, SaleDate, SubTotal, Discount, GrandTotal, AmountPaid, DueAmount, PaymentMethod FROM Sales ORDER BY Id DESC"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

Write-Host "--- LATEST SALE DETAILS ---"
$cmd.CommandText = @"
    SELECT TOP 10 sd.SaleId, s.InvoiceNumber, sd.ProductId, p.Code, p.Name, sd.Quantity, sd.UnitPrice, sd.Total 
    FROM SaleDetails sd
    INNER JOIN Sales s ON sd.SaleId = s.Id
    INNER JOIN Products p ON sd.ProductId = p.Id
    ORDER BY sd.Id DESC
"@
$dtDetails = New-Object System.Data.DataTable
$adapter.SelectCommand = $cmd
$adapter.Fill($dtDetails) | Out-Null
$dtDetails | Format-Table -AutoSize

Write-Host "--- LATEST SALES RETURNS ---"
$cmd.CommandText = "SELECT TOP 5 Id, ReturnNumber, SaleId, ReturnDate, TotalRefund, CashRefund FROM SalesReturns ORDER BY Id DESC"
$dtReturns = New-Object System.Data.DataTable
$adapter.Fill($dtReturns) | Out-Null
$dtReturns | Format-Table -AutoSize

Write-Host "--- LATEST SALES RETURN DETAILS ---"
$cmd.CommandText = @"
    SELECT TOP 10 srd.ReturnId, sr.ReturnNumber, srd.ProductId, p.Name, srd.Quantity, srd.RefundPrice, srd.Total, srd.ItemCondition
    FROM SalesReturnDetails srd
    INNER JOIN SalesReturns sr ON srd.ReturnId = sr.Id
    INNER JOIN Products p ON srd.ProductId = p.Id
    ORDER BY srd.Id DESC
"@
$dtReturnDetails = New-Object System.Data.DataTable
$adapter.Fill($dtReturnDetails) | Out-Null
$dtReturnDetails | Format-Table -AutoSize

$conn.Close()
