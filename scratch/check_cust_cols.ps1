Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MeroDokanSaloonDB;Integrated Security=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Customers'"
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    Write-Output ($rdr["COLUMN_NAME"])
}
$rdr.Close()
$conn.Close()
