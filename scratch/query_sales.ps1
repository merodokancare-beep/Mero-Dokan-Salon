$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'StaffId')
    ALTER TABLE Users ADD StaffId INT NULL FOREIGN KEY REFERENCES Staff(Id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StaffAttendance')
BEGIN
    CREATE TABLE StaffAttendance (
        Id INT PRIMARY KEY IDENTITY(1,1),
        StaffId INT NOT NULL FOREIGN KEY REFERENCES Staff(Id) ON DELETE CASCADE,
        WorkDate DATE NOT NULL,
        CheckInTime DATETIME NULL,
        CheckOutTime DATETIME NULL,
        AvailabilityStatus NVARCHAR(30) NOT NULL DEFAULT 'Available',
        StatusNotes NVARCHAR(200) NULL,
        UpdatedAt DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_Staff_WorkDate UNIQUE (StaffId, WorkDate)
    );
END
"@
$cmd.ExecuteNonQuery()

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('Users', 'Staff', 'StaffAttendance', 'Appointments')"
$r = $cmd2.ExecuteReader()
while($r.Read()) {
    Write-Host "TABLE: $($r['TABLE_NAME'])"
}
$conn.Close()
