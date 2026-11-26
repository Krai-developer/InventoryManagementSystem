using System;

namespace InventoryManagementSystem.Models
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }

    /// <summary>
    /// Represents a registered application account (Admin or User).
    /// Plain-text passwords are never stored in this model.
    /// </summary>
    public class UserAccount
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string Role { get; set; } = UserRoles.User;
        public bool IsActive { get; set; } = true;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }

        public bool IsAdmin => string.Equals(Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase);

        public string AccountStatus
        {
            get
            {
                if (!IsActive) return "Disabled";
                if (LockoutUntil.HasValue && LockoutUntil.Value > DateTime.Now) return "Locked";
                return "Active";
            }
        }
    }
}
