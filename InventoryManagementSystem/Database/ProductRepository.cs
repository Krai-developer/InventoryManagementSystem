using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Database
{
    public class ProductRepository
    {
        private const string BaseSelectSql = @"
                SELECT 
                    p.ProductID,
                    p.SKU,
                    p.ProductName,
                    ISNULL(p.Description, N'') AS Description,
                    p.CategoryID,
                    c.CategoryName,
                    p.SupplierID,
                    COALESCE(s.SupplierName, p.SupplierName, N'') AS SupplierName,
                    ISNULL(p.PurchasePrice, 0) AS PurchasePrice,
                    p.UnitPrice,
                    p.QuantityInStock,
                    p.ReorderLevel,
                    ISNULL(p.ImagePath, N'') AS ImagePath,
                    p.IsActive,
                    p.CreatedAt
                FROM dbo.Products p
                INNER JOIN dbo.Categories c ON p.CategoryID = c.CategoryID
                LEFT JOIN dbo.Suppliers s ON p.SupplierID = s.SupplierID";

        public List<Product> SearchProducts(
            string searchKeyword = "",
            int categoryId = 0,
            string stockFilter = "ALL",
            int supplierId = 0)
        {
            var products = new List<Product>();

            string sql = BaseSelectSql + @"
                WHERE p.IsActive = 1
                  AND (@Keyword = N'' 
                       OR CAST(p.ProductID AS NVARCHAR(20)) = @Keyword
                       OR p.SKU LIKE @SearchPattern 
                       OR p.ProductName LIKE @SearchPattern 
                       OR p.Description LIKE @SearchPattern
                       OR COALESCE(s.SupplierName, p.SupplierName, N'') LIKE @SearchPattern
                       OR c.CategoryName LIKE @SearchPattern)
                  AND (@CategoryID = 0 OR p.CategoryID = @CategoryID)
                  AND (@SupplierID = 0 OR p.SupplierID = @SupplierID)
                  AND (
                        @StockFilter = N'ALL'
                        OR (@StockFilter = N'IN_STOCK' AND p.QuantityInStock > p.ReorderLevel)
                        OR (@StockFilter = N'LOW_STOCK' AND p.QuantityInStock > 0 AND p.QuantityInStock <= p.ReorderLevel)
                        OR (@StockFilter = N'LOW_OR_OUT' AND p.QuantityInStock <= p.ReorderLevel)
                        OR (@StockFilter = N'OUT_OF_STOCK' AND p.QuantityInStock = 0)
                      )
                ORDER BY p.ProductName ASC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Keyword", trimmed);
                cmd.Parameters.AddWithValue("@SearchPattern", $"%{trimmed}%");
                cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                cmd.Parameters.AddWithValue("@SupplierID", supplierId);
                cmd.Parameters.AddWithValue("@StockFilter", stockFilter ?? "ALL");

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(MapProduct(reader));
                    }
                }
            }

            return products;
        }

        public bool SkuExists(string sku, int excludeProductId = 0)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return false;

            string sql = @"
                SELECT COUNT(*)
                FROM dbo.Products
                WHERE UPPER(SKU) = @SKU
                  AND ProductID <> @ExcludeID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@SKU", sku.Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@ExcludeID", excludeProductId);

                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public List<Product> GetLowStockProducts(int categoryId = 0, bool onlyOutOfStock = false)
        {
            var products = new List<Product>();

            string sql = BaseSelectSql + @"
                WHERE p.IsActive = 1
                  AND p.QuantityInStock <= p.ReorderLevel
                  AND (@CategoryID = 0 OR p.CategoryID = @CategoryID)
                  AND (@OnlyOut = 0 OR p.QuantityInStock = 0)
                ORDER BY p.QuantityInStock ASC, (p.ReorderLevel - p.QuantityInStock) DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                cmd.Parameters.AddWithValue("@OnlyOut", onlyOutOfStock ? 1 : 0);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(MapProduct(reader));
                    }
                }
            }

            return products;
        }

        public Product? GetProductById(int productId)
        {
            string sql = BaseSelectSql + " WHERE p.ProductID = @ProductID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ProductID", productId);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        return MapProduct(reader);
                }
            }

            return null;
        }

        public int AddProduct(Product product)
        {
            if (!product.Validate(out string validationError))
                throw new ArgumentException(validationError);

            if (SkuExists(product.SKU))
                throw new InvalidOperationException($"Duplicate Product SKU/ID: '{product.SKU}' already exists.");

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        string insertSql = @"
                            INSERT INTO dbo.Products 
                                (SKU, ProductName, Description, CategoryID, SupplierID, PurchasePrice, UnitPrice,
                                 QuantityInStock, ReorderLevel, SupplierName, ImagePath, IsActive, CreatedAt)
                            VALUES 
                                (@SKU, @ProductName, @Description, @CategoryID, @SupplierID, @PurchasePrice, @UnitPrice,
                                 @QuantityInStock, @ReorderLevel, @SupplierName, @ImagePath, 1, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int newProductId;
                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@SKU", product.SKU);
                            cmd.Parameters.AddWithValue("@ProductName", product.ProductName);
                            cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(product.Description) ? DBNull.Value : product.Description);
                            cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID);
                            cmd.Parameters.AddWithValue("@SupplierID", product.SupplierID.HasValue ? product.SupplierID.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@PurchasePrice", product.PurchasePrice);
                            cmd.Parameters.AddWithValue("@UnitPrice", product.UnitPrice);
                            cmd.Parameters.AddWithValue("@QuantityInStock", product.QuantityInStock);
                            cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
                            cmd.Parameters.AddWithValue("@SupplierName", string.IsNullOrWhiteSpace(product.SupplierName) ? DBNull.Value : product.SupplierName);
                            cmd.Parameters.AddWithValue("@ImagePath", string.IsNullOrWhiteSpace(product.ImagePath) ? DBNull.Value : product.ImagePath);

                            newProductId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        if (product.QuantityInStock > 0)
                        {
                            string logSql = @"
                                INSERT INTO dbo.StockTransactions
                                    (ProductID, SupplierID, TransactionType, Quantity, PreviousStock, NewStock, ReferenceNo, Remarks, TransactionDate)
                                VALUES
                                    (@ProductID, @SupplierID, N'IN', @Quantity, 0, @NewStock, N'INIT-STOCK', N'Opening stock on product creation', GETDATE());";

                            using (SqlCommand logCmd = new SqlCommand(logSql, conn, tx))
                            {
                                logCmd.Parameters.AddWithValue("@ProductID", newProductId);
                                logCmd.Parameters.AddWithValue("@SupplierID", product.SupplierID.HasValue ? product.SupplierID.Value : DBNull.Value);
                                logCmd.Parameters.AddWithValue("@Quantity", product.QuantityInStock);
                                logCmd.Parameters.AddWithValue("@NewStock", product.QuantityInStock);
                                logCmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                        product.ProductID = newProductId;

                        ActivityLogRepository.LogSafe("Product Added", $"{product.SKU} - {product.ProductName}",
                            $"Initial Qty: {product.QuantityInStock}, Selling Price: ₱{product.UnitPrice:N2}.");

                        return newProductId;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public bool UpdateProduct(Product product)
        {
            if (!product.Validate(out string validationError))
                throw new ArgumentException(validationError);

            if (SkuExists(product.SKU, product.ProductID))
                throw new InvalidOperationException($"Duplicate Product SKU/ID: Another product already uses '{product.SKU}'.");

            string sql = @"
                UPDATE dbo.Products
                SET SKU           = @SKU,
                    ProductName   = @ProductName,
                    Description   = @Description,
                    CategoryID    = @CategoryID,
                    SupplierID    = @SupplierID,
                    PurchasePrice = @PurchasePrice,
                    UnitPrice     = @UnitPrice,
                    ReorderLevel  = @ReorderLevel,
                    SupplierName  = @SupplierName,
                    ImagePath     = @ImagePath
                WHERE ProductID   = @ProductID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ProductID", product.ProductID);
                cmd.Parameters.AddWithValue("@SKU", product.SKU);
                cmd.Parameters.AddWithValue("@ProductName", product.ProductName);
                cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(product.Description) ? DBNull.Value : product.Description);
                cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID);
                cmd.Parameters.AddWithValue("@SupplierID", product.SupplierID.HasValue ? product.SupplierID.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@PurchasePrice", product.PurchasePrice);
                cmd.Parameters.AddWithValue("@UnitPrice", product.UnitPrice);
                cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
                cmd.Parameters.AddWithValue("@SupplierName", string.IsNullOrWhiteSpace(product.SupplierName) ? DBNull.Value : product.SupplierName);
                cmd.Parameters.AddWithValue("@ImagePath", string.IsNullOrWhiteSpace(product.ImagePath) ? DBNull.Value : product.ImagePath);

                conn.Open();
                bool ok = cmd.ExecuteNonQuery() > 0;
                if (ok)
                {
                    ActivityLogRepository.LogSafe("Product Updated", $"{product.SKU} - {product.ProductName}",
                        $"Price: ₱{product.UnitPrice:N2}, Cost: ₱{product.PurchasePrice:N2}, Min Stock: {product.ReorderLevel}.");
                }
                return ok;
            }
        }

        public bool DeleteProduct(int productId)
        {
            Product? existing = GetProductById(productId);
            string sql = "DELETE FROM dbo.Products WHERE ProductID = @ProductID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ProductID", productId);
                conn.Open();
                bool deleted = cmd.ExecuteNonQuery() > 0;
                if (deleted)
                {
                    string label = existing != null ? $"{existing.SKU} - {existing.ProductName}" : $"Product #{productId}";
                    ActivityLogRepository.LogSafe("Product Deleted", label, $"Deleted product record #{productId}.");
                }
                return deleted;
            }
        }

        private static Product MapProduct(SqlDataReader reader)
        {
            int catId = reader.GetInt32(4);
            string catName = reader.GetString(5);
            int? supId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            string supName = reader.GetString(7);

            var product = new Product
            {
                ProductID = reader.GetInt32(0),
                SKU = reader.GetString(1),
                ProductName = reader.GetString(2),
                Description = reader.GetString(3),
                CategoryID = catId,
                CategoryName = catName,
                Category = new Category(catId, catName),
                SupplierID = supId,
                SupplierName = supName,
                PurchasePrice = reader.GetDecimal(8),
                UnitPrice = reader.GetDecimal(9),
                QuantityInStock = reader.GetInt32(10),
                ReorderLevel = reader.GetInt32(11),
                ImagePath = reader.GetString(12),
                IsActive = reader.GetBoolean(13),
                CreatedAt = reader.GetDateTime(14)
            };

            if (supId.HasValue)
            {
                product.Supplier = new Supplier(supId.Value, supName);
            }

            return product;
        }
    }
}
