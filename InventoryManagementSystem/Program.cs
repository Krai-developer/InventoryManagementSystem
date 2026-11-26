using System;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Forms;

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
                    "Database Connection Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            Application.Run(new MainForm(statusMessage));
        }
    }
}
