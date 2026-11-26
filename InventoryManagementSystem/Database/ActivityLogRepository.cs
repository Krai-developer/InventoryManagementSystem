using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem.Database
{
    public class ActivityLogRepository
    {
        public void LogActivity(string action, string relatedRecord, string details)
        {
            string sql = @"
                INSERT INTO dbo.ActivityLogs (UserID, Username, Action, RelatedRecord, Details, PerformedBy, ActivityDate)
                VALUES (@UserID, @Username, @Action, @RelatedRecord, @Details, @PerformedBy, GETDATE());";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                int? currentUid = SessionManager.CurrentUser?.UserID;
                string currentUsername = SessionManager.CurrentUser?.Username ?? "System";

                cmd.Parameters.AddWithValue("@UserID", currentUid.HasValue ? currentUid.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@Username", currentUsername);
                cmd.Parameters.AddWithValue("@Action", (action ?? string.Empty).Trim());
                cmd.Parameters.AddWithValue("@RelatedRecord", (relatedRecord ?? string.Empty).Trim());
                cmd.Parameters.AddWithValue("@Details", string.IsNullOrWhiteSpace(details) ? DBNull.Value : details.Trim());
                cmd.Parameters.AddWithValue("@PerformedBy", SessionManager.CurrentActorLabel);

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
            // Permission Matrix: Admin views ALL activities; User views OWN activities only.
            SessionManager.RequireAuthenticated("view activity history");

            bool isAdmin = SessionManager.IsAdmin;
            int currentUid = SessionManager.CurrentUser!.UserID;
            string currentUsername = SessionManager.CurrentUser!.Username;

            var logs = new List<ActivityLog>();

            string sql = $@"
                SELECT TOP ({maxRows})
                    ActivityID,
                    UserID,
                    ISNULL(Username, N'') AS Username,
                    Action,
                    RelatedRecord,
                    ISNULL(Details, N'') AS Details,
                    ISNULL(PerformedBy, N'System') AS PerformedBy,
                    ActivityDate
                FROM dbo.ActivityLogs
                WHERE (@IsAdmin = 1 OR UserID = @CurrentUserID OR Username = @CurrentUsername)
                  AND (@Action = N'ALL' OR Action = @Action)
                  AND (@FromDate IS NULL OR ActivityDate >= @FromDate)
                  AND (@ToDate IS NULL OR ActivityDate < @ToDate)
                  AND (@Keyword = N''
                       OR Action LIKE @Pattern
                       OR RelatedRecord LIKE @Pattern
                       OR Details LIKE @Pattern
                       OR Username LIKE @Pattern
                       OR PerformedBy LIKE @Pattern)
                ORDER BY ActivityDate DESC, ActivityID DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@IsAdmin", isAdmin ? 1 : 0);
                cmd.Parameters.AddWithValue("@CurrentUserID", currentUid);
                cmd.Parameters.AddWithValue("@CurrentUsername", currentUsername);
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
                            UserID = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                            Username = reader.GetString(2),
                            Action = reader.GetString(3),
                            RelatedRecord = reader.GetString(4),
                            Details = reader.GetString(5),
                            PerformedBy = reader.GetString(6),
                            ActivityDate = reader.GetDateTime(7)
                        });
                    }
                }
            }

            return logs;
        }
    }
}
