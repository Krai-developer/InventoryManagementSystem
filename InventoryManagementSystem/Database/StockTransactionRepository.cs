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
            int? supplierId = null,
            DateTime? transactionDate = null)
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
                                (ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, UserID, PerformedBy, TransactionDate)
                            VALUES
                                (@ProductID, @SupplierID, @TransactionType, @Quantity, @PreviousStock, @NewStock, @ReferenceNo, @Remarks, @UserID, @PerformedBy, @TransactionDate);";

                        using (SqlCommand insertCmd = new SqlCommand(insertSql, conn, tx))
                        {
                            int? uid = SessionManager.CurrentUser?.UserID;
                            insertCmd.Parameters.AddWithValue("@ProductID", productId);
                            insertCmd.Parameters.AddWithValue("@SupplierID", effectiveSupplierId.HasValue ? effectiveSupplierId.Value : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@TransactionType", normalizedType);
                            insertCmd.Parameters.AddWithValue("@Quantity", quantity);
                            insertCmd.Parameters.AddWithValue("@PreviousStock", currentStock);
                            insertCmd.Parameters.AddWithValue("@NewStock", newStock);
                            insertCmd.Parameters.AddWithValue("@ReferenceNo", string.IsNullOrWhiteSpace(referenceNo) ? DBNull.Value : referenceNo.Trim());
                            insertCmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
                            insertCmd.Parameters.AddWithValue("@UserID", uid.HasValue ? uid.Value : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@PerformedBy", SessionManager.CurrentActorLabel);
                            insertCmd.Parameters.AddWithValue("@TransactionDate", transactionDate ?? DateTime.Now);

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

        /// <summary>
        /// Controlled Administrator-only inventory stock adjustment with mandatory reason and full audit history.
        /// </summary>
        public bool RecordStockAdjustment(
            int productId,
            int targetNewStock,
            string reason,
            string remarks,
            out string message,
            DateTime? transactionDate = null)
        {
            SessionManager.RequireAdmin("perform controlled stock adjustments");
            message = string.Empty;

            if (targetNewStock < 0)
            {
                message = "Adjusted stock quantity cannot be negative.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                message = "A reason is required for every controlled stock adjustment.";
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

                        using (SqlCommand selectCmd = new SqlCommand(
                            "SELECT QuantityInStock, ProductName, SKU FROM dbo.Products WITH (UPDLOCK, ROWLOCK) WHERE ProductID = @ProductID;", conn, tx))
                        {
                            selectCmd.Parameters.AddWithValue("@ProductID", productId);
                            using (SqlDataReader r = selectCmd.ExecuteReader())
                            {
                                if (!r.Read())
                                {
                                    tx.Rollback();
                                    message = "Selected product was not found.";
                                    return false;
                                }
                                currentStock = r.GetInt32(0);
                                productName = r.GetString(1);
                                sku = r.GetString(2);
                            }
                        }

                        int delta = Math.Abs(targetNewStock - currentStock);
                        if (delta == 0)
                        {
                            tx.Rollback();
                            message = "New stock quantity matches current stock. No adjustment needed.";
                            return false;
                        }

                        using (SqlCommand updateCmd = new SqlCommand(
                            "UPDATE dbo.Products SET QuantityInStock = @NewStock WHERE ProductID = @ProductID;", conn, tx))
                        {
                            updateCmd.Parameters.AddWithValue("@NewStock", targetNewStock);
                            updateCmd.Parameters.AddWithValue("@ProductID", productId);
                            updateCmd.ExecuteNonQuery();
                        }

                        using (SqlCommand insertCmd = new SqlCommand(@"
                            INSERT INTO dbo.StockTransactions
                                (ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, UserID, PerformedBy, TransactionDate)
                            VALUES
                                (@ProductID, NULL, N'ADJUST', @Quantity, @PreviousStock, @NewStock, @ReferenceNo, @Remarks, @UserID, @PerformedBy, @TransactionDate);", conn, tx))
                        {
                            int? uid = SessionManager.CurrentUser?.UserID;
                            insertCmd.Parameters.AddWithValue("@ProductID", productId);
                            insertCmd.Parameters.AddWithValue("@Quantity", delta);
                            insertCmd.Parameters.AddWithValue("@PreviousStock", currentStock);
                            insertCmd.Parameters.AddWithValue("@NewStock", targetNewStock);
                            insertCmd.Parameters.AddWithValue("@ReferenceNo", reason.Trim());
                            insertCmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
                            insertCmd.Parameters.AddWithValue("@UserID", uid.HasValue ? uid.Value : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@PerformedBy", SessionManager.CurrentActorLabel);
                            insertCmd.Parameters.AddWithValue("@TransactionDate", transactionDate ?? DateTime.Now);
                            insertCmd.ExecuteNonQuery();
                        }

                        tx.Commit();

                        ActivityLogRepository.LogSafe("Stock Adjusted", $"{sku} - {productName}",
                            $"Adjusted stock {currentStock} -> {targetNewStock}. Reason: {reason.Trim()}");

                        message = $"Stock adjustment recorded! '{productName}' adjusted from {currentStock} to {targetNewStock}.";
                        return true;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        message = $"Stock adjustment failed: {ex.Message}";
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
            int supplierId = 0,
            string performedByFilter = "")
        {
            // Permission Matrix: Admin views ALL transactions; User views OWN recorded transactions only.
            SessionManager.RequireAuthenticated("view stock transactions");

            bool isAdmin = SessionManager.IsAdmin;
            int currentUid = SessionManager.CurrentUser!.UserID;
            string currentUsername = SessionManager.CurrentUser!.Username;

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
                    t.UserID,
                    ISNULL(t.PerformedBy, N'System') AS PerformedBy,
                    t.TransactionDate
                FROM dbo.StockTransactions t
                INNER JOIN dbo.Products p ON t.ProductID = p.ProductID
                INNER JOIN dbo.Categories c ON p.CategoryID = c.CategoryID
                LEFT JOIN dbo.Suppliers s ON t.SupplierID = s.SupplierID
                LEFT JOIN dbo.Suppliers ps ON p.SupplierID = ps.SupplierID
                WHERE (@IsAdmin = 1 OR t.UserID = @CurrentUserID OR ISNULL(t.PerformedBy, N'') LIKE @CurrentUserPattern)
                  AND (@Type = N'ALL' OR t.TransactionType = @Type)
                  AND (@CategoryID = 0 OR p.CategoryID = @CategoryID)
                  AND (@ProductID = 0 OR t.ProductID = @ProductID)
                  AND (@SupplierID = 0 OR t.SupplierID = @SupplierID OR p.SupplierID = @SupplierID)
                  AND (@PerformedBy = N'' OR ISNULL(t.PerformedBy, N'') LIKE @PerformedByPattern)
                  AND (@FromDate IS NULL OR t.TransactionDate >= @FromDate)
                  AND (@ToDate IS NULL OR t.TransactionDate < @ToDate)
                  AND (@Keyword = N'' 
                       OR p.SKU LIKE @Pattern 
                       OR p.ProductName LIKE @Pattern 
                       OR t.ReferenceNo LIKE @Pattern 
                       OR t.Remarks LIKE @Pattern
                       OR ISNULL(t.PerformedBy, N'') LIKE @Pattern)
                ORDER BY t.TransactionDate DESC, t.TransactionID DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmedKeyword = (searchKeyword ?? string.Empty).Trim();
                string trimmedActor = (performedByFilter ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@IsAdmin", isAdmin ? 1 : 0);
                cmd.Parameters.AddWithValue("@CurrentUserID", currentUid);
                cmd.Parameters.AddWithValue("@CurrentUserPattern", $"{currentUsername}%");
                cmd.Parameters.AddWithValue("@Type", (transactionType ?? "ALL").ToUpperInvariant());
                cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                cmd.Parameters.AddWithValue("@ProductID", productId);
                cmd.Parameters.AddWithValue("@SupplierID", supplierId);
                cmd.Parameters.AddWithValue("@PerformedBy", trimmedActor);
                cmd.Parameters.AddWithValue("@PerformedByPattern", $"%{trimmedActor}%");
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
                            UserID = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                            PerformedBy = reader.GetString(14),
                            TransactionDate = reader.GetDateTime(15)
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
