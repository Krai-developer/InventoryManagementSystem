# Inventory and Stock Management System

Desktop inventory application built with C# Windows Forms (`.NET 9.0-windows`), ADO.NET (`Microsoft.Data.SqlClient`), and Microsoft SQL Server.

## Features

- **Dashboard**: Summary counts, total stock value, stock status breakdown, low-stock list, and recent transactions.
- **Products**: Add, view, search (by ID, SKU, name, category, or status), update, and delete products, with optional product image preview.
- **Categories**: Create, edit, and delete product categories (blocks deleting categories that still contain products).
- **Stock In & Stock Out**: Record incoming deliveries and outgoing dispatches using SQL transactions (`SqlTransaction`) with stock validation.
- **Low Stock Alerts**: Filter items at or below reorder level and restock directly.
- **Reports**: View current stock valuation or date-filtered transaction history, and export to CSV.

## Running the Project

1. Open `InventoryManagementSystem.sln` in Visual Studio and press **F5**, or run from PowerShell:
   ```powershell
   dotnet run --project InventoryManagementSystem/InventoryManagementSystem.csproj
   ```
2. On first run, `DatabaseInitializer` creates `InventoryManagementDB` and its tables (`Categories`, `Products`, `StockTransactions`) on `(localdb)\MSSQLLocalDB` or `.\SQLEXPRESS`.
3. To set up the database manually in SSMS, run `InventoryManagementSystem/Database/DatabaseSetup.sql`.
