$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

# 1. Update SaleDetails for Sale 11 to Sujata Singh Mizar (Id = 7) and 3500.00
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
-- Update SaleDetails
UPDATE SaleDetails
SET StaffId = 7,
    UnitPrice = 3500.00,
    Total = 3500.00,
    TaxableAmount = 3333.33,
    CGSTAmount = 83.33,
    SGSTAmount = 83.34
WHERE SaleId = 11;

-- Update Sales Header
UPDATE Sales
SET SubTotal = 3500.00,
    TaxableAmount = 3333.33,
    CGSTAmount = 83.33,
    SGSTAmount = 83.34,
    Tax = 166.67,
    GrandTotal = 3500.00,
    AmountPaid = 3500.00,
    CashAmount = 3500.00
WHERE Id = 11;

-- Ensure Appointments table is aligned
UPDATE Appointments
SET StaffId = 7,
    SaleId = 11,
    ServiceStaffIds = '160:7|10:00 AM – 10:30 AM'
WHERE Id = 11;
"@
$cmd.ExecuteNonQuery()

# 2. Check Daily Sales / Stylist reports query
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
SELECT 
    s.InvoiceNumber,
    s.SaleDate,
    c.Name AS CustomerName,
    sd.ItemType,
    srv.Name AS ServiceName,
    sd.UnitPrice,
    sd.Total,
    st.Name AS StylistName
FROM Sales s
INNER JOIN SaleDetails sd ON s.Id = sd.SaleId
LEFT JOIN Customers c ON s.CustomerId = c.Id
LEFT JOIN Services srv ON sd.ServiceId = srv.Id
LEFT JOIN Staff st ON sd.StaffId = st.Id
WHERE s.Id = 11
"@
$r = $cmd2.ExecuteReader()
Write-Host "=== Verified Sale & Report Line for Sale 11 ==="
while($r.Read()) {
    Write-Host "Inv: $($r['InvoiceNumber']) | Cust: $($r['CustomerName']) | Srv: $($r['ServiceName']) | Rate: $($r['UnitPrice']) | Total: $($r['Total']) | Stylist: $($r['StylistName'])"
}
$r.Close()

$conn.Close()
