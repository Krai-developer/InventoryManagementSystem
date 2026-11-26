using System;

namespace InventoryManagementSystem.Models
{
    public class Category
    {
        private int _categoryId;
        private string _categoryName = string.Empty;
        private string _description = string.Empty;
        private int _productCount;

        public int CategoryID
        {
            get => _categoryId;
            set => _categoryId = value >= 0 ? value : 0;
        }

        public string CategoryName
        {
            get => _categoryName;
            set => _categoryName = (value ?? string.Empty).Trim();
        }

        public string Description
        {
            get => _description;
            set => _description = (value ?? string.Empty).Trim();
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int ProductCount
        {
            get => _productCount;
            set => _productCount = value >= 0 ? value : 0;
        }

        public Category()
        {
        }

        public Category(int categoryId, string categoryName, string description = "")
        {
            CategoryID = categoryId;
            CategoryName = categoryName;
            Description = description;
            CreatedAt = DateTime.Now;
        }

        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(CategoryName))
            {
                errorMessage = "Category Name cannot be empty.";
                return false;
            }

            if (CategoryName.Length > 100)
            {
                errorMessage = "Category Name cannot exceed 100 characters.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public override string ToString()
        {
            return CategoryName;
        }
    }
}
