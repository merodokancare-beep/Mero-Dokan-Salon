$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Appointments'
"@
$r = $cmd.ExecuteReader()
while($r.Read()) {
    Write-Host "$($r['COLUMN_NAME']) : $($r['DATA_TYPE']) ($($r['CHARACTER_MAXIMUM_LENGTH']))"
}
$r.Close()
$conn.Close()
