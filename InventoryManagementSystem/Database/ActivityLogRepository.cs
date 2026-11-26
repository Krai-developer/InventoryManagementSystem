using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Database
{
    public class ActivityLogRepository
    {
        public void LogActivity(string action, string relatedRecord, string details)
        {
            string sql = @"
                INSERT INTO dbo.ActivityLogs (Action, RelatedRecord, Details, ActivityDate)
                VALUES (@Action, @RelatedRecord, @Details, GETDATE());";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Action", (action ?? string.Empty).Trim());
                cmd.Parameters.AddWithValue("@RelatedRecord", (relatedRecord ?? string.Empty).Trim());
                cmd.Parameters.AddWithValue("@Details", string.IsNullOrWhiteSpace(details) ? DBNull.Value : details.Trim());

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public static void LogSafe(string action, string relatedRecord, string details)
        {
            try
            {
                new ActivityLogRepository().LogActivity(action, relatedRecord, details);
            }
            catch
            {
                // Activity logging should not interrupt the primary operation if logging fails
            }
        }

        public List<ActivityLog> GetActivities(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string actionFilter = "ALL",
            string searchKeyword = "",
            int maxRows = 250)
        {
            var logs = new List<ActivityLog>();

            string sql = $@"
                SELECT TOP ({maxRows})
                    ActivityID,
                    Action,
                    RelatedRecord,
                    ISNULL(Details, N'') AS Details,
                    ActivityDate
                FROM dbo.ActivityLogs
                WHERE (@Action = N'ALL' OR Action = @Action)
                  AND (@FromDate IS NULL OR ActivityDate >= @FromDate)
                  AND (@ToDate IS NULL OR ActivityDate < @ToDate)
                  AND (@Keyword = N''
                       OR Action LIKE @Pattern
                       OR RelatedRecord LIKE @Pattern
                       OR Details LIKE @Pattern)
                ORDER BY ActivityDate DESC, ActivityID DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Action", string.IsNullOrWhiteSpace(actionFilter) ? "ALL" : actionFilter.Trim());
                cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? fromDate.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? toDate.Value.Date.AddDays(1) : DBNull.Value);
                cmd.Parameters.AddWithValue("@Keyword", trimmed);
                cmd.Parameters.AddWithValue("@Pattern", $"%{trimmed}%");

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new ActivityLog
                        {
                            ActivityID = reader.GetInt32(0),
                            Action = reader.GetString(1),
                            RelatedRecord = reader.GetString(2),
                            Details = reader.GetString(3),
                            ActivityDate = reader.GetDateTime(4)
                        });
                    }
                }
            }

            return logs;
        }
    }
}
