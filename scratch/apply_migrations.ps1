$connectionStrings = @(
    "Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;",
    "Server=localhost;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;",
    "Server=.\SQLEXPRESS;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;"
)

foreach ($cs in $connectionStrings) {
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
        $conn.Open()
        Write-Host "Connected successfully using: $cs"
        
        # 1. Add Type column
        $cmd1 = $conn.CreateCommand()
        $cmd1.CommandText = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Type') ALTER TABLE Categories ADD Type NVARCHAR(20) NOT NULL DEFAULT 'Service';"
        $cmd1.ExecuteNonQuery()
        Write-Host "Added Type column."

        # 2. Add HsnSacCode column
        $cmd2 = $conn.CreateCommand()
        $cmd2.CommandText = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'HsnSacCode') ALTER TABLE Categories ADD HsnSacCode NVARCHAR(50) NULL DEFAULT '999721';"
        $cmd2.ExecuteNonQuery()
        Write-Host "Added HsnSacCode column."

        # 3. Add GSTRate column
        $cmd3 = $conn.CreateCommand()
        $cmd3.CommandText = "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'GSTRate') ALTER TABLE Categories ADD GSTRate DECIMAL(5,2) NOT NULL DEFAULT 18.00;"
        $cmd3.ExecuteNonQuery()
        Write-Host "Added GSTRate column."

        # 4. Now execute the update in a separate command
        $cmd4 = $conn.CreateCommand()
        $cmd4.CommandText = @"
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Hair Services';
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Beard & Grooming';
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999722', GSTRate = 18.00 WHERE Name = 'Facial & Skin Care';
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999721', GSTRate = 18.00 WHERE Name = 'Hair Spa & Treatments';
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999729', GSTRate = 18.00 WHERE Name = 'Body Massage & Spa';
            UPDATE Categories SET Type = 'Service', HsnSacCode = '999722', GSTRate = 18.00 WHERE Name = 'Manicure & Pedicure';
            UPDATE Categories SET Type = 'Product', HsnSacCode = '3305', GSTRate = 18.00 WHERE Name = 'Hair Care Products';
            UPDATE Categories SET Type = 'Product', HsnSacCode = '3304', GSTRate = 18.00 WHERE Name = 'Skin Care Products';
            UPDATE Categories SET Type = 'Product', HsnSacCode = '8214', GSTRate = 18.00 WHERE Name = 'Grooming Accessories';
"@
        $cmd4.ExecuteNonQuery()
        Write-Host "Categories data updated successfully!"
        
        $conn.Close()
        Write-Host "MIGRATION COMPLETE."
        break
    } catch {
        Write-Host "Failed with $cs : $($_.Exception.Message)"
    }
}
