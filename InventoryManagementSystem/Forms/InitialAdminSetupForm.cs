using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// First-run setup form that creates the single Administrator account.
    /// Automatically disabled once an Admin account exists in the database.
    /// </summary>
    public class InitialAdminSetupForm : Form
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

        public InitialAdminSetupForm()
        {
            Text = "Initial Administrator Setup — Inventory Pro";
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(510, 680);
            StartPosition = FormStartPosition.CenterScreen;
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

            var logoPanel = new Panel
            {
                Size = new Size(56, 56),
                Location = new Point((Width - 56) / 2, 18),
                BackColor = Color.Transparent
            };
            logoPanel.Paint += (s, e) => UITheme.DrawIsometricCube(e.Graphics, new RectangleF(4, 4, 48, 48), glowing: true);
            logoPanel.MouseDown += DragWindow;
            Controls.Add(logoPanel);

            var lblTitle = new Label
            {
                Text = "Initial Administrator Setup",
                UseMnemonic = false,
                Font = UITheme.FontWelcome,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 74),
                Size = new Size(Width - 40, 42)
            };
            lblTitle.MouseDown += DragWindow;
            Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Set up the single Administrator account for this installation.\nOnce created, no additional Admin accounts can be registered.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(24, 116),
                Size = new Size(Width - 48, 40)
            };
            lblSub.MouseDown += DragWindow;
            Controls.Add(lblSub);

            var card = new GlassCardPanel
            {
                Location = new Point(28, 156),
                Size = new Size(Width - 56, 496),
                Padding = new Padding(26, 20, 26, 18)
            };

            int x = 26;
            int w = card.Width - 52;
            int y = 20;

            card.Controls.Add(CreateFieldLabel("Administrator Full Name *", x, y)); y += 21;
            _txtFullName = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "e.g., System Administrator" };
            UITheme.StyleTextBox(_txtFullName);
            card.Controls.Add(_txtFullName); y += 40;

            card.Controls.Add(CreateFieldLabel("Admin Username *", x, y)); y += 21;
            _txtUsername = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "Choose an admin username" };
            UITheme.StyleTextBox(_txtUsername);
            card.Controls.Add(_txtUsername); y += 40;

            card.Controls.Add(CreateFieldLabel("Admin Email Address *", x, y)); y += 21;
            _txtEmail = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), PlaceholderText = "admin@company.com" };
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
                Text = "Create Administrator Account",
                Location = new Point(x, y),
                Size = new Size(w, 42),
                CornerRadius = 10,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue,
                Font = UITheme.FontBold
            };
            btnCreate.Click += BtnCreate_Click;
            card.Controls.Add(btnCreate);

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

            if (_userRepo.HasAdminAccount())
            {
                _lblError.Text = "An Administrator account already exists. Setup is disabled.";
                _lblError.Visible = true;
                return;
            }

            bool ok = _userRepo.CreateInitialAdmin(
                _txtFullName.Text,
                _txtUsername.Text,
                _txtEmail.Text,
                _txtPassword.Text,
                _txtConfirm.Text,
                out string message);

            if (ok)
            {
                MessageBox.Show(message, "Administrator Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
