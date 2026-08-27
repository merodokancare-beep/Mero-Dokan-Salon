$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
-- Simulate Appointment update for Appt 11 with Tshering Doma Tamang (Id = 8)
DECLARE @selectedApptId INT = 11;
DECLARE @custId INT = 11;
DECLARE @staffId INT = 8;
DECLARE @primaryServiceId INT = 69;
DECLARE @serviceIdsCsv NVARCHAR(500) = '69';
DECLARE @serviceNamesCsv NVARCHAR(500) = '#1 Acrylic new set [10:00 AM • Tshering Doma Tamang]';
DECLARE @serviceStaffIdsCsv NVARCHAR(500) = '69:8|10:00 AM – 10:30 AM';
DECLARE @apptDate DATE = '2026-08-25';
DECLARE @fullSpanTimeSlot NVARCHAR(100) = '10:00 AM – 10:30 AM';
DECLARE @status NVARCHAR(50) = 'Billed';
DECLARE @notes NVARCHAR(500) = '';

UPDATE Appointments
SET CustomerId = @custId, StaffId = @staffId, ServiceId = @primaryServiceId, ServiceIds = @serviceIdsCsv, ServiceNames = @serviceNamesCsv, ServiceStaffIds = @serviceStaffIdsCsv,
    AppointmentDate = @apptDate, AppointmentTime = @fullSpanTimeSlot, Status = @status, Notes = @notes
WHERE Id = @selectedApptId;

-- Synchronize linked Sales and SaleDetails
DECLARE @linkedSaleId INT = 0;
SELECT @linkedSaleId = ISNULL(SaleId, 0) FROM Appointments WHERE Id = @selectedApptId;
IF @linkedSaleId IS NULL OR @linkedSaleId = 0
    SELECT TOP 1 @linkedSaleId = Id FROM Sales WHERE AppointmentId = @selectedApptId ORDER BY Id DESC;

IF @linkedSaleId > 0
BEGIN
    UPDATE SaleDetails SET StaffId = @staffId, UnitPrice = 3500.00, Total = 3500.00 WHERE SaleId = @linkedSaleId AND ItemType = 'Service';
    UPDATE Sales SET CustomerId = @custId, SaleDate = @apptDate, SubTotal = 3500.00, GrandTotal = 3500.00, AmountPaid = 3500.00 WHERE Id = @linkedSaleId;
END
"@
$cmd.ExecuteNonQuery()

$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = @"
SELECT a.Id, st.Name AS ApptStaff, sd.StaffId AS DetailStaffId, st2.Name AS DetailStaff, s.GrandTotal 
FROM Appointments a
LEFT JOIN Staff st ON a.StaffId = st.Id
LEFT JOIN Sales s ON a.SaleId = s.Id
LEFT JOIN SaleDetails sd ON s.Id = sd.SaleId
LEFT JOIN Staff st2 ON sd.StaffId = st2.Id
WHERE a.Id = 11;
"@
$r = $cmd2.ExecuteReader()
Write-Host "=== Verified Update Execution in DB ==="
while($r.Read()) {
    Write-Host "Appt: $($r['Id']) | ApptStaff: $($r['ApptStaff']) | DetailStaff: $($r['DetailStaff']) | Total: $($r['GrandTotal'])"
}
$r.Close()

$conn.Close()
