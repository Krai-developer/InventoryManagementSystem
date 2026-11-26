using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Main Application Window matching the dark navy & electric-blue glass reference layout.
    /// Includes custom frameless dark title bar (draggable with Min/Max/Close), MenuStrip,
    /// left sidebar with 3D cube logo and Dark Mode toggle, and live bottom status bar.
    /// </summary>
    public class MainForm : Form
    {
        // Win32 native drag for custom frameless title bar
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        private Panel _titleBarPanel = null!;
        private MenuStrip _mainMenuStrip = null!;
        private Panel _sidebarPanel = null!;
        private Panel _contentHostPanel = null!;

        private ToggleSwitch _tglDarkMode = null!;

        private GradientButton _btnNavDashboard = null!;
        private GradientButton _btnNavProducts = null!;
        private GradientButton _btnNavCategories = null!;
        private GradientButton _btnNavSuppliers = null!;
        private GradientButton _btnNavStock = null!;
        private GradientButton _btnNavLowStock = null!;
        private GradientButton _btnNavReports = null!;
        private GradientButton _btnNavActivity = null!;
        private GradientButton _btnNavSettings = null!;
        private GradientButton _btnNavAbout = null!;
        private readonly List<GradientButton> _navButtons = new List<GradientButton>();
        private GradientButton? _activeNavButton;

        private DashboardView _dashboardView = null!;
        private ProductsForm _productsForm = null!;
        private CategoriesForm _categoriesForm = null!;
        private SuppliersForm _suppliersForm = null!;
        private StockTransactionForm _stockTransactionForm = null!;
        private LowStockForm _lowStockForm = null!;
        private ReportsForm _reportsForm = null!;
        private ActivityHistoryForm _activityHistoryForm = null!;
        private SettingsForm _settingsForm = null!;

        public MainForm(string dbStatusMessage)
        {
            _ = dbStatusMessage;

            Text = "Inventory and Stock Management System";
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(1380, 820);
            MinimumSize = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UITheme.CanvasBg;
            Font = UITheme.FontRegular;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            InitializeTitleBar();
            InitializeMenuStrip();
            InitializeShellLayout();
            InitializeViews();

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        // Allow resizing the frameless window from edges
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                Point pos = PointToClient(Cursor.Position);
                int grip = 6;
                if (pos.X <= grip && pos.Y <= grip) m.Result = (IntPtr)HTTOPLEFT;
                else if (pos.X >= Width - grip && pos.Y <= grip) m.Result = (IntPtr)HTTOPRIGHT;
                else if (pos.X <= grip && pos.Y >= Height - grip) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (pos.X >= Width - grip && pos.Y >= Height - grip) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (pos.X <= grip) m.Result = (IntPtr)HTLEFT;
                else if (pos.X >= Width - grip) m.Result = (IntPtr)HTRIGHT;
                else if (pos.Y <= grip) m.Result = (IntPtr)HTTOP;
                else if (pos.Y >= Height - grip) m.Result = (IntPtr)HTBOTTOM;
                return;
            }
            base.WndProc(ref m);
        }

        private void InitializeTitleBar()
        {
            _titleBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = UITheme.TopBarBg
            };
            _titleBarPanel.MouseDown += TitleBar_MouseDown;
            _titleBarPanel.DoubleClick += (s, e) => ToggleMaximize();
            _titleBarPanel.Paint += (s, e) =>
            {
                UITheme.DrawIsometricCube(e.Graphics, new RectangleF(10, 7, 22, 22), glowing: false);
                using (Pen border = new Pen(Color.FromArgb(40, 56, 139, 253), 1))
                {
                    e.Graphics.DrawLine(border, 0, _titleBarPanel.Height - 1, _titleBarPanel.Width, _titleBarPanel.Height - 1);
                }
            };

            var lblAppTitle = new Label
            {
                Text = "Inventory and Stock Management System",
                Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Regular),
                ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.Transparent,
                Location = new Point(38, 9),
                AutoSize = true
            };
            lblAppTitle.MouseDown += TitleBar_MouseDown;

            var btnClose = CreateWindowButton("✕", Color.FromArgb(220, 38, 38));
            btnClose.Click += (s, e) => Close();

            var btnMax = CreateWindowButton("☐", Color.FromArgb(30, 58, 110));
            btnMax.Click += (s, e) => ToggleMaximize();

            var btnMin = CreateWindowButton("—", Color.FromArgb(30, 58, 110));
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _titleBarPanel.Controls.Add(lblAppTitle);
            _titleBarPanel.Controls.Add(btnMin);
            _titleBarPanel.Controls.Add(btnMax);
            _titleBarPanel.Controls.Add(btnClose);
        }

        private Button CreateWindowButton(string glyph, Color hoverBg)
        {
            var btn = new Button
            {
                Text = glyph,
                Dock = DockStyle.Right,
                Width = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hoverBg;
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(hoverBg, 0.1f);
            return btn;
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void InitializeMenuStrip()
        {
            _mainMenuStrip = new MenuStrip
            {
                BackColor = UITheme.TopBarBg,
                ForeColor = Color.FromArgb(191, 219, 254),
                Font = UITheme.FontSmall,
                Padding = new Padding(10, 2, 10, 2),
                Dock = DockStyle.Top,
                Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable())
            };

            var mnuFile = CreateTopMenu("&File");
            mnuFile.DropDownItems.Add(CreateSubMenu("&Refresh View", (s, e) => RefreshActiveView(), Keys.F5));
            mnuFile.DropDownItems.Add(new ToolStripSeparator());
            mnuFile.DropDownItems.Add(CreateSubMenu("E&xit", (s, e) => Close(), Keys.Alt | Keys.F4));

            var mnuInventory = CreateTopMenu("&Inventory");
            mnuInventory.DropDownItems.Add(CreateSubMenu("&Dashboard", (s, e) => _btnNavDashboard.PerformClick()));
            mnuInventory.DropDownItems.Add(CreateSubMenu("&Products", (s, e) => _btnNavProducts.PerformClick()));
            mnuInventory.DropDownItems.Add(CreateSubMenu("&Categories", (s, e) => _btnNavCategories.PerformClick()));
            mnuInventory.DropDownItems.Add(CreateSubMenu("&Suppliers", (s, e) => _btnNavSuppliers.PerformClick()));
            mnuInventory.DropDownItems.Add(new ToolStripSeparator());
            mnuInventory.DropDownItems.Add(CreateSubMenu("Stock &In", (s, e) => OpenStockView("IN", 0)));
            mnuInventory.DropDownItems.Add(CreateSubMenu("Stock &Out", (s, e) => OpenStockView("OUT", 0)));
            mnuInventory.DropDownItems.Add(CreateSubMenu("&Low Stock Alerts", (s, e) => OpenLowStockView()));

            var mnuReports = CreateTopMenu("&Reports");
            mnuReports.DropDownItems.Add(CreateSubMenu("Inventory &Reports & CSV Export", (s, e) => _btnNavReports.PerformClick()));
            mnuReports.DropDownItems.Add(CreateSubMenu("&Activity History", (s, e) => _btnNavActivity.PerformClick()));
            mnuReports.DropDownItems.Add(new ToolStripSeparator());
            mnuReports.DropDownItems.Add(CreateSubMenu("&Settings / Backup & Restore", (s, e) => _btnNavSettings.PerformClick()));

            var mnuHelp = CreateTopMenu("&Help");
            mnuHelp.DropDownItems.Add(CreateSubMenu("&About Inventory Pro", (s, e) => ShowAboutDialog()));

            _mainMenuStrip.Items.AddRange(new ToolStripItem[] { mnuFile, mnuInventory, mnuReports, mnuHelp });
            MainMenuStrip = _mainMenuStrip;
        }

        private static ToolStripMenuItem CreateTopMenu(string text) => new ToolStripMenuItem(text) { ForeColor = Color.FromArgb(191, 219, 254) };

        private static ToolStripMenuItem CreateSubMenu(string text, EventHandler onClick, Keys shortcut = Keys.None)
        {
            var item = new ToolStripMenuItem(text, null, onClick) { ForeColor = Color.White };
            if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
            return item;
        }

        private void InitializeShellLayout()
        {
            // Left Sidebar matching the reference screenshot
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 212,
                BackColor = UITheme.SidebarBg
            };
            _sidebarPanel.Paint += (s, e) =>
            {
                if (_sidebarPanel.ClientRectangle.Width <= 0 || _sidebarPanel.ClientRectangle.Height <= 0) return;
                Graphics g = e.Graphics;
                using (LinearGradientBrush sbBg = new LinearGradientBrush(
                    _sidebarPanel.ClientRectangle,
                    Color.FromArgb(10, 20, 42),
                    Color.FromArgb(6, 13, 28),
                    90f))
                {
                    g.FillRectangle(sbBg, _sidebarPanel.ClientRectangle);
                }

                // Glowing blue right border separating sidebar from dashboard
                using (Pen rightEdge = new Pen(Color.FromArgb(68, 56, 139, 253), 1f))
                {
                    g.DrawLine(rightEdge, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
                }

                // 3D Glowing Isometric Cube Brand Logo at top center
                UITheme.DrawIsometricCube(g, new RectangleF(72, 22, 68, 68), glowing: true);
            };

            var lblBrandName = new Label
            {
                Text = "Inventory Pro",
                Font = UITheme.FontBrandTitle,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 98),
                Size = new Size(188, 24)
            };

            var lblBrandSub = new Label
            {
                Text = "Stock Management",
                Font = UITheme.FontBrandSub,
                ForeColor = UITheme.SidebarTextMuted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 122),
                Size = new Size(188, 18)
            };

            _sidebarPanel.Controls.Add(lblBrandName);
            _sidebarPanel.Controls.Add(lblBrandSub);

            // Navigation Pill Buttons
            int navY = 152;
            _btnNavDashboard  = CreateSidebarNavButton("Dashboard", ref navY);
            _btnNavProducts   = CreateSidebarNavButton("Products", ref navY);
            _btnNavCategories = CreateSidebarNavButton("Categories", ref navY);
            _btnNavSuppliers  = CreateSidebarNavButton("Suppliers", ref navY);
            _btnNavStock      = CreateSidebarNavButton("Stock In / Out", ref navY);
            _btnNavLowStock   = CreateSidebarNavButton("Low Stock", ref navY);
            _btnNavReports    = CreateSidebarNavButton("Reports", ref navY);
            _btnNavActivity   = CreateSidebarNavButton("Activity", ref navY);
            _btnNavSettings   = CreateSidebarNavButton("Settings", ref navY);
            _btnNavAbout      = CreateSidebarNavButton("About", ref navY);

            // Bottom Dark Mode Toggle Card inside Sidebar
            var darkModePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = Color.Transparent,
                Padding = new Padding(14, 10, 14, 12)
            };
            darkModePanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle box = new Rectangle(14, 10, darkModePanel.Width - 28, 40);
                using (GraphicsPath p = UITheme.CreateRoundedPath(box, 10))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(16, 29, 58)))
                using (Pen border = new Pen(Color.FromArgb(65, 56, 139, 253), 1f))
                {
                    e.Graphics.FillPath(fill, p);
                    e.Graphics.DrawPath(border, p);
                }
            };

            var lblDarkMode = new Label
            {
                Text = "Dark Mode",
                Font = UITheme.FontBold,
                ForeColor = Color.FromArgb(219, 234, 254),
                BackColor = Color.FromArgb(16, 29, 58),
                Location = new Point(28, 21),
                AutoSize = true
            };

            _tglDarkMode = new ToggleSwitch
            {
                Checked = true,
                Location = new Point(140, 19)
            };
            _tglDarkMode.CheckedChanged += (s, e) => ApplyThemeMode(_tglDarkMode.Checked);

            darkModePanel.Controls.Add(lblDarkMode);
            darkModePanel.Controls.Add(_tglDarkMode);
            _sidebarPanel.Controls.Add(darkModePanel);

            // Main Content Host Panel with ambient blue lighting
            _contentHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UITheme.CanvasBg
            };
            _contentHostPanel.Paint += (s, e) => UITheme.PaintAmbientBackground(e.Graphics, _contentHostPanel.ClientRectangle);

            Controls.Add(_contentHostPanel);
            Controls.Add(_sidebarPanel);
            Controls.Add(_mainMenuStrip);
            Controls.Add(_titleBarPanel);
        }

        private GradientButton CreateSidebarNavButton(string text, ref int y)
        {
            var btn = new GradientButton
            {
                Text = text,
                Location = new Point(14, y),
                Size = new Size(184, 36),
                CornerRadius = 9,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0),
                Font = UITheme.FontNavButton,
                IsOutline = true,
                GradientStart = Color.FromArgb(24, 48, 92),
                GradientEnd = Color.FromArgb(18, 36, 72)
            };

            _sidebarPanel.Controls.Add(btn);
            _navButtons.Add(btn);
            y += 41;
            return btn;
        }

        private void InitializeViews()
        {
            _dashboardView = new DashboardView();
            _productsForm = new ProductsForm();
            _categoriesForm = new CategoriesForm();
            _suppliersForm = new SuppliersForm();
            _stockTransactionForm = new StockTransactionForm();
            _lowStockForm = new LowStockForm();
            _reportsForm = new ReportsForm();
            _activityHistoryForm = new ActivityHistoryForm();
            _settingsForm = new SettingsForm();

            _btnNavDashboard.Click += (s, e) =>
            {
                NavigateTo(_btnNavDashboard, _dashboardView);
                _dashboardView.RefreshDashboard();
            };

            _btnNavProducts.Click += (s, e) =>
            {
                NavigateTo(_btnNavProducts, _productsForm);
                _productsForm.RefreshData();
            };

            _btnNavCategories.Click += (s, e) =>
            {
                NavigateTo(_btnNavCategories, _categoriesForm);
                _categoriesForm.RefreshData();
            };

            _btnNavSuppliers.Click += (s, e) =>
            {
                NavigateTo(_btnNavSuppliers, _suppliersForm);
                _suppliersForm.RefreshData();
            };

            _btnNavStock.Click += (s, e) => OpenStockView("IN", 0);

            _btnNavLowStock.Click += (s, e) => OpenLowStockView();

            _btnNavReports.Click += (s, e) =>
            {
                NavigateTo(_btnNavReports, _reportsForm);
                _reportsForm.RefreshData();
            };

            _btnNavActivity.Click += (s, e) =>
            {
                NavigateTo(_btnNavActivity, _activityHistoryForm);
                _activityHistoryForm.RefreshData();
            };

            _btnNavSettings.Click += (s, e) =>
            {
                NavigateTo(_btnNavSettings, _settingsForm);
                _settingsForm.RefreshData();
            };

            _btnNavAbout.Click += (s, e) => ShowAboutDialog();

            // Wire Dashboard Quick Actions & Links to actual working features
            _dashboardView.NavigateToProductsRequested += () => _btnNavProducts.PerformClick();
            _dashboardView.NavigateToProductEditRequested += productId =>
            {
                NavigateTo(_btnNavProducts, _productsForm);
                _productsForm.RefreshData(productId);
            };
            _dashboardView.NavigateToLowStockRequested += () => OpenLowStockView();
            _dashboardView.NavigateToReportsRequested += () => _btnNavReports.PerformClick();
            _dashboardView.NavigateToActivityRequested += () => _btnNavActivity.PerformClick();

            // Wire cross-view updates
            _productsForm.QuickStockInRequested += productId => OpenStockView("IN", productId);
            _productsForm.QuickStockOutRequested += productId => OpenStockView("OUT", productId);
            _lowStockForm.OpenFullStockInRequested += productId => OpenStockView("IN", productId);
            _lowStockForm.OpenProductEditRequested += productId =>
            {
                NavigateTo(_btnNavProducts, _productsForm);
                _productsForm.RefreshData(productId);
            };
            _settingsForm.DataRestored += () => _dashboardView.RefreshDashboard();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            NavigateTo(_btnNavDashboard, _dashboardView);
            _dashboardView.RefreshDashboard();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                DialogResult result = MessageBox.Show(
                    "Are you sure you want to close Inventory Pro?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void OpenStockView(string mode, int preselectedProductId)
        {
            NavigateTo(_btnNavStock, _stockTransactionForm);
            _stockTransactionForm.SetTransactionMode(mode, preselectedProductId);
        }

        private void OpenLowStockView()
        {
            NavigateTo(_btnNavLowStock, _lowStockForm);
            _lowStockForm.RefreshData();
        }

        private void NavigateTo(GradientButton activeBtn, UserControl view)
        {
            _activeNavButton = activeBtn;
            foreach (var btn in _navButtons)
            {
                btn.IsOutline = true;
                btn.GradientStart = Color.FromArgb(24, 48, 92);
                btn.GradientEnd = Color.FromArgb(18, 36, 72);
                btn.ForeColor = UITheme.SidebarTextMuted;
                btn.Invalidate();
            }

            activeBtn.IsOutline = false;
            activeBtn.GradientStart = UITheme.ElectricBlue;
            activeBtn.GradientEnd = UITheme.PrimaryBlue;
            activeBtn.ForeColor = Color.White;
            activeBtn.Invalidate();

            _contentHostPanel.SuspendLayout();
            _contentHostPanel.Controls.Clear();
            view.Dock = DockStyle.Fill;
            _contentHostPanel.Controls.Add(view);
            _contentHostPanel.ResumeLayout();
        }

        private void ApplyThemeMode(bool isDark)
        {
            UITheme.IsDarkMode = isDark;
            BackColor = UITheme.CanvasBg;
            _contentHostPanel.BackColor = UITheme.CanvasBg;
            _contentHostPanel.Invalidate(true);

            _dashboardView.ApplyTheme();
            _productsForm.ApplyTheme();
            _categoriesForm.ApplyTheme();
            _suppliersForm.ApplyTheme();
            _stockTransactionForm.ApplyTheme();
            _lowStockForm.ApplyTheme();
            _reportsForm.ApplyTheme();
            _activityHistoryForm.ApplyTheme();
            _settingsForm.ApplyTheme();

            if (_activeNavButton != null)
            {
                _activeNavButton.Invalidate();
            }
        }

        private void RefreshActiveView()
        {
            if (_contentHostPanel.Controls.Count == 0) return;
            Control active = _contentHostPanel.Controls[0];

            if (active == _dashboardView) _dashboardView.RefreshDashboard();
            else if (active == _productsForm) _productsForm.RefreshData();
            else if (active == _categoriesForm) _categoriesForm.RefreshData();
            else if (active == _suppliersForm) _suppliersForm.RefreshData();
            else if (active == _stockTransactionForm) _stockTransactionForm.RefreshData();
            else if (active == _lowStockForm) _lowStockForm.RefreshData();
            else if (active == _reportsForm) _reportsForm.RefreshData();
            else if (active == _activityHistoryForm) _activityHistoryForm.RefreshData();
            else if (active == _settingsForm) _settingsForm.RefreshData();
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "Inventory Pro — Inventory and Stock Management System\n\n" +
                "Platform: C# Windows Forms (.NET 9.0)\n" +
                "Database: Microsoft SQL Server (ADO.NET)\n" +
                "Features: Product Management, Categories, Suppliers,\n" +
                "Stock-In / Stock-Out, Low-Stock Alerts, 7 Reports & CSV Export,\n" +
                "Activity History, and Verified Database Backup & Restore.",
                "About Inventory Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
