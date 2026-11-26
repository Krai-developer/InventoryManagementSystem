using System;

namespace InventoryManagementSystem.Models
{
    /// <summary>
    /// Represents a real audit event logged by the application (with UserID & Username attribution).
    /// </summary>
    public class ActivityLog
    {
        public int ActivityID { get; set; }
        public int? UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string RelatedRecord { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; } = DateTime.Now;
    }
}
