using System;

namespace InventoryManagementSystem.Models
{
    public class Product
    {
        private int _productId;
        private string _sku = string.Empty;
        private string _productName = string.Empty;
        private string _description = string.Empty;
        private int _categoryId;
        private string _categoryName = string.Empty;
        private int? _supplierId;
        private string _supplierName = string.Empty;
        private decimal _purchasePrice;
        private decimal _unitPrice;
        private int _quantityInStock;
        private int _reorderLevel = 10;
        private Category? _category;
        private Supplier? _supplier;

        public int ProductID
        {
            get => _productId;
            set => _productId = value >= 0 ? value : 0;
        }

        public string SKU
        {
            get => _sku;
            set => _sku = (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        public string ProductName
        {
            get => _productName;
            set => _productName = (value ?? string.Empty).Trim();
        }

        public string Description
        {
            get => _description;
            set => _description = (value ?? string.Empty).Trim();
        }

        public int CategoryID
        {
            get => _categoryId;
            set
            {
                _categoryId = value >= 0 ? value : 0;
                if (_category != null && _category.CategoryID != _categoryId)
                    _category.CategoryID = _categoryId;
            }
        }

        public string CategoryName
        {
            get => _category != null && !string.IsNullOrEmpty(_category.CategoryName)
                ? _category.CategoryName
                : _categoryName;
            set
            {
                _categoryName = (value ?? string.Empty).Trim();
                if (_category != null)
                    _category.CategoryName = _categoryName;
            }
        }

        public Category? Category
        {
            get => _category;
            set
            {
                _category = value;
                if (value != null)
                {
                    _categoryId = value.CategoryID;
                    _categoryName = value.CategoryName;
                }
            }
        }

        public int? SupplierID
        {
            get => _supplierId;
            set => _supplierId = (value.HasValue && value.Value > 0) ? value.Value : null;
        }

        public string SupplierName
        {
            get => _supplier != null && !string.IsNullOrEmpty(_supplier.SupplierName)
                ? _supplier.SupplierName
                : _supplierName;
            set
            {
                _supplierName = (value ?? string.Empty).Trim();
                if (_supplier != null)
                    _supplier.SupplierName = _supplierName;
            }
        }

        public Supplier? Supplier
        {
            get => _supplier;
            set
            {
                _supplier = value;
                if (value != null)
                {
                    _supplierId = value.SupplierID > 0 ? value.SupplierID : null;
                    _supplierName = value.SupplierName;
                }
            }
        }

        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(PurchasePrice), "Purchase price cannot be negative.");
                _purchasePrice = value;
            }
        }

        /// <summary>
        /// Selling Price per unit.
        /// </summary>
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(UnitPrice), "Selling price cannot be negative.");
                _unitPrice = value;
            }
        }

        public int QuantityInStock
        {
            get => _quantityInStock;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(QuantityInStock), "Quantity in stock cannot be negative.");
                _quantityInStock = value;
            }
        }

        public int ReorderLevel
        {
            get => _reorderLevel;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(ReorderLevel), "Minimum stock level cannot be negative.");
                _reorderLevel = value;
            }
        }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string ImagePath { get; set; } = string.Empty;

        public Product()
        {
        }

        public Product(
            string sku,
            string productName,
            int categoryId,
            decimal unitPrice,
            int quantityInStock,
            int reorderLevel = 10,
            string supplierName = "",
            decimal purchasePrice = 0m,
            string description = "",
            int? supplierId = null)
        {
            SKU = sku;
            ProductName = productName;
            CategoryID = categoryId;
            UnitPrice = unitPrice;
            PurchasePrice = purchasePrice;
            QuantityInStock = quantityInStock;
            ReorderLevel = reorderLevel;
            SupplierName = supplierName;
            SupplierID = supplierId;
            Description = description;
            IsActive = true;
            CreatedAt = DateTime.Now;
        }

        public decimal TotalStockValue => UnitPrice * QuantityInStock;
        public decimal TotalCostValue => PurchasePrice * QuantityInStock;
        public bool IsLowStock => QuantityInStock > 0 && QuantityInStock <= ReorderLevel;
        public bool IsOutOfStock => QuantityInStock <= 0;

        public string StockStatus
        {
            get
            {
                if (IsOutOfStock)
                    return "Out of Stock";
                if (IsLowStock)
                    return "Low Stock";
                return "In Stock";
            }
        }

        public string DisplayLabel => $"{SKU} - {ProductName} (Stock: {QuantityInStock})";

        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(SKU))
            {
                errorMessage = "Product ID / SKU cannot be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ProductName))
            {
                errorMessage = "Product Name cannot be empty.";
                return false;
            }

            if (CategoryID <= 0)
            {
                errorMessage = "Please select a valid category.";
                return false;
            }

            if (PurchasePrice < 0)
            {
                errorMessage = "Purchase Price cannot be negative.";
                return false;
            }

            if (UnitPrice < 0)
            {
                errorMessage = "Selling Price cannot be negative.";
                return false;
            }

            if (QuantityInStock < 0)
            {
                errorMessage = "Quantity cannot be negative.";
                return false;
            }

            if (ReorderLevel < 0)
            {
                errorMessage = "Minimum Stock Level cannot be negative.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public bool TryAdjustStock(string transactionType, int quantity, out string errorMessage)
        {
            if (quantity <= 0)
            {
                errorMessage = "Quantity must be greater than zero.";
                return false;
            }

            string normalized = (transactionType ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized == "IN")
            {
                QuantityInStock += quantity;
                errorMessage = string.Empty;
                return true;
            }

            if (normalized == "OUT")
            {
                if (quantity > QuantityInStock)
                {
                    errorMessage = $"Insufficient stock for '{ProductName}'. Available: {QuantityInStock}, Requested: {quantity}.";
                    return false;
                }

                QuantityInStock -= quantity;
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "Transaction type must be 'IN' or 'OUT'.";
            return false;
        }

        public bool MatchesSearch(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return true;

            string term = keyword.Trim();
            return ProductID.ToString().Equals(term, StringComparison.OrdinalIgnoreCase)
                || SKU.Contains(term, StringComparison.OrdinalIgnoreCase)
                || ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase);
        }
    }
}
