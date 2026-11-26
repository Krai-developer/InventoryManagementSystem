using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class LowStockForm : UserControl
    {
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        public event Action? StockRestocked;
        public event Action<int>? OpenFullStockInRequested;
        public event Action<int>? OpenProductEditRequested;

        private ComboBox _cboCategory = null!;
        private CheckBox _chkOnlyOutOfStock = null!;
        private Label _lblAlertSummary = null!;
        private DataGridView _dgvAlerts = null!;
        private NumericUpDown _numQuickRestockQty = null!;
        private Button _btnOpenStockIn = null!;

        private bool _suppressEvents = false;

        public LowStockForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Padding = new Padding(18, 14, 18, 16);

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var topCard = UITheme.CreateCardPanel(UITheme.WarningAmber, topTint: true);
            topCard.Dock = DockStyle.Fill;
            topCard.Margin = new Padding(0, 0, 0, 12);

            topCard.Controls.Add(new Label
            {
                Text = "Low Stock Alerts",
                Font = UITheme.FontSmall,
                ForeColor = Color.FromArgb(251, 191, 36),
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            });

            _lblAlertSummary = new Label
            {
                Text = "Checking inventory...",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 32),
                AutoSize = true
            };

            topCard.Controls.Add(new Label
            {
                Text = "Category:",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(450, 35),
                AutoSize = true
            });

            _cboCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(522, 31),
                Size = new Size(195, 27)
            };
            UITheme.StyleComboBox(_cboCategory);
            _cboCategory.SelectedIndexChanged += (s, e) => LoadAlerts();

            _chkOnlyOutOfStock = new CheckBox
            {
                Text = "Out of stock only",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(734, 33),
                AutoSize = true
            };
            _chkOnlyOutOfStock.CheckedChanged += (s, e) => LoadAlerts();

            topCard.Controls.Add(_lblAlertSummary);
            topCard.Controls.Add(_cboCategory);
            topCard.Controls.Add(_chkOnlyOutOfStock);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            var bottomActionPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(4, 8, 4, 0),
                BackColor = Color.Transparent
            };

            bottomActionPanel.Controls.Add(new Label
            {
                Text = "Restock Quantity:",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(4, 14),
                AutoSize = true
            });

            _numQuickRestockQty = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = 20,
                Location = new Point(135, 11),
                Size = new Size(90, 27)
            };
            UITheme.StyleNumericUpDown(_numQuickRestockQty);

            var btnInstantRestock = new GradientButton
            {
                Text = "Restock Selected",
                Location = new Point(236, 9),
                Size = new Size(155, 32),
                CornerRadius = 8,
                GradientStart = UITheme.TealGlow,
                GradientEnd = UITheme.SuccessGreen
            };
            btnInstantRestock.Click += BtnInstantRestock_Click;

            _btnOpenStockIn = new Button
            {
                Text = "Open Stock In Form",
                Location = new Point(400, 9),
                Size = new Size(155, 32)
            };
            UITheme.StyleSecondaryButton(_btnOpenStockIn);
            _btnOpenStockIn.Click += BtnOpenStockInScreen_Click;

            var btnOpenProduct = new GradientButton
            {
                Text = "Open Affected Product",
                Location = new Point(564, 9),
                Size = new Size(170, 32),
                CornerRadius = 8,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            btnOpenProduct.Click += (s, e) =>
            {
                if (_dgvAlerts.CurrentRow?.Tag is Product p)
                    OpenProductEditRequested?.Invoke(p.ProductID);
                else
                    MessageBox.Show("Select an alert row first.", "Select Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            bottomActionPanel.Controls.Add(_numQuickRestockQty);
            bottomActionPanel.Controls.Add(btnInstantRestock);
            bottomActionPanel.Controls.Add(_btnOpenStockIn);
            bottomActionPanel.Controls.Add(btnOpenProduct);

            _dgvAlerts = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvAlerts);
            _dgvAlerts.SelectionChanged += DgvAlerts_SelectionChanged;
            _dgvAlerts.CellPainting += DgvAlerts_CellPainting;
            _dgvAlerts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && _dgvAlerts.Rows[e.RowIndex].Tag is Product p)
                    OpenProductEditRequested?.Invoke(p.ProductID);
            };

            gridCard.Controls.Add(_dgvAlerts);
            gridCard.Controls.Add(bottomActionPanel);

            mainLayout.Controls.Add(topCard, 0, 0);
            mainLayout.Controls.Add(gridCard, 0, 1);
            Controls.Add(mainLayout);
        }

        public void ApplyTheme()
        {
            _lblAlertSummary.ForeColor = UITheme.TextPrimary;
            _chkOnlyOutOfStock.ForeColor = UITheme.TextSecondary;
            UITheme.StyleComboBox(_cboCategory);
            UITheme.StyleNumericUpDown(_numQuickRestockQty);
            UITheme.StyleSecondaryButton(_btnOpenStockIn);
            UITheme.StyleDataGridView(_dgvAlerts);
            Invalidate(true);
        }

        public void RefreshData()
        {
            try
            {
                _suppressEvents = true;
                List<Category> categories = _categoryRepo.GetAllCategories();
                _cboCategory.Items.Clear();
                _cboCategory.Items.Add(new Category(0, "All Categories"));
                foreach (var cat in categories) _cboCategory.Items.Add(cat);
                _cboCategory.SelectedIndex = 0;
                _suppressEvents = false;

                LoadAlerts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading low-stock items:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadAlerts()
        {
            if (_suppressEvents) return;

            int catId = (_cboCategory.SelectedItem as Category)?.CategoryID ?? 0;
            bool onlyOut = _chkOnlyOutOfStock.Checked;

            List<Product> alerts = _productRepo.GetLowStockProducts(catId, onlyOut);

            _dgvAlerts.Rows.Clear();
            _dgvAlerts.Columns.Clear();

            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductID", HeaderText = "ID", Visible = false });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 16 });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 16 });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product Name", FillWeight = 30 });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 18 });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentStock",
                HeaderText = "Current Stock",
                FillWeight = 13,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
            });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ReorderLevel",
                HeaderText = "Reorder Level",
                FillWeight = 13,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SuggestedOrder",
                HeaderText = "Suggested Order",
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
            });
            _dgvAlerts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 20 });

            int outOfStockCount = 0;
            foreach (var p in alerts)
            {
                if (p.QuantityInStock == 0) outOfStockCount++;
                int suggestedOrder = Math.Max(10, (p.ReorderLevel * 2) - p.QuantityInStock);

                int idx = _dgvAlerts.Rows.Add(
                    p.ProductID,
                    p.StockStatus,
                    p.SKU,
                    p.ProductName,
                    p.CategoryName,
                    p.QuantityInStock,
                    p.ReorderLevel,
                    suggestedOrder,
                    p.SupplierName);
                _dgvAlerts.Rows[idx].Tag = p;
            }

            _lblAlertSummary.Text = alerts.Count > 0
                ? $"{alerts.Count} item(s) at or below reorder level ({outOfStockCount} out of stock)"
                : "All products are stocked above reorder levels";
        }

        private void DgvAlerts_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvAlerts.CurrentRow?.Tag is Product p)
            {
                int suggested = Math.Max(10, (p.ReorderLevel * 2) - p.QuantityInStock);
                _numQuickRestockQty.Value = suggested;
            }
        }

        private void BtnInstantRestock_Click(object? sender, EventArgs e)
        {
            if (_dgvAlerts.CurrentRow?.Tag is not Product p)
            {
                MessageBox.Show("Select a product row first.", "Select Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int qty = (int)_numQuickRestockQty.Value;
            string refNo = $"PO-{DateTime.Now:yyyy}-{Random.Shared.Next(100, 999)}";

            if (_txRepo.RecordStockMovement(p.ProductID, "IN", qty, refNo, "Restocked from Low Stock screen", out string msg))
            {
                MessageBox.Show(msg, "Restocked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadAlerts();
                StockRestocked?.Invoke();
            }
            else
            {
                MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpenStockInScreen_Click(object? sender, EventArgs e)
        {
            if (_dgvAlerts.CurrentRow?.Tag is Product p)
                OpenFullStockInRequested?.Invoke(p.ProductID);
            else
                MessageBox.Show("Select a product row first.", "Select Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DgvAlerts_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_dgvAlerts.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                UITheme.PaintStatusPillCell(e, e.Value.ToString());
            }
        }
    }
}
