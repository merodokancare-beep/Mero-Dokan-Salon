$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT 
    s.Id AS SaleId,
    s.InvoiceNumber,
    s.SaleDate,
    c.Name AS CustomerName,
    sd.Id AS DetailId,
    sd.ItemType,
    sd.ServiceId,
    srv.Name AS ServiceName,
    sd.StaffId AS DetailStaffId,
    st.Name AS DetailStaffName,
    a.Id AS ApptId,
    a.StaffId AS ApptStaffId,
    st2.Name AS ApptStaffName,
    a.ServiceNames,
    a.ServiceStaffIds
FROM Sales s
LEFT JOIN SaleDetails sd ON s.Id = sd.SaleId
LEFT JOIN Customers c ON s.CustomerId = c.Id
LEFT JOIN Services srv ON sd.ServiceId = srv.Id
LEFT JOIN Staff st ON sd.StaffId = st.Id
LEFT JOIN Appointments a ON s.AppointmentId = a.Id OR a.SaleId = s.Id
LEFT JOIN Staff st2 ON a.StaffId = st2.Id
WHERE s.InvoiceNumber LIKE '%133338%';
"@
$r = $cmd.ExecuteReader()
Write-Host "=== Inspection for Invoice INV-260825-133338 ==="
while($r.Read()) {
    Write-Host "SaleId: $($r['SaleId']) | Inv: $($r['InvoiceNumber']) | Cust: $($r['CustomerName'])"
    Write-Host "  Detail: Item=$($r['ServiceName']), DetailStaffId=$($r['DetailStaffId']), DetailStaffName=$($r['DetailStaffName'])"
    Write-Host "  Appt: ApptId=$($r['ApptId']), ApptStaffId=$($r['ApptStaffId']), ApptStaffName=$($r['ApptStaffName'])"
    Write-Host "  Appt ServiceNames=$($r['ServiceNames'])"
    Write-Host "  Appt ServiceStaffIds=$($r['ServiceStaffIds'])"
}
$r.Close()
$conn.Close()
