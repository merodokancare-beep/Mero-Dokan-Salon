$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

# 1. Run schema migrations
$cmd1 = $conn.CreateCommand()
$cmd1.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'SaleId')
    ALTER TABLE Appointments ADD SaleId INT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'AppointmentId')
    ALTER TABLE Sales ADD AppointmentId INT NULL;
"@
$cmd1.ExecuteNonQuery()

# 2. Run data backfill updates
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
-- Auto-link legacy Billed appointments to Sales by CustomerId and Date if not already linked
UPDATE a
SET a.SaleId = s.Id
FROM Appointments a
CROSS APPLY (
    SELECT TOP 1 Id FROM Sales 
    WHERE CustomerId = a.CustomerId 
      AND CAST(SaleDate AS DATE) = a.AppointmentDate 
    ORDER BY Id DESC
) s
WHERE a.Status = 'Billed' AND a.SaleId IS NULL;

UPDATE s
SET s.AppointmentId = a.Id
FROM Sales s
INNER JOIN Appointments a ON a.SaleId = s.Id
WHERE s.AppointmentId IS NULL;
"@
$cmd2.ExecuteNonQuery()

# 3. Check Billed appointments and linked sales
$cmd3 = $conn.CreateCommand()
$cmd3.CommandText = @"
SELECT TOP 5 
    a.Id AS ApptId, 
    a.AppointmentNumber, 
    a.Status AS ApptStatus, 
    a.SaleId, 
    s.InvoiceNumber, 
    s.GrandTotal,
    s.PaymentMethod
FROM Appointments a
LEFT JOIN Sales s ON a.SaleId = s.Id
WHERE a.Status = 'Billed'
ORDER BY a.Id DESC
"@
$r = $cmd3.ExecuteReader()
Write-Host "=== Billed Appointments and Linked Invoices ==="
while($r.Read()) {
    Write-Host "Appt: #$($r['ApptId']) ($($r['AppointmentNumber'])) | SaleId: $($r['SaleId']) | Inv: $($r['InvoiceNumber']) | Total: $($r['GrandTotal']) | Pay: $($r['PaymentMethod'])"
}
$r.Close()

$conn.Close()
