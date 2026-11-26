using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem.Database
{
    public class StockTransactionRepository
    {
        public bool RecordStockMovement(
            int productId,
            string transactionType,
            int quantity,
            string referenceNo,
            string remarks,
            out string message,
            int? supplierId = null)
        {
            SessionManager.RequireAuthenticated("record stock transactions");

            message = string.Empty;

            if (quantity <= 0)
            {
                message = "Quantity must be a positive number greater than zero.";
                return false;
            }

            string normalizedType = (transactionType ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedType != "IN" && normalizedType != "OUT")
            {
                message = "Transaction type must be 'IN' or 'OUT'.";
                return false;
            }

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        int currentStock;
                        string productName;
                        string sku;
                        string selectSql = @"
                            SELECT QuantityInStock, ProductName, SKU
                            FROM dbo.Products WITH (UPDLOCK, ROWLOCK)
                            WHERE ProductID = @ProductID;";

                        using (SqlCommand selectCmd = new SqlCommand(selectSql, conn, tx))
                        {
                            selectCmd.Parameters.AddWithValue("@ProductID", productId);
                            using (SqlDataReader reader = selectCmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tx.Rollback();
                                    message = "Selected product was not found in the database.";
                                    return false;
                                }

                                currentStock = reader.GetInt32(0);
                                productName = reader.GetString(1);
                                sku = reader.GetString(2);
                            }
                        }

                        if (normalizedType == "OUT" && quantity > currentStock)
                        {
                            tx.Rollback();
                            message = $"Insufficient stock for '{productName}'. Available: {currentStock}, Requested: {quantity}.";
                            return false;
                        }

                        int newStock = normalizedType == "IN"
                            ? currentStock + quantity
                            : currentStock - quantity;

                        string updateSql = @"
                            UPDATE dbo.Products
                            SET QuantityInStock = @NewStock
                            WHERE ProductID = @ProductID;";

                        using (SqlCommand updateCmd = new SqlCommand(updateSql, conn, tx))
                        {
                            updateCmd.Parameters.AddWithValue("@NewStock", newStock);
                            updateCmd.Parameters.AddWithValue("@ProductID", productId);
                            updateCmd.ExecuteNonQuery();
                        }

                        int? effectiveSupplierId = (normalizedType == "IN" && supplierId.HasValue && supplierId.Value > 0)
                            ? supplierId.Value
                            : null;

                        string insertSql = @"
                            INSERT INTO dbo.StockTransactions
                                (ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, TransactionDate)
                            VALUES
                                (@ProductID, @SupplierID, @TransactionType, @Quantity, @PreviousStock, @NewStock, @ReferenceNo, @Remarks, GETDATE());";

                        using (SqlCommand insertCmd = new SqlCommand(insertSql, conn, tx))
                        {
                            insertCmd.Parameters.AddWithValue("@ProductID", productId);
                            insertCmd.Parameters.AddWithValue("@SupplierID", effectiveSupplierId.HasValue ? effectiveSupplierId.Value : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@TransactionType", normalizedType);
                            insertCmd.Parameters.AddWithValue("@Quantity", quantity);
                            insertCmd.Parameters.AddWithValue("@PreviousStock", currentStock);
                            insertCmd.Parameters.AddWithValue("@NewStock", newStock);
                            insertCmd.Parameters.AddWithValue("@ReferenceNo", string.IsNullOrWhiteSpace(referenceNo) ? DBNull.Value : referenceNo.Trim());
                            insertCmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());

                            insertCmd.ExecuteNonQuery();
                        }

                        tx.Commit();

                        string actionTitle = normalizedType == "IN" ? "Stock Received" : "Stock Released";
                        string sign = normalizedType == "IN" ? "+" : "-";
                        ActivityLogRepository.LogSafe(actionTitle, $"{sku} - {productName}",
                            $"{sign}{quantity} units (Stock: {currentStock} -> {newStock}). Ref: {referenceNo}");

                        string actionVerb = normalizedType == "IN" ? "received into" : "released from";
                        message = $"Successfully {actionVerb} stock! '{productName}' updated from {currentStock} to {newStock}.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        message = $"Transaction failed: {ex.Message}";
                        return false;
                    }
                }
            }
        }

        public List<StockTransaction> GetTransactions(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string transactionType = "ALL",
            int categoryId = 0,
            string searchKeyword = "",
            int maxRows = 500,
            int productId = 0,
            int supplierId = 0)
        {
            var list = new List<StockTransaction>();

            string sql = $@"
                SELECT TOP ({maxRows})
                    t.TransactionID,
                    t.ProductID,
                    p.SKU,
                    p.ProductName,
                    c.CategoryName,
                    t.SupplierID,
                    COALESCE(s.SupplierName, ps.SupplierName, p.SupplierName, N'') AS SupplierName,
                    t.TransactionType,
                    t.Quantity,
                    t.PreviousStock,
                    t.NewStock,
                    ISNULL(t.ReferenceNo, N'') AS ReferenceNo,
                    ISNULL(t.Remarks, N'') AS Remarks,
                    t.TransactionDate
                FROM dbo.StockTransactions t
                INNER JOIN dbo.Products p ON t.ProductID = p.ProductID
                INNER JOIN dbo.Categories c ON p.CategoryID = c.CategoryID
                LEFT JOIN dbo.Suppliers s ON t.SupplierID = s.SupplierID
                LEFT JOIN dbo.Suppliers ps ON p.SupplierID = ps.SupplierID
                WHERE (@Type = N'ALL' OR t.TransactionType = @Type)
                  AND (@CategoryID = 0 OR p.CategoryID = @CategoryID)
                  AND (@ProductID = 0 OR t.ProductID = @ProductID)
                  AND (@SupplierID = 0 OR t.SupplierID = @SupplierID OR p.SupplierID = @SupplierID)
                  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
                  AND (@ToDate IS NULL OR t.TransactionDate < @ToDate)
                  AND (@Keyword = N'' 
                       OR p.SKU LIKE @Pattern 
                       OR p.ProductName LIKE @Pattern 
                       OR t.ReferenceNo LIKE @Pattern 
                       OR t.Remarks LIKE @Pattern)
                ORDER BY t.TransactionDate DESC, t.TransactionID DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmedKeyword = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Type", (transactionType ?? "ALL").ToUpperInvariant());
                cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                cmd.Parameters.AddWithValue("@ProductID", productId);
                cmd.Parameters.AddWithValue("@SupplierID", supplierId);
                cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? toDate.Value.Date.AddDays(1) : DBNull.Value);
                cmd.Parameters.AddWithValue("@Keyword", trimmedKeyword);
                cmd.Parameters.AddWithValue("@Pattern", $"%{trimmedKeyword}%");

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new StockTransaction
                        {
                            TransactionID = reader.GetInt32(0),
                            ProductID = reader.GetInt32(1),
                            SKU = reader.GetString(2),
                            ProductName = reader.GetString(3),
                            CategoryName = reader.GetString(4),
                            SupplierID = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            SupplierName = reader.GetString(6),
                            TransactionType = reader.GetString(7),
                            Quantity = reader.GetInt32(8),
                            PreviousStock = reader.GetInt32(9),
                            NewStock = reader.GetInt32(10),
                            ReferenceNo = reader.GetString(11),
                            Remarks = reader.GetString(12),
                            TransactionDate = reader.GetDateTime(13)
                        });
                    }
                }
            }

            return list;
        }

        public DashboardSummary GetDashboardSummary()
        {
            var summary = new DashboardSummary();

            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM dbo.Products WHERE IsActive = 1) AS TotalProducts,
                    (SELECT COUNT(*) FROM dbo.Categories) AS TotalCategories,
                    (SELECT ISNULL(SUM(QuantityInStock), 0) FROM dbo.Products WHERE IsActive = 1) AS TotalUnits,
                    (SELECT ISNULL(SUM(CAST(QuantityInStock AS DECIMAL(18,2)) * UnitPrice), 0) FROM dbo.Products WHERE IsActive = 1) AS TotalValue,
                    (SELECT COUNT(*) FROM dbo.Products WHERE IsActive = 1 AND QuantityInStock <= ReorderLevel) AS LowStockCount,
                    (SELECT COUNT(*) FROM dbo.Products WHERE IsActive = 1 AND QuantityInStock = 0) AS OutOfStockCount,
                    (SELECT COUNT(*) FROM dbo.StockTransactions WHERE TransactionDate >= CAST(GETDATE() AS DATE)) AS TodayTransactions;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        summary.TotalProducts = reader.GetInt32(0);
                        summary.TotalCategories = reader.GetInt32(1);
                        summary.TotalUnitsInStock = reader.GetInt32(2);
                        summary.TotalInventoryValue = reader.GetDecimal(3);
                        summary.LowStockCount = reader.GetInt32(4);
                        summary.OutOfStockCount = reader.GetInt32(5);
                        summary.TodayTransactionsCount = reader.GetInt32(6);
                    }
                }
            }

            return summary;
        }
    }
}
