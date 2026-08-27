$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Role FROM Staff"
$r = $cmd.ExecuteReader()
while($r.Read()) {
    Write-Host "$($r['Id']) - $($r['Name']) ($($r['Role']))"
}
$r.Close()
$conn.Close()
