using System;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Displays the currently signed-in user's account profile, role permissions summary,
    /// and allows changing their own password securely.
    /// </summary>
    public class UserProfileForm : UserControl
    {
        private readonly UserRepository _userRepo = new UserRepository();

        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private Label _lblProfileInfo = null!;
        private Label _lblRoleSummary = null!;

        private TextBox _txtCurrentPwd = null!;
        private TextBox _txtNewPwd = null!;
        private TextBox _txtConfirmPwd = null!;
        private CheckBox _chkShowPwd = null!;

        public UserProfileForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
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
                RowCount = 3,
                Padding = new Padding(22, 16, 22, 16),
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblTitle = new Label
            {
                Text = "My Account Profile",
                Font = UITheme.FontWelcome,
                ForeColor = UITheme.TextPrimary,
                Location = new Point(0, 0),
                AutoSize = true
            };
            _lblSubtitle = new Label
            {
                Text = "View your signed-in account details, role access, and update your password.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                Location = new Point(2, 32),
                AutoSize = true
            };
            header.Controls.Add(_lblTitle);
            header.Controls.Add(_lblSubtitle);
            root.Controls.Add(header, 0, 0);

            var infoCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            infoCard.Dock = DockStyle.Fill;
            infoCard.Margin = new Padding(0, 6, 0, 12);

            infoCard.Controls.Add(new Label
            {
                Text = "Account Information",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(22, 16),
                AutoSize = true
            });

            _lblProfileInfo = new Label
            {
                Text = "Loading profile...",
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(22, 48),
                Size = new Size(440, 120)
            };

            _lblRoleSummary = new Label
            {
                Text = string.Empty,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Location = new Point(480, 48),
                Size = new Size(440, 120)
            };

            infoCard.Controls.Add(_lblProfileInfo);
            infoCard.Controls.Add(_lblRoleSummary);
            root.Controls.Add(infoCard, 0, 1);

            var pwdCard = UITheme.CreateCardPanel();
            pwdCard.Dock = DockStyle.Fill;

            int x = 22;
            int y = 16;
            pwdCard.Controls.Add(new Label
            {
                Text = "Change Password",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true
            });
            y += 34;

            pwdCard.Controls.Add(new Label { Text = "Current Password", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(x, y), AutoSize = true });
            y += 19;
            _txtCurrentPwd = new TextBox { Location = new Point(x, y), Size = new Size(340, 28), UseSystemPasswordChar = true };
            UITheme.StyleTextBox(_txtCurrentPwd);
            pwdCard.Controls.Add(_txtCurrentPwd);
            y += 38;

            pwdCard.Controls.Add(new Label { Text = "New Password", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(x, y), AutoSize = true });
            y += 19;
            _txtNewPwd = new TextBox { Location = new Point(x, y), Size = new Size(340, 28), UseSystemPasswordChar = true, PlaceholderText = "Minimum 6 characters" };
            UITheme.StyleTextBox(_txtNewPwd);
            pwdCard.Controls.Add(_txtNewPwd);
            y += 38;

            pwdCard.Controls.Add(new Label { Text = "Confirm New Password", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(x, y), AutoSize = true });
            y += 19;
            _txtConfirmPwd = new TextBox { Location = new Point(x, y), Size = new Size(340, 28), UseSystemPasswordChar = true };
            UITheme.StyleTextBox(_txtConfirmPwd);
            pwdCard.Controls.Add(_txtConfirmPwd);
            y += 34;

            _chkShowPwd = new CheckBox
            {
                Text = "Show passwords",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true
            };
            _chkShowPwd.CheckedChanged += (s, e) =>
            {
                bool hide = !_chkShowPwd.Checked;
                _txtCurrentPwd.UseSystemPasswordChar = hide;
                _txtNewPwd.UseSystemPasswordChar = hide;
                _txtConfirmPwd.UseSystemPasswordChar = hide;
            };
            pwdCard.Controls.Add(_chkShowPwd);
            y += 30;

            var btnUpdatePwd = new GradientButton
            {
                Text = "Update Password",
                Location = new Point(x, y),
                Size = new Size(200, 38),
                CornerRadius = 10,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            btnUpdatePwd.Click += BtnUpdatePwd_Click;
            pwdCard.Controls.Add(btnUpdatePwd);

            root.Controls.Add(pwdCard, 0, 2);
            Controls.Add(root);
        }

        public void RefreshData()
        {
            UserAccount? user = SessionManager.CurrentUser;
            if (user == null)
            {
                _lblProfileInfo.Text = "Not signed in.";
                _lblRoleSummary.Text = string.Empty;
                return;
            }

            string lastLogin = user.LastLoginAt.HasValue ? user.LastLoginAt.Value.ToString("MMM dd, yyyy hh:mm tt") : "Current session";
            _lblProfileInfo.Text =
                $"User ID: {user.UserID}\n" +
                $"Full Name: {user.FullName}\n" +
                $"Username: {user.Username}\n" +
                $"Email: {user.EmailAddress}\n" +
                $"Role: {user.Role}   |   Status: {user.AccountStatus}\n" +
                $"Registered: {user.CreatedAt:MMM dd, yyyy}   |   Last Login: {lastLogin}";

            _lblRoleSummary.Text = user.IsAdmin
                ? "Permissions (Admin):\n• Full access to Dashboard, Products (Add/Edit/Delete), Categories, Suppliers\n• Stock-In / Stock-Out / Controlled Stock Adjustments, All 7 Reports, Activity History\n• User Management, Backup & Restore, Application Settings"
                : "Permissions (Inventory Staff — User):\n• Access Dashboard, View & Search Products (View-Only), Low-Stock Alerts\n• Record Stock-In & Stock-Out, View Transaction History & Permitted Reports\n• Restricted: Add/Edit/Delete Products, Categories, Suppliers, Adjustments, Activity History, Users, Settings";
        }

        private void BtnUpdatePwd_Click(object? sender, EventArgs e)
        {
            bool ok = _userRepo.ChangeOwnPassword(_txtCurrentPwd.Text, _txtNewPwd.Text, _txtConfirmPwd.Text, out string msg);
            if (ok)
            {
                _txtCurrentPwd.Clear();
                _txtNewPwd.Clear();
                _txtConfirmPwd.Clear();
                MessageBox.Show(msg, "Password Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(msg, "Password Change Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ApplyTheme()
        {
            _lblTitle.ForeColor = UITheme.TextPrimary;
            _lblSubtitle.ForeColor = UITheme.TextMuted;
            UITheme.StyleTextBox(_txtCurrentPwd);
            UITheme.StyleTextBox(_txtNewPwd);
            UITheme.StyleTextBox(_txtConfirmPwd);
            Invalidate(true);
        }
    }
}
