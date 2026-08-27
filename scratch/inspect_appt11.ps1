$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT 
    a.Id AS ApptId, 
    a.CustomerId, 
    a.StaffId AS ApptStaffId, 
    a.ServiceIds, 
    a.ServiceNames, 
    a.ServiceStaffIds, 
    a.SaleId, 
    s.Id AS SaleId2, 
    s.InvoiceNumber, 
    s.GrandTotal, 
    sd.Id AS DetailId, 
    sd.ItemType, 
    sd.ServiceId, 
    sd.UnitPrice,
    sd.StaffId AS DetailStaffId, 
    st1.Name AS ApptStaffName, 
    st2.Name AS DetailStaffName 
FROM Appointments a 
LEFT JOIN Sales s ON a.SaleId = s.Id 
LEFT JOIN SaleDetails sd ON s.Id = sd.SaleId 
LEFT JOIN Staff st1 ON a.StaffId = st1.Id 
LEFT JOIN Staff st2 ON sd.StaffId = st2.Id 
WHERE a.Id = 11 OR s.InvoiceNumber LIKE '%130147%'
"@
$r = $cmd.ExecuteReader()
Write-Host "--- Database inspection for Appt 11 and Sale ---"
while($r.Read()) {
    Write-Host "Appt: $($r['ApptId']) | ApptStaff: $($r['ApptStaffName']) | SaleId: $($r['SaleId']) | Inv: $($r['InvoiceNumber']) | DetailStaff: $($r['DetailStaffName']) | DetailUnitPrice: $($r['UnitPrice']) | GrandTotal: $($r['GrandTotal'])"
}
$r.Close()
$conn.Close()
