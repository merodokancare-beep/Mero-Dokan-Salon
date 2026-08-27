$conn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MeroDokanSaloonDB;Integrated Security=True;TrustServerCertificate=True;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT sd.Id, sd.ItemType, sd.ProductId, sd.ServiceId, sd.StaffId, sd.Quantity, sd.UnitPrice, sd.Total,
       ISNULL(sd.HSNSAC, '') AS HSNSAC, ISNULL(sd.GSTRate, 18.00) AS GSTRate,
       ISNULL(sd.PurchaseCostAtSale, 0) AS PurchaseCostAtSale,
       p.Name AS ProductName, p.Code AS ProductCode, p.Category AS ProductCategory, ISNULL(p.PurchasePrice, 0) AS ProdCost,
       srv.Name AS ServiceName, srv.Code AS ServiceCode, srv.Category AS ServiceCategory,
       st.Name AS StaffName, st.Role AS StaffRole
FROM SaleDetails sd
LEFT JOIN Products p ON sd.ProductId = p.Id
LEFT JOIN Services srv ON sd.ServiceId = srv.Id
LEFT JOIN Staff st ON sd.StaffId = st.Id
WHERE sd.SaleId IN (SELECT TOP 1 Id FROM Sales ORDER BY Id DESC)
ORDER BY sd.Id ASC
"@
$r = $cmd.ExecuteReader()
Write-Host "--- Query result ---"
while($r.Read()) {
    Write-Host "Item: $($r['ItemType']) | Srv: $($r['ServiceName']) | Prod: $($r['ProductName']) | Staff: $($r['StaffName']) | Price: $($r['UnitPrice'])"
}
$r.Close()
$conn.Close()
