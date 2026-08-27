$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT Id, ServiceId, ServiceIds, ServiceNames, ServiceStaffIds FROM Appointments WHERE Id = 11;
SELECT Id, Code, Name, Price FROM Services;
"@
$r = $cmd.ExecuteReader()
Write-Host "--- Appointments #11 ---"
while($r.Read()) {
    Write-Host "Id: $($r['Id']) | SrvId: $($r['ServiceId']) | SrvIds: $($r['ServiceIds']) | Names: $($r['ServiceNames']) | StaffIds: $($r['ServiceStaffIds'])"
}
$r.NextResult()
Write-Host "--- Services in DB ---"
while($r.Read()) {
    Write-Host "Service Id: $($r['Id']) | Code: $($r['Code']) | Name: $($r['Name']) | Price: $($r['Price'])"
}
$r.Close()
$conn.Close()
