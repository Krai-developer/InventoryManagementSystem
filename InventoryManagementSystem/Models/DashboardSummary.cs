namespace InventoryManagementSystem.Models
{
    public class DashboardSummary
    {
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUnitsInStock { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int TodayTransactionsCount { get; set; }
    }
}
