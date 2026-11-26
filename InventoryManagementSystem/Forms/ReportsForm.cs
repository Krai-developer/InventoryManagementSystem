using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class ReportsForm : UserControl
    {
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly SupplierRepository _supplierRepo = new SupplierRepository();
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        private ComboBox _cboReportType = null!;
        private ComboBox _cboCategory = null!;
        private ComboBox _cboSupplier = null!;
        private ComboBox _cboProduct = null!;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;
        private Panel _pnlDateFilters = null!;

        private Label _lblSummaryMetric1 = null!;
        private Label _lblSummaryMetric2 = null!;
        private Label _lblSummaryMetric3 = null!;

        private DataGridView _dgvReport = null!;
        private bool _suppressEvents = false;

        public ReportsForm()
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
                RowCount = 3,
                BackColor = Color.Transparent
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var filterCard = UITheme.CreateCardPanel();
            filterCard.Dock = DockStyle.Fill;
            filterCard.Margin = new Padding(0, 0, 0, 12);

            filterCard.Controls.Add(new Label
            {
                Text = "Reports & Analytics",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            });

            filterCard.Controls.Add(new Label
            {
                Text = "Report:",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, 42),
                AutoSize = true
            });

            _cboReportType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(76, 38),
                Size = new Size(245, 27)
            };
            UITheme.StyleComboBox(_cboReportType);
            _cboReportType.Items.AddRange(new object[]
            {
                "1. Current Inventory Report",
                "2. Low-Stock Report",
                "3. Out-of-Stock Report",
                "4. Stock-In Report",
                "5. Stock-Out Report",
                "6. Stock Movement History",
                "7. Inventory Value Summary"
            });
            _cboReportType.SelectedIndex = 0;
            _cboReportType.SelectedIndexChanged += (s, e) =>
            {
                bool isTxReport = _cboReportType.SelectedIndex >= 3 && _cboReportType.SelectedIndex <= 5;
                _pnlDateFilters.Visible = isTxReport;
                GenerateReport();
            };

            filterCard.Controls.Add(new Label
            {
                Text = "Category:",
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(336, 42),
                AutoSize = true
            });

            _cboCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(404, 38),
                Size = new Size(165, 27)
            };
            UITheme.StyleComboBox(_cboCategory);
            _cboCategory.SelectedIndexChanged += (s, e) => GenerateReport();

            filterCard.Controls.Add(new Label
            {
                Text = "Supplier:",
                Font = UITheme.FontRegular,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(582, 42),
                AutoSize = true
            });

            _cboSupplier = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(644, 38),
                Size = new Size(165, 27)
            };
            UITheme.StyleComboBox(_cboSupplier);
            _cboSupplier.SelectedIndexChanged += (s, e) => GenerateReport();

            // Row 2 inside filterCard: Date Range + Product Filter (for transaction reports) + Export CSV
            _pnlDateFilters = new Panel
            {
                Location = new Point(16, 74),
                Size = new Size(760, 34),
                BackColor = Color.Transparent,
                Visible = false
            };

            _pnlDateFilters.Controls.Add(new Label { Text = "From:", Font = UITheme.FontRegular, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(0, 7), AutoSize = true });
            _dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = UITheme.FontRegular, Value = DateTime.Now.AddDays(-30), Location = new Point(46, 3), Size = new Size(115, 27) };
            _dtpFrom.ValueChanged += (s, e) => GenerateReport();
            _pnlDateFilters.Controls.Add(_dtpFrom);

            _pnlDateFilters.Controls.Add(new Label { Text = "To:", Font = UITheme.FontRegular, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(172, 7), AutoSize = true });
            _dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = UITheme.FontRegular, Value = DateTime.Now, Location = new Point(200, 3), Size = new Size(115, 27) };
            _dtpTo.ValueChanged += (s, e) => GenerateReport();
            _pnlDateFilters.Controls.Add(_dtpTo);

            _pnlDateFilters.Controls.Add(new Label { Text = "Product:", Font = UITheme.FontRegular, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(330, 7), AutoSize = true });
            _cboProduct = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(392, 3), Size = new Size(240, 27) };
            UITheme.StyleComboBox(_cboProduct);
            _cboProduct.SelectedIndexChanged += (s, e) => GenerateReport();
            _pnlDateFilters.Controls.Add(_cboProduct);

            var btnExportCsv = new GradientButton
            {
                Text = "Export CSV",
                Size = new Size(140, 34),
                CornerRadius = 9,
                GradientStart = UITheme.TealGlow,
                GradientEnd = UITheme.SuccessGreen,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(filterCard.Width - 158, 72)
            };
            btnExportCsv.Click += BtnExportCsv_Click;

            filterCard.Controls.Add(_cboReportType);
            filterCard.Controls.Add(_cboCategory);
            filterCard.Controls.Add(_cboSupplier);
            filterCard.Controls.Add(_pnlDateFilters);
            filterCard.Controls.Add(btnExportCsv);

            // Summary Row
            var summaryCard = UITheme.CreateCardPanel();
            summaryCard.Dock = DockStyle.Fill;
            summaryCard.Margin = new Padding(0, 0, 0, 12);

            _lblSummaryMetric1 = new Label { Text = "Records: 0", Font = UITheme.FontBold, ForeColor = UITheme.TextPrimary, BackColor = Color.Transparent, Location = new Point(16, 11), AutoSize = true };
            _lblSummaryMetric2 = new Label { Text = "Total Units: 0", Font = UITheme.FontBold, ForeColor = UITheme.ElectricBlue, BackColor = Color.Transparent, Location = new Point(280, 11), AutoSize = true };
            _lblSummaryMetric3 = new Label { Text = "Total Value: ₱0.00", Font = UITheme.FontBold, ForeColor = UITheme.TealGlow, BackColor = Color.Transparent, Location = new Point(540, 11), AutoSize = true };

            summaryCard.Controls.Add(_lblSummaryMetric1);
            summaryCard.Controls.Add(_lblSummaryMetric2);
            summaryCard.Controls.Add(_lblSummaryMetric3);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            _dgvReport = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvReport);
            _dgvReport.CellPainting += DgvReport_CellPainting;
            _dgvReport.CellFormatting += DgvReport_CellFormatting;

            gridCard.Controls.Add(_dgvReport);

            mainLayout.Controls.Add(filterCard, 0, 0);
            mainLayout.Controls.Add(summaryCard, 0, 1);
            mainLayout.Controls.Add(gridCard, 0, 2);
            Controls.Add(mainLayout);
        }

        public void ApplyTheme()
        {
            _lblSummaryMetric1.ForeColor = UITheme.TextPrimary;
            UITheme.StyleComboBox(_cboReportType);
            UITheme.StyleComboBox(_cboCategory);
            UITheme.StyleComboBox(_cboSupplier);
            UITheme.StyleComboBox(_cboProduct);
            UITheme.StyleDataGridView(_dgvReport);
            Invalidate(true);
        }

        public void RefreshData()
        {
            try
            {
                _suppressEvents = true;

                _cboCategory.Items.Clear();
                _cboCategory.Items.Add(new Category(0, "All Categories"));
                foreach (var c in _categoryRepo.GetAllCategories()) _cboCategory.Items.Add(c);
                _cboCategory.SelectedIndex = 0;

                _cboSupplier.Items.Clear();
                _cboSupplier.Items.Add(new Supplier(0, "All Suppliers"));
                foreach (var s in _supplierRepo.GetAllSuppliers()) _cboSupplier.Items.Add(s);
                _cboSupplier.SelectedIndex = 0;

                _cboProduct.Items.Clear();
                _cboProduct.Items.Add(new Product { ProductID = 0, SKU = "ALL", ProductName = "All Products" });
                foreach (var p in _productRepo.SearchProducts()) _cboProduct.Items.Add(p);
                _cboProduct.DisplayMember = "ProductName";
                _cboProduct.SelectedIndex = 0;

                _suppressEvents = false;
                GenerateReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reports:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateReport()
        {
            if (_suppressEvents) return;

            int catId = (_cboCategory.SelectedItem as Category)?.CategoryID ?? 0;
            int supId = (_cboSupplier.SelectedItem as Supplier)?.SupplierID ?? 0;
            int prodId = (_cboProduct.SelectedItem as Product)?.ProductID ?? 0;

            _dgvReport.Rows.Clear();
            _dgvReport.Columns.Clear();

            switch (_cboReportType.SelectedIndex)
            {
                case 0: // Current Inventory Report
                    RenderProductReport(_productRepo.SearchProducts("", catId, "ALL", supId));
                    break;
                case 1: // Low-Stock Report (Qty > 0 and <= ReorderLevel)
                    RenderProductReport(_productRepo.SearchProducts("", catId, "LOW_STOCK", supId));
                    break;
                case 2: // Out-of-Stock Report (Qty == 0)
                    RenderProductReport(_productRepo.SearchProducts("", catId, "OUT_OF_STOCK", supId));
                    break;
                case 3: // Stock-In Report
                    RenderTransactionReport(_txRepo.GetTransactions(_dtpFrom.Value, _dtpTo.Value, "IN", catId, "", 1000, prodId, supId));
                    break;
                case 4: // Stock-Out Report
                    RenderTransactionReport(_txRepo.GetTransactions(_dtpFrom.Value, _dtpTo.Value, "OUT", catId, "", 1000, prodId, supId));
                    break;
                case 5: // Stock Movement History
                    RenderTransactionReport(_txRepo.GetTransactions(_dtpFrom.Value, _dtpTo.Value, "ALL", catId, "", 1000, prodId, supId));
                    break;
                case 6: // Inventory Value Summary (grouped by Category)
                    RenderValueSummaryReport(_productRepo.SearchProducts("", catId, "ALL", supId));
                    break;
            }
        }

        private void RenderProductReport(List<Product> products)
        {
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 14 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product Name", FillWeight = 26 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 16 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 16 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cost", HeaderText = "Cost (₱)", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Price (₱)", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalValue", HeaderText = "Value (₱)", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = UITheme.FontBold } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 16 });

            int totalUnits = 0;
            decimal totalValue = 0m;
            foreach (var p in products)
            {
                totalUnits += p.QuantityInStock;
                totalValue += p.TotalStockValue;
                _dgvReport.Rows.Add(p.SKU, p.ProductName, p.CategoryName, p.SupplierName,
                    p.PurchasePrice.ToString("N2"), p.UnitPrice.ToString("N2"),
                    p.QuantityInStock, p.TotalStockValue.ToString("N2"), p.StockStatus);
            }

            _lblSummaryMetric1.Text = $"Products: {products.Count:N0}";
            _lblSummaryMetric2.Text = $"Total Units: {totalUnits:N0}";
            _lblSummaryMetric3.Text = $"Total Value: {UITheme.FormatCurrency(totalValue)}";
        }

        private void RenderTransactionReport(List<StockTransaction> movements)
        {
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TxID", HeaderText = "ID", FillWeight = 8 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 17 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 10 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 14 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Product", HeaderText = "Product", FillWeight = 24 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 16 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Before", HeaderText = "Before", FillWeight = 9, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "After", HeaderText = "After", FillWeight = 9, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ref", HeaderText = "Reference", FillWeight = 14 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remarks", HeaderText = "Notes", FillWeight = 20 });

            int totalIn = 0, totalOut = 0;
            foreach (var tx in movements)
            {
                if (tx.TransactionType == "IN") totalIn += tx.Quantity;
                else totalOut += tx.Quantity;

                string qtyText = tx.TransactionType == "IN" ? $"+{tx.Quantity}" : $"-{tx.Quantity}";
                _dgvReport.Rows.Add(tx.TransactionID, tx.TransactionDate.ToString("yyyy-MM-dd HH:mm"), tx.TransactionType,
                    tx.SKU, tx.ProductName, tx.SupplierName, qtyText, tx.PreviousStock, tx.NewStock, tx.ReferenceNo, tx.Remarks);
            }

            _lblSummaryMetric1.Text = $"Transactions: {movements.Count:N0}";
            _lblSummaryMetric2.Text = $"Stock In: +{totalIn:N0}";
            _lblSummaryMetric3.Text = $"Stock Out: -{totalOut:N0}";
        }

        private void RenderValueSummaryReport(List<Product> products)
        {
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 26 });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductCount", HeaderText = "Products", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalUnits", HeaderText = "Total Units", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalCost", HeaderText = "Total Cost (₱)", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalRetail", HeaderText = "Total Value (₱)", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = UITheme.FontBold } });
            _dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "LowStock", HeaderText = "Low / Out", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            var grouped = products.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);
            int grandUnits = 0;
            decimal grandValue = 0m;
            decimal grandCost = 0m;

            foreach (var grp in grouped)
            {
                int units = grp.Sum(p => p.QuantityInStock);
                decimal cost = grp.Sum(p => p.TotalCostValue);
                decimal val = grp.Sum(p => p.TotalStockValue);
                int lowOrOut = grp.Count(p => p.QuantityInStock <= p.ReorderLevel);

                grandUnits += units;
                grandCost += cost;
                grandValue += val;

                _dgvReport.Rows.Add(grp.Key, grp.Count(), units, cost.ToString("N2"), val.ToString("N2"), lowOrOut);
            }

            _lblSummaryMetric1.Text = $"Categories: {grouped.Count():N0}";
            _lblSummaryMetric2.Text = $"Total Units: {grandUnits:N0} (Cost: {UITheme.FormatCurrency(grandCost)})";
            _lblSummaryMetric3.Text = $"Total Value: {UITheme.FormatCurrency(grandValue)}";
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (_dgvReport.Rows.Count == 0)
            {
                MessageBox.Show("No rows to export.", "Empty Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string reportSlug = (_cboReportType.SelectedItem?.ToString() ?? "Report")
                .Replace(" ", "_").Replace(".", "").Replace("-", "_");

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Export Report to CSV";
                sfd.Filter = "CSV Files (*.csv)|*.csv";
                sfd.FileName = $"{reportSlug}_{DateTime.Now:yyyyMMdd_HHmm}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        var headers = new List<string>();
                        foreach (DataGridViewColumn col in _dgvReport.Columns)
                        {
                            if (col.Visible) headers.Add(EscapeCsv(col.HeaderText));
                        }
                        sb.AppendLine(string.Join(",", headers));

                        foreach (DataGridViewRow row in _dgvReport.Rows)
                        {
                            var cells = new List<string>();
                            foreach (DataGridViewColumn col in _dgvReport.Columns)
                            {
                                if (col.Visible)
                                {
                                    string val = row.Cells[col.Index].FormattedValue?.ToString() ?? string.Empty;
                                    cells.Add(EscapeCsv(val));
                                }
                            }
                            sb.AppendLine(string.Join(",", cells));
                        }

                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show($"Report exported to:\n{sfd.FileName}", "Export Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export CSV:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private void DgvReport_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_dgvReport.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                UITheme.PaintStatusPillCell(e, e.Value.ToString());
            }
        }

        private void DgvReport_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string colName = _dgvReport.Columns[e.ColumnIndex].Name;
            if ((colName == "Type" || colName == "Qty") && _cboReportType.SelectedIndex >= 3 && _cboReportType.SelectedIndex <= 5 && e.Value != null)
            {
                string val = e.Value.ToString() ?? string.Empty;
                var cell = _dgvReport.Rows[e.RowIndex].Cells[e.ColumnIndex];
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
