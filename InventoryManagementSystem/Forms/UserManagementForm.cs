using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Administrator-only User Management view for viewing, searching, activating/deactivating,
    /// and resetting passwords of registered user accounts.
    /// Uses FlowLayoutPanel + AutoSize labels on the details card so text never clips at 100%, 125%, or 150% DPI.
    /// </summary>
    public class UserManagementForm : UserControl
    {
        private readonly UserRepository _userRepo = new UserRepository();

        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private TextBox _txtSearch = null!;
        private Label _lblCount = null!;
        private DataGridView _dgvUsers = null!;

        private GlassCardPanel _detailsCard = null!;
        private Label _lblSelName = null!;
        private Label _lblSelMeta = null!;
        private Label _lblSelDates = null!;
        private GradientButton _btnToggleStatus = null!;
        private Button _btnDeleteUser = null!;

        private TextBox _txtNewPassword = null!;
        private TextBox _txtConfirmPassword = null!;
        private CheckBox _chkShowPwd = null!;
        private GradientButton _btnResetPassword = null!;

        private UserAccount? _selectedUser;

        public UserManagementForm()
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
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(18, 14, 18, 16),
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 368F));

            // Left: Header + Search + Grid
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var topCard = UITheme.CreateCardPanel();
            topCard.Dock = DockStyle.Fill;
            topCard.Margin = new Padding(0, 0, 0, 12);

            _lblTitle = new Label
            {
                Text = "User Management",
                UseMnemonic = false,
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            };

            _lblSubtitle = new Label
            {
                Text = "Administrator-only control over registered accounts, status, and password resets.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(16, 35),
                AutoSize = true
            };

            _lblCount = new Label
            {
                Text = "0 accounts",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(topCard.Width - 140, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                Location = new Point(16, 62),
                Size = new Size(320, 28),
                PlaceholderText = "Search by name, username, email, or role..."
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => LoadUsers();

            var btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(346, 60),
                Size = new Size(92, 30)
            };
            UITheme.StyleSecondaryButton(btnRefresh);
            btnRefresh.Click += (s, e) => RefreshData();

            topCard.Controls.Add(_lblTitle);
            topCard.Controls.Add(_lblSubtitle);
            topCard.Controls.Add(_lblCount);
            topCard.Controls.Add(_txtSearch);
            topCard.Controls.Add(btnRefresh);
            leftLayout.Controls.Add(topCard, 0, 0);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(12);

            _dgvUsers = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            UITheme.StyleDataGridView(_dgvUsers);
            _dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;
            gridCard.Controls.Add(_dgvUsers);
            leftLayout.Controls.Add(gridCard, 0, 1);

            root.Controls.Add(leftLayout, 0, 0);

            // Right: Selected Account Details + Actions (FlowLayoutPanel prevents any vertical text clipping at High DPI)
            _detailsCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            _detailsCard.Dock = DockStyle.Fill;
            _detailsCard.Padding = new Padding(18, 16, 18, 16);

            int w = 316;
            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            stack.Controls.Add(new Label
            {
                Text = "Account Details & Actions",
                UseMnemonic = false,
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            });

            _lblSelName = new Label
            {
                Text = "Select a user account",
                UseMnemonic = false,
                Font = UITheme.FontBold,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                MaximumSize = new Size(w, 0),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            stack.Controls.Add(_lblSelName);

            _lblSelMeta = new Label
            {
                Text = "Click any row in the table to inspect registration details and manage account access.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                MaximumSize = new Size(w, 0),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            stack.Controls.Add(_lblSelMeta);

            _lblSelDates = new Label
            {
                Text = string.Empty,
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                MaximumSize = new Size(w, 0),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            stack.Controls.Add(_lblSelDates);

            _btnToggleStatus = new GradientButton
            {
                Text = "Deactivate Account",
                Size = new Size(w, 36),
                CornerRadius = 9,
                GradientStart = Color.FromArgb(245, 158, 11),
                GradientEnd = Color.FromArgb(180, 83, 9),
                Enabled = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            _btnToggleStatus.Click += BtnToggleStatus_Click;
            stack.Controls.Add(_btnToggleStatus);

            _btnDeleteUser = new Button
            {
                Text = "Delete User Account",
                Size = new Size(w, 34),
                Enabled = false,
                Margin = new Padding(0, 0, 0, 16)
            };
            UITheme.StyleSecondaryButton(_btnDeleteUser);
            _btnDeleteUser.ForeColor = Color.FromArgb(248, 113, 113);
            _btnDeleteUser.Click += BtnDeleteUser_Click;
            stack.Controls.Add(_btnDeleteUser);

            stack.Controls.Add(new Label
            {
                Text = "Secure Password Reset",
                UseMnemonic = false,
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            });

            stack.Controls.Add(new Label
            {
                Text = "New Password",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 3)
            });

            _txtNewPassword = new TextBox
            {
                Size = new Size(w, 28),
                UseSystemPasswordChar = true,
                PlaceholderText = "Minimum 6 characters",
                Margin = new Padding(0, 0, 0, 10)
            };
            UITheme.StyleTextBox(_txtNewPassword);
            stack.Controls.Add(_txtNewPassword);

            stack.Controls.Add(new Label
            {
                Text = "Confirm New Password",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 3)
            });

            _txtConfirmPassword = new TextBox
            {
                Size = new Size(w, 28),
                UseSystemPasswordChar = true,
                PlaceholderText = "Re-enter new password",
                Margin = new Padding(0, 0, 0, 8)
            };
            UITheme.StyleTextBox(_txtConfirmPassword);
            stack.Controls.Add(_txtConfirmPassword);

            _chkShowPwd = new CheckBox
            {
                Text = "Show passwords",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            _chkShowPwd.CheckedChanged += (s, e) =>
            {
                bool hide = !_chkShowPwd.Checked;
                _txtNewPassword.UseSystemPasswordChar = hide;
                _txtConfirmPassword.UseSystemPasswordChar = hide;
            };
            stack.Controls.Add(_chkShowPwd);

            _btnResetPassword = new GradientButton
            {
                Text = "Reset User Password",
                Size = new Size(w, 36),
                CornerRadius = 9,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue,
                Enabled = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            _btnResetPassword.Click += BtnResetPassword_Click;
            stack.Controls.Add(_btnResetPassword);

            _detailsCard.Controls.Add(stack);
            root.Controls.Add(_detailsCard, 1, 0);
            Controls.Add(root);
        }

        public void RefreshData()
        {
            if (!SessionManager.IsAdmin)
            {
                _dgvUsers.Rows.Clear();
                _lblCount.Text = "Admin only";
                return;
            }
            LoadUsers();
        }

        private void LoadUsers()
        {
            if (!SessionManager.IsAdmin) return;

            try
            {
                List<UserAccount> users = _userRepo.GetAllUsers(_txtSearch.Text);
                _dgvUsers.Rows.Clear();
                _dgvUsers.Columns.Clear();

                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserID", HeaderText = "User ID", FillWeight = 10 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "FullName", HeaderText = "Full Name", FillWeight = 22 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Username", FillWeight = 16 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "Email", FillWeight = 22 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Role", FillWeight = 10 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 10 });
                _dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Registered", HeaderText = "Date Registered", FillWeight = 18 });

                foreach (var u in users)
                {
                    int idx = _dgvUsers.Rows.Add(
                        u.UserID,
                        u.FullName,
                        u.Username,
                        u.EmailAddress,
                        u.Role,
                        u.AccountStatus,
                        u.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                    _dgvUsers.Rows[idx].Tag = u;
                }

                _lblCount.Text = $"{users.Count} account(s)";
                UpdateSelectedUserPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load user accounts:\n{ex.Message}", "User Management Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvUsers_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectedUserPanel();
        }

        private void UpdateSelectedUserPanel()
        {
            _selectedUser = _dgvUsers.CurrentRow?.Tag as UserAccount;
            if (_selectedUser == null)
            {
                _lblSelName.Text = "Select a user account";
                _lblSelMeta.Text = "Click any row in the table to inspect registration details and manage account access.";
                _lblSelDates.Text = string.Empty;
                _btnToggleStatus.Enabled = false;
                _btnDeleteUser.Enabled = false;
                _btnResetPassword.Enabled = false;
                return;
            }

            _lblSelName.Text = $"{_selectedUser.FullName}  ({_selectedUser.Role})";
            _lblSelMeta.Text =
                $"User ID: {_selectedUser.UserID}\n" +
                $"Username: {_selectedUser.Username}\n" +
                $"Email: {_selectedUser.EmailAddress}\n" +
                $"Status: {_selectedUser.AccountStatus}";

            string lastLogin = _selectedUser.LastLoginAt.HasValue
                ? _selectedUser.LastLoginAt.Value.ToString("MMM dd, yyyy hh:mm tt")
                : "Never";
            _lblSelDates.Text =
                $"Registered: {_selectedUser.CreatedAt:MMM dd, yyyy hh:mm tt}\n" +
                $"Last Login: {lastLogin}\n" +
                $"Failed Login Attempts: {_selectedUser.FailedLoginAttempts}";

            bool isAdminRow = _selectedUser.IsAdmin;
            _btnToggleStatus.Enabled = !isAdminRow;
            _btnToggleStatus.Text = _selectedUser.IsActive ? "Deactivate Account" : "Activate Account";
            _btnDeleteUser.Enabled = !isAdminRow;
            _btnResetPassword.Enabled = true;
        }

        private void BtnToggleStatus_Click(object? sender, EventArgs e)
        {
            if (_selectedUser == null) return;

            bool nextActive = !_selectedUser.IsActive;
            string verb = nextActive ? "activate" : "deactivate";

            if (MessageBox.Show(
                $"Are you sure you want to {verb} account '{_selectedUser.Username}'?",
                "Confirm Account Status Change",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                bool ok = _userRepo.SetUserActiveStatus(_selectedUser.UserID, nextActive, out string msg);
                MessageBox.Show(msg, ok ? "Account Updated" : "Action Blocked",
                    MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                if (ok) LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnResetPassword_Click(object? sender, EventArgs e)
        {
            if (_selectedUser == null) return;

            try
            {
                bool ok = _userRepo.ResetUserPassword(
                    _selectedUser.UserID,
                    _txtNewPassword.Text,
                    _txtConfirmPassword.Text,
                    out string msg);

                if (ok)
                {
                    _txtNewPassword.Clear();
                    _txtConfirmPassword.Clear();
                    MessageBox.Show(msg, "Password Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadUsers();
                }
                else
                {
                    MessageBox.Show(msg, "Password Reset Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnDeleteUser_Click(object? sender, EventArgs e)
        {
            if (_selectedUser == null) return;

            if (MessageBox.Show(
                $"Permanently delete user account '{_selectedUser.Username}' ({_selectedUser.FullName})?",
                "Confirm Delete User",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                bool ok = _userRepo.DeleteUser(_selectedUser.UserID, out string msg);
                MessageBox.Show(msg, ok ? "User Deleted" : "Delete Blocked",
                    MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                if (ok) LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ApplyTheme()
        {
            UITheme.ApplyThemeRecursive(this);
            Invalidate(true);
        }
    }
}
