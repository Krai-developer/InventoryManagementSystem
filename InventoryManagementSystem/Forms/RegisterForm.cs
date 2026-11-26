using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Self-registration form for standard User accounts.
    /// Role is strictly assigned as 'User' in the data layer; users cannot request Admin.
    /// </summary>
    public class RegisterForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private readonly UserRepository _userRepo = new UserRepository();

        private TextBox _txtFullName = null!;
        private TextBox _txtUsername = null!;
        private TextBox _txtEmail = null!;
        private TextBox _txtPassword = null!;
        private TextBox _txtConfirm = null!;
        private CheckBox _chkShowPasswords = null!;
        private Label _lblError = null!;

        public string RegisteredUsername { get; private set; } = string.Empty;

        public RegisterForm()
        {
            Text = "Create Account — Inventory Pro";
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(490, 650);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = UITheme.CanvasBg;
            Font = UITheme.FontRegular;
            DoubleBuffered = true;

            Paint += (s, e) =>
            {
                UITheme.PaintAmbientBackground(e.Graphics, ClientRectangle);
                UITheme.PaintFramelessWindowBorder(e.Graphics, ClientRectangle);
            };
            MouseDown += DragWindow;

            InitializeControls();
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void InitializeControls()
        {
            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(38, 34),
                Location = new Point(Width - 44, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(203, 213, 225),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
            btnClose.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(btnClose);

            var lblTitle = new Label
            {
                Text = "Create User Account",
                Font = UITheme.FontWelcome,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 22),
                Size = new Size(Width - 40, 34)
            };
            lblTitle.MouseDown += DragWindow;
            Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Register a new standard User account for Inventory Pro",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 58),
                Size = new Size(Width - 40, 20)
            };
            lblSub.MouseDown += DragWindow;
            Controls.Add(lblSub);

            var card = new GlassCardPanel
            {
                Location = new Point(28, 92),
                Size = new Size(Width - 56, 530),
                Padding = new Padding(26, 20, 26, 18)
            };

            int x = 26;
            int w = card.Width - 52;
            int y = 20;

            card.Controls.Add(CreateFieldLabel("Full Name *", x, y)); y += 21;
            _txtFullName = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "e.g., Juan Dela Cruz" };
            UITheme.StyleTextBox(_txtFullName);
            card.Controls.Add(_txtFullName); y += 40;

            card.Controls.Add(CreateFieldLabel("Username *", x, y)); y += 21;
            _txtUsername = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "At least 3 characters, no spaces" };
            UITheme.StyleTextBox(_txtUsername);
            card.Controls.Add(_txtUsername); y += 40;

            card.Controls.Add(CreateFieldLabel("Email Address *", x, y)); y += 21;
            _txtEmail = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "name@example.com" };
            UITheme.StyleTextBox(_txtEmail);
            card.Controls.Add(_txtEmail); y += 40;

            card.Controls.Add(CreateFieldLabel("Password *", x, y)); y += 21;
            _txtPassword = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), UseSystemPasswordChar = true, PlaceholderText = "Minimum 6 characters" };
            UITheme.StyleTextBox(_txtPassword);
            card.Controls.Add(_txtPassword); y += 40;

            card.Controls.Add(CreateFieldLabel("Confirm Password *", x, y)); y += 21;
            _txtConfirm = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), UseSystemPasswordChar = true, PlaceholderText = "Re-enter your password" };
            UITheme.StyleTextBox(_txtConfirm);
            card.Controls.Add(_txtConfirm); y += 34;

            _chkShowPasswords = new CheckBox
            {
                Text = "Show passwords",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _chkShowPasswords.CheckedChanged += (s, e) =>
            {
                bool hide = !_chkShowPasswords.Checked;
                _txtPassword.UseSystemPasswordChar = hide;
                _txtConfirm.UseSystemPasswordChar = hide;
            };
            card.Controls.Add(_chkShowPasswords); y += 28;

            var lblRoleNote = new Label
            {
                Text = "Account Role: User (assigned automatically)",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true
            };
            card.Controls.Add(lblRoleNote); y += 24;

            _lblError = new Label
            {
                Text = string.Empty,
                Font = UITheme.FontSmall,
                ForeColor = Color.FromArgb(248, 113, 113),
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w, 34),
                Visible = false
            };
            card.Controls.Add(_lblError); y += 40;

            var btnCreate = new GradientButton
            {
                Text = "Create Account",
                Location = new Point(x, y),
                Size = new Size(w, 40),
                CornerRadius = 10,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue,
                Font = UITheme.FontBold
            };
            btnCreate.Click += BtnCreate_Click;
            card.Controls.Add(btnCreate); y += 48;

            var btnCancel = new Button
            {
                Text = "Back to Sign In",
                Location = new Point(x, y),
                Size = new Size(w, 34)
            };
            UITheme.StyleSecondaryButton(btnCancel);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            card.Controls.Add(btnCancel);

            Controls.Add(card);
            AcceptButton = btnCreate;
        }

        private static Label CreateFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = UITheme.FontBold,
                ForeColor = Color.FromArgb(219, 234, 254),
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            _lblError.Visible = false;
            _lblError.Text = string.Empty;

            bool ok = _userRepo.RegisterUser(
                _txtFullName.Text,
                _txtUsername.Text,
                _txtEmail.Text,
                _txtPassword.Text,
                _txtConfirm.Text,
                out string message);

            if (ok)
            {
                RegisteredUsername = _txtUsername.Text.Trim();
                MessageBox.Show(message, "Account Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblError.Text = message;
                _lblError.Visible = true;
            }
        }
    }
}
