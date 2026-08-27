$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanDB;Integrated Security=True;Encrypt=False")
$conn.Open()

Write-Host "--- CUSTOMERS ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Phone FROM Customers"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

Write-Host "--- SALES ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, InvoiceNumber, CustomerId, SaleDate, SubTotal, Discount, Tax, GrandTotal, AmountPaid, DueAmount, PaymentMethod FROM Sales"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

Write-Host "--- CUSTOMER PAYMENTS ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, CustomerId, PaymentDate, Amount, PaymentMethod, Remarks, SaleId FROM CustomerPayments"
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize


Write-Host "--- TEST DAILY SALES QUERY ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
    SELECT s.InvoiceNumber as [Invoice No], s.SaleDate as [Sale Date], c.Name as [Customer],
           s.SubTotal as [SubTotal], s.Discount as [Discount], s.Tax as [Tax], 
           s.GrandTotal as [Grand Total], 
           (s.AmountPaid + ISNULL((SELECT SUM(Amount) FROM CustomerPayments WHERE SaleId = s.Id), 0)) as [Amount Paid], 
           (s.DueAmount - ISNULL((SELECT SUM(Amount) FROM CustomerPayments WHERE SaleId = s.Id), 0)) as [Due Amount], 
           s.PaymentMethod as [Pay Mode]
    FROM Sales s
    LEFT JOIN Customers c ON s.CustomerId = c.Id
    ORDER BY s.SaleDate DESC
"@
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

Write-Host "--- TEST DAILY SALES SUMMARY QUERY ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
    SELECT COUNT(*), ISNULL(SUM(s.Discount), 0) as DiscountTotal, ISNULL(SUM(s.Tax), 0) as TaxTotal, ISNULL(SUM(s.GrandTotal), 0) as GrandTotal, 
           ISNULL(SUM(s.AmountPaid + ISNULL(p.TotalPaid, 0)), 0) as AmountPaidTotal,
           ISNULL(SUM(s.DueAmount - ISNULL(p.TotalPaid, 0)), 0) as DueAmountTotal
    FROM Sales s
    LEFT JOIN (
        SELECT SaleId, SUM(Amount) AS TotalPaid
        FROM CustomerPayments
        GROUP BY SaleId
    ) p ON s.Id = p.SaleId
"@
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

Write-Host "--- TEST REPRINT INVOICE QUERY ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
    SELECT s.InvoiceNumber, s.SaleDate, s.SubTotal, s.Discount, s.Tax, s.GrandTotal, s.PaymentMethod,
           c.Name, c.Phone, c.Address, s.AmountPaid, s.DueAmount,
           ISNULL((SELECT (SUM(s2.DueAmount) - ISNULL((SELECT SUM(p.Amount) FROM CustomerPayments p WHERE p.CustomerId = c.Id), 0))
                   FROM Sales s2 WHERE s2.CustomerId = c.Id), 0) AS CurrentCustomerDue,
           (s.DueAmount - ISNULL((SELECT SUM(Amount) FROM CustomerPayments WHERE SaleId = s.Id), 0)) AS InvoiceRemainingDue
    FROM Sales s
    LEFT JOIN Customers c ON s.CustomerId = c.Id
    WHERE s.Id = 2033
"@
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
$dt = New-Object System.Data.DataTable
$adapter.Fill($dt) | Out-Null
$dt | Format-Table -AutoSize

$conn.Close()

