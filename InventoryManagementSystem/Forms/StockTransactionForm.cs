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
    public class StockTransactionForm : UserControl
    {
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly SupplierRepository _supplierRepo = new SupplierRepository();
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        public event Action? StockMovementRecorded;

        private string _transactionType = "IN";

        private GlassCardPanel _entryCard = null!;
        private GradientButton _btnModeStockIn = null!;
        private GradientButton _btnModeStockOut = null!;
        private GradientButton _btnModeAdjust = null!;
        private Label _lblFormBanner = null!;
        private Label _lblQuantityCaption = null!;
        private DateTimePicker _dtpTxDate = null!;

        private ComboBox _cboProduct = null!;
        private Label _lblSupplierCaption = null!;
        private ComboBox _cboSupplier = null!;
        private Panel _previewBox = null!;
        private Label _lblSkuValue = null!;
        private Label _lblCategoryValue = null!;
        private Label _lblCurrentStockValue = null!;
        private Label _lblProjectedStockValue = null!;
        private NumericUpDown _numQuantity = null!;
        private TextBox _txtReferenceNo = null!;
        private TextBox _txtRemarks = null!;
        private GradientButton _btnSubmit = null!;

        private DataGridView _dgvHistory = null!;

        public StockTransactionForm()
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
            var rootSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 396F));
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _entryCard = UITheme.CreateCardPanel(UITheme.TealGlow, topTint: true);
            _entryCard.Dock = DockStyle.Fill;
            _entryCard.Margin = new Padding(0, 0, 12, 0);
            _entryCard.AutoScroll = true;

            int y = 14;

            bool canAdjust = SessionManager.CanAdjustStock;
            int modeW = canAdjust ? 112 : 170;

            _btnModeStockIn = new GradientButton
            {
                Text = "Stock In",
                Location = new Point(16, y),
                Size = new Size(modeW, 36),
                CornerRadius = 9
            };
            _btnModeStockIn.Click += (s, e) => SetTransactionMode("IN");

            _btnModeStockOut = new GradientButton
            {
                Text = "Stock Out",
                Location = new Point(16 + modeW + 6, y),
                Size = new Size(modeW, 36),
                CornerRadius = 9
            };
            _btnModeStockOut.Click += (s, e) => SetTransactionMode("OUT");

            _btnModeAdjust = new GradientButton
            {
                Text = "Adjust",
                Location = new Point(16 + (modeW + 6) * 2, y),
                Size = new Size(modeW, 36),
                CornerRadius = 9,
                Visible = canAdjust
            };
            _btnModeAdjust.Click += (s, e) => SetTransactionMode("ADJUST");

            _entryCard.Controls.Add(_btnModeStockIn);
            _entryCard.Controls.Add(_btnModeStockOut);
            _entryCard.Controls.Add(_btnModeAdjust);
            y += 46;

            _lblFormBanner = new Label
            {
                Text = "Record Incoming Stock",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            _entryCard.Controls.Add(_lblFormBanner);
            y += 30;

            _entryCard.Controls.Add(CreateLabel("Product *", 16, y));
            y += 19;
            _cboProduct = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, y),
                Size = new Size(348, 27)
            };
            UITheme.StyleComboBox(_cboProduct);
            _cboProduct.SelectedIndexChanged += (s, e) => UpdateProductPreview();
            _entryCard.Controls.Add(_cboProduct);
            y += 34;

            _lblSupplierCaption = CreateLabel("Supplier (for Stock In)", 16, y);
            _entryCard.Controls.Add(_lblSupplierCaption);
            y += 19;
            _cboSupplier = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, y),
                Size = new Size(348, 27)
            };
            UITheme.StyleComboBox(_cboSupplier);
            _entryCard.Controls.Add(_cboSupplier);
            y += 36;

            _previewBox = new Panel
            {
                BackColor = UITheme.InputSurface,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(16, y),
                Size = new Size(348, 86)
            };

            _previewBox.Controls.Add(new Label { Text = "SKU:", Font = UITheme.FontSmall, ForeColor = UITheme.TextMuted, Location = new Point(12, 8), AutoSize = true });
            _lblSkuValue = new Label { Text = "-", Font = UITheme.FontBold, ForeColor = UITheme.ElectricBlue, Location = new Point(84, 8), AutoSize = true };
            _previewBox.Controls.Add(_lblSkuValue);

            _previewBox.Controls.Add(new Label { Text = "Category:", Font = UITheme.FontSmall, ForeColor = UITheme.TextMuted, Location = new Point(12, 31), AutoSize = true });
            _lblCategoryValue = new Label { Text = "-", Font = UITheme.FontRegular, ForeColor = UITheme.TextSecondary, Location = new Point(84, 31), AutoSize = true };
            _previewBox.Controls.Add(_lblCategoryValue);

            _previewBox.Controls.Add(new Label { Text = "Current:", Font = UITheme.FontSmall, ForeColor = UITheme.TextMuted, Location = new Point(12, 56), AutoSize = true });
            _lblCurrentStockValue = new Label { Text = "0", Font = UITheme.FontBold, ForeColor = UITheme.TextPrimary, Location = new Point(84, 55), AutoSize = true };
            _previewBox.Controls.Add(_lblCurrentStockValue);

            _previewBox.Controls.Add(new Label { Text = "After:", Font = UITheme.FontSmall, ForeColor = UITheme.TextMuted, Location = new Point(188, 56), AutoSize = true });
            _lblProjectedStockValue = new Label { Text = "0", Font = UITheme.FontBold, ForeColor = UITheme.TealGlow, Location = new Point(236, 55), AutoSize = true };
            _previewBox.Controls.Add(_lblProjectedStockValue);

            _entryCard.Controls.Add(_previewBox);
            y += 96;

            _lblQuantityCaption = CreateLabel("Quantity *", 16, y);
            _entryCard.Controls.Add(_lblQuantityCaption);
            y += 19;
            _numQuantity = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100000,
                Value = 1,
                Location = new Point(16, y),
                Size = new Size(168, 27)
            };
            UITheme.StyleNumericUpDown(_numQuantity);
            _numQuantity.ValueChanged += (s, e) => UpdateProductPreview();
            _entryCard.Controls.Add(_numQuantity);

            _entryCard.Controls.Add(CreateLabel("Transaction Date", 196, y - 19));
            _dtpTxDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Location = new Point(196, y),
                Size = new Size(168, 27),
                Value = DateTime.Today
            };
            _entryCard.Controls.Add(_dtpTxDate);
            y += 35;

            _entryCard.Controls.Add(CreateLabel("Reference Number", 16, y));
            var lnkAutoRef = new LinkLabel
            {
                Text = "Generate",
                Font = UITheme.FontSmall,
                LinkColor = UITheme.ElectricBlue,
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(304, y),
                AutoSize = true
            };
            lnkAutoRef.LinkClicked += (s, e) => GenerateReferenceNumber();
            _entryCard.Controls.Add(lnkAutoRef);
            y += 19;

            _txtReferenceNo = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(348, 27),
                PlaceholderText = "e.g., PO-2026-105"
            };
            UITheme.StyleTextBox(_txtReferenceNo);
            _entryCard.Controls.Add(_txtReferenceNo);
            y += 35;

            _entryCard.Controls.Add(CreateLabel("Reason / Notes", 16, y));
            y += 19;
            _txtRemarks = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(348, 50),
                Multiline = true,
                PlaceholderText = "Delivery notes or stock release reason"
            };
            UITheme.StyleTextBox(_txtRemarks);
            _entryCard.Controls.Add(_txtRemarks);
            y += 62;

            _btnSubmit = new GradientButton
            {
                Text = "Save Stock In",
                Location = new Point(16, y),
                Size = new Size(348, 38),
                CornerRadius = 9
            };
            _btnSubmit.Click += BtnSubmit_Click;
            _entryCard.Controls.Add(_btnSubmit);

            var historyCard = UITheme.CreateCardPanel();
            historyCard.Dock = DockStyle.Fill;

            var historyTop = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent };
            historyTop.Controls.Add(new Label
            {
                Text = "Stock Transaction History",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(2, 4),
                AutoSize = true
            });

            _dgvHistory = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvHistory);
            _dgvHistory.CellFormatting += DgvHistory_CellFormatting;

            historyCard.Controls.Add(_dgvHistory);
            historyCard.Controls.Add(historyTop);

            rootSplit.Controls.Add(_entryCard, 0, 0);
            rootSplit.Controls.Add(historyCard, 1, 0);
            Controls.Add(rootSplit);

            SetTransactionMode("IN");
        }

        public void ApplyTheme()
        {
            _lblFormBanner.ForeColor = UITheme.TextPrimary;
            _previewBox.BackColor = UITheme.InputSurface;
            _lblCurrentStockValue.ForeColor = UITheme.TextPrimary;
            UITheme.StyleComboBox(_cboProduct);
            UITheme.StyleComboBox(_cboSupplier);
            UITheme.StyleNumericUpDown(_numQuantity);
            UITheme.StyleTextBox(_txtReferenceNo);
            UITheme.StyleTextBox(_txtRemarks);
            UITheme.StyleDataGridView(_dgvHistory);
            Invalidate(true);
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        public void SetTransactionMode(string mode, int preselectedProductId = 0)
        {
            string upper = (mode ?? "IN").ToUpperInvariant();
            if (upper == "ADJUST" && !SessionManager.CanAdjustStock)
            {
                upper = "IN";
            }
            _transactionType = upper == "OUT" ? "OUT" : (upper == "ADJUST" ? "ADJUST" : "IN");

            _btnModeStockIn.IsOutline = _transactionType != "IN";
            _btnModeStockOut.IsOutline = _transactionType != "OUT";
            _btnModeAdjust.IsOutline = _transactionType != "ADJUST";

            if (_transactionType == "IN")
            {
                _entryCard.GlowAccent = UITheme.TealGlow;
                _btnModeStockIn.GradientStart = UITheme.TealGlow;
                _btnModeStockIn.GradientEnd = UITheme.SuccessGreen;
                _lblFormBanner.Text = "Record Incoming Stock";
                _lblQuantityCaption.Text = "Received Quantity *";
                _numQuantity.Minimum = 1;
                if (_numQuantity.Value < 1) _numQuantity.Value = 1;
                _cboSupplier.Enabled = true;
                _lblSupplierCaption.Text = "Supplier (for Stock In)";
                _txtReferenceNo.PlaceholderText = "e.g., PO-2026-105";
                _btnSubmit.GradientStart = UITheme.TealGlow;
                _btnSubmit.GradientEnd = UITheme.SuccessGreen;
                _btnSubmit.Text = "Save Stock In";
            }
            else if (_transactionType == "OUT")
            {
                _entryCard.GlowAccent = UITheme.WarningAmber;
                _btnModeStockOut.GradientStart = Color.FromArgb(245, 158, 11);
                _btnModeStockOut.GradientEnd = UITheme.WarningAmber;
                _lblFormBanner.Text = "Record Outgoing Stock";
                _lblQuantityCaption.Text = "Released Quantity *";
                _numQuantity.Minimum = 1;
                if (_numQuantity.Value < 1) _numQuantity.Value = 1;
                _cboSupplier.Enabled = false;
                _lblSupplierCaption.Text = "Supplier (N/A for Stock Out)";
                _txtReferenceNo.PlaceholderText = "Reason (e.g., Customer Order, Dispatch)";
                _btnSubmit.GradientStart = Color.FromArgb(245, 158, 11);
                _btnSubmit.GradientEnd = UITheme.WarningAmber;
                _btnSubmit.Text = "Save Stock Out";
            }
            else
            {
                _entryCard.GlowAccent = UITheme.ElectricBlue;
                _btnModeAdjust.GradientStart = UITheme.ElectricBlue;
                _btnModeAdjust.GradientEnd = UITheme.PrimaryBlue;
                _lblFormBanner.Text = "Controlled Stock Adjustment";
                _lblQuantityCaption.Text = "New Target Stock *";
                _numQuantity.Minimum = 0;
                _cboSupplier.Enabled = false;
                _lblSupplierCaption.Text = "Supplier (N/A for Adjustment)";
                _txtReferenceNo.PlaceholderText = "Required adjustment reason (e.g., Audit Count)";
                _btnSubmit.GradientStart = UITheme.ElectricBlue;
                _btnSubmit.GradientEnd = UITheme.PrimaryBlue;
                _btnSubmit.Text = "Save Stock Adjustment";
            }

            _entryCard.Invalidate(true);
            RefreshData(preselectedProductId);
            if (_transactionType == "ADJUST")
            {
                _txtReferenceNo.Clear();
            }
            else
            {
                GenerateReferenceNumber();
            }
        }

        private void GenerateReferenceNumber()
        {
            string prefix = _transactionType == "IN" ? "PO" : "SO";
            _txtReferenceNo.Text = $"{prefix}-{DateTime.Now:yyyy}-{Random.Shared.Next(100, 999)}";
        }

        public void RefreshData(int preselectedProductId = 0)
        {
            try
            {
                List<Product> products = _productRepo.SearchProducts();
                _cboProduct.DataSource = null;
                _cboProduct.DataSource = products;
                _cboProduct.DisplayMember = "DisplayLabel";
                _cboProduct.ValueMember = "ProductID";

                List<Supplier> suppliers = new List<Supplier> { new Supplier(0, "(None)") };
                suppliers.AddRange(_supplierRepo.GetAllSuppliers());
                _cboSupplier.DataSource = null;
                _cboSupplier.DataSource = suppliers;
                _cboSupplier.DisplayMember = "SupplierName";
                _cboSupplier.ValueMember = "SupplierID";

                if (preselectedProductId > 0)
                    _cboProduct.SelectedValue = preselectedProductId;
                else if (_cboProduct.Items.Count > 0)
                    _cboProduct.SelectedIndex = 0;

                UpdateProductPreview();
                LoadHistoryGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading stock data:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateProductPreview()
        {
            if (_cboProduct.SelectedItem is Product p)
            {
                _lblSkuValue.Text = p.SKU;
                _lblCategoryValue.Text = p.CategoryName;
                _lblCurrentStockValue.Text = p.QuantityInStock.ToString();

                if (_transactionType == "IN" && p.SupplierID.HasValue)
                {
                    _cboSupplier.SelectedValue = p.SupplierID.Value;
                }

                int qty = (int)_numQuantity.Value;
                int projected = _transactionType == "IN"
                    ? p.QuantityInStock + qty
                    : p.QuantityInStock - qty;

                _lblProjectedStockValue.Text = projected.ToString();
                if (projected < 0)
                {
                    _lblProjectedStockValue.ForeColor = UITheme.DangerRed;
                    _lblProjectedStockValue.Text = $"{projected} (Insufficient)";
                }
                else
                {
                    _lblProjectedStockValue.ForeColor = _transactionType == "IN"
                        ? UITheme.TealGlow
                        : Color.FromArgb(251, 191, 36);
                }
            }
            else
            {
                _lblSkuValue.Text = "-";
                _lblCategoryValue.Text = "-";
                _lblCurrentStockValue.Text = "0";
                _lblProjectedStockValue.Text = "0";
            }
        }

        private void LoadHistoryGrid()
        {
            List<StockTransaction> history = _txRepo.GetTransactions(maxRows: 100);

            _dgvHistory.Rows.Clear();
            _dgvHistory.Columns.Clear();

            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 18 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 10 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 14 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Product", HeaderText = "Product", FillWeight = 26 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 16 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Qty",
                HeaderText = "Qty",
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "New",
                HeaderText = "Balance",
                FillWeight = 11,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
            });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ref", HeaderText = "Ref / Reason", FillWeight = 14 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actor", HeaderText = "By", FillWeight = 15 });

            foreach (var tx in history)
            {
                string qtyDisplay = tx.TransactionType == "IN"
                    ? $"+{tx.Quantity}"
                    : (tx.TransactionType == "OUT" ? $"-{tx.Quantity}" : $"={tx.NewStock}");
                _dgvHistory.Rows.Add(
                    tx.TransactionDate.ToString("yyyy-MM-dd HH:mm"),
                    tx.TransactionType,
                    tx.SKU,
                    tx.ProductName,
                    tx.SupplierName,
                    qtyDisplay,
                    tx.NewStock,
                    tx.ReferenceNo,
                    string.IsNullOrWhiteSpace(tx.PerformedBy) ? "System" : tx.PerformedBy);
            }
        }

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (_cboProduct.SelectedItem is not Product selectedProduct)
            {
                MessageBox.Show("Please select a product.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int quantity = (int)_numQuantity.Value;
            DateTime txDate = _dtpTxDate.Value.Date.Add(DateTime.Now.TimeOfDay);

            if (_transactionType == "ADJUST")
            {
                if (!SessionManager.CanAdjustStock)
                {
                    MessageBox.Show("Access denied: Only the Administrator can perform stock adjustments.",
                        "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    bool adjOk = _txRepo.RecordStockAdjustment(
                        selectedProduct.ProductID,
                        quantity,
                        _txtReferenceNo.Text.Trim(),
                        _txtRemarks.Text.Trim(),
                        out string adjMsg,
                        txDate);

                    if (adjOk)
                    {
                        MessageBox.Show(adjMsg, "Adjustment Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        int keepId = selectedProduct.ProductID;
                        _txtReferenceNo.Clear();
                        _txtRemarks.Clear();
                        RefreshData(keepId);
                        StockMovementRecorded?.Invoke();
                    }
                    else
                    {
                        MessageBox.Show(adjMsg, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            if (_transactionType == "OUT" && quantity > selectedProduct.QuantityInStock)
            {
                MessageBox.Show(
                    $"Cannot release {quantity} units of '{selectedProduct.ProductName}'. Available stock: {selectedProduct.QuantityInStock}.",
                    "Insufficient Stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int? supplierId = null;
            if (_transactionType == "IN" && _cboSupplier.SelectedValue != null)
            {
                int sid = Convert.ToInt32(_cboSupplier.SelectedValue);
                if (sid > 0) supplierId = sid;
            }

            bool ok = _txRepo.RecordStockMovement(
                selectedProduct.ProductID,
                _transactionType,
                quantity,
                _txtReferenceNo.Text.Trim(),
                _txtRemarks.Text.Trim(),
                out string resultMessage,
                supplierId,
                txDate);

            if (ok)
            {
                MessageBox.Show(resultMessage, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                int keepProductId = selectedProduct.ProductID;
                _numQuantity.Value = 1;
                _txtRemarks.Clear();
                GenerateReferenceNumber();
                RefreshData(keepProductId);
                StockMovementRecorded?.Invoke();
            }
            else
            {
                MessageBox.Show(resultMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvHistory_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string colName = _dgvHistory.Columns[e.ColumnIndex].Name;
            if ((colName == "Type" || colName == "Qty") && e.Value != null)
            {
                string val = e.Value.ToString() ?? string.Empty;
                var cell = _dgvHistory.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                if (val == "IN" || val.StartsWith("+"))
                {
                    cell.Style.ForeColor = UITheme.TealGlow;
                    cell.Style.Font = UITheme.FontBold;
                }
                else if (val == "OUT" || val.StartsWith("-"))
                {
                    cell.Style.ForeColor = Color.FromArgb(251, 191, 36);
                    cell.Style.Font = UITheme.FontBold;
                }
            }
        }
    }
}
