using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    /// <summary>
    /// Main Dashboard matching the reference dark navy & electric-blue glass layout:
    /// - Welcome header with user info and live date/time
    /// - 4 glowing glass summary cards (Total Products, Total Stock, Low Stock Items, Total Value)
    /// - Center Inventory List with search, category filter, pill status badges, action buttons, and pagination
    /// - Right Stock Overview Donut Chart + 2x2 Quick Actions (Add, Search, Update, Delete)
    /// - Bottom Recent Activity (real SQL transactions) + Inventory Info card
    /// </summary>
    public class DashboardView : UserControl
    {
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly ActivityLogRepository _activityRepo = new ActivityLogRepository();

        public event Action? NavigateToProductsRequested;
        public event Action<int>? NavigateToProductEditRequested;
        public event Action? NavigateToLowStockRequested;
        public event Action? NavigateToReportsRequested;
        public event Action? NavigateToActivityRequested;

        // Header Controls
        private Label _lblWelcome = null!;
        private Label _lblWelcomeSub = null!;
        private Label _lblAdminName = null!;
        private Label _lblDateTime = null!;

        // Summary KPI Labels
        private Label _lblTotalProductsValue = null!;
        private Label _lblTotalProductsMeta = null!;
        private Label _lblTotalStockValue = null!;
        private Label _lblTotalStockMeta = null!;
        private Label _lblLowStockValue = null!;
        private Label _lblTotalValueAmount = null!;
        private Label _lblTotalValueMeta = null!;

        // Inventory List Controls + Pagination
        private TextBox _txtSearch = null!;
        private ComboBox _cboCategory = null!;
        private DataGridView _dgvInventory = null!;
        private Label _lblPageInfo = null!;
        private Button _btnPrevPage = null!;
        private Button _btnCurrentPage = null!;
        private Button _btnNextPage = null!;

        private List<Product> _filteredProducts = new List<Product>();
        private int _currentPage = 1;
        private const int PageSize = 5;
        private bool _suppressFilters = false;

        // Right Stock Overview Donut Chart
        private DonutChartControl _donutChart = null!;
        private Label _lblLegendInStock = null!;
        private Label _lblLegendLowStock = null!;
        private Label _lblLegendOutStock = null!;

        // Bottom Recent Activity + Info Panel
        private Panel _pnlActivityList = null!;
        private Label _lblInfoSummary = null!;

        public DashboardView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Padding = new Padding(18, 12, 18, 14);

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));   // Row 0: Welcome Header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));  // Row 1: 4 Summary Cards
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // Row 2: Inventory List + Right Panels
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));  // Row 3: Bottom Activity + Info Card

            // =================================================================
            // ROW 0: DASHBOARD WELCOME HEADER
            // =================================================================
            var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            headerPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                // Left electric-blue vertical accent bar
                using (SolidBrush bar = new SolidBrush(UITheme.ElectricBlue))
                {
                    e.Graphics.FillRectangle(bar, 2, 6, 4, 42);
                }

                // Right circular User Avatar + online dot
                int avX = headerPanel.Width - 206;
                int avY = 8;
                Rectangle avRect = new Rectangle(avX, avY, 38, 38);
                using (Pen ring = new Pen(UITheme.ElectricBlue, 2f))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 40, 82)))
                {
                    e.Graphics.FillEllipse(fill, avRect);
                    e.Graphics.DrawEllipse(ring, avRect);
                }
                // Simple user silhouette inside circle
                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(white, avX + 13, avY + 8, 12, 12);
                    e.Graphics.FillPie(white, avX + 8, avY + 21, 22, 18, 180, 180);
                }
                using (SolidBrush onlineDot = new SolidBrush(UITheme.TealGlow))
                {
                    e.Graphics.FillEllipse(onlineDot, avX + 28, avY + 28, 9, 9);
                }
            };

            _lblWelcome = new Label
            {
                Text = "Welcome back!",
                Font = UITheme.FontWelcome,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(14, 2),
                AutoSize = true
            };

            _lblWelcomeSub = new Label
            {
                Text = "Manage your inventory efficiently and keep track of your stock.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(16, 33),
                AutoSize = true
            };

            _lblAdminName = new Label
            {
                Text = "Administrator",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerPanel.Width - 158, 9),
                AutoSize = true
            };

            _lblDateTime = new Label
            {
                Text = DateTime.Now.ToString("MMM dd, yyyy  •  hh:mm tt"),
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerPanel.Width - 158, 28),
                AutoSize = true
            };

            headerPanel.Controls.Add(_lblWelcome);
            headerPanel.Controls.Add(_lblWelcomeSub);
            headerPanel.Controls.Add(_lblAdminName);
            headerPanel.Controls.Add(_lblDateTime);
            root.Controls.Add(headerPanel, 0, 0);

            // =================================================================
            // ROW 1: 4 GLOWING GLASS SUMMARY CARDS
            // =================================================================
            var kpiGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 12),
                BackColor = Color.Transparent
            };
            for (int i = 0; i < 4; i++)
                kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            kpiGrid.Controls.Add(CreateKpiGlassCard(
                "Total Products", "0", "0 categories", "CUBE",
                UITheme.ElectricBlue,
                out _lblTotalProductsValue, out _lblTotalProductsMeta,
                new Padding(0, 0, 8, 0)), 0, 0);

            kpiGrid.Controls.Add(CreateKpiGlassCard(
                "Total Stock", "0", "Units on hand", "WAREHOUSE",
                UITheme.TealGlow,
                out _lblTotalStockValue, out _lblTotalStockMeta,
                new Padding(5, 0, 5, 0)), 1, 0);

            kpiGrid.Controls.Add(CreateLowStockKpiCard(new Padding(5, 0, 5, 0)), 2, 0);

            kpiGrid.Controls.Add(CreateKpiGlassCard(
                "Total Value", "₱0.00", "Current valuation", "CURRENCY",
                UITheme.PurpleAccent,
                out _lblTotalValueAmount, out _lblTotalValueMeta,
                new Padding(8, 0, 0, 0)), 3, 0);

            root.Controls.Add(kpiGrid, 0, 1);

            // =================================================================
            // ROW 2: MIDDLE SPLIT (INVENTORY LIST 70% | RIGHT PANELS 30%)
            // =================================================================
            var middleSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.Transparent
            };
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            // --- LEFT: INVENTORY LIST GLASS CARD ---
            var invCard = UITheme.CreateCardPanel();
            invCard.Dock = DockStyle.Fill;
            invCard.Margin = new Padding(0, 0, 10, 0);
            invCard.Padding = new Padding(14, 12, 14, 10);

            var invTopBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
            invTopBar.Paint += (s, e) => UITheme.DrawIsometricCube(e.Graphics, new RectangleF(2, 6, 24, 24), glowing: false);

            var lblInvTitle = new Label
            {
                Text = "Inventory List",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(32, 8),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search products...",
                Location = new Point(220, 7),
                Size = new Size(210, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) =>
            {
                _currentPage = 1;
                ApplyInventoryFilter();
            };

            _cboCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(440, 7),
                Size = new Size(165, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            UITheme.StyleComboBox(_cboCategory);
            _cboCategory.SelectedIndexChanged += (s, e) =>
            {
                _currentPage = 1;
                ApplyInventoryFilter();
            };

            invTopBar.Resize += (s, e) =>
            {
                _cboCategory.Left = invTopBar.Width - _cboCategory.Width - 4;
                _txtSearch.Left = _cboCategory.Left - _txtSearch.Width - 10;
            };

            invTopBar.Controls.Add(lblInvTitle);
            invTopBar.Controls.Add(_txtSearch);
            invTopBar.Controls.Add(_cboCategory);

            // Pagination Footer Bar
            var invFooter = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.Transparent };
            _lblPageInfo = new Label
            {
                Text = "Showing 0 of 0 items",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(2, 10),
                AutoSize = true
            };

            _btnNextPage = new Button { Text = ">", Size = new Size(30, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(invFooter.Width - 34, 6) };
            UITheme.StyleSecondaryButton(_btnNextPage);
            _btnNextPage.Click += (s, e) => ChangePage(1);

            _btnCurrentPage = new Button { Text = "1", Size = new Size(30, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(invFooter.Width - 68, 6) };
            UITheme.StyleButton(_btnCurrentPage, UITheme.PrimaryBlue);

            _btnPrevPage = new Button { Text = "<", Size = new Size(30, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(invFooter.Width - 102, 6) };
            UITheme.StyleSecondaryButton(_btnPrevPage);
            _btnPrevPage.Click += (s, e) => ChangePage(-1);

            invFooter.Controls.Add(_lblPageInfo);
            invFooter.Controls.Add(_btnPrevPage);
            invFooter.Controls.Add(_btnCurrentPage);
            invFooter.Controls.Add(_btnNextPage);

            _dgvInventory = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvInventory);
            _dgvInventory.CellPainting += DgvInventory_CellPainting;
            _dgvInventory.CellClick += DgvInventory_CellClick;
            _dgvInventory.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && _dgvInventory.Rows[e.RowIndex].Tag is Product p)
                {
                    using var dlg = new ProductDetailsDialog(p);
                    dlg.ShowDialog(FindForm());
                }
            };

            invCard.Controls.Add(_dgvInventory);
            invCard.Controls.Add(invFooter);
            invCard.Controls.Add(invTopBar);

            // --- RIGHT STACK: STOCK OVERVIEW DONUT + QUICK ACTIONS 2x2 ---
            var rightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));

            // Right Top: Stock Overview Donut Card
            var overviewCard = UITheme.CreateCardPanel();
            overviewCard.Dock = DockStyle.Fill;
            overviewCard.Margin = new Padding(0, 0, 0, 10);
            overviewCard.Padding = new Padding(14, 12, 14, 10);

            var lblOverviewTitle = new Label
            {
                Text = "Stock Overview",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(14, 10),
                AutoSize = true
            };

            _donutChart = new DonutChartControl
            {
                Location = new Point(12, 38),
                Size = new Size(134, 134)
            };

            int legX = 158;
            int legY = 58;
            overviewCard.Controls.Add(CreateLegendRow("In Stock", UITheme.ElectricBlue, legX, legY, out _lblLegendInStock)); legY += 32;
            overviewCard.Controls.Add(CreateLegendRow("Low Stock", UITheme.WarningAmber, legX, legY, out _lblLegendLowStock)); legY += 32;
            overviewCard.Controls.Add(CreateLegendRow("Out of Stock", UITheme.DangerRed, legX, legY, out _lblLegendOutStock));

            overviewCard.Controls.Add(lblOverviewTitle);
            overviewCard.Controls.Add(_donutChart);

            // Right Bottom: Quick Actions 2x2 Grid Card
            var quickCard = UITheme.CreateCardPanel();
            quickCard.Dock = DockStyle.Fill;
            quickCard.Padding = new Padding(14, 10, 14, 12);

            var lblQuickTitle = new Label
            {
                Text = "Quick Actions",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Dock = DockStyle.Top,
                Height = 28
            };

            var qaGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            qaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            qaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            qaGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            qaGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var btnAdd = CreateQuickActionTile("Add Product", Color.FromArgb(37, 99, 235), Color.FromArgb(29, 78, 216));
            btnAdd.Click += (s, e) => NavigateToProductsRequested?.Invoke();

            var btnSearch = CreateQuickActionTile("Search Product", Color.FromArgb(16, 185, 129), Color.FromArgb(5, 150, 105));
            btnSearch.Click += (s, e) =>
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
            };

            var btnUpdate = CreateQuickActionTile("Update Product", Color.FromArgb(124, 58, 237), Color.FromArgb(91, 33, 182));
            btnUpdate.Click += (s, e) =>
            {
                if (_dgvInventory.CurrentRow?.Tag is Product p)
                    NavigateToProductEditRequested?.Invoke(p.ProductID);
                else
                    NavigateToProductsRequested?.Invoke();
            };

            var btnDelete = CreateQuickActionTile("Delete Product", Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28));
            btnDelete.Enabled = SessionManager.CanDeleteProducts;
            btnDelete.Visible = SessionManager.CanDeleteProducts;
            btnDelete.Click += (s, e) => DeleteSelectedFromDashboard();

            qaGrid.Controls.Add(btnAdd, 0, 0);
            qaGrid.Controls.Add(btnSearch, 1, 0);
            qaGrid.Controls.Add(btnUpdate, 0, 1);
            qaGrid.Controls.Add(btnDelete, 1, 1);

            quickCard.Controls.Add(qaGrid);
            quickCard.Controls.Add(lblQuickTitle);

            rightStack.Controls.Add(overviewCard, 0, 0);
            rightStack.Controls.Add(quickCard, 0, 1);

            middleSplit.Controls.Add(invCard, 0, 0);
            middleSplit.Controls.Add(rightStack, 1, 0);
            root.Controls.Add(middleSplit, 0, 2);

            // =================================================================
            // ROW 3: BOTTOM SPLIT (RECENT ACTIVITY 58% | INFO CARD 42%)
            // =================================================================
            var bottomSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            // Bottom Left: Recent Activity Glass Card
            var actCard = UITheme.CreateCardPanel();
            actCard.Dock = DockStyle.Fill;
            actCard.Margin = new Padding(0, 0, 10, 0);
            actCard.Padding = new Padding(16, 12, 16, 10);

            var actTop = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
            var lblActTitle = new Label
            {
                Text = "Recent Activity",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(0, 2),
                AutoSize = true
            };
            var lnkViewAll = new LinkLabel
            {
                Text = "View All ->",
                Font = UITheme.FontSmall,
                LinkColor = UITheme.ElectricBlue,
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(actTop.Width - 76, 4),
                AutoSize = true,
                Visible = SessionManager.CanViewActivityHistory
            };
            lnkViewAll.LinkClicked += (s, e) =>
            {
                if (!SessionManager.CanViewActivityHistory) return;
                if (NavigateToActivityRequested != null) NavigateToActivityRequested.Invoke();
                else NavigateToReportsRequested?.Invoke();
            };

            actTop.Controls.Add(lblActTitle);
            actTop.Controls.Add(lnkViewAll);

            _pnlActivityList = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = false
            };

            actCard.Controls.Add(_pnlActivityList);
            actCard.Controls.Add(actTop);

            // Bottom Right: Inventory Information Glass Card
            var infoCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            infoCard.Dock = DockStyle.Fill;
            infoCard.Padding = new Padding(16, 14, 16, 12);
            infoCard.Paint += (s, e) =>
            {
                // Draw clean vector box + checklist illustration on the left side of the card
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                UITheme.DrawIsometricCube(g, new RectangleF(18, 30, 74, 74), glowing: true);
            };

            var lblInfoTitle = new Label
            {
                Text = "Keep Your Inventory in Check",
                Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(110, 18),
                AutoSize = true
            };

            _lblInfoSummary = new Label
            {
                Text = "Track stock levels, manage products, and get alerts for low stock items.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(110, 44),
                Size = new Size(320, 36)
            };

            var btnManage = new GradientButton
            {
                Text = "Manage Inventory  ->",
                Location = new Point(110, 90),
                Size = new Size(176, 34),
                CornerRadius = 17,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            btnManage.Click += (s, e) => NavigateToProductsRequested?.Invoke();

            infoCard.Controls.Add(lblInfoTitle);
            infoCard.Controls.Add(_lblInfoSummary);
            infoCard.Controls.Add(btnManage);

            bottomSplit.Controls.Add(actCard, 0, 0);
            bottomSplit.Controls.Add(infoCard, 1, 0);
            root.Controls.Add(bottomSplit, 0, 3);

            Controls.Add(root);
        }

        private GlassCardPanel CreateKpiGlassCard(
            string labelText,
            string initialValue,
            string metaText,
            string iconKind,
            Color glowColor,
            out Label valueLabel,
            out Label metaLabel,
            Padding margin)
        {
            var card = UITheme.CreateCardPanel(glowColor, topTint: true);
            card.Dock = DockStyle.Fill;
            card.Margin = margin;

            var iconBox = new Panel
            {
                Location = new Point(16, 20),
                Size = new Size(52, 52),
                BackColor = Color.Transparent
            };
            iconBox.Paint += (s, e) => PaintKpiIcon(e.Graphics, iconKind, glowColor);

            valueLabel = new Label
            {
                Text = initialValue,
                Font = UITheme.FontCardValue,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(80, 16),
                AutoSize = true
            };

            var lblCaption = new Label
            {
                Text = labelText,
                Font = UITheme.FontCardLabel,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(82, 54),
                AutoSize = true
            };

            metaLabel = new Label
            {
                Text = metaText,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = glowColor,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(card.Width - 110, card.Height - 28),
                AutoSize = true
            };

            card.Controls.Add(iconBox);
            card.Controls.Add(valueLabel);
            card.Controls.Add(lblCaption);
            card.Controls.Add(metaLabel);
            return card;
        }

        private GlassCardPanel CreateLowStockKpiCard(Padding margin)
        {
            var card = UITheme.CreateCardPanel(UITheme.WarningAmber, topTint: true);
            card.Dock = DockStyle.Fill;
            card.Margin = margin;

            var iconBox = new Panel
            {
                Location = new Point(16, 20),
                Size = new Size(52, 52),
                BackColor = Color.Transparent
            };
            iconBox.Paint += (s, e) => PaintKpiIcon(e.Graphics, "WARNING", UITheme.WarningAmber);

            _lblLowStockValue = new Label
            {
                Text = "0",
                Font = UITheme.FontCardValue,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(80, 16),
                AutoSize = true
            };

            var lblCaption = new Label
            {
                Text = "Low Stock Items",
                Font = UITheme.FontCardLabel,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(82, 54),
                AutoSize = true
            };

            var lnkDetails = new LinkLabel
            {
                Text = "View Details ->",
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                LinkColor = Color.FromArgb(251, 191, 36),
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(card.Width - 102, card.Height - 28),
                AutoSize = true
            };
            lnkDetails.LinkClicked += (s, e) => NavigateToLowStockRequested?.Invoke();

            card.Controls.Add(iconBox);
            card.Controls.Add(_lblLowStockValue);
            card.Controls.Add(lblCaption);
            card.Controls.Add(lnkDetails);
            return card;
        }

        private void PaintKpiIcon(Graphics g, string kind, Color accent)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circle = new Rectangle(4, 4, 44, 44);
            using (SolidBrush softFill = new SolidBrush(Color.FromArgb(42, accent)))
            using (Pen glowRing = new Pen(Color.FromArgb(175, accent), 1.8f))
            {
                g.FillEllipse(softFill, circle);
                g.DrawEllipse(glowRing, circle);
            }

            if (kind == "CUBE")
            {
                UITheme.DrawIsometricCube(g, new RectangleF(13, 13, 26, 26), glowing: false);
            }
            else if (kind == "WAREHOUSE")
            {
                using (SolidBrush b = new SolidBrush(accent))
                {
                    g.FillPolygon(b, new[] { new PointF(26, 13), new PointF(38, 22), new PointF(14, 22) });
                    g.FillRectangle(b, 16, 22, 20, 15);
                }
                using (SolidBrush door = new SolidBrush(Color.FromArgb(14, 28, 52)))
                {
                    g.FillRectangle(door, 22, 27, 8, 10);
                }
            }
            else if (kind == "WARNING")
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(245, 158, 11)))
                {
                    g.FillPolygon(b, new[] { new PointF(26, 13), new PointF(39, 36), new PointF(13, 36) });
                }
                using (Font f = new Font("Segoe UI", 12F, FontStyle.Bold))
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(24, 18, 8)))
                {
                    g.DrawString("!", f, dark, 21.5f, 17.5f);
                }
            }
            else
            {
                using (Font f = new Font("Segoe UI", 16F, FontStyle.Bold))
                using (SolidBrush white = new SolidBrush(Color.White))
                {
                    g.DrawString("₱", f, white, 16.5f, 11.5f);
                }
            }
        }

        private Panel CreateLegendRow(string text, Color dotColor, int x, int y, out Label countLabel)
        {
            var row = new Panel { Location = new Point(x, y), Size = new Size(165, 24), BackColor = Color.Transparent };
            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(dotColor))
                {
                    e.Graphics.FillEllipse(b, 2, 7, 10, 10);
                }
            };

            var lblName = new Label
            {
                Text = text,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(18, 4),
                AutoSize = true
            };

            countLabel = new Label
            {
                Text = "0",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(115, 3),
                AutoSize = true
            };

            row.Controls.Add(lblName);
            row.Controls.Add(countLabel);
            return row;
        }

        private GradientButton CreateQuickActionTile(string text, Color gradStart, Color gradEnd)
        {
            return new GradientButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                CornerRadius = 10,
                GradientStart = gradStart,
                GradientEnd = gradEnd,
                Font = UITheme.FontBold
            };
        }

        public void ApplyTheme()
        {
            _lblWelcome.ForeColor = UITheme.TextPrimary;
            _lblWelcomeSub.ForeColor = UITheme.TextMuted;
            _lblAdminName.ForeColor = UITheme.TextPrimary;
            _lblDateTime.ForeColor = UITheme.TextMuted;

            _lblTotalProductsValue.ForeColor = UITheme.TextPrimary;
            _lblTotalStockValue.ForeColor = UITheme.TextPrimary;
            _lblLowStockValue.ForeColor = UITheme.TextPrimary;
            _lblTotalValueAmount.ForeColor = UITheme.TextPrimary;

            UITheme.StyleTextBox(_txtSearch);
            UITheme.StyleComboBox(_cboCategory);
            UITheme.StyleDataGridView(_dgvInventory);
            UITheme.StyleSecondaryButton(_btnPrevPage);
            UITheme.StyleSecondaryButton(_btnNextPage);

            _donutChart.Invalidate();
            Invalidate(true);
            RefreshDashboard();
        }

        public void RefreshDashboard()
        {
            try
            {
                _lblDateTime.Text = DateTime.Now.ToString("MMM dd, yyyy  •  hh:mm tt");
                if (SessionManager.CurrentUser != null)
                {
                    _lblAdminName.Text = $"{SessionManager.CurrentUser.FullName} ({SessionManager.CurrentUser.Role})";
                }

                // 1. Load live KPI metrics
                DashboardSummary summary = _txRepo.GetDashboardSummary();
                _lblTotalProductsValue.Text = summary.TotalProducts.ToString("N0");
                _lblTotalProductsMeta.Text = $"{summary.TotalCategories} categories";

                _lblTotalStockValue.Text = summary.TotalUnitsInStock.ToString("N0");
                _lblTotalStockMeta.Text = "Units on hand";

                _lblLowStockValue.Text = summary.LowStockCount.ToString("N0");

                _lblTotalValueAmount.Text = UITheme.FormatCurrency(summary.TotalInventoryValue);
                _lblTotalValueMeta.Text = $"{summary.TodayTransactionsCount} moves today";

                // 2. Update Donut Chart + Legend
                int outCount = summary.OutOfStockCount;
                int lowOnly = Math.Max(0, summary.LowStockCount - summary.OutOfStockCount);
                int inStock = Math.Max(0, summary.TotalProducts - summary.LowStockCount);

                _donutChart.SetData(inStock, lowOnly, outCount);
                _lblLegendInStock.Text = inStock.ToString();
                _lblLegendLowStock.Text = lowOnly.ToString();
                _lblLegendOutStock.Text = outCount.ToString();

                _lblInfoSummary.Text = summary.LowStockCount > 0
                    ? $"You currently have {summary.LowStockCount} low-stock item(s) across {summary.TotalProducts} products."
                    : $"All {summary.TotalProducts} products are currently healthy and above reorder thresholds.";

                // 3. Populate Category Filter
                _suppressFilters = true;
                int prevCatId = (_cboCategory.SelectedItem as Category)?.CategoryID ?? 0;
                List<Category> cats = _categoryRepo.GetAllCategories();
                _cboCategory.Items.Clear();
                _cboCategory.Items.Add(new Category(0, "All Categories"));
                foreach (var c in cats) _cboCategory.Items.Add(c);
                _cboCategory.SelectedIndex = 0;
                for (int i = 0; i < _cboCategory.Items.Count; i++)
                {
                    if (((Category)_cboCategory.Items[i]!).CategoryID == prevCatId)
                    {
                        _cboCategory.SelectedIndex = i;
                        break;
                    }
                }
                _suppressFilters = false;

                ApplyInventoryFilter();
                LoadRecentActivity();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing dashboard:\n{ex.Message}", "Dashboard Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyInventoryFilter()
        {
            if (_suppressFilters) return;

            string kw = _txtSearch.Text.Trim();
            int catId = (_cboCategory.SelectedItem as Category)?.CategoryID ?? 0;
            _filteredProducts = _productRepo.SearchProducts(kw, catId, "ALL");

            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredProducts.Count / (double)PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            RenderInventoryPage();
        }

        private void ChangePage(int delta)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredProducts.Count / (double)PageSize));
            int next = _currentPage + delta;
            if (next >= 1 && next <= totalPages)
            {
                _currentPage = next;
                RenderInventoryPage();
            }
        }

        private void RenderInventoryPage()
        {
            int total = _filteredProducts.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            var pageItems = _filteredProducts.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

            _dgvInventory.Rows.Clear();
            _dgvInventory.Columns.Clear();

            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "ID", FillWeight = 14 });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product Name", FillWeight = 26 });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 18 });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Quantity",
                HeaderText = "Quantity",
                FillWeight = 11,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
            });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price",
                HeaderText = "Price (₱)",
                FillWeight = 14,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 16 });
            _dgvInventory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "Actions",
                FillWeight = 14,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = UITheme.ElectricBlue, Font = UITheme.FontSmall }
            });

            foreach (var p in pageItems)
            {
                int idx = _dgvInventory.Rows.Add(
                    p.SKU,
                    p.ProductName,
                    p.CategoryName,
                    p.QuantityInStock,
                    p.UnitPrice.ToString("N2"),
                    p.StockStatus,
                    SessionManager.CanDeleteProducts ? "Edit  |  Del" : "Edit");
                _dgvInventory.Rows[idx].Tag = p;
            }

            int start = total == 0 ? 0 : ((_currentPage - 1) * PageSize) + 1;
            int end = Math.Min(_currentPage * PageSize, total);
            _lblPageInfo.Text = $"Showing {start} - {end} of {total} items";
            _btnCurrentPage.Text = _currentPage.ToString();
            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < totalPages;
        }

        private void DgvInventory_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_dgvInventory.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                UITheme.PaintStatusPillCell(e, e.Value.ToString());
            }
        }

        private void DgvInventory_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvInventory.Columns[e.ColumnIndex].Name != "Actions") return;
            if (_dgvInventory.Rows[e.RowIndex].Tag is not Product p) return;

            // Determine whether user clicked left half ("Edit") or right half ("Del") of the Actions cell
            Rectangle cellRect = _dgvInventory.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            Point cursorPos = _dgvInventory.PointToClient(Cursor.Position);
            bool clickedRightHalf = cursorPos.X > cellRect.X + (cellRect.Width / 2);

            if (clickedRightHalf && SessionManager.CanDeleteProducts)
            {
                ConfirmAndDeleteProduct(p);
            }
            else
            {
                NavigateToProductEditRequested?.Invoke(p.ProductID);
            }
        }

        private void DeleteSelectedFromDashboard()
        {
            if (!SessionManager.CanDeleteProducts)
            {
                MessageBox.Show("Access denied: Only the Administrator can delete products.",
                    "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_dgvInventory.CurrentRow?.Tag is Product p)
            {
                ConfirmAndDeleteProduct(p);
            }
            else
            {
                MessageBox.Show("Select a product row in the Inventory List first to delete it.",
                    "Select Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ConfirmAndDeleteProduct(Product p)
        {
            if (!SessionManager.CanDeleteProducts)
            {
                MessageBox.Show("Access denied: Only the Administrator can delete products.",
                    "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult confirm = MessageBox.Show(
                $"Delete '{p.ProductName}' ({p.SKU}) from inventory?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    _productRepo.DeleteProduct(p.ProductID);
                    RefreshDashboard();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting product:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadRecentActivity()
        {
            _pnlActivityList.Controls.Clear();

            if (!SessionManager.CanViewActivityHistory)
            {
                var lblRestricted = new Label
                {
                    Text = "Activity history is visible to the Administrator only.",
                    Font = UITheme.FontSmall,
                    ForeColor = UITheme.TextMuted,
                    BackColor = Color.Transparent,
                    Location = new Point(4, 18),
                    AutoSize = true
                };
                _pnlActivityList.Controls.Add(lblRestricted);
                return;
            }

            List<ActivityLog> logs = _activityRepo.GetActivities(maxRows: 3);

            if (logs.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No recent activity yet. Add products, categories, suppliers, or record stock movements.",
                    Font = UITheme.FontSmall,
                    ForeColor = UITheme.TextMuted,
                    BackColor = Color.Transparent,
                    Location = new Point(4, 18),
                    AutoSize = true
                };
                _pnlActivityList.Controls.Add(lblEmpty);
                return;
            }

            int y = 6;
            foreach (var log in logs)
            {
                Color dotColor = UITheme.ElectricBlue;
                if (log.Action.Contains("Added") || log.Action.Contains("Received") || log.Action.Contains("Backup"))
                    dotColor = UITheme.TealGlow;
                else if (log.Action.Contains("Updated") || log.Action.Contains("Released") || log.Action.Contains("Restored"))
                    dotColor = UITheme.WarningAmber;
                else if (log.Action.Contains("Deleted") || log.Action.Contains("Cleared"))
                    dotColor = UITheme.DangerRed;

                var row = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(_pnlActivityList.Width - 6, 28),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.Transparent
                };
                row.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush b = new SolidBrush(dotColor))
                    {
                        e.Graphics.FillEllipse(b, 4, 9, 9, 9);
                    }
                };

                var lblDesc = new Label
                {
                    Text = $"{log.Action}: {log.RelatedRecord}",
                    Font = UITheme.FontSmall,
                    ForeColor = UITheme.TextPrimary,
                    BackColor = Color.Transparent,
                    Location = new Point(22, 5),
                    AutoSize = true
                };

                var lblTime = new Label
                {
                    Text = log.ActivityDate.ToString("MMM dd, yyyy  •  hh:mm tt"),
                    Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                    ForeColor = UITheme.TextMuted,
                    BackColor = Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(row.Width - 175, 5),
                    AutoSize = true
                };

                row.Controls.Add(lblDesc);
                row.Controls.Add(lblTime);
                _pnlActivityList.Controls.Add(row);
                y += 30;
            }
        }
    }
}
