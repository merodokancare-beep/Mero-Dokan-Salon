Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MeroDokanSaloonDB;Integrated Security=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "UPDATE SaleDetails SET StaffId = 5 WHERE SaleId = 14 AND ServiceId = 15"
$rows = $cmd.ExecuteNonQuery()
Write-Output ("Updated rows in SaleDetails: " + $rows)

$cmd.CommandText = "SELECT sd.Id, sd.ServiceId, sd.StaffId, st.Name as StaffName, s.Name as ServiceName FROM SaleDetails sd LEFT JOIN Staff st ON sd.StaffId = st.Id LEFT JOIN Services s ON sd.ServiceId = s.Id WHERE sd.SaleId = 14"
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    Write-Output ("Detail: SrvId " + $rdr["ServiceId"] + " (" + $rdr["ServiceName"] + ") -> StaffId " + $rdr["StaffId"] + " (" + $rdr["StaffName"] + ")")
}
$rdr.Close()
$conn.Close()
