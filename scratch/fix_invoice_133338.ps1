$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

# 1. Update SaleDetails & Appointments for Sale 12 / Appt 12 to Dorjee Sherpa (Id = 6)
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
UPDATE SaleDetails
SET StaffId = 6
WHERE SaleId = 12;

UPDATE Appointments
SET StaffId = 6,
    ServiceStaffIds = '67:6|10:00 AM – 10:30 AM',
    ServiceNames = '#1 Acrylic refil [10:00 AM • Dorjee Sherpa]'
WHERE Id = 12;
"@
$cmd.ExecuteNonQuery()

# 2. Check Daily Sales / Stylist / Invoice Details query for INV-260825-133338
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
    st.Name AS StylistName,
    st.Role AS StylistRole
FROM Sales s
INNER JOIN SaleDetails sd ON s.Id = sd.SaleId
LEFT JOIN Customers c ON s.CustomerId = c.Id
LEFT JOIN Services srv ON sd.ServiceId = srv.Id
LEFT JOIN Staff st ON sd.StaffId = st.Id
WHERE s.Id = 12
"@
$r = $cmd2.ExecuteReader()
Write-Host "=== Verified Invoice Details for INV-260825-133338 ==="
while($r.Read()) {
    Write-Host "Inv: $($r['InvoiceNumber']) | Cust: $($r['CustomerName']) | Srv: $($r['ServiceName']) | Stylist: $($r['StylistName']) ($($r['StylistRole'])) | Total: $($r['Total'])"
}
$r.Close()

$conn.Close()
