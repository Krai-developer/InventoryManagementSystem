using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Security;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class ProductsForm : UserControl
    {
        private readonly ProductRepository _productRepo = new ProductRepository();
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();
        private readonly SupplierRepository _supplierRepo = new SupplierRepository();

        public event Action? InventoryChanged;
        public event Action<int>? QuickStockInRequested;
        public event Action<int>? QuickStockOutRequested;

        private TextBox _txtSearch = null!;
        private ComboBox _cboFilterCategory = null!;
        private ComboBox _cboFilterStatus = null!;
        private Label _lblResultsCount = null!;

        private DataGridView _dgvProducts = null!;
        private ContextMenuStrip _ctxGridMenu = null!;

        private GroupBox _grpEditor = null!;
        private Label _lblFormMode = null!;
        private int _selectedProductId = 0;
        private TextBox _txtSku = null!;
        private TextBox _txtProductName = null!;
        private TextBox _txtDescription = null!;
        private ComboBox _cboCategory = null!;
        private ComboBox _cboSupplier = null!;
        private NumericUpDown _numPurchasePrice = null!;
        private NumericUpDown _numUnitPrice = null!;
        private NumericUpDown _numInitialStock = null!;
        private Label _lblInitialStockCaption = null!;
        private NumericUpDown _numReorderLevel = null!;

        private PictureBox _picProductThumb = null!;
        private string _currentImagePath = string.Empty;

        private GradientButton _btnSave = null!;
        private GradientButton _btnDelete = null!;
        private Button _btnClear = null!;
        private Button _btnResetSearch = null!;
        private Button _btnInspect = null!;
        private Button _btnBrowseImage = null!;

        private bool _suppressFilterEvents = false;

        public ProductsForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Padding = new Padding(18, 14, 18, 16);

            InitializeLayout();
            InitializeContextMenu();
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
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 378F));

            // Left: Search Bar + Products Grid
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var searchCard = UITheme.CreateCardPanel();
            searchCard.Dock = DockStyle.Fill;
            searchCard.Margin = new Padding(0, 0, 0, 12);

            searchCard.Controls.Add(new Label
            {
                Text = "Product Inventory",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            });

            _lblResultsCount = new Label
            {
                Text = "0 products",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(searchCard.Width - 150, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search ID, SKU, name, or supplier...",
                Location = new Point(16, 38),
                Size = new Size(205, 27)
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => ApplyFilters(showNotFoundMessage: false);
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ApplyFilters(showNotFoundMessage: true);
                }
            };

            var btnFind = new GradientButton
            {
                Text = "Search",
                Location = new Point(227, 36),
                Size = new Size(72, 29),
                CornerRadius = 8,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            btnFind.Click += (s, e) => ApplyFilters(showNotFoundMessage: true);

            _cboFilterCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(306, 38),
                Size = new Size(158, 27)
            };
            UITheme.StyleComboBox(_cboFilterCategory);
            _cboFilterCategory.SelectedIndexChanged += (s, e) => ApplyFilters(showNotFoundMessage: false);

            _cboFilterStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(470, 38),
                Size = new Size(134, 27)
            };
            UITheme.StyleComboBox(_cboFilterStatus);
            _cboFilterStatus.Items.AddRange(new object[] { "All Statuses", "In Stock", "Low Stock", "Out of Stock" });
            _cboFilterStatus.SelectedIndex = 0;
            _cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilters(showNotFoundMessage: false);

            _btnResetSearch = new Button
            {
                Text = "Reset",
                Location = new Point(610, 36),
                Size = new Size(64, 29)
            };
            UITheme.StyleSecondaryButton(_btnResetSearch);
            _btnResetSearch.Click += (s, e) =>
            {
                _suppressFilterEvents = true;
                _txtSearch.Clear();
                if (_cboFilterCategory.Items.Count > 0) _cboFilterCategory.SelectedIndex = 0;
                _cboFilterStatus.SelectedIndex = 0;
                _suppressFilterEvents = false;
                ApplyFilters(showNotFoundMessage: false);
            };

            var btnClearAll = new GradientButton
            {
                Text = "Clear All",
                Location = new Point(680, 36),
                Size = new Size(80, 29),
                CornerRadius = 8,
                GradientStart = Color.FromArgb(239, 68, 68),
                GradientEnd = UITheme.DangerRed
            };
            btnClearAll.Click += (s, e) =>
            {
                DialogResult confirm = MessageBox.Show(
                    "Remove all products and stock history from the database?",
                    "Confirm Clear All",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    if (DatabaseInitializer.ClearAllProductsAndStock(out string msg))
                    {
                        ApplyFilters(showNotFoundMessage: false);
                        ResetEditorForm();
                        InventoryChanged?.Invoke();
                        MessageBox.Show(msg, "Cleared", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            searchCard.Controls.Add(_lblResultsCount);
            searchCard.Controls.Add(_txtSearch);
            searchCard.Controls.Add(btnFind);
            searchCard.Controls.Add(_cboFilterCategory);
            searchCard.Controls.Add(_cboFilterStatus);
            searchCard.Controls.Add(_btnResetSearch);
            searchCard.Controls.Add(btnClearAll);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            var gridFooter = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 6, 0, 0), BackColor = Color.Transparent };
            gridFooter.Controls.Add(new Label
            {
                Text = "Select a row to edit, or double-click to inspect details.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(2, 12)
            });

            _btnInspect = new Button
            {
                Text = "View Details",
                Size = new Size(106, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(gridFooter.Width - 344, 6)
            };
            UITheme.StyleSecondaryButton(_btnInspect);
            _btnInspect.Click += (s, e) => OpenProductInspector();

            var btnRowStockIn = new GradientButton
            {
                Text = "Stock In",
                Size = new Size(108, 30),
                CornerRadius = 8,
                GradientStart = UITheme.TealGlow,
                GradientEnd = UITheme.SuccessGreen,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(gridFooter.Width - 230, 6)
            };
            btnRowStockIn.Click += (s, e) =>
            {
                if (_selectedProductId > 0) QuickStockInRequested?.Invoke(_selectedProductId);
                else MessageBox.Show("Select a product from the table first.", "Select Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnRowStockOut = new GradientButton
            {
                Text = "Stock Out",
                Size = new Size(108, 30),
                CornerRadius = 8,
                GradientStart = Color.FromArgb(245, 158, 11),
                GradientEnd = UITheme.WarningAmber,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(gridFooter.Width - 114, 6)
            };
            btnRowStockOut.Click += (s, e) =>
            {
                if (_selectedProductId > 0) QuickStockOutRequested?.Invoke(_selectedProductId);
                else MessageBox.Show("Select a product from the table first.", "Select Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            gridFooter.Controls.Add(_btnInspect);
            gridFooter.Controls.Add(btnRowStockIn);
            gridFooter.Controls.Add(btnRowStockOut);

            _dgvProducts = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvProducts);
            _dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;
            _dgvProducts.CellPainting += DgvProducts_CellPainting;
            _dgvProducts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) OpenProductInspector();
            };

            gridCard.Controls.Add(_dgvProducts);
            gridCard.Controls.Add(gridFooter);

            leftLayout.Controls.Add(searchCard, 0, 0);
            leftLayout.Controls.Add(gridCard, 0, 1);

            // Right: Product Editor Card (scrollable if window is short)
            var formCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            formCard.Dock = DockStyle.Fill;
            formCard.AutoScroll = true;

            _lblFormMode = new Label
            {
                Text = "Add Product",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            };
            formCard.Controls.Add(_lblFormMode);

            _grpEditor = new GroupBox
            {
                Text = "Product Information",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(14, 36),
                Size = new Size(346, 616)
            };

            int y = 22;

            _picProductThumb = new PictureBox
            {
                Location = new Point(14, y),
                Size = new Size(50, 50),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(18, 32, 62)
            };
            RenderDefaultThumb("IMG");
            _grpEditor.Controls.Add(_picProductThumb);

            _btnBrowseImage = new Button
            {
                Text = "Choose Image...",
                Location = new Point(76, y + 4),
                Size = new Size(134, 26)
            };
            UITheme.StyleSecondaryButton(_btnBrowseImage);
            _btnBrowseImage.Click += BtnBrowseImage_Click;
            _grpEditor.Controls.Add(_btnBrowseImage);

            _grpEditor.Controls.Add(new Label
            {
                Text = "Optional product photo",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(76, y + 32),
                AutoSize = true
            });
            y += 58;

            _grpEditor.Controls.Add(CreateFieldLabel("Product ID / SKU *", 14, y));
            var lnkGenSku = new LinkLabel
            {
                Text = "Generate",
                Font = UITheme.FontSmall,
                LinkColor = UITheme.ElectricBlue,
                ActiveLinkColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(272, y),
                AutoSize = true
            };
            lnkGenSku.LinkClicked += (s, e) => _txtSku.Text = $"P{Random.Shared.Next(100, 999)}";
            _grpEditor.Controls.Add(lnkGenSku);
            y += 18;

            _txtSku = new TextBox { Location = new Point(14, y), Size = new Size(316, 26), PlaceholderText = "e.g., P001" };
            UITheme.StyleTextBox(_txtSku);
            _grpEditor.Controls.Add(_txtSku);
            y += 32;

            _grpEditor.Controls.Add(CreateFieldLabel("Product Name *", 14, y)); y += 18;
            _txtProductName = new TextBox { Location = new Point(14, y), Size = new Size(316, 26), PlaceholderText = "Enter product name" };
            UITheme.StyleTextBox(_txtProductName);
            _grpEditor.Controls.Add(_txtProductName);
            y += 32;

            _grpEditor.Controls.Add(CreateFieldLabel("Description", 14, y)); y += 18;
            _txtDescription = new TextBox { Location = new Point(14, y), Size = new Size(316, 44), Multiline = true, PlaceholderText = "Optional product details" };
            UITheme.StyleTextBox(_txtDescription);
            _grpEditor.Controls.Add(_txtDescription);
            y += 50;

            _grpEditor.Controls.Add(CreateFieldLabel("Category *", 14, y)); y += 18;
            _cboCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(14, y), Size = new Size(316, 26) };
            UITheme.StyleComboBox(_cboCategory);
            _grpEditor.Controls.Add(_cboCategory);
            y += 32;

            _grpEditor.Controls.Add(CreateFieldLabel("Supplier", 14, y)); y += 18;
            _cboSupplier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(14, y), Size = new Size(316, 26) };
            UITheme.StyleComboBox(_cboSupplier);
            _grpEditor.Controls.Add(_cboSupplier);
            y += 32;

            // Purchase Price & Selling Price side-by-side
            _grpEditor.Controls.Add(CreateFieldLabel("Purchase Price (₱)", 14, y));
            _grpEditor.Controls.Add(CreateFieldLabel("Selling Price (₱) *", 178, y));
            y += 18;

            _numPurchasePrice = new NumericUpDown
            {
                DecimalPlaces = 2,
                Maximum = 1000000M,
                Minimum = 0M,
                Increment = 5M,
                ThousandsSeparator = true,
                Location = new Point(14, y),
                Size = new Size(152, 26)
            };
            UITheme.StyleNumericUpDown(_numPurchasePrice);
            _grpEditor.Controls.Add(_numPurchasePrice);

            _numUnitPrice = new NumericUpDown
            {
                DecimalPlaces = 2,
                Maximum = 1000000M,
                Minimum = 0M,
                Increment = 5M,
                ThousandsSeparator = true,
                Location = new Point(178, y),
                Size = new Size(152, 26)
            };
            UITheme.StyleNumericUpDown(_numUnitPrice);
            _grpEditor.Controls.Add(_numUnitPrice);
            y += 32;

            // Opening Stock & Min Stock Level side-by-side
            _lblInitialStockCaption = CreateFieldLabel("Opening Stock", 14, y);
            _grpEditor.Controls.Add(_lblInitialStockCaption);
            _grpEditor.Controls.Add(CreateFieldLabel("Min Stock Level *", 178, y));
            y += 18;

            _numInitialStock = new NumericUpDown { Maximum = 100000, Minimum = 0, Location = new Point(14, y), Size = new Size(152, 26) };
            UITheme.StyleNumericUpDown(_numInitialStock);
            _grpEditor.Controls.Add(_numInitialStock);

            _numReorderLevel = new NumericUpDown { Maximum = 100000, Minimum = 0, Value = 10, Location = new Point(178, y), Size = new Size(152, 26) };
            UITheme.StyleNumericUpDown(_numReorderLevel);
            _grpEditor.Controls.Add(_numReorderLevel);
            y += 38;

            _btnSave = new GradientButton
            {
                Text = "Save Product",
                Location = new Point(14, y),
                Size = new Size(316, 36),
                CornerRadius = 9,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            _btnSave.Click += BtnSave_Click;
            _grpEditor.Controls.Add(_btnSave);
            y += 42;

            _btnClear = new Button { Text = "Clear", Location = new Point(14, y), Size = new Size(152, 32) };
            UITheme.StyleSecondaryButton(_btnClear);
            _btnClear.Click += (s, e) => ResetEditorForm();
            _grpEditor.Controls.Add(_btnClear);

            _btnDelete = new GradientButton
            {
                Text = "Delete",
                Location = new Point(178, y),
                Size = new Size(152, 32),
                CornerRadius = 8,
                GradientStart = Color.FromArgb(239, 68, 68),
                GradientEnd = UITheme.DangerRed,
                Enabled = false
            };
            _btnDelete.Click += BtnDelete_Click;
            _grpEditor.Controls.Add(_btnDelete);

            formCard.Controls.Add(_grpEditor);

            rootSplit.Controls.Add(leftLayout, 0, 0);
            rootSplit.Controls.Add(formCard, 1, 0);
            Controls.Add(rootSplit);
        }

        public void ApplyTheme()
        {
            _lblFormMode.ForeColor = UITheme.TextPrimary;
            _grpEditor.ForeColor = UITheme.TextSecondary;
            UITheme.StyleTextBox(_txtSearch);
            UITheme.StyleComboBox(_cboFilterCategory);
            UITheme.StyleComboBox(_cboFilterStatus);
            UITheme.StyleTextBox(_txtSku);
            UITheme.StyleTextBox(_txtProductName);
            UITheme.StyleTextBox(_txtDescription);
            UITheme.StyleComboBox(_cboCategory);
            UITheme.StyleComboBox(_cboSupplier);
            UITheme.StyleNumericUpDown(_numPurchasePrice);
            UITheme.StyleNumericUpDown(_numUnitPrice);
            UITheme.StyleNumericUpDown(_numInitialStock);
            UITheme.StyleNumericUpDown(_numReorderLevel);
            UITheme.StyleDataGridView(_dgvProducts);
            UITheme.StyleSecondaryButton(_btnResetSearch);
            UITheme.StyleSecondaryButton(_btnInspect);
            UITheme.StyleSecondaryButton(_btnClear);
            UITheme.StyleSecondaryButton(_btnBrowseImage);
            Invalidate(true);
        }

        private void InitializeContextMenu()
        {
            _ctxGridMenu = new ContextMenuStrip();

            var mnuInspect = new ToolStripMenuItem("View Product Details");
            mnuInspect.Click += (s, e) => OpenProductInspector();

            var mnuStockIn = new ToolStripMenuItem("Stock In");
            mnuStockIn.Click += (s, e) => { if (_selectedProductId > 0) QuickStockInRequested?.Invoke(_selectedProductId); };

            var mnuStockOut = new ToolStripMenuItem("Stock Out");
            mnuStockOut.Click += (s, e) => { if (_selectedProductId > 0) QuickStockOutRequested?.Invoke(_selectedProductId); };

            var mnuDelete = new ToolStripMenuItem("Delete Product") { ForeColor = UITheme.DangerRed };
            mnuDelete.Click += BtnDelete_Click;

            _ctxGridMenu.Items.Add(mnuInspect);
            _ctxGridMenu.Items.Add(new ToolStripSeparator());
            _ctxGridMenu.Items.Add(mnuStockIn);
            _ctxGridMenu.Items.Add(mnuStockOut);
            _ctxGridMenu.Items.Add(new ToolStripSeparator());
            _ctxGridMenu.Items.Add(mnuDelete);

            _dgvProducts.ContextMenuStrip = _ctxGridMenu;
        }

        private void OpenProductInspector()
        {
            if (_dgvProducts.CurrentRow?.Tag is Product selected)
            {
                using (var dlg = new ProductDetailsDialog(selected))
                {
                    dlg.ShowDialog(FindForm());
                    if (!string.Equals(_currentImagePath, selected.ImagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentImagePath = selected.ImagePath;
                        LoadThumbFromFile(_currentImagePath, selected.SKU);
                    }
                }
            }
            else
            {
                MessageBox.Show("Select a product row first.", "No Product Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnBrowseImage_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Product Photo";
                ofd.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _currentImagePath = ofd.FileName;
                    LoadThumbFromFile(_currentImagePath, _txtSku.Text);
                }
            }
        }

        private void LoadThumbFromFile(string path, string fallbackLabel)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _picProductThumb.Image = Image.FromStream(stream);
                    return;
                }
                catch { }
            }
            RenderDefaultThumb(fallbackLabel);
        }

        private void RenderDefaultThumb(string label)
        {
            Bitmap bmp = new Bitmap(50, 50);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(18, 32, 62));
                UITheme.DrawIsometricCube(g, new RectangleF(8, 8, 34, 34), glowing: false);
            }
            _picProductThumb.Image = bmp;
        }

        private Label CreateFieldLabel(string text, int x, int y)
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

        public void RefreshData(int selectProductId = 0)
        {
            try
            {
                _suppressFilterEvents = true;

                List<Category> categories = _categoryRepo.GetAllCategories();
                int prevFilterCatId = (_cboFilterCategory.SelectedItem as Category)?.CategoryID ?? 0;
                _cboFilterCategory.Items.Clear();
                _cboFilterCategory.Items.Add(new Category(0, "All Categories"));
                foreach (var cat in categories) _cboFilterCategory.Items.Add(cat);
                _cboFilterCategory.SelectedIndex = 0;
                for (int i = 0; i < _cboFilterCategory.Items.Count; i++)
                {
                    if (((Category)_cboFilterCategory.Items[i]!).CategoryID == prevFilterCatId)
                    {
                        _cboFilterCategory.SelectedIndex = i;
                        break;
                    }
                }

                _cboCategory.DataSource = null;
                _cboCategory.DataSource = categories;
                _cboCategory.DisplayMember = "CategoryName";
                _cboCategory.ValueMember = "CategoryID";

                List<Supplier> suppliers = new List<Supplier> { new Supplier(0, "(None)") };
                suppliers.AddRange(_supplierRepo.GetAllSuppliers());
                _cboSupplier.DataSource = null;
                _cboSupplier.DataSource = suppliers;
                _cboSupplier.DisplayMember = "SupplierName";
                _cboSupplier.ValueMember = "SupplierID";

                _suppressFilterEvents = false;

                ApplyFilters(showNotFoundMessage: false);

                if (selectProductId > 0)
                {
                    foreach (DataGridViewRow row in _dgvProducts.Rows)
                    {
                        if (row.Tag is Product p && p.ProductID == selectProductId)
                        {
                            row.Selected = true;
                            _dgvProducts.CurrentCell = row.Cells["SKU"];
                            LoadProductIntoEditor(p);
                            return;
                        }
                    }
                }

                ResetEditorForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters(bool showNotFoundMessage = false)
        {
            if (_suppressFilterEvents) return;

            string keyword = _txtSearch.Text.Trim();
            int categoryId = (_cboFilterCategory.SelectedItem as Category)?.CategoryID ?? 0;

            string stockFilter = _cboFilterStatus.SelectedIndex switch
            {
                1 => "IN_STOCK",
                2 => "LOW_STOCK",
                3 => "OUT_OF_STOCK",
                _ => "ALL"
            };

            List<Product> products = _productRepo.SearchProducts(keyword, categoryId, stockFilter);

            _dgvProducts.SelectionChanged -= DgvProducts_SelectionChanged;
            _dgvProducts.Rows.Clear();
            _dgvProducts.Columns.Clear();

            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductID",
                HeaderText = "ID",
                FillWeight = 7,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "SKU", HeaderText = "SKU", FillWeight = 14 });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product Name", FillWeight = 26 });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "CategoryName", HeaderText = "Category", FillWeight = 16 });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 16 });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PurchasePrice",
                HeaderText = "Cost (₱)",
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UnitPrice",
                HeaderText = "Price (₱)",
                FillWeight = 12,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "QuantityInStock",
                HeaderText = "Qty",
                FillWeight = 9,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
            });
            _dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 16 });

            foreach (var p in products)
            {
                int rowIdx = _dgvProducts.Rows.Add(
                    p.ProductID,
                    p.SKU,
                    p.ProductName,
                    p.CategoryName,
                    p.SupplierName,
                    p.PurchasePrice.ToString("N2"),
                    p.UnitPrice.ToString("N2"),
                    p.QuantityInStock,
                    p.StockStatus);
                _dgvProducts.Rows[rowIdx].Tag = p;
            }

            if (products.Count == 0 && !string.IsNullOrEmpty(keyword))
            {
                _lblResultsCount.Text = $"No results for '{keyword}'";
                _lblResultsCount.ForeColor = UITheme.DangerRed;
                if (showNotFoundMessage)
                {
                    MessageBox.Show($"No product found matching '{keyword}'.", "Product Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                _lblResultsCount.Text = $"{products.Count} product(s)";
                _lblResultsCount.ForeColor = UITheme.ElectricBlue;
            }

            _dgvProducts.ClearSelection();
            _dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;
        }

        private void DgvProducts_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvProducts.CurrentRow?.Tag is Product selected)
            {
                LoadProductIntoEditor(selected);
            }
        }

        private void LoadProductIntoEditor(Product selected)
        {
            _selectedProductId = selected.ProductID;
            _lblFormMode.Text = $"Edit Product #{selected.ProductID}";
            _txtSku.Text = selected.SKU;
            _txtProductName.Text = selected.ProductName;
            _txtDescription.Text = selected.Description;
            _cboCategory.SelectedValue = selected.CategoryID;
            _cboSupplier.SelectedValue = selected.SupplierID ?? 0;
            _numPurchasePrice.Value = selected.PurchasePrice;
            _numUnitPrice.Value = selected.UnitPrice;
            _numInitialStock.Value = selected.QuantityInStock;
            _numInitialStock.Enabled = false;
            _lblInitialStockCaption.Text = "Current Stock";
            _numReorderLevel.Value = selected.ReorderLevel;

            _currentImagePath = selected.ImagePath;
            LoadThumbFromFile(_currentImagePath, selected.SKU);

            _btnSave.Text = "Update Product";
            _btnSave.Enabled = SessionManager.CanManageProducts;
            _btnSave.Visible = SessionManager.CanManageProducts;
            _btnDelete.Enabled = SessionManager.CanDeleteProducts;
            _btnDelete.Visible = SessionManager.CanDeleteProducts;
            _btnBrowseImage.Enabled = SessionManager.CanManageProducts;
            _btnBrowseImage.Visible = SessionManager.CanManageProducts;
            SetEditorReadOnly(!SessionManager.CanManageProducts);
            if (!SessionManager.CanManageProducts)
            {
                _lblFormMode.Text = $"Product Details #{selected.ProductID} (View Only)";
            }
        }

        private void SetEditorReadOnly(bool readOnly)
        {
            _txtSku.ReadOnly = readOnly;
            _txtProductName.ReadOnly = readOnly;
            _txtDescription.ReadOnly = readOnly;
            _cboCategory.Enabled = !readOnly;
            _cboSupplier.Enabled = !readOnly;
            _numPurchasePrice.Enabled = !readOnly;
            _numUnitPrice.Enabled = !readOnly;
            _numReorderLevel.Enabled = !readOnly;
            if (readOnly) _numInitialStock.Enabled = false;
        }

        private void ResetEditorForm()
        {
            _selectedProductId = 0;
            _lblFormMode.Text = SessionManager.CanManageProducts ? "Add Product" : "Product Details (View Only)";
            _txtSku.Clear();
            _txtProductName.Clear();
            _txtDescription.Clear();
            if (_cboCategory.Items.Count > 0) _cboCategory.SelectedIndex = 0;
            if (_cboSupplier.Items.Count > 0) _cboSupplier.SelectedIndex = 0;
            _numPurchasePrice.Value = 0M;
            _numUnitPrice.Value = 0M;
            _numInitialStock.Value = 0;
            _numInitialStock.Enabled = SessionManager.CanManageProducts;
            _lblInitialStockCaption.Text = "Opening Stock";
            _numReorderLevel.Value = 10;
            _currentImagePath = string.Empty;
            RenderDefaultThumb("IMG");

            _btnSave.Text = "Save Product";
            _btnSave.Enabled = SessionManager.CanManageProducts;
            _btnSave.Visible = SessionManager.CanManageProducts;
            _btnDelete.Enabled = false;
            _btnDelete.Visible = SessionManager.CanDeleteProducts;
            _btnBrowseImage.Enabled = SessionManager.CanManageProducts;
            _btnBrowseImage.Visible = SessionManager.CanManageProducts;
            SetEditorReadOnly(!SessionManager.CanManageProducts);
            _dgvProducts.ClearSelection();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!SessionManager.CanManageProducts)
            {
                MessageBox.Show("Access denied: Standard Users have view-only access to product master records.",
                    "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtSku.Text))
            {
                MessageBox.Show("Product ID / SKU cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSku.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtProductName.Text))
            {
                MessageBox.Show("Product Name cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtProductName.Focus();
                return;
            }

            if (_cboCategory.SelectedValue == null)
            {
                MessageBox.Show("Please select a category. Add one under Categories first if none exist.",
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedSupplierId = _cboSupplier.SelectedValue != null ? Convert.ToInt32(_cboSupplier.SelectedValue) : 0;
            string selectedSupplierName = selectedSupplierId > 0 && _cboSupplier.SelectedItem is Supplier s ? s.SupplierName : string.Empty;

            var product = new Product(
                sku: _txtSku.Text,
                productName: _txtProductName.Text,
                categoryId: Convert.ToInt32(_cboCategory.SelectedValue),
                unitPrice: _numUnitPrice.Value,
                quantityInStock: (int)_numInitialStock.Value,
                reorderLevel: (int)_numReorderLevel.Value,
                supplierName: selectedSupplierName,
                purchasePrice: _numPurchasePrice.Value,
                description: _txtDescription.Text,
                supplierId: selectedSupplierId > 0 ? selectedSupplierId : null)
            {
                ProductID = _selectedProductId,
                ImagePath = _currentImagePath
            };

            if (!product.Validate(out string validationError))
            {
                MessageBox.Show(validationError, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_productRepo.SkuExists(product.SKU, _selectedProductId))
                {
                    MessageBox.Show($"A product with SKU '{product.SKU}' already exists.",
                        "Duplicate SKU", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtSku.Focus();
                    _txtSku.SelectAll();
                    return;
                }

                if (_selectedProductId == 0)
                {
                    _productRepo.AddProduct(product);
                    MessageBox.Show($"Product '{product.ProductName}' saved.", "Product Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _productRepo.UpdateProduct(product);
                    MessageBox.Show($"Product '{product.ProductName}' updated.", "Product Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                ApplyFilters(showNotFoundMessage: false);
                ResetEditorForm();
                InventoryChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save product:\n{ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (!SessionManager.CanDeleteProducts)
            {
                MessageBox.Show("Access denied: Only the Administrator can delete products.",
                    "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_selectedProductId <= 0)
            {
                MessageBox.Show("Select a product to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Delete '{_txtProductName.Text}' ({_txtSku.Text})?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    _productRepo.DeleteProduct(_selectedProductId);
                    ApplyFilters(showNotFoundMessage: false);
                    ResetEditorForm();
                    InventoryChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting product:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DgvProducts_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_dgvProducts.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                UITheme.PaintStatusPillCell(e, e.Value.ToString());
            }
        }
    }
}
