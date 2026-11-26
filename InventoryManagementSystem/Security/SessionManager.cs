using System;
using InventoryManagementSystem.Models;

namespace InventoryManagementSystem.Security
{
    /// <summary>
    /// Tracks the active authenticated user session and enforces role-based permissions
    /// across application logic and repositories.
    /// </summary>
    public static class SessionManager
    {
        public static UserAccount? CurrentUser { get; private set; }

        public static bool IsAuthenticated => CurrentUser != null && CurrentUser.IsActive;
        public static bool IsAdmin => IsAuthenticated && CurrentUser!.IsAdmin;
        public static bool IsStandardUser => IsAuthenticated && !CurrentUser!.IsAdmin;

        public static bool CanManageProducts => IsAdmin;
        public static bool CanDeleteProducts => IsAdmin;
        public static bool CanManageCategories => IsAdmin;
        public static bool CanManageSuppliers => IsAdmin;
        public static bool CanAdjustStock => IsAdmin;
        public static bool CanManageUsers => IsAdmin;
        public static bool CanViewActivityHistory => IsAdmin;
        public static bool CanViewValuationReport => IsAdmin;
        public static bool CanAccessBackupRestore => IsAdmin;
        public static bool CanManageSettings => IsAdmin;

        public static string CurrentActorLabel => CurrentUser != null
            ? $"{CurrentUser.Username} ({CurrentUser.Role})"
            : "System";

        public static event Action? SessionChanged;

        public static void StartSession(UserAccount user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            if (!user.IsActive)
                throw new UnauthorizedAccessException("Cannot start a session for a disabled account.");

            CurrentUser = user;
            SessionChanged?.Invoke();
        }

        public static void Logout()
        {
            CurrentUser = null;
            SessionChanged?.Invoke();
        }

        public static void RequireAuthenticated(string actionDescription)
        {
            if (!IsAuthenticated)
            {
                throw new UnauthorizedAccessException($"Authentication required to {actionDescription}. Please log in.");
            }
        }

        public static void RequireAdmin(string actionDescription)
        {
            RequireAuthenticated(actionDescription);
            if (!IsAdmin)
            {
                throw new UnauthorizedAccessException($"Access denied: Administrator privileges are required to {actionDescription}.");
            }
        }
    }
}
