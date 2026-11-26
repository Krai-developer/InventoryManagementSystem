using System;

namespace InventoryManagementSystem.Models
{
    public class StockTransaction
    {
        private int _quantity;
        private string _transactionType = "IN";

        public int TransactionID { get; set; }
        public int ProductID { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        public string TransactionType
        {
            get => _transactionType;
            set
            {
                string upper = (value ?? "IN").Trim().ToUpperInvariant();
                _transactionType = (upper == "OUT") ? "OUT" : "IN";
            }
        }

        public int Quantity
        {
            get => _quantity;
            set => _quantity = value >= 0 ? value : 0;
        }

        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public StockTransaction()
        {
        }

        public StockTransaction(
            int productId,
            string transactionType,
            int quantity,
            int previousStock,
            int newStock,
            string referenceNo = "",
            string remarks = "",
            int? supplierId = null)
        {
            ProductID = productId;
            TransactionType = transactionType;
            Quantity = quantity;
            PreviousStock = previousStock;
            NewStock = newStock;
            ReferenceNo = referenceNo;
            Remarks = remarks;
            SupplierID = supplierId;
            TransactionDate = DateTime.Now;
        }
    }
}
