$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

# 1. Check Appointment 11 and its ServiceId
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT 
    a.Id, 
    a.CustomerId, 
    a.StaffId, 
    st.Name AS StaffName, 
    a.ServiceId, 
    srv.Name AS ServiceName, 
    a.ServiceIds, 
    a.ServiceStaffIds,
    a.SaleId,
    s.InvoiceNumber,
    s.GrandTotal
FROM Appointments a
LEFT JOIN Staff st ON a.StaffId = st.Id
LEFT JOIN Services srv ON a.ServiceId = srv.Id
LEFT JOIN Sales s ON a.SaleId = s.Id
WHERE a.Id = 11;
"@
$r = $cmd.ExecuteReader()
Write-Host "=== Current Appointment #11 in DB ==="
while($r.Read()) {
    Write-Host "Appt: $($r['Id']) | Staff: $($r['StaffName']) | Srv: $($r['ServiceName']) (Id: $($r['ServiceId'])) | SrvIds: $($r['ServiceIds']) | Inv: $($r['InvoiceNumber']) | Total: $($r['GrandTotal'])"
}
$r.Close()

$conn.Close()
