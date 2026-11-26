using System;
using Microsoft.Data.SqlClient;

namespace InventoryManagementSystem.Database
{
    /// <summary>
    /// Automatically creates InventoryManagementDB and applies non-destructive schema upgrades
    /// (Suppliers, ActivityLogs, and extended Product/StockTransaction columns) while preserving all existing records.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static bool InitializeDatabase(out string statusMessage)
        {
            try
            {
                using (SqlConnection masterConn = new SqlConnection(DbConnection.GetMasterConnectionString()))
                {
                    masterConn.Open();
                    string checkDbSql = $@"
                        IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{DbConnection.DatabaseName}')
                        BEGIN
                            CREATE DATABASE [{DbConnection.DatabaseName}];
                        END";

                    using (SqlCommand cmd = new SqlCommand(checkDbSql, masterConn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                using (SqlConnection conn = DbConnection.GetConnection())
                {
                    conn.Open();

                    string schemaSql = @"
                        -- 1. Categories Table
                        IF OBJECT_ID('dbo.Categories', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.Categories (
                                CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
                                CategoryName NVARCHAR(100) NOT NULL UNIQUE,
                                Description  NVARCHAR(255) NULL,
                                CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END

                        -- 2. Suppliers Table
                        IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.Suppliers (
                                SupplierID      INT IDENTITY(1,1) PRIMARY KEY,
                                SupplierName    NVARCHAR(150) NOT NULL UNIQUE,
                                ContactPerson   NVARCHAR(120) NULL,
                                PhoneNumber     NVARCHAR(50) NULL,
                                EmailAddress    NVARCHAR(150) NULL,
                                BusinessAddress NVARCHAR(255) NULL,
                                CreatedAt       DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END

                        -- 3. Products Table
                        IF OBJECT_ID('dbo.Products', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.Products (
                                ProductID       INT IDENTITY(1,1) PRIMARY KEY,
                                SKU             NVARCHAR(50) NOT NULL UNIQUE,
                                ProductName     NVARCHAR(150) NOT NULL,
                                Description     NVARCHAR(500) NULL,
                                CategoryID      INT NOT NULL,
                                SupplierID      INT NULL,
                                PurchasePrice   DECIMAL(18,2) NOT NULL DEFAULT 0 CHECK (PurchasePrice >= 0),
                                UnitPrice       DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
                                QuantityInStock INT NOT NULL DEFAULT 0 CHECK (QuantityInStock >= 0),
                                ReorderLevel    INT NOT NULL DEFAULT 10 CHECK (ReorderLevel >= 0),
                                SupplierName    NVARCHAR(150) NULL,
                                ImagePath       NVARCHAR(500) NULL,
                                IsActive        BIT NOT NULL DEFAULT 1,
                                CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryID)
                                    REFERENCES dbo.Categories(CategoryID)
                                    ON DELETE NO ACTION,
                                CONSTRAINT FK_Products_Suppliers FOREIGN KEY (SupplierID)
                                    REFERENCES dbo.Suppliers(SupplierID)
                                    ON DELETE NO ACTION
                            );
                        END
                        ELSE
                        BEGIN
                            -- Non-destructive column upgrades for existing databases
                            IF COL_LENGTH('dbo.Products', 'Description') IS NULL
                                ALTER TABLE dbo.Products ADD Description NVARCHAR(500) NULL;

                            IF COL_LENGTH('dbo.Products', 'PurchasePrice') IS NULL
                                ALTER TABLE dbo.Products ADD PurchasePrice DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_PurchasePrice DEFAULT 0;

                            IF COL_LENGTH('dbo.Products', 'SupplierID') IS NULL
                            BEGIN
                                ALTER TABLE dbo.Products ADD SupplierID INT NULL;
                                ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Suppliers FOREIGN KEY (SupplierID)
                                    REFERENCES dbo.Suppliers(SupplierID) ON DELETE NO ACTION;
                            END

                            IF COL_LENGTH('dbo.Products', 'ImagePath') IS NULL
                                ALTER TABLE dbo.Products ADD ImagePath NVARCHAR(500) NULL;
                        END

                        -- 4. StockTransactions Table
                        IF OBJECT_ID('dbo.StockTransactions', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.StockTransactions (
                                TransactionID   INT IDENTITY(1,1) PRIMARY KEY,
                                ProductID       INT NOT NULL,
                                SupplierID      INT NULL,
                                TransactionType NVARCHAR(10) NOT NULL CHECK (TransactionType IN ('IN', 'OUT')),
                                Quantity        INT NOT NULL CHECK (Quantity > 0),
                                PreviousStock   INT NOT NULL,
                                NewStock        INT NOT NULL,
                                ReferenceNo     NVARCHAR(100) NULL,
                                Remarks         NVARCHAR(255) NULL,
                                TransactionDate DATETIME NOT NULL DEFAULT GETDATE(),
                                CONSTRAINT FK_StockTransactions_Products FOREIGN KEY (ProductID)
                                    REFERENCES dbo.Products(ProductID)
                                    ON DELETE CASCADE,
                                CONSTRAINT FK_StockTransactions_Suppliers FOREIGN KEY (SupplierID)
                                    REFERENCES dbo.Suppliers(SupplierID)
                                    ON DELETE NO ACTION
                            );
                        END
                        ELSE
                        BEGIN
                            IF COL_LENGTH('dbo.StockTransactions', 'SupplierID') IS NULL
                            BEGIN
                                ALTER TABLE dbo.StockTransactions ADD SupplierID INT NULL;
                                ALTER TABLE dbo.StockTransactions ADD CONSTRAINT FK_StockTransactions_Suppliers FOREIGN KEY (SupplierID)
                                    REFERENCES dbo.Suppliers(SupplierID) ON DELETE NO ACTION;
                            END
                        END

                        -- 5. ActivityLogs Table
                        IF OBJECT_ID('dbo.ActivityLogs', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.ActivityLogs (
                                ActivityID    INT IDENTITY(1,1) PRIMARY KEY,
                                Action        NVARCHAR(100) NOT NULL,
                                RelatedRecord NVARCHAR(200) NOT NULL,
                                Details       NVARCHAR(500) NULL,
                                ActivityDate  DATETIME NOT NULL DEFAULT GETDATE()
                            );
                        END

                        -- 6. Users Table (Authentication, Roles & Single-Admin Enforcement)
                        IF OBJECT_ID('dbo.Users', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.Users (
                                UserID              INT IDENTITY(1,1) PRIMARY KEY,
                                FullName            NVARCHAR(120) NOT NULL,
                                Username            NVARCHAR(60) NOT NULL UNIQUE,
                                EmailAddress        NVARCHAR(150) NOT NULL UNIQUE,
                                PasswordHash        NVARCHAR(256) NOT NULL,
                                PasswordSalt        NVARCHAR(128) NOT NULL,
                                Role                NVARCHAR(20) NOT NULL CHECK (Role IN ('Admin', 'User')),
                                IsActive            BIT NOT NULL DEFAULT 1,
                                FailedLoginAttempts INT NOT NULL DEFAULT 0,
                                LockoutUntil        DATETIME NULL,
                                CreatedAt           DATETIME NOT NULL DEFAULT GETDATE(),
                                LastLoginAt         DATETIME NULL
                            );
                        END

                        -- Enforce at the database engine level that at most ONE Admin account can ever exist
                        IF NOT EXISTS (
                            SELECT 1 FROM sys.indexes
                            WHERE name = 'UX_Users_SingleAdmin'
                              AND object_id = OBJECT_ID('dbo.Users'))
                        BEGIN
                            CREATE UNIQUE INDEX UX_Users_SingleAdmin
                            ON dbo.Users (Role)
                            WHERE Role = N'Admin';
                        END";

                    using (SqlCommand cmd = new SqlCommand(schemaSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                statusMessage = $"Connected to SQL Server ({DbConnection.ActiveServer})";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = $"Database connection failed: {ex.Message}";
                return false;
            }
        }

        public static bool ClearAllProductsAndStock(out string message)
        {
            if (!InventoryManagementSystem.Security.SessionManager.IsAdmin)
            {
                message = "Access denied: Administrator privileges are required to clear inventory data.";
                return false;
            }

            try
            {
                using (SqlConnection conn = DbConnection.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        DELETE FROM dbo.StockTransactions;
                        DELETE FROM dbo.Products;
                        DBCC CHECKIDENT ('dbo.StockTransactions', RESEED, 0);
                        DBCC CHECKIDENT ('dbo.Products', RESEED, 0);";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                ActivityLogRepository.LogSafe("Inventory Cleared", "All Products", "Removed all products and stock transactions.");
                message = "All products and stock transactions have been cleared.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Failed to clear products/stock: {ex.Message}";
                return false;
            }
        }
    }
}
