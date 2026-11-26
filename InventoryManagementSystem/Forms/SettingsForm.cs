using System;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Settings screen providing database Backup & Restore with integrity verification
    /// and live SQL Server connection/record diagnostics. High-DPI safe with UseMnemonic = false.
    /// </summary>
    public class SettingsForm : UserControl
    {
        private readonly BackupService _backupService = new BackupService();
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly SupplierRepository _supplierRepo = new SupplierRepository();
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();
        private readonly ActivityLogRepository _activityRepo = new ActivityLogRepository();

        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private GlassCardPanel _backupCard = null!;
        private GlassCardPanel _restoreCard = null!;
        private GlassCardPanel _systemCard = null!;

        private Label _lblBackupStatus = null!;
        private Label _lblRestoreStatus = null!;
        private Label _lblServerInfo = null!;
        private Label _lblDatabaseInfo = null!;
        private Label _lblRecordCounts = null!;

        public event Action? DataRestored;

        public SettingsForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = UITheme.FontRegular;

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(22, 14, 22, 16),
                BackColor = Color.Transparent,
                AutoScroll = true
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 208));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Header (FlowLayoutPanel prevents title/subtitle overlap at 100%, 125%, or 150% Windows DPI)
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            _lblTitle = new Label
            {
                Text = "Settings & Data Backup",
                UseMnemonic = false,
                Font = UITheme.FontWelcome,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            _lblSubtitle = new Label
            {
                Text = "Create verified backups, restore inventory data safely, and inspect active SQL Server storage.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 0)
            };
            headerPanel.Controls.Add(_lblTitle);
            headerPanel.Controls.Add(_lblSubtitle);
            root.Controls.Add(headerPanel, 0, 0);

            // 1. Backup Card
            _backupCard = new GlassCardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 8),
                Padding = new Padding(22, 16, 22, 16)
            };

            var lblBackupHeader = new Label
            {
                Text = "Create Inventory Backup",
                UseMnemonic = false,
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(22, 14),
                AutoSize = true
            };
            var lblBackupDesc = new Label
            {
                Text = "Exports all Categories, Suppliers, Products, Stock Transactions, and Activity Logs into a verified .json backup file. The backup file is checked immediately after saving so you can safely archive or transfer it to another PC.",
                UseMnemonic = false,
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(22, 46),
                MaximumSize = new Size(780, 0),
                AutoSize = true
            };
            var btnBackup = new GradientButton
            {
                Text = "Create Backup (.json)...",
                Location = new Point(22, 118),
                Size = new Size(216, 38),
                CornerRadius = 10,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            btnBackup.Click += BtnBackup_Click;

            _lblBackupStatus = new Label
            {
                Text = "No backup created in this session yet.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(252, 128),
                AutoSize = true
            };

            _backupCard.Controls.Add(lblBackupHeader);
            _backupCard.Controls.Add(lblBackupDesc);
            _backupCard.Controls.Add(btnBackup);
            _backupCard.Controls.Add(_lblBackupStatus);
            root.Controls.Add(_backupCard, 0, 1);

            // 2. Restore Card
            _restoreCard = new GlassCardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 8),
                Padding = new Padding(22, 16, 22, 16)
            };

            var lblRestoreHeader = new Label
            {
                Text = "Restore from Backup",
                UseMnemonic = false,
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(22, 14),
                AutoSize = true
            };
            var lblRestoreDesc = new Label
            {
                Text = "Restores all inventory tables from a previously saved .json backup file. The file is validated before any changes are made, and the restore runs inside a protected database transaction so existing data is preserved if an error occurs.",
                UseMnemonic = false,
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(22, 46),
                MaximumSize = new Size(780, 0),
                AutoSize = true
            };
            var btnRestore = new GradientButton
            {
                Text = "Restore Backup (.json)...",
                Location = new Point(22, 120),
                Size = new Size(216, 38),
                CornerRadius = 10,
                GradientStart = Color.FromArgb(245, 158, 11),
                GradientEnd = Color.FromArgb(180, 83, 9)
            };
            btnRestore.Click += BtnRestore_Click;

            _lblRestoreStatus = new Label
            {
                Text = "Warning: Restoring replaces current inventory records with the selected backup.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.WarningAmber,
                BackColor = Color.Transparent,
                Location = new Point(252, 130),
                AutoSize = true
            };

            _restoreCard.Controls.Add(lblRestoreHeader);
            _restoreCard.Controls.Add(lblRestoreDesc);
            _restoreCard.Controls.Add(btnRestore);
            _restoreCard.Controls.Add(_lblRestoreStatus);
            root.Controls.Add(_restoreCard, 0, 2);

            // 3. System & Database Diagnostics Card
            _systemCard = new GlassCardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0),
                Padding = new Padding(22, 16, 22, 16),
                MinimumSize = new Size(0, 196)
            };

            var sysStack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var lblSystemHeader = new Label
            {
                Text = "Database & System Information",
                UseMnemonic = false,
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblServerInfo = new Label
            {
                Text = $"SQL Server Instance: {DbConnection.ActiveServer}",
                UseMnemonic = false,
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblDatabaseInfo = new Label
            {
                Text = $"Database Name: {DbConnection.DatabaseName}  |  Storage Engine: Microsoft SQL Server (ADO.NET)",
                UseMnemonic = false,
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblRecordCounts = new Label
            {
                Text = "Loading record summary...",
                UseMnemonic = false,
                Font = UITheme.FontRegular,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            var btnRefreshInfo = new Button
            {
                Text = "Refresh Status",
                Size = new Size(146, 34),
                Margin = new Padding(0)
            };
            UITheme.StyleSecondaryButton(btnRefreshInfo);
            btnRefreshInfo.Click += (s, e) => RefreshData();

            sysStack.Controls.Add(lblSystemHeader);
            sysStack.Controls.Add(_lblServerInfo);
            sysStack.Controls.Add(_lblDatabaseInfo);
            sysStack.Controls.Add(_lblRecordCounts);
            sysStack.Controls.Add(btnRefreshInfo);
            _systemCard.Controls.Add(sysStack);
            root.Controls.Add(_systemCard, 0, 3);

            Controls.Add(root);
        }

        public void RefreshData()
        {
            try
            {
                int products = _productRepo.SearchProducts().Count;
                int categories = _categoryRepo.GetAllCategories().Count;
                int suppliers = _supplierRepo.GetAllSuppliers().Count;
                int transactions = _txRepo.GetTransactions(maxRows: 5000).Count;
                int activities = _activityRepo.GetActivities(maxRows: 5000).Count;

                _lblServerInfo.Text = $"SQL Server Instance: {DbConnection.ActiveServer}";
                _lblRecordCounts.Text = $"Active Records:  {products} Products   |   {categories} Categories   |   {suppliers} Suppliers   |   {transactions} Stock Transactions   |   {activities} Activity Logs";
            }
            catch (Exception ex)
            {
                _lblRecordCounts.Text = $"Unable to read database statistics: {ex.Message}";
            }
        }

        private void BtnBackup_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Save Inventory Database Backup";
                sfd.Filter = "Inventory Backup (*.json)|*.json";
                sfd.FileName = $"InventoryBackup_{DateTime.Now:yyyyMMdd_HHmm}.json";

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                bool ok = _backupService.CreateBackup(sfd.FileName, out string msg);
                if (ok)
                {
                    _lblBackupStatus.Text = $"Last backup saved & verified: {DateTime.Now:MMM dd, yyyy h:mm tt}";
                    _lblBackupStatus.ForeColor = UITheme.SuccessGreen;
                    RefreshData();
                    MessageBox.Show(msg, "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _lblBackupStatus.Text = "Backup failed. See error details.";
                    _lblBackupStatus.ForeColor = UITheme.DangerRed;
                    MessageBox.Show(msg, "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnRestore_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Inventory Backup File to Restore";
                ofd.Filter = "Inventory Backup (*.json)|*.json";

                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                if (!_backupService.ValidateBackupFile(ofd.FileName, out InventoryBackupData? preview, out string error) || preview == null)
                {
                    _lblRestoreStatus.Text = "Selected backup file is invalid. Existing data was not modified.";
                    _lblRestoreStatus.ForeColor = UITheme.DangerRed;
                    MessageBox.Show($"Cannot restore from this file:\n\n{error}", "Invalid Backup File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string confirmText =
                    $"Backup file validated successfully.\n\n" +
                    $"Exported At: {preview.ExportedAt:MMM dd, yyyy h:mm tt}\n" +
                    $"Categories: {preview.Categories.Count}\n" +
                    $"Suppliers: {preview.Suppliers.Count}\n" +
                    $"Products: {preview.Products.Count}\n" +
                    $"Stock Transactions: {preview.StockTransactions.Count}\n" +
                    $"Activity Logs: {preview.ActivityLogs.Count}\n\n" +
                    "WARNING: Restoring will replace all current inventory records in the database.\n" +
                    "Do you want to continue?";

                if (MessageBox.Show(confirmText, "Confirm Data Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                bool ok = _backupService.RestoreBackup(ofd.FileName, out string msg);
                if (ok)
                {
                    _lblRestoreStatus.Text = $"Data restored successfully on {DateTime.Now:MMM dd, yyyy h:mm tt}.";
                    _lblRestoreStatus.ForeColor = UITheme.SuccessGreen;
                    RefreshData();
                    DataRestored?.Invoke();
                    MessageBox.Show(msg, "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _lblRestoreStatus.Text = "Restore failed; database was rolled back safely.";
                    _lblRestoreStatus.ForeColor = UITheme.DangerRed;
                    MessageBox.Show(msg, "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void ApplyTheme()
        {
            UITheme.ApplyThemeRecursive(this);
            Invalidate(true);
        }
    }
}
