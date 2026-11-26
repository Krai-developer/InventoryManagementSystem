using System;

namespace InventoryManagementSystem.Models
{
    public class ActivityLog
    {
        public int ActivityID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string RelatedRecord { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; } = DateTime.Now;

        public ActivityLog()
        {
        }

        public ActivityLog(string action, string relatedRecord, string details)
        {
            Action = (action ?? string.Empty).Trim();
            RelatedRecord = (relatedRecord ?? string.Empty).Trim();
            Details = (details ?? string.Empty).Trim();
            ActivityDate = DateTime.Now;
        }
    }
}
