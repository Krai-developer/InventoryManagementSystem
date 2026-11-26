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
    /// Displays the currently signed-in user's account profile, allows updating permitted personal details
    /// (Full Name and Email Address), and allows securely changing their own password.
    /// Role changes are strictly disallowed.
    /// </summary>
    public class UserProfileForm : UserControl
    {
        private readonly UserRepository _userRepo = new UserRepository();

        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private Label _lblProfileMeta = null!;
        private Label _lblRoleSummary = null!;

        private TextBox _txtFullName = null!;
        private TextBox _txtEmail = null!;

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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 215));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblTitle = new Label
            {
                Text = "My Account",
                Font = UITheme.FontWelcome,
                ForeColor = UITheme.TextPrimary,
                Location = new Point(0, 0),
                AutoSize = true
            };
            _lblSubtitle = new Label
            {
                Text = "View your account role, update permitted personal details, and change your password.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                Location = new Point(2, 32),
                AutoSize = true
            };
            header.Controls.Add(_lblTitle);
            header.Controls.Add(_lblSubtitle);
            root.Controls.Add(header, 0, 0);

            // Top Card: Personal Details + Role Summary
            var infoCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            infoCard.Dock = DockStyle.Fill;
            infoCard.Margin = new Padding(0, 6, 0, 12);

            infoCard.Controls.Add(new Label
            {
                Text = "Personal Details",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(22, 14),
                AutoSize = true
            });

            _lblProfileMeta = new Label
            {
                Text = "Loading...",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Location = new Point(22, 40),
                Size = new Size(440, 22)
            };

            infoCard.Controls.Add(new Label { Text = "Full Name", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(22, 66), AutoSize = true });
            _txtFullName = new TextBox { Location = new Point(22, 85), Size = new Size(210, 27) };
            UITheme.StyleTextBox(_txtFullName);

            infoCard.Controls.Add(new Label { Text = "Email Address", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(246, 66), AutoSize = true });
            _txtEmail = new TextBox { Location = new Point(246, 85), Size = new Size(220, 27) };
            UITheme.StyleTextBox(_txtEmail);

            var btnSaveProfile = new GradientButton
            {
                Text = "Save Personal Details",
                Location = new Point(22, 126),
                Size = new Size(200, 36),
                CornerRadius = 9,
                GradientStart = UITheme.TealGlow,
                GradientEnd = UITheme.SuccessGreen
            };
            btnSaveProfile.Click += BtnSaveProfile_Click;

            _lblRoleSummary = new Label
            {
                Text = string.Empty,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(495, 42),
                Size = new Size(430, 130)
            };

            infoCard.Controls.Add(_lblProfileMeta);
            infoCard.Controls.Add(_txtFullName);
            infoCard.Controls.Add(_txtEmail);
            infoCard.Controls.Add(btnSaveProfile);
            infoCard.Controls.Add(_lblRoleSummary);
            root.Controls.Add(infoCard, 0, 1);

            // Bottom Card: Change Password
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
                _lblProfileMeta.Text = "Not signed in.";
                return;
            }

            _lblProfileMeta.Text = $"User ID #{user.UserID}  •  Username: {user.Username}  •  Role: {user.Role} ({user.AccountStatus})";
            _txtFullName.Text = user.FullName;
            _txtEmail.Text = user.EmailAddress;

            _lblRoleSummary.Text = user.IsAdmin
                ? "Permissions (Admin):\n• Full access to Products, Categories, Suppliers, Stock Adjustments\n• View All Transactions, All Reports, Complete Activity History\n• User Management, Backup & Restore, System Settings"
                : "Permissions (Inventory Staff — User):\n• View & Search Products (read-only), Record Stock In & Stock Out\n• View Own Recorded Transactions, Own Activities & Permitted Reports\n• Update Personal Details & Change Own Password\n• Restricted: Master Product Edits, Categories, Suppliers, Users, Settings";
        }

        private void BtnSaveProfile_Click(object? sender, EventArgs e)
        {
            bool ok = _userRepo.UpdateOwnProfile(_txtFullName.Text, _txtEmail.Text, out string msg);
            MessageBox.Show(msg, ok ? "Profile Updated" : "Validation Error",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            if (ok) RefreshData();
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
            UITheme.StyleTextBox(_txtFullName);
            UITheme.StyleTextBox(_txtEmail);
            UITheme.StyleTextBox(_txtCurrentPwd);
            UITheme.StyleTextBox(_txtNewPwd);
            UITheme.StyleTextBox(_txtConfirmPwd);
            Invalidate(true);
        }
    }
}
