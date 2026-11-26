using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem.Database
{
    public class InventoryBackupData
    {
        public string FormatVersion { get; set; } = "1.0";
        public string ApplicationName { get; set; } = "InventoryManagementSystem";
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public List<CategoryBackupRow> Categories { get; set; } = new List<CategoryBackupRow>();
        public List<SupplierBackupRow> Suppliers { get; set; } = new List<SupplierBackupRow>();
        public List<ProductBackupRow> Products { get; set; } = new List<ProductBackupRow>();
        public List<StockTransactionBackupRow> StockTransactions { get; set; } = new List<StockTransactionBackupRow>();
        public List<ActivityLogBackupRow> ActivityLogs { get; set; } = new List<ActivityLogBackupRow>();
    }

    public class CategoryBackupRow
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SupplierBackupRow
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? BusinessAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductBackupRow
    {
        public int ProductID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryID { get; set; }
        public int? SupplierID { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal UnitPrice { get; set; }
        public int QuantityInStock { get; set; }
        public int ReorderLevel { get; set; }
        public string? SupplierName { get; set; }
        public string? ImagePath { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StockTransactionBackupRow
    {
        public int TransactionID { get; set; }
        public int ProductID { get; set; }
        public int? SupplierID { get; set; }
        public string TransactionType { get; set; } = "IN";
        public int Quantity { get; set; }
        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class ActivityLogBackupRow
    {
        public int ActivityID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string RelatedRecord { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime ActivityDate { get; set; }
    }

    /// <summary>
    /// Handles full database backup, integrity verification, and ACID-protected restore
    /// for all InventoryManagementDB tables.
    /// </summary>
    public class BackupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public bool CreateBackup(string filePath, out string message)
        {
            SessionManager.RequireAdmin("create database backups");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                message = "Please specify a valid backup file path.";
                return false;
            }

            try
            {
                InventoryBackupData backup = ExportAllTables();

                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(backup, JsonOptions);
                File.WriteAllText(filePath, json);

                // Verify the created backup file immediately after writing
                if (!ValidateBackupFile(filePath, out InventoryBackupData? verified, out string verifyErr) || verified == null)
                {
                    message = $"Backup file was written but failed verification: {verifyErr}";
                    return false;
                }

                if (verified.Categories.Count != backup.Categories.Count ||
                    verified.Suppliers.Count != backup.Suppliers.Count ||
                    verified.Products.Count != backup.Products.Count ||
                    verified.StockTransactions.Count != backup.StockTransactions.Count ||
                    verified.ActivityLogs.Count != backup.ActivityLogs.Count)
                {
                    message = "Backup verification failed: record counts did not match exported data.";
                    return false;
                }

                ActivityLogRepository.LogSafe(
                    "Backup Created",
                    Path.GetFileName(filePath),
                    $"Exported {backup.Products.Count} products, {backup.Categories.Count} categories, {backup.Suppliers.Count} suppliers, {backup.StockTransactions.Count} transactions.");

                message = $"Backup created and verified successfully.\n" +
                          $"File: {filePath}\n" +
                          $"Records: {backup.Categories.Count} categories, {backup.Suppliers.Count} suppliers, " +
                          $"{backup.Products.Count} products, {backup.StockTransactions.Count} stock transactions, " +
                          $"{backup.ActivityLogs.Count} activity logs.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Failed to create backup: {ex.Message}";
                return false;
            }
        }

        public bool ValidateBackupFile(string filePath, out InventoryBackupData? data, out string errorMessage)
        {
            data = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                errorMessage = "Backup file does not exist.";
                return false;
            }

            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length == 0)
                {
                    errorMessage = "Backup file is empty.";
                    return false;
                }

                string json = File.ReadAllText(filePath);
                InventoryBackupData? parsed = JsonSerializer.Deserialize<InventoryBackupData>(json, JsonOptions);
                if (parsed == null)
                {
                    errorMessage = "Backup file could not be parsed as valid inventory backup JSON.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(parsed.FormatVersion) ||
                    !string.Equals(parsed.ApplicationName, "InventoryManagementSystem", StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Selected file is not a recognized Inventory Management System backup.";
                    return false;
                }

                if (parsed.Categories == null || parsed.Suppliers == null || parsed.Products == null ||
                    parsed.StockTransactions == null || parsed.ActivityLogs == null)
                {
                    errorMessage = "Backup file is missing one or more required data sections.";
                    return false;
                }

                var categoryIds = new HashSet<int>(parsed.Categories.Select(c => c.CategoryID));
                var supplierIds = new HashSet<int>(parsed.Suppliers.Select(s => s.SupplierID));
                var productIds = new HashSet<int>(parsed.Products.Select(p => p.ProductID));

                foreach (var p in parsed.Products)
                {
                    if (string.IsNullOrWhiteSpace(p.SKU) || string.IsNullOrWhiteSpace(p.ProductName))
                    {
                        errorMessage = $"Backup contains an invalid product record (ID {p.ProductID}).";
                        return false;
                    }
                    if (!categoryIds.Contains(p.CategoryID))
                    {
                        errorMessage = $"Backup integrity error: Product '{p.SKU}' references missing CategoryID {p.CategoryID}.";
                        return false;
                    }
                    if (p.SupplierID.HasValue && !supplierIds.Contains(p.SupplierID.Value))
                    {
                        errorMessage = $"Backup integrity error: Product '{p.SKU}' references missing SupplierID {p.SupplierID.Value}.";
                        return false;
                    }
                    if (p.PurchasePrice < 0 || p.UnitPrice < 0 || p.QuantityInStock < 0 || p.ReorderLevel < 0)
                    {
                        errorMessage = $"Backup integrity error: Product '{p.SKU}' has negative price or stock values.";
                        return false;
                    }
                }

                foreach (var tx in parsed.StockTransactions)
                {
                    if (!productIds.Contains(tx.ProductID))
                    {
                        errorMessage = $"Backup integrity error: Stock transaction #{tx.TransactionID} references missing ProductID {tx.ProductID}.";
                        return false;
                    }
                    if (tx.SupplierID.HasValue && !supplierIds.Contains(tx.SupplierID.Value))
                    {
                        errorMessage = $"Backup integrity error: Stock transaction #{tx.TransactionID} references missing SupplierID {tx.SupplierID.Value}.";
                        return false;
                    }
                    if (tx.TransactionType != "IN" && tx.TransactionType != "OUT")
                    {
                        errorMessage = $"Backup integrity error: Stock transaction #{tx.TransactionID} has invalid type '{tx.TransactionType}'.";
                        return false;
                    }
                    if (tx.Quantity <= 0)
                    {
                        errorMessage = $"Backup integrity error: Stock transaction #{tx.TransactionID} has non-positive quantity.";
                        return false;
                    }
                }

                data = parsed;
                return true;
            }
            catch (JsonException jex)
            {
                errorMessage = $"Invalid JSON format in backup file: {jex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to read backup file: {ex.Message}";
                return false;
            }
        }

        public bool RestoreBackup(string filePath, out string message)
        {
            SessionManager.RequireAdmin("restore database backups");

            if (!ValidateBackupFile(filePath, out InventoryBackupData? backup, out string error) || backup == null)
            {
                message = $"Restore aborted. {error}";
                return false;
            }

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Clear existing tables in FK-safe order
                        ExecuteNonQuery(conn, tx, "DELETE FROM dbo.StockTransactions;");
                        ExecuteNonQuery(conn, tx, "DELETE FROM dbo.Products;");
                        ExecuteNonQuery(conn, tx, "DELETE FROM dbo.Suppliers;");
                        ExecuteNonQuery(conn, tx, "DELETE FROM dbo.Categories;");
                        ExecuteNonQuery(conn, tx, "DELETE FROM dbo.ActivityLogs;");

                        // 2. Restore Categories
                        if (backup.Categories.Count > 0)
                        {
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Categories ON;");
                            foreach (var c in backup.Categories)
                            {
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Categories (CategoryID, CategoryName, Description, CreatedAt)
                                    VALUES (@CategoryID, @CategoryName, @Description, @CreatedAt);", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@CategoryID", c.CategoryID);
                                    cmd.Parameters.AddWithValue("@CategoryName", c.CategoryName);
                                    cmd.Parameters.AddWithValue("@Description", (object?)c.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CreatedAt", c.CreatedAt);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Categories OFF;");
                        }

                        // 3. Restore Suppliers
                        if (backup.Suppliers.Count > 0)
                        {
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Suppliers ON;");
                            foreach (var s in backup.Suppliers)
                            {
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Suppliers (SupplierID, SupplierName, ContactPerson, PhoneNumber, EmailAddress, BusinessAddress, CreatedAt)
                                    VALUES (@SupplierID, @SupplierName, @ContactPerson, @PhoneNumber, @EmailAddress, @BusinessAddress, @CreatedAt);", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@SupplierID", s.SupplierID);
                                    cmd.Parameters.AddWithValue("@SupplierName", s.SupplierName);
                                    cmd.Parameters.AddWithValue("@ContactPerson", (object?)s.ContactPerson ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@PhoneNumber", (object?)s.PhoneNumber ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@EmailAddress", (object?)s.EmailAddress ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@BusinessAddress", (object?)s.BusinessAddress ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CreatedAt", s.CreatedAt);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Suppliers OFF;");
                        }

                        // 4. Restore Products
                        if (backup.Products.Count > 0)
                        {
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Products ON;");
                            foreach (var p in backup.Products)
                            {
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO dbo.Products (ProductID, SKU, ProductName, Description, CategoryID, SupplierID, PurchasePrice, UnitPrice, QuantityInStock, ReorderLevel, SupplierName, ImagePath, IsActive, CreatedAt)
                                    VALUES (@ProductID, @SKU, @ProductName, @Description, @CategoryID, @SupplierID, @PurchasePrice, @UnitPrice, @QuantityInStock, @ReorderLevel, @SupplierName, @ImagePath, @IsActive, @CreatedAt);", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@ProductID", p.ProductID);
                                    cmd.Parameters.AddWithValue("@SKU", p.SKU);
                                    cmd.Parameters.AddWithValue("@ProductName", p.ProductName);
                                    cmd.Parameters.AddWithValue("@Description", (object?)p.Description ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CategoryID", p.CategoryID);
                                    cmd.Parameters.AddWithValue("@SupplierID", p.SupplierID.HasValue ? p.SupplierID.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@PurchasePrice", p.PurchasePrice);
                                    cmd.Parameters.AddWithValue("@UnitPrice", p.UnitPrice);
                                    cmd.Parameters.AddWithValue("@QuantityInStock", p.QuantityInStock);
                                    cmd.Parameters.AddWithValue("@ReorderLevel", p.ReorderLevel);
                                    cmd.Parameters.AddWithValue("@SupplierName", (object?)p.SupplierName ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ImagePath", (object?)p.ImagePath ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IsActive", p.IsActive);
                                    cmd.Parameters.AddWithValue("@CreatedAt", p.CreatedAt);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.Products OFF;");
                        }

                        // 5. Restore StockTransactions
                        if (backup.StockTransactions.Count > 0)
                        {
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.StockTransactions ON;");
                            foreach (var st in backup.StockTransactions)
                            {
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO dbo.StockTransactions (TransactionID, ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, TransactionDate)
                                    VALUES (@TransactionID, @ProductID, @SupplierID, @TransactionType, @Quantity, @PreviousStock, @NewStock, @ReferenceNo, @Remarks, @TransactionDate);", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@TransactionID", st.TransactionID);
                                    cmd.Parameters.AddWithValue("@ProductID", st.ProductID);
                                    cmd.Parameters.AddWithValue("@SupplierID", st.SupplierID.HasValue ? st.SupplierID.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@TransactionType", st.TransactionType);
                                    cmd.Parameters.AddWithValue("@Quantity", st.Quantity);
                                    cmd.Parameters.AddWithValue("@PreviousStock", st.PreviousStock);
                                    cmd.Parameters.AddWithValue("@NewStock", st.NewStock);
                                    cmd.Parameters.AddWithValue("@ReferenceNo", (object?)st.ReferenceNo ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Remarks", (object?)st.Remarks ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@TransactionDate", st.TransactionDate);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.StockTransactions OFF;");
                        }

                        // 6. Restore ActivityLogs
                        if (backup.ActivityLogs.Count > 0)
                        {
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.ActivityLogs ON;");
                            foreach (var al in backup.ActivityLogs)
                            {
                                using (SqlCommand cmd = new SqlCommand(@"
                                    INSERT INTO dbo.ActivityLogs (ActivityID, Action, RelatedRecord, Details, ActivityDate)
                                    VALUES (@ActivityID, @Action, @RelatedRecord, @Details, @ActivityDate);", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@ActivityID", al.ActivityID);
                                    cmd.Parameters.AddWithValue("@Action", al.Action);
                                    cmd.Parameters.AddWithValue("@RelatedRecord", al.RelatedRecord);
                                    cmd.Parameters.AddWithValue("@Details", (object?)al.Details ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ActivityDate", al.ActivityDate);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            ExecuteNonQuery(conn, tx, "SET IDENTITY_INSERT dbo.ActivityLogs OFF;");
                        }

                        // 7. Reseed identities so future inserts continue cleanly
                        ReseedTable(conn, tx, "dbo.Categories", backup.Categories.Count > 0 ? backup.Categories.Max(x => x.CategoryID) : 0);
                        ReseedTable(conn, tx, "dbo.Suppliers", backup.Suppliers.Count > 0 ? backup.Suppliers.Max(x => x.SupplierID) : 0);
                        ReseedTable(conn, tx, "dbo.Products", backup.Products.Count > 0 ? backup.Products.Max(x => x.ProductID) : 0);
                        ReseedTable(conn, tx, "dbo.StockTransactions", backup.StockTransactions.Count > 0 ? backup.StockTransactions.Max(x => x.TransactionID) : 0);
                        ReseedTable(conn, tx, "dbo.ActivityLogs", backup.ActivityLogs.Count > 0 ? backup.ActivityLogs.Max(x => x.ActivityID) : 0);

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        message = $"Restore failed and was rolled back so existing data remains intact.\nError: {ex.Message}";
                        return false;
                    }
                }
            }

            ActivityLogRepository.LogSafe(
                "Data Restored",
                Path.GetFileName(filePath),
                $"Restored {backup.Products.Count} products, {backup.Categories.Count} categories, {backup.Suppliers.Count} suppliers, {backup.StockTransactions.Count} transactions.");

            message = $"Backup restored successfully.\n" +
                      $"Restored {backup.Categories.Count} categories, {backup.Suppliers.Count} suppliers, " +
                      $"{backup.Products.Count} products, {backup.StockTransactions.Count} stock transactions, and " +
                      $"{backup.ActivityLogs.Count} activity logs.";
            return true;
        }

        private InventoryBackupData ExportAllTables()
        {
            var data = new InventoryBackupData
            {
                FormatVersion = "1.0",
                ApplicationName = "InventoryManagementSystem",
                ExportedAt = DateTime.Now
            };

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand("SELECT CategoryID, CategoryName, Description, CreatedAt FROM dbo.Categories ORDER BY CategoryID;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.Categories.Add(new CategoryBackupRow
                        {
                            CategoryID = r.GetInt32(0),
                            CategoryName = r.GetString(1),
                            Description = r.IsDBNull(2) ? null : r.GetString(2),
                            CreatedAt = r.GetDateTime(3)
                        });
                    }
                }

                using (SqlCommand cmd = new SqlCommand("SELECT SupplierID, SupplierName, ContactPerson, PhoneNumber, EmailAddress, BusinessAddress, CreatedAt FROM dbo.Suppliers ORDER BY SupplierID;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.Suppliers.Add(new SupplierBackupRow
                        {
                            SupplierID = r.GetInt32(0),
                            SupplierName = r.GetString(1),
                            ContactPerson = r.IsDBNull(2) ? null : r.GetString(2),
                            PhoneNumber = r.IsDBNull(3) ? null : r.GetString(3),
                            EmailAddress = r.IsDBNull(4) ? null : r.GetString(4),
                            BusinessAddress = r.IsDBNull(5) ? null : r.GetString(5),
                            CreatedAt = r.GetDateTime(6)
                        });
                    }
                }

                using (SqlCommand cmd = new SqlCommand("SELECT ProductID, SKU, ProductName, Description, CategoryID, SupplierID, PurchasePrice, UnitPrice, QuantityInStock, ReorderLevel, SupplierName, ImagePath, IsActive, CreatedAt FROM dbo.Products ORDER BY ProductID;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.Products.Add(new ProductBackupRow
                        {
                            ProductID = r.GetInt32(0),
                            SKU = r.GetString(1),
                            ProductName = r.GetString(2),
                            Description = r.IsDBNull(3) ? null : r.GetString(3),
                            CategoryID = r.GetInt32(4),
                            SupplierID = r.IsDBNull(5) ? null : r.GetInt32(5),
                            PurchasePrice = r.GetDecimal(6),
                            UnitPrice = r.GetDecimal(7),
                            QuantityInStock = r.GetInt32(8),
                            ReorderLevel = r.GetInt32(9),
                            SupplierName = r.IsDBNull(10) ? null : r.GetString(10),
                            ImagePath = r.IsDBNull(11) ? null : r.GetString(11),
                            IsActive = r.GetBoolean(12),
                            CreatedAt = r.GetDateTime(13)
                        });
                    }
                }

                using (SqlCommand cmd = new SqlCommand("SELECT TransactionID, ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, TransactionDate FROM dbo.StockTransactions ORDER BY TransactionID;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.StockTransactions.Add(new StockTransactionBackupRow
                        {
                            TransactionID = r.GetInt32(0),
                            ProductID = r.GetInt32(1),
                            SupplierID = r.IsDBNull(2) ? null : r.GetInt32(2),
                            TransactionType = r.GetString(3),
                            Quantity = r.GetInt32(4),
                            PreviousStock = r.GetInt32(5),
                            NewStock = r.GetInt32(6),
                            ReferenceNo = r.IsDBNull(7) ? null : r.GetString(7),
                            Remarks = r.IsDBNull(8) ? null : r.GetString(8),
                            TransactionDate = r.GetDateTime(9)
                        });
                    }
                }

                using (SqlCommand cmd = new SqlCommand("SELECT ActivityID, Action, RelatedRecord, Details, ActivityDate FROM dbo.ActivityLogs ORDER BY ActivityID;", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.ActivityLogs.Add(new ActivityLogBackupRow
                        {
                            ActivityID = r.GetInt32(0),
                            Action = r.GetString(1),
                            RelatedRecord = r.GetString(2),
                            Details = r.IsDBNull(3) ? null : r.GetString(3),
                            ActivityDate = r.GetDateTime(4)
                        });
                    }
                }
            }

            return data;
        }

        private static void ExecuteNonQuery(SqlConnection conn, SqlTransaction tx, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static void ReseedTable(SqlConnection conn, SqlTransaction tx, string tableName, int seedValue)
        {
            using (SqlCommand cmd = new SqlCommand($"DBCC CHECKIDENT ('{tableName}', RESEED, {seedValue});", conn, tx))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
