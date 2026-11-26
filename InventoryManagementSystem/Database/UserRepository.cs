using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;
using Microsoft.Data.SqlClient;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem.Database
{
    /// <summary>
    /// Handles all account persistence, authentication, registration, first-run Admin setup,
    /// single-admin concurrency enforcement, lockout protection, and Admin user management.
    /// </summary>
    public class UserRepository
    {
        public const int MaxFailedLoginAttempts = 5;
        public const int LockoutMinutes = 5;
        public const int MinPasswordLength = 6;

        public bool HasAdminAccount()
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Users WHERE Role = N'Admin';";
            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public bool UsernameExists(string username, int excludeUserId = 0)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Users WHERE LOWER(Username) = @Username AND UserID <> @ExcludeID;";
            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Username", (username ?? string.Empty).Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@ExcludeID", excludeUserId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public bool EmailExists(string email, int excludeUserId = 0)
        {
            const string sql = "SELECT COUNT(*) FROM dbo.Users WHERE LOWER(EmailAddress) = @Email AND UserID <> @ExcludeID;";
            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Email", (email ?? string.Empty).Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@ExcludeID", excludeUserId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Creates the single initial Administrator account during first-run setup.
        /// Protected by BOTH a SERIALIZABLE transaction lock AND SQL Server's UX_Users_SingleAdmin
        /// filtered unique index so even simultaneous creation attempts can only ever create ONE Admin.
        /// </summary>
        public bool CreateInitialAdmin(
            string fullName,
            string username,
            string email,
            string password,
            string confirmPassword,
            out string message)
        {
            if (!ValidateAccountInputs(fullName, username, email, password, confirmPassword, out message))
                return false;

            string cleanName = fullName.Trim();
            string cleanUser = username.Trim();
            string cleanEmail = email.Trim().ToLowerInvariant();
            var (hash, salt) = PasswordHasher.HashPassword(password);

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        using (SqlCommand chkAdmin = new SqlCommand(
                            "SELECT COUNT(*) FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Role = N'Admin';", conn, tx))
                        {
                            if (Convert.ToInt32(chkAdmin.ExecuteScalar()) > 0)
                            {
                                tx.Rollback();
                                message = "An Administrator account already exists. Only one Admin account is permitted.";
                                return false;
                            }
                        }

                        using (SqlCommand chkDup = new SqlCommand(
                            "SELECT COUNT(*) FROM dbo.Users WHERE LOWER(Username) = @U OR LOWER(EmailAddress) = @E;", conn, tx))
                        {
                            chkDup.Parameters.AddWithValue("@U", cleanUser.ToLowerInvariant());
                            chkDup.Parameters.AddWithValue("@E", cleanEmail);
                            if (Convert.ToInt32(chkDup.ExecuteScalar()) > 0)
                            {
                                tx.Rollback();
                                message = "That username or email address is already registered.";
                                return false;
                            }
                        }

                        const string insertSql = @"
                            INSERT INTO dbo.Users
                                (FullName, Username, EmailAddress, PasswordHash, PasswordSalt, Role, IsActive, FailedLoginAttempts, CreatedAt)
                            VALUES
                                (@FullName, @Username, @EmailAddress, @PasswordHash, @PasswordSalt, N'Admin', 1, 0, GETDATE());";

                        using (SqlCommand cmd = new SqlCommand(insertSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@FullName", cleanName);
                            cmd.Parameters.AddWithValue("@Username", cleanUser);
                            cmd.Parameters.AddWithValue("@EmailAddress", cleanEmail);
                            cmd.Parameters.AddWithValue("@PasswordHash", hash);
                            cmd.Parameters.AddWithValue("@PasswordSalt", salt);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        ActivityLogRepository.LogSafe("Admin Setup", cleanUser, "Initial Administrator account created.");
                        message = "Administrator account created successfully. You can now log in.";
                        return true;
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                    {
                        try { tx.Rollback(); } catch { }
                        message = "An Administrator account already exists or the username/email is already in use.";
                        return false;
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        message = $"Unable to create Administrator account: {ex.Message}";
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Public self-registration for standard users.
        /// Always assigns Role = 'User'; never accepts or honors any Admin role request.
        /// </summary>
        public bool RegisterUser(
            string fullName,
            string username,
            string email,
            string password,
            string confirmPassword,
            out string message)
        {
            if (!ValidateAccountInputs(fullName, username, email, password, confirmPassword, out message))
                return false;

            string cleanName = fullName.Trim();
            string cleanUser = username.Trim();
            string cleanEmail = email.Trim().ToLowerInvariant();

            if (UsernameExists(cleanUser))
            {
                message = $"Username '{cleanUser}' is already taken. Please choose another username.";
                return false;
            }

            if (EmailExists(cleanEmail))
            {
                message = $"Email address '{cleanEmail}' is already registered.";
                return false;
            }

            var (hash, salt) = PasswordHasher.HashPassword(password);

            const string sql = @"
                INSERT INTO dbo.Users
                    (FullName, Username, EmailAddress, PasswordHash, PasswordSalt, Role, IsActive, FailedLoginAttempts, CreatedAt)
                VALUES
                    (@FullName, @Username, @EmailAddress, @PasswordHash, @PasswordSalt, N'User', 1, 0, GETDATE());";

            try
            {
                using (SqlConnection conn = DbConnection.GetConnection())
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@FullName", cleanName);
                    cmd.Parameters.AddWithValue("@Username", cleanUser);
                    cmd.Parameters.AddWithValue("@EmailAddress", cleanEmail);
                    cmd.Parameters.AddWithValue("@PasswordHash", hash);
                    cmd.Parameters.AddWithValue("@PasswordSalt", salt);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                ActivityLogRepository.LogSafe("User Registered", cleanUser, $"New standard User account registered ({cleanEmail}).");
                message = "Account created successfully! You can now log in with your credentials.";
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                message = "That username or email address is already registered.";
                return false;
            }
            catch (Exception ex)
            {
                message = $"Registration failed: {ex.Message}";
                return false;
            }
        }

        public bool Authenticate(
            string usernameOrEmail,
            string password,
            out UserAccount? authenticatedUser,
            out string errorMessage)
        {
            authenticatedUser = null;
            errorMessage = string.Empty;

            string identifier = (usernameOrEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password))
            {
                errorMessage = "Please enter both your username/email and password.";
                return false;
            }

            const string selectSql = @"
                SELECT UserID, FullName, Username, EmailAddress, PasswordHash, PasswordSalt,
                       Role, IsActive, FailedLoginAttempts, LockoutUntil, CreatedAt, LastLoginAt
                FROM dbo.Users
                WHERE LOWER(Username) = @Identifier OR LOWER(EmailAddress) = @Identifier;";

            int userId = 0;
            string storedHash = string.Empty;
            string storedSalt = string.Empty;
            UserAccount? candidate = null;

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(selectSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Identifier", identifier.ToLowerInvariant());
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            errorMessage = "Invalid username/email or password.";
                            return false;
                        }

                        userId = r.GetInt32(0);
                        storedHash = r.GetString(4);
                        storedSalt = r.GetString(5);
                        candidate = new UserAccount
                        {
                            UserID = userId,
                            FullName = r.GetString(1),
                            Username = r.GetString(2),
                            EmailAddress = r.GetString(3),
                            Role = r.GetString(6),
                            IsActive = r.GetBoolean(7),
                            FailedLoginAttempts = r.GetInt32(8),
                            LockoutUntil = r.IsDBNull(9) ? null : r.GetDateTime(9),
                            CreatedAt = r.GetDateTime(10),
                            LastLoginAt = r.IsDBNull(11) ? null : r.GetDateTime(11)
                        };
                    }
                }

                if (!candidate.IsActive)
                {
                    errorMessage = "This account has been disabled by the Administrator.";
                    return false;
                }

                if (candidate.LockoutUntil.HasValue && candidate.LockoutUntil.Value > DateTime.Now)
                {
                    int remainingMins = Math.Max(1, (int)Math.Ceiling((candidate.LockoutUntil.Value - DateTime.Now).TotalMinutes));
                    errorMessage = $"Account is temporarily locked due to repeated failed login attempts. Try again in {remainingMins} minute(s).";
                    return false;
                }

                bool passwordValid = PasswordHasher.VerifyPassword(password, storedHash, storedSalt);
                if (!passwordValid)
                {
                    int nextAttempts = candidate.FailedLoginAttempts + 1;
                    DateTime? lockUntil = nextAttempts >= MaxFailedLoginAttempts
                        ? DateTime.Now.AddMinutes(LockoutMinutes)
                        : null;

                    using (SqlCommand failCmd = new SqlCommand(@"
                        UPDATE dbo.Users
                        SET FailedLoginAttempts = @Attempts,
                            LockoutUntil = @LockoutUntil
                        WHERE UserID = @UserID;", conn))
                    {
                        failCmd.Parameters.AddWithValue("@Attempts", nextAttempts);
                        failCmd.Parameters.AddWithValue("@LockoutUntil", lockUntil.HasValue ? lockUntil.Value : DBNull.Value);
                        failCmd.Parameters.AddWithValue("@UserID", userId);
                        failCmd.ExecuteNonQuery();
                    }

                    if (lockUntil.HasValue)
                    {
                        errorMessage = $"Too many failed login attempts. This account is locked for {LockoutMinutes} minutes.";
                    }
                    else
                    {
                        int left = MaxFailedLoginAttempts - nextAttempts;
                        errorMessage = $"Invalid username/email or password. ({left} attempt(s) remaining before temporary lockout.)";
                    }
                    return false;
                }

                DateTime now = DateTime.Now;
                using (SqlCommand okCmd = new SqlCommand(@"
                    UPDATE dbo.Users
                    SET FailedLoginAttempts = 0,
                        LockoutUntil = NULL,
                        LastLoginAt = @Now
                    WHERE UserID = @UserID;", conn))
                {
                    okCmd.Parameters.AddWithValue("@Now", now);
                    okCmd.Parameters.AddWithValue("@UserID", userId);
                    okCmd.ExecuteNonQuery();
                }

                candidate.FailedLoginAttempts = 0;
                candidate.LockoutUntil = null;
                candidate.LastLoginAt = now;
                authenticatedUser = candidate;

                ActivityLogRepository.LogSafe("User Login", candidate.Username, $"Signed in as {candidate.Role}.");
                return true;
            }
        }

        public List<UserAccount> GetAllUsers(string searchKeyword = "")
        {
            SessionManager.RequireAdmin("view registered user accounts");

            var users = new List<UserAccount>();
            const string sql = @"
                SELECT UserID, FullName, Username, EmailAddress, Role, IsActive,
                       FailedLoginAttempts, LockoutUntil, CreatedAt, LastLoginAt
                FROM dbo.Users
                WHERE (@Keyword = N''
                       OR FullName LIKE @Pattern
                       OR Username LIKE @Pattern
                       OR EmailAddress LIKE @Pattern
                       OR Role LIKE @Pattern)
                ORDER BY CASE WHEN Role = N'Admin' THEN 0 ELSE 1 END, CreatedAt DESC;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                string trimmed = (searchKeyword ?? string.Empty).Trim();
                cmd.Parameters.AddWithValue("@Keyword", trimmed);
                cmd.Parameters.AddWithValue("@Pattern", $"%{trimmed}%");

                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        users.Add(MapUser(r));
                    }
                }
            }

            return users;
        }

        public UserAccount? GetUserById(int userId)
        {
            SessionManager.RequireAuthenticated("view account details");
            if (!SessionManager.IsAdmin && SessionManager.CurrentUser!.UserID != userId)
            {
                throw new UnauthorizedAccessException("Access denied: You can only view your own account details.");
            }

            const string sql = @"
                SELECT UserID, FullName, Username, EmailAddress, Role, IsActive,
                       FailedLoginAttempts, LockoutUntil, CreatedAt, LastLoginAt
                FROM dbo.Users
                WHERE UserID = @UserID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    return r.Read() ? MapUser(r) : null;
                }
            }
        }

        public bool SetUserActiveStatus(int userId, bool isActive, out string message)
        {
            SessionManager.RequireAdmin("activate or deactivate user accounts");

            UserAccount? target = GetUserById(userId);
            if (target == null)
            {
                message = "User account was not found.";
                return false;
            }

            if (target.IsAdmin && !isActive)
            {
                message = "The sole Administrator account cannot be deactivated.";
                return false;
            }

            const string sql = @"
                UPDATE dbo.Users
                SET IsActive = @IsActive,
                    FailedLoginAttempts = CASE WHEN @IsActive = 1 THEN 0 ELSE FailedLoginAttempts END,
                    LockoutUntil = CASE WHEN @IsActive = 1 THEN NULL ELSE LockoutUntil END
                WHERE UserID = @UserID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@IsActive", isActive);
                cmd.Parameters.AddWithValue("@UserID", userId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            string statusText = isActive ? "Activated" : "Deactivated";
            ActivityLogRepository.LogSafe("Account Status Changed", target.Username, $"Admin {statusText.ToLowerInvariant()} user account.");
            message = $"Account '{target.Username}' has been {statusText.ToLowerInvariant()}.";
            return true;
        }

        public bool ResetUserPassword(int userId, string newPassword, string confirmPassword, out string message)
        {
            SessionManager.RequireAdmin("reset user passwords");

            if (!ValidatePassword(newPassword, confirmPassword, out message))
                return false;

            UserAccount? target = GetUserById(userId);
            if (target == null)
            {
                message = "User account was not found.";
                return false;
            }

            var (hash, salt) = PasswordHasher.HashPassword(newPassword);

            const string sql = @"
                UPDATE dbo.Users
                SET PasswordHash = @Hash,
                    PasswordSalt = @Salt,
                    FailedLoginAttempts = 0,
                    LockoutUntil = NULL
                WHERE UserID = @UserID;";

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Hash", hash);
                cmd.Parameters.AddWithValue("@Salt", salt);
                cmd.Parameters.AddWithValue("@UserID", userId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            ActivityLogRepository.LogSafe("Password Reset", target.Username, "Administrator reset account password.");
            message = $"Password for '{target.Username}' has been reset securely.";
            return true;
        }

        public bool ChangeOwnPassword(string currentPassword, string newPassword, string confirmPassword, out string message)
        {
            SessionManager.RequireAuthenticated("change password");

            if (string.IsNullOrEmpty(currentPassword))
            {
                message = "Please enter your current password.";
                return false;
            }

            if (!ValidatePassword(newPassword, confirmPassword, out message))
                return false;

            int userId = SessionManager.CurrentUser!.UserID;

            using (SqlConnection conn = DbConnection.GetConnection())
            {
                conn.Open();
                string storedHash;
                string storedSalt;

                using (SqlCommand sel = new SqlCommand("SELECT PasswordHash, PasswordSalt FROM dbo.Users WHERE UserID = @ID;", conn))
                {
                    sel.Parameters.AddWithValue("@ID", userId);
                    using (SqlDataReader r = sel.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            message = "Current user account could not be found.";
                            return false;
                        }
                        storedHash = r.GetString(0);
                        storedSalt = r.GetString(1);
                    }
                }

                if (!PasswordHasher.VerifyPassword(currentPassword, storedHash, storedSalt))
                {
                    message = "Current password is incorrect.";
                    return false;
                }

                var (hash, salt) = PasswordHasher.HashPassword(newPassword);
                using (SqlCommand upd = new SqlCommand("UPDATE dbo.Users SET PasswordHash = @Hash, PasswordSalt = @Salt WHERE UserID = @ID;", conn))
                {
                    upd.Parameters.AddWithValue("@Hash", hash);
                    upd.Parameters.AddWithValue("@Salt", salt);
                    upd.Parameters.AddWithValue("@ID", userId);
                    upd.ExecuteNonQuery();
                }
            }

            ActivityLogRepository.LogSafe("Password Changed", SessionManager.CurrentUser.Username, "User updated their own password.");
            message = "Your password has been updated successfully.";
            return true;
        }

        /// <summary>
        /// Explicitly blocks role escalation and demotion of the single Admin account.
        /// </summary>
        public bool ChangeUserRole(int userId, string requestedRole, out string message)
        {
            SessionManager.RequireAdmin("modify account roles");

            if (string.Equals(requestedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                message = "Role change denied: Only one Admin account is permitted in the system.";
                return false;
            }

            UserAccount? target = GetUserById(userId);
            if (target == null)
            {
                message = "User account was not found.";
                return false;
            }

            if (target.IsAdmin)
            {
                message = "Role change denied: The sole Administrator account cannot be demoted.";
                return false;
            }

            message = "Account is already assigned the standard User role.";
            return false;
        }

        public bool DeleteUser(int userId, out string message)
        {
            SessionManager.RequireAdmin("delete user accounts");

            UserAccount? target = GetUserById(userId);
            if (target == null)
            {
                message = "User account was not found.";
                return false;
            }

            if (target.IsAdmin)
            {
                message = "The sole Administrator account cannot be deleted.";
                return false;
            }

            using (SqlConnection conn = DbConnection.GetConnection())
            using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.Users WHERE UserID = @ID AND Role <> N'Admin';", conn))
            {
                cmd.Parameters.AddWithValue("@ID", userId);
                conn.Open();
                int affected = cmd.ExecuteNonQuery();
                if (affected == 0)
                {
                    message = "Unable to delete this account.";
                    return false;
                }
            }

            ActivityLogRepository.LogSafe("User Deleted", target.Username, $"Administrator removed user account #{target.UserID}.");
            message = $"User account '{target.Username}' has been deleted.";
            return true;
        }

        private static bool ValidateAccountInputs(
            string fullName,
            string username,
            string email,
            string password,
            string confirmPassword,
            out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            {
                error = "Full Name is required (at least 2 characters).";
                return false;
            }

            string cleanUser = (username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanUser) || cleanUser.Length < 3 || cleanUser.Contains(' '))
            {
                error = "Username is required (at least 3 characters, no spaces).";
                return false;
            }

            string cleanEmail = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanEmail) || !IsValidEmail(cleanEmail))
            {
                error = "Please enter a valid Email Address.";
                return false;
            }

            return ValidatePassword(password, confirmPassword, out error);
        }

        private static bool ValidatePassword(string password, string confirmPassword, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                error = $"Password must be at least {MinPasswordLength} characters long.";
                return false;
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                error = "Password and Confirm Password do not match.";
                return false;
            }

            return true;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase) && email.Contains('.');
            }
            catch
            {
                return false;
            }
        }

        private static UserAccount MapUser(SqlDataReader r)
        {
            return new UserAccount
            {
                UserID = r.GetInt32(0),
                FullName = r.GetString(1),
                Username = r.GetString(2),
                EmailAddress = r.GetString(3),
                Role = r.GetString(4),
                IsActive = r.GetBoolean(5),
                FailedLoginAttempts = r.GetInt32(6),
                LockoutUntil = r.IsDBNull(7) ? null : r.GetDateTime(7),
                CreatedAt = r.GetDateTime(8),
                LastLoginAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            };
        }
    }
}
