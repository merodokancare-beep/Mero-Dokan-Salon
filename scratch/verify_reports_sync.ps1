$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
-- Check Daily Sales details query for Invoice INV-260825-130147
SELECT 
    s.InvoiceNumber,
    s.GrandTotal,
    s.PaymentMethod,
    c.Name AS CustomerName,
    sd.ItemType,
    sd.UnitPrice,
    sd.Total,
    ISNULL(st.Name, 'None') AS StylistName,
    ISNULL(st.Role, '') AS StylistRole,
    ISNULL(srv.Name, p.Name) AS ItemName
FROM Sales s
LEFT JOIN SaleDetails sd ON s.Id = sd.SaleId
LEFT JOIN Customers c ON s.CustomerId = c.Id
LEFT JOIN Services srv ON sd.ServiceId = srv.Id
LEFT JOIN Products p ON sd.ProductId = p.Id
LEFT JOIN Staff st ON sd.StaffId = st.Id
WHERE s.InvoiceNumber = 'INV-260825-130147';
"@
$r = $cmd.ExecuteReader()
Write-Host "=== Daily Sales & Stylist Report verification for INV-260825-130147 ==="
while($r.Read()) {
    Write-Host "Invoice: $($r['InvoiceNumber']) | Item: $($r['ItemName']) | Rate: $($r['UnitPrice']) | Total: $($r['Total']) | Stylist: $($r['StylistName']) ($($r['StylistRole']))"
}
$r.Close()
$conn.Close()
