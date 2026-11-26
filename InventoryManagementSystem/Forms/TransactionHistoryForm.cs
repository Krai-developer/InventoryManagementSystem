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
    /// Read-only Transaction History view allowing Inventory Staff and Admins to inspect
    /// stock-in, stock-out, and adjustment records with search, date filtering, and "My Transactions" filter.
    /// Completed transactions cannot be edited or deleted.
    /// </summary>
    public class TransactionHistoryForm : UserControl
    {
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        private Label _lblTitle = null!;
        private Label _lblCount = null!;
        private TextBox _txtSearch = null!;
        private ComboBox _cboType = null!;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;
        private CheckBox _chkOnlyMine = null!;
        private DataGridView _dgvHistory = null!;

        public TransactionHistoryForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Padding = new Padding(18, 14, 18, 16);

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var filterCard = UITheme.CreateCardPanel();
            filterCard.Dock = DockStyle.Fill;
            filterCard.Margin = new Padding(0, 0, 0, 12);

            _lblTitle = new Label
            {
                Text = "Stock Transaction History",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            };

            _lblCount = new Label
            {
                Text = "0 records",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(filterCard.Width - 140, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                Location = new Point(16, 40),
                Size = new Size(220, 27),
                PlaceholderText = "Search SKU, product, ref, notes..."
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => LoadHistory();

            _cboType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(246, 40),
                Size = new Size(125, 27)
            };
            UITheme.StyleComboBox(_cboType);
            _cboType.Items.AddRange(new object[] { "All Types", "Stock In", "Stock Out", "Adjustment" });
            _cboType.SelectedIndex = 0;
            _cboType.SelectedIndexChanged += (s, e) => LoadHistory();

            filterCard.Controls.Add(new Label
            {
                Text = "From:",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(384, 45),
                AutoSize = true
            });

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Location = new Point(426, 41),
                Size = new Size(110, 27),
                Value = DateTime.Today.AddMonths(-1)
            };
            _dtpFrom.ValueChanged += (s, e) => LoadHistory();

            filterCard.Controls.Add(new Label
            {
                Text = "To:",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(546, 45),
                AutoSize = true
            });

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Location = new Point(572, 41),
                Size = new Size(110, 27),
                Value = DateTime.Today
            };
            _dtpTo.ValueChanged += (s, e) => LoadHistory();

            _chkOnlyMine = new CheckBox
            {
                Text = "Only My Transactions",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, 74),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _chkOnlyMine.CheckedChanged += (s, e) => LoadHistory();

            var btnDetails = new Button
            {
                Text = "View Details",
                Location = new Point(696, 40),
                Size = new Size(108, 28)
            };
            UITheme.StyleSecondaryButton(btnDetails);
            btnDetails.Click += (s, e) => ShowSelectedTransactionDetails();

            var btnRefresh = new Button
            {
                Text = "Refresh",
                Location = new Point(812, 40),
                Size = new Size(86, 28)
            };
            UITheme.StyleSecondaryButton(btnRefresh);
            btnRefresh.Click += (s, e) => LoadHistory();

            filterCard.Controls.Add(_lblTitle);
            filterCard.Controls.Add(_lblCount);
            filterCard.Controls.Add(_txtSearch);
            filterCard.Controls.Add(_cboType);
            filterCard.Controls.Add(_dtpFrom);
            filterCard.Controls.Add(_dtpTo);
            filterCard.Controls.Add(_chkOnlyMine);
            filterCard.Controls.Add(btnDetails);
            filterCard.Controls.Add(btnRefresh);
            layout.Controls.Add(filterCard, 0, 0);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(12);

            _dgvHistory = new DataGridView
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
            UITheme.StyleDataGridView(_dgvHistory);
            _dgvHistory.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) ShowSelectedTransactionDetails();
            };

            gridCard.Controls.Add(_dgvHistory);
            layout.Controls.Add(gridCard, 0, 1);

            Controls.Add(layout);
        }

        public void RefreshData()
        {
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                string typeFilter = _cboType.SelectedIndex switch
                {
                    1 => "IN",
                    2 => "OUT",
                    3 => "ADJUST",
                    _ => "ALL"
                };

                string actorFilter = _chkOnlyMine.Checked && SessionManager.CurrentUser != null
                    ? SessionManager.CurrentUser.Username
                    : string.Empty;

                List<StockTransaction> list = _txRepo.GetTransactions(
                    fromDate: _dtpFrom.Value.Date,
                    toDate: _dtpTo.Value.Date,
                    transactionType: typeFilter,
                    searchKeyword: _txtSearch.Text,
                    maxRows: 500,
                    performedByFilter: actorFilter);

                _dgvHistory.Rows.Clear();
                _dgvHistory.Columns.Clear();

                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "TxID", HeaderText = "ID", FillWeight = 7 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 16 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 10 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 12 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Product", HeaderText = "Product", FillWeight = 20 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", FillWeight = 8 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "Stock Change", FillWeight = 13 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 14 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ref", HeaderText = "Ref / Reason", FillWeight = 14 });
                _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actor", HeaderText = "Recorded By", FillWeight = 15 });

                foreach (var tx in list)
                {
                    int idx = _dgvHistory.Rows.Add(
                        tx.TransactionID,
                        tx.TransactionDate.ToString("yyyy-MM-dd HH:mm"),
                        tx.TransactionType,
                        tx.SKU,
                        tx.ProductName,
                        tx.Quantity,
                        $"{tx.PreviousStock} -> {tx.NewStock}",
                        string.IsNullOrWhiteSpace(tx.SupplierName) ? "-" : tx.SupplierName,
                        string.IsNullOrWhiteSpace(tx.ReferenceNo) ? "-" : tx.ReferenceNo,
                        string.IsNullOrWhiteSpace(tx.PerformedBy) ? "System" : tx.PerformedBy);
                    _dgvHistory.Rows[idx].Tag = tx;
                }

                _lblCount.Text = $"{list.Count} record(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transaction history:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSelectedTransactionDetails()
        {
            if (_dgvHistory.CurrentRow?.Tag is not StockTransaction tx)
            {
                MessageBox.Show("Select a transaction row first to view its details.",
                    "Transaction Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string details =
                $"Transaction ID: #{tx.TransactionID}\n" +
                $"Type: {tx.TransactionType}\n" +
                $"Date: {tx.TransactionDate:MMM dd, yyyy hh:mm tt}\n" +
                $"Product: {tx.ProductName} ({tx.SKU})\n" +
                $"Category: {tx.CategoryName}\n" +
                $"Supplier: {(string.IsNullOrWhiteSpace(tx.SupplierName) ? "-" : tx.SupplierName)}\n" +
                $"Quantity: {tx.Quantity}\n" +
                $"Stock Balance: {tx.PreviousStock} -> {tx.NewStock}\n" +
                $"Reference / Reason: {(string.IsNullOrWhiteSpace(tx.ReferenceNo) ? "-" : tx.ReferenceNo)}\n" +
                $"Notes: {(string.IsNullOrWhiteSpace(tx.Remarks) ? "-" : tx.Remarks)}\n" +
                $"Recorded By: {(string.IsNullOrWhiteSpace(tx.PerformedBy) ? "System" : tx.PerformedBy)}\n\n" +
                "(Completed transactions are read-only and cannot be modified or deleted.)";

            MessageBox.Show(details, $"Transaction #{tx.TransactionID} Details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ApplyTheme()
        {
            _lblTitle.ForeColor = UITheme.TextPrimary;
            UITheme.StyleTextBox(_txtSearch);
            UITheme.StyleComboBox(_cboType);
            UITheme.StyleDataGridView(_dgvHistory);
            Invalidate(true);
        }
    }
}
