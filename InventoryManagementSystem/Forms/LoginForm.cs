using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Dark navy & electric-blue glass Login screen shown before accessing the main dashboard.
    /// </summary>
    public class LoginForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private readonly UserRepository _userRepo = new UserRepository();

        private TextBox _txtIdentifier = null!;
        private TextBox _txtPassword = null!;
        private CheckBox _chkShowPassword = null!;
        private Label _lblError = null!;
        private GradientButton _btnLogin = null!;

        public LoginForm()
        {
            Text = "Sign In — Inventory Pro";
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(460, 540);
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
                Size = new Size(64, 64),
                Location = new Point((Width - 64) / 2, 26),
                BackColor = Color.Transparent
            };
            logoPanel.Paint += (s, e) => UITheme.DrawIsometricCube(e.Graphics, new RectangleF(4, 4, 56, 56), glowing: true);
            logoPanel.MouseDown += DragWindow;
            Controls.Add(logoPanel);

            var lblBrand = new Label
            {
                Text = "Inventory Pro",
                UseMnemonic = false,
                Font = UITheme.FontWelcome,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 90),
                Size = new Size(Width - 40, 42)
            };
            lblBrand.MouseDown += DragWindow;
            Controls.Add(lblBrand);

            var lblSub = new Label
            {
                Text = "Sign in to access your inventory workspace",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 132),
                Size = new Size(Width - 40, 24)
            };
            lblSub.MouseDown += DragWindow;
            Controls.Add(lblSub);

            var card = new GlassCardPanel
            {
                Location = new Point(30, 162),
                Size = new Size(Width - 60, 346),
                Padding = new Padding(26, 24, 26, 20)
            };

            int x = 26;
            int fieldW = card.Width - 52;
            int y = 24;

            card.Controls.Add(CreateFieldLabel("Username or Email", x, y));
            y += 22;

            _txtIdentifier = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(fieldW, 30),
                PlaceholderText = "Enter your username or email"
            };
            UITheme.StyleTextBox(_txtIdentifier);
            card.Controls.Add(_txtIdentifier);
            y += 46;

            card.Controls.Add(CreateFieldLabel("Password", x, y));
            y += 22;

            _txtPassword = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(fieldW, 30),
                UseSystemPasswordChar = true,
                PlaceholderText = "Enter your password"
            };
            UITheme.StyleTextBox(_txtPassword);
            card.Controls.Add(_txtPassword);
            y += 36;

            _chkShowPassword = new CheckBox
            {
                Text = "Show password",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _chkShowPassword.CheckedChanged += (s, e) => _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
            card.Controls.Add(_chkShowPassword);
            y += 32;

            _lblError = new Label
            {
                Text = string.Empty,
                Font = UITheme.FontSmall,
                ForeColor = Color.FromArgb(248, 113, 113),
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(fieldW, 36),
                Visible = false
            };
            card.Controls.Add(_lblError);
            y += 42;

            _btnLogin = new GradientButton
            {
                Text = "Sign In",
                Location = new Point(x, y),
                Size = new Size(fieldW, 42),
                CornerRadius = 10,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue,
                Font = UITheme.FontBold
            };
            _btnLogin.Click += BtnLogin_Click;
            card.Controls.Add(_btnLogin);
            y += 54;

            var lblNoAccount = new Label
            {
                Text = "New user?",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(x + 88, y + 2),
                AutoSize = true
            };

            var lnkRegister = new LinkLabel
            {
                Text = "Create Account",
                Font = UITheme.FontBold,
                LinkColor = UITheme.ElectricBlue,
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(x + 152, y + 1),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            lnkRegister.LinkClicked += LnkRegister_LinkClicked;

            card.Controls.Add(lblNoAccount);
            card.Controls.Add(lnkRegister);

            Controls.Add(card);
            AcceptButton = _btnLogin;
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

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            _lblError.Visible = false;
            _lblError.Text = string.Empty;

            try
            {
                if (_userRepo.Authenticate(_txtIdentifier.Text, _txtPassword.Text, out UserAccount? user, out string error) && user != null)
                {
                    _txtPassword.Clear();
                    SessionManager.StartSession(user);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    _lblError.Text = error;
                    _lblError.Visible = true;
                    _txtPassword.Clear();
                    _txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                _lblError.Text = $"Login failed: {ex.Message}";
                _lblError.Visible = true;
            }
        }

        private void LnkRegister_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            using (var regForm = new RegisterForm())
            {
                if (regForm.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(regForm.RegisteredUsername))
                {
                    _txtIdentifier.Text = regForm.RegisteredUsername;
                    _txtPassword.Clear();
                    _txtPassword.Focus();
                    _lblError.Visible = false;
                }
            }
        }
    }
}
