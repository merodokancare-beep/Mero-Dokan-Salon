Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MeroDokanSaloonDB;Integrated Security=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Phone FROM Customers"
$rdr = $cmd.ExecuteReader()
while ($rdr.Read()) {
    Write-Output ("Cust Id: " + $rdr["Id"] + " | Name: " + $rdr["Name"] + " | Phone: " + $rdr["Phone"])
}
$rdr.Close()
$conn.Close()
