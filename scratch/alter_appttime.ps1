$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "ALTER TABLE Appointments ALTER COLUMN AppointmentTime NVARCHAR(100) NOT NULL;"
$cmd.ExecuteNonQuery()
Write-Host "AppointmentTime expanded to NVARCHAR(100) successfully!"
$conn.Close()
