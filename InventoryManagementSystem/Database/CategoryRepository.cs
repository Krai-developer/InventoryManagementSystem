using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Database
{
    public class CategoryRepository
    {
        public List<Category> GetAllCategories(string searchKeyword = "")
        {
            var categories = new List<Category>();

            string sql = @"
                SELECT 
                    c.CategoryID,
                    c.CategoryName,
                    ISNULL(c.Description, N'') AS Description,
                    c.CreatedAt,
                    COUNT(p.ProductID) AS ProductCount
                FROM dbo.Categories c
                LEFT JOIN dbo.Products p ON c.CategoryID = p.CategoryID AND p.IsActive = 1
                WHERE (@Keyword = N'' OR c.CategoryName LIKE @SearchPattern OR c.Description LIKE @SearchPattern)
                GROUP BY c.CategoryID, c.CategoryName, c.Description, c.CreatedAt
                ORDER BY c.CategoryName ASC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Keyword", trimmed);
                cmd.Parameters.AddWithValue("@SearchPattern", $"%{trimmed}%");

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new Category
                        {
                            CategoryID = reader.GetInt32(0),
                            CategoryName = reader.GetString(1),
                            Description = reader.GetString(2),
                            CreatedAt = reader.GetDateTime(3),
                            ProductCount = reader.GetInt32(4)
                        });
                    }
                }
            }

            return categories;
        }

        public bool CategoryNameExists(string categoryName, int excludeCategoryId = 0)
        {
            string sql = "SELECT COUNT(*) FROM dbo.Categories WHERE UPPER(CategoryName) = @Name AND CategoryID <> @ExcludeID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Name", (categoryName ?? string.Empty).Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@ExcludeID", excludeCategoryId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public int AddCategory(Category category)
        {
            if (!category.Validate(out string err))
                throw new ArgumentException(err);

            if (CategoryNameExists(category.CategoryName))
                throw new InvalidOperationException($"A category named '{category.CategoryName}' already exists.");

            string sql = @"
                INSERT INTO dbo.Categories (CategoryName, Description, CreatedAt)
                VALUES (@CategoryName, @Description, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);
                cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(category.Description)
                    ? DBNull.Value
                    : category.Description);

                conn.Open();
                object? result = cmd.ExecuteScalar();
                int newId = result != null ? Convert.ToInt32(result) : 0;
                category.CategoryID = newId;

                ActivityLogRepository.LogSafe("Category Added", category.CategoryName, $"Added category #{newId}.");
                return newId;
            }
        }

        public bool UpdateCategory(Category category)
        {
            if (!category.Validate(out string err))
                throw new ArgumentException(err);

            if (CategoryNameExists(category.CategoryName, category.CategoryID))
                throw new InvalidOperationException($"Another category named '{category.CategoryName}' already exists.");

            string sql = @"
                UPDATE dbo.Categories
                SET CategoryName = @CategoryName,
                    Description  = @Description
                WHERE CategoryID = @CategoryID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@CategoryID", category.CategoryID);
                cmd.Parameters.AddWithValue("@CategoryName", category.CategoryName);
                cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(category.Description)
                    ? DBNull.Value
                    : category.Description);

                conn.Open();
                bool updated = cmd.ExecuteNonQuery() > 0;
                if (updated)
                {
                    ActivityLogRepository.LogSafe("Category Updated", category.CategoryName, $"Updated category #{category.CategoryID}.");
                }
                return updated;
            }
        }

        public int GetAssignedProductCount(int categoryId)
        {
            string sql = "SELECT COUNT(*) FROM dbo.Products WHERE CategoryID = @CategoryID AND IsActive = 1;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public bool DeleteCategory(int categoryId, out string errorMessage)
        {
            errorMessage = string.Empty;

            int assignedProducts = GetAssignedProductCount(categoryId);
            if (assignedProducts > 0)
            {
                errorMessage = $"Cannot delete this category because {assignedProducts} product(s) are currently assigned to it. Please reassign or remove those products first.";
                return false;
            }

            try
            {
                using (SqlConnection conn = DbConnection.GetConnection())
                {
                    conn.Open();

                    string catName = $"Category #{categoryId}";
                    using (SqlCommand nameCmd = new SqlCommand("SELECT CategoryName FROM dbo.Categories WHERE CategoryID = @ID;", conn))
                    {
                        nameCmd.Parameters.AddWithValue("@ID", categoryId);
                        object? n = nameCmd.ExecuteScalar();
                        if (n != null) catName = n.ToString() ?? catName;
                    }

                    using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.Categories WHERE CategoryID = @CategoryID;", conn))
                    {
                        cmd.Parameters.AddWithValue("@CategoryID", categoryId);
                        bool deleted = cmd.ExecuteNonQuery() > 0;
                        if (deleted)
                        {
                            ActivityLogRepository.LogSafe("Category Deleted", catName, $"Deleted category #{categoryId}.");
                        }
                        return deleted;
                    }
                }
            }
            catch (SqlException ex)
            {
                errorMessage = $"Database constraint prevented deletion: {ex.Message}";
                return false;
            }
        }
    }
}
