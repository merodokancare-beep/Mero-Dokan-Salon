Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MeroDokanSaloonDB;Integrated Security=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT s.Id, s.InvoiceNumber, s.AppointmentId, a.StaffId as ApptStaffId, a.ServiceStaffIds, a.ServiceNames FROM Sales s LEFT JOIN Appointments a ON s.AppointmentId = a.Id WHERE s.InvoiceNumber = 'INV-260825-130129'"
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    Write-Output ("SaleId: " + $rdr["Id"] + " | ApptId: " + $rdr["AppointmentId"] + " | ApptStaffId: " + $rdr["ApptStaffId"])
    Write-Output ("ServiceStaffIds: " + $rdr["ServiceStaffIds"])
    Write-Output ("ServiceNames: " + $rdr["ServiceNames"])
}
$rdr.Close()

$cmd.CommandText = "SELECT sd.Id, sd.ServiceId, sd.StaffId, st.Name as StaffName, s.Name as ServiceName FROM SaleDetails sd LEFT JOIN Staff st ON sd.StaffId = st.Id LEFT JOIN Services s ON sd.ServiceId = s.Id WHERE sd.SaleId = (SELECT Id FROM Sales WHERE InvoiceNumber = 'INV-260825-130129')"
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    Write-Output ("Detail: SrvId " + $rdr["ServiceId"] + " (" + $rdr["ServiceName"] + ") -> StaffId " + $rdr["StaffId"] + " (" + $rdr["StaffName"] + ")")
}
$rdr.Close()
$conn.Close()
