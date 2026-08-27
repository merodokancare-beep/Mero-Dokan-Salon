Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection("Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MeroDokanSaloonDB;Integrated Security=True")
$conn.Open()

# Check if Walk-in Customer exists
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Phone FROM Customers WHERE Name LIKE '%Walk-in%' OR Phone = '9800000000' OR Phone = '+977-9800000000'"
$rdr = $cmd.ExecuteReader()
$exists = $false
$id = 0
if ($rdr.Read()) {
    $exists = $true
    $id = $rdr["Id"]
    Write-Output ("Walk-in Customer already exists with ID: " + $id)
}
$rdr.Close()

if (-not $exists) {
    $cmdIns = $conn.CreateCommand()
    $cmdIns.CommandText = "INSERT INTO Customers (Name, Phone, Email, Address, GSTIN, StateName, StateCode, CreatedAt) OUTPUT INSERTED.Id VALUES ('Walk-in Customer', '+977-9800000000', '', '', '', 'Delhi', '07', GETDATE())"
    $newId = $cmdIns.ExecuteScalar()
    Write-Output ("Created Walk-in Customer with ID: " + $newId)
}

$conn.Close()
