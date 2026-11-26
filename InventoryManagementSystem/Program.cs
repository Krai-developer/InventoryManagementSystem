using System;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Forms;
using InventoryManagementSystem.Security;

namespace InventoryManagementSystem
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            bool dbReady = DatabaseInitializer.InitializeDatabase(out string statusMessage);
            if (!dbReady)
            {
                MessageBox.Show(
                    $"{statusMessage}\n\nMake sure SQL Server LocalDB ((localdb)\\MSSQLLocalDB) or SQL Server Express (.\\SQLEXPRESS) is running, or run Database\\DatabaseSetup.sql in SSMS.",
                    "Database Connection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var userRepo = new UserRepository();

            // 1. First-run single-Admin setup: if no Admin exists yet, require initial Admin creation
            if (!userRepo.HasAdminAccount())
            {
                using (var setupForm = new InitialAdminSetupForm())
                {
                    if (setupForm.ShowDialog() != DialogResult.OK || !userRepo.HasAdminAccount())
                    {
                        return;
                    }
                }
            }

            // 2. Authentication & Session Loop: always require Login before opening MainForm.
            //    When a user logs out, MainForm is disposed completely and control returns to LoginForm.
            while (true)
            {
                SessionManager.Logout();

                using (var loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() != DialogResult.OK || !SessionManager.IsAuthenticated)
                    {
                        break;
                    }
                }

                bool loggedOut = false;
                using (var mainForm = new MainForm(statusMessage))
                {
                    Application.Run(mainForm);
                    loggedOut = mainForm.LoggedOut;
                }

                SessionManager.Logout();

                if (!loggedOut)
                {
                    break;
                }
            }
        }
    }
}
