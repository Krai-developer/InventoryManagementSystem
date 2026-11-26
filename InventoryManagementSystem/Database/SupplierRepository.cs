using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem.Database
{
    public class SupplierRepository
    {
        public List<Supplier> GetAllSuppliers(string searchKeyword = "")
        {
            var suppliers = new List<Supplier>();

            string sql = @"
                SELECT
                    s.SupplierID,
                    s.SupplierName,
                    ISNULL(s.ContactPerson, N'') AS ContactPerson,
                    ISNULL(s.PhoneNumber, N'') AS PhoneNumber,
                    ISNULL(s.EmailAddress, N'') AS EmailAddress,
                    ISNULL(s.BusinessAddress, N'') AS BusinessAddress,
                    s.CreatedAt,
                    COUNT(p.ProductID) AS ProductCount
                FROM dbo.Suppliers s
                LEFT JOIN dbo.Products p ON s.SupplierID = p.SupplierID AND p.IsActive = 1
                WHERE (@Keyword = N''
                       OR s.SupplierName LIKE @Pattern
                       OR s.ContactPerson LIKE @Pattern
                       OR s.PhoneNumber LIKE @Pattern
                       OR s.EmailAddress LIKE @Pattern
                       OR s.BusinessAddress LIKE @Pattern)
                GROUP BY s.SupplierID, s.SupplierName, s.ContactPerson, s.PhoneNumber, s.EmailAddress, s.BusinessAddress, s.CreatedAt
                ORDER BY s.SupplierName ASC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Keyword", trimmed);
                cmd.Parameters.AddWithValue("@Pattern", $"%{trimmed}%");

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        suppliers.Add(new Supplier
                        {
                            SupplierID = reader.GetInt32(0),
                            SupplierName = reader.GetString(1),
                            ContactPerson = reader.GetString(2),
                            PhoneNumber = reader.GetString(3),
                            EmailAddress = reader.GetString(4),
                            BusinessAddress = reader.GetString(5),
                            CreatedAt = reader.GetDateTime(6),
                            ProductCount = reader.GetInt32(7)
                        });
                    }
                }
            }

            return suppliers;
        }

        public bool SupplierNameExists(string supplierName, int excludeSupplierId = 0)
        {
            string sql = "SELECT COUNT(*) FROM dbo.Suppliers WHERE UPPER(SupplierName) = @Name AND SupplierID <> @ExcludeID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Name", (supplierName ?? string.Empty).Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@ExcludeID", excludeSupplierId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public int AddSupplier(Supplier supplier)
        {
            SessionManager.RequireAdmin("add suppliers");

            if (!supplier.Validate(out string err))
                throw new ArgumentException(err);

            if (SupplierNameExists(supplier.SupplierName))
                throw new InvalidOperationException($"A supplier named '{supplier.SupplierName}' already exists.");

            string sql = @"
                INSERT INTO dbo.Suppliers (SupplierName, ContactPerson, PhoneNumber, EmailAddress, BusinessAddress, CreatedAt)
                VALUES (@SupplierName, @ContactPerson, @PhoneNumber, @EmailAddress, @BusinessAddress, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@SupplierName", supplier.SupplierName);
                cmd.Parameters.AddWithValue("@ContactPerson", string.IsNullOrWhiteSpace(supplier.ContactPerson) ? DBNull.Value : supplier.ContactPerson);
                cmd.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(supplier.PhoneNumber) ? DBNull.Value : supplier.PhoneNumber);
                cmd.Parameters.AddWithValue("@EmailAddress", string.IsNullOrWhiteSpace(supplier.EmailAddress) ? DBNull.Value : supplier.EmailAddress);
                cmd.Parameters.AddWithValue("@BusinessAddress", string.IsNullOrWhiteSpace(supplier.BusinessAddress) ? DBNull.Value : supplier.BusinessAddress);

                conn.Open();
                int newId = Convert.ToInt32(cmd.ExecuteScalar());
                supplier.SupplierID = newId;

                ActivityLogRepository.LogSafe("Supplier Added", supplier.SupplierName,
                    $"Added supplier #{newId} (Contact: {supplier.ContactPerson}).");

                return newId;
            }
        }

        public bool UpdateSupplier(Supplier supplier)
        {
            SessionManager.RequireAdmin("update suppliers");

            if (!supplier.Validate(out string err))
                throw new ArgumentException(err);

            if (SupplierNameExists(supplier.SupplierName, supplier.SupplierID))
                throw new InvalidOperationException($"Another supplier named '{supplier.SupplierName}' already exists.");

            string sql = @"
                UPDATE dbo.Suppliers
                SET SupplierName    = @SupplierName,
                    ContactPerson   = @ContactPerson,
                    PhoneNumber     = @PhoneNumber,
                    EmailAddress    = @EmailAddress,
                    BusinessAddress = @BusinessAddress
                WHERE SupplierID    = @SupplierID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@SupplierID", supplier.SupplierID);
                cmd.Parameters.AddWithValue("@SupplierName", supplier.SupplierName);
                cmd.Parameters.AddWithValue("@ContactPerson", string.IsNullOrWhiteSpace(supplier.ContactPerson) ? DBNull.Value : supplier.ContactPerson);
                cmd.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(supplier.PhoneNumber) ? DBNull.Value : supplier.PhoneNumber);
                cmd.Parameters.AddWithValue("@EmailAddress", string.IsNullOrWhiteSpace(supplier.EmailAddress) ? DBNull.Value : supplier.EmailAddress);
                cmd.Parameters.AddWithValue("@BusinessAddress", string.IsNullOrWhiteSpace(supplier.BusinessAddress) ? DBNull.Value : supplier.BusinessAddress);

                conn.Open();
                bool ok = cmd.ExecuteNonQuery() > 0;
                if (ok)
                {
                    ActivityLogRepository.LogSafe("Supplier Updated", supplier.SupplierName,
                        $"Updated supplier #{supplier.SupplierID}.");
                }
                return ok;
            }
        }

        public bool DeleteSupplier(int supplierId, out string errorMessage)
        {
            SessionManager.RequireAdmin("delete suppliers");

            errorMessage = string.Empty;

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();

                int productRefs;
                using (SqlCommand chkProd = new SqlCommand("SELECT COUNT(*) FROM dbo.Products WHERE SupplierID = @ID;", conn))
                {
                    chkProd.Parameters.AddWithValue("@ID", supplierId);
                    productRefs = Convert.ToInt32(chkProd.ExecuteScalar());
                }

                if (productRefs > 0)
                {
                    errorMessage = $"Cannot delete this supplier because {productRefs} product(s) are currently linked to it. Reassign those products first.";
                    return false;
                }

                int txRefs;
                using (SqlCommand chkTx = new SqlCommand("SELECT COUNT(*) FROM dbo.StockTransactions WHERE SupplierID = @ID;", conn))
                {
                    chkTx.Parameters.AddWithValue("@ID", supplierId);
                    txRefs = Convert.ToInt32(chkTx.ExecuteScalar());
                }

                if (txRefs > 0)
                {
                    errorMessage = $"Cannot delete this supplier because {txRefs} stock transaction(s) reference it.";
                    return false;
                }

                string supplierName = $"Supplier #{supplierId}";
                using (SqlCommand nameCmd = new SqlCommand("SELECT SupplierName FROM dbo.Suppliers WHERE SupplierID = @ID;", conn))
                {
                    nameCmd.Parameters.AddWithValue("@ID", supplierId);
                    object? n = nameCmd.ExecuteScalar();
                    if (n != null) supplierName = n.ToString() ?? supplierName;
                }

                using (SqlCommand delCmd = new SqlCommand("DELETE FROM dbo.Suppliers WHERE SupplierID = @ID;", conn))
                {
                    delCmd.Parameters.AddWithValue("@ID", supplierId);
                    bool deleted = delCmd.ExecuteNonQuery() > 0;
                    if (deleted)
                    {
                        ActivityLogRepository.LogSafe("Supplier Deleted", supplierName, $"Deleted supplier #{supplierId}.");
                    }
                    return deleted;
                }
            }
        }
    }
}
