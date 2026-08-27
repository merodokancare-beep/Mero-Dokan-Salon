$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments'"
$r = $cmd.ExecuteReader()
Write-Host "--- Appointments Columns ---"
while($r.Read()) {
    Write-Host "$($r['COLUMN_NAME']) : $($r['DATA_TYPE'])"
}
$r.Close()

$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Sales'"
$r = $cmd.ExecuteReader()
Write-Host "--- Sales Columns ---"
while($r.Read()) {
    Write-Host "$($r['COLUMN_NAME']) : $($r['DATA_TYPE'])"
}
$r.Close()

$cmd.CommandText = "SELECT TOP 5 a.Id, a.AppointmentNumber, a.Status, a.CustomerId, a.AppointmentDate, a.ServiceNames FROM Appointments a WHERE a.Status = 'Billed' ORDER BY a.Id DESC"
$r = $cmd.ExecuteReader()
Write-Host "--- Recent Billed Appointments ---"
while($r.Read()) {
    Write-Host "Appt: $($r['Id']) | $($r['AppointmentNumber']) | $($r['Status']) | Cust: $($r['CustomerId']) | $($r['AppointmentDate']) | $($r['ServiceNames'])"
}
$r.Close()

$conn.Close()
