using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class CategoriesForm : UserControl
    {
        private readonly CategoryRepository _categoryRepo = new CategoryRepository();

        public event Action? CategoriesChanged;

        private TextBox _txtSearch = null!;
        private Label _lblTotalCategories = null!;
        private DataGridView _dgvCategories = null!;

        private int _selectedCategoryId = 0;
        private Label _lblFormMode = null!;
        private TextBox _txtCategoryName = null!;
        private TextBox _txtDescription = null!;
        private Label _lblAssignedProductsInfo = null!;

        private GradientButton _btnSave = null!;
        private GradientButton _btnDelete = null!;
        private Button _btnClear = null!;
        private Button _btnRefresh = null!;

        public CategoriesForm()
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
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));

            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.Transparent
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var searchCard = UITheme.CreateCardPanel();
            searchCard.Dock = DockStyle.Fill;
            searchCard.Margin = new Padding(0, 0, 0, 12);

            var lblHeader = new Label
            {
                Text = "Product Categories",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            };

            _lblTotalCategories = new Label
            {
                Text = "0 categories",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(searchCard.Width - 135, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search category name or description...",
                Location = new Point(16, 38),
                Size = new Size(360, 27)
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => LoadGrid();

            _btnRefresh = new Button
            {
                Text = "Reset",
                Location = new Point(386, 36),
                Size = new Size(84, 29)
            };
            UITheme.StyleSecondaryButton(_btnRefresh);
            _btnRefresh.Click += (s, e) =>
            {
                _txtSearch.Clear();
                LoadGrid();
            };

            searchCard.Controls.Add(lblHeader);
            searchCard.Controls.Add(_lblTotalCategories);
            searchCard.Controls.Add(_txtSearch);
            searchCard.Controls.Add(_btnRefresh);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            _dgvCategories = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvCategories);
            _dgvCategories.SelectionChanged += DgvCategories_SelectionChanged;
            gridCard.Controls.Add(_dgvCategories);

            leftLayout.Controls.Add(searchCard, 0, 0);
            leftLayout.Controls.Add(gridCard, 0, 1);

            var formCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            formCard.Dock = DockStyle.Fill;
            formCard.AutoScroll = true;

            int y = 16;
            _lblFormMode = new Label
            {
                Text = "Add Category",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            formCard.Controls.Add(_lblFormMode);
            y += 38;

            formCard.Controls.Add(new Label
            {
                Text = "Category Name *",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            });
            y += 22;

            _txtCategoryName = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(320, 27),
                PlaceholderText = "Enter category name"
            };
            UITheme.StyleTextBox(_txtCategoryName);
            formCard.Controls.Add(_txtCategoryName);
            y += 42;

            formCard.Controls.Add(new Label
            {
                Text = "Description",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            });
            y += 22;

            _txtDescription = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(320, 100),
                Multiline = true,
                PlaceholderText = "Optional category notes"
            };
            UITheme.StyleTextBox(_txtDescription);
            formCard.Controls.Add(_txtDescription);
            y += 116;

            _lblAssignedProductsInfo = new Label
            {
                Text = "Enter a unique category name.",
                UseMnemonic = false,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                MaximumSize = new Size(320, 0),
                AutoSize = true
            };
            formCard.Controls.Add(_lblAssignedProductsInfo);
            y += 46;

            _btnSave = new GradientButton
            {
                Text = "Save Category",
                Location = new Point(16, y),
                Size = new Size(320, 38),
                CornerRadius = 9,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            _btnSave.Click += BtnSave_Click;
            formCard.Controls.Add(_btnSave);
            y += 46;

            _btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(16, y),
                Size = new Size(154, 34)
            };
            UITheme.StyleSecondaryButton(_btnClear);
            _btnClear.Click += (s, e) => ResetEditor();
            formCard.Controls.Add(_btnClear);

            _btnDelete = new GradientButton
            {
                Text = "Delete",
                Location = new Point(182, y),
                Size = new Size(154, 34),
                CornerRadius = 8,
                GradientStart = Color.FromArgb(239, 68, 68),
                GradientEnd = UITheme.DangerRed,
                Enabled = false
            };
            _btnDelete.Click += BtnDelete_Click;
            formCard.Controls.Add(_btnDelete);

            rootSplit.Controls.Add(leftLayout, 0, 0);
            rootSplit.Controls.Add(formCard, 1, 0);
            Controls.Add(rootSplit);
        }

        public void ApplyTheme()
        {
            UITheme.ApplyThemeRecursive(this);
            Invalidate(true);
        }

        public void RefreshData()
        {
            LoadGrid();
            ResetEditor();
        }

        private void LoadGrid()
        {
            try
            {
                List<Category> categories = _categoryRepo.GetAllCategories(_txtSearch.Text);

                _dgvCategories.SelectionChanged -= DgvCategories_SelectionChanged;
                _dgvCategories.Rows.Clear();
                _dgvCategories.Columns.Clear();

                _dgvCategories.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "CategoryID",
                    HeaderText = "ID",
                    FillWeight = 10,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                _dgvCategories.Columns.Add(new DataGridViewTextBoxColumn { Name = "CategoryName", HeaderText = "Category Name", FillWeight = 28 });
                _dgvCategories.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", FillWeight = 42 });
                _dgvCategories.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "ProductCount",
                    HeaderText = "Products",
                    FillWeight = 16,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
                });
                _dgvCategories.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", HeaderText = "Created", FillWeight = 16 });

                foreach (var cat in categories)
                {
                    int idx = _dgvCategories.Rows.Add(
                        cat.CategoryID,
                        cat.CategoryName,
                        cat.Description,
                        cat.ProductCount,
                        cat.CreatedAt.ToString("yyyy-MM-dd"));
                    _dgvCategories.Rows[idx].Tag = cat;
                }

                _lblTotalCategories.Text = $"{categories.Count} category(s)";
                _dgvCategories.ClearSelection();
                _dgvCategories.SelectionChanged += DgvCategories_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading categories:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvCategories_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvCategories.CurrentRow?.Tag is Category cat)
            {
                _selectedCategoryId = cat.CategoryID;
                _lblFormMode.Text = $"Edit Category #{cat.CategoryID}";
                _txtCategoryName.Text = cat.CategoryName;
                _txtDescription.Text = cat.Description;
                _lblAssignedProductsInfo.Text = $"{cat.ProductCount} product(s) assigned to this category.";
                _btnSave.Text = "Update Category";
                _btnDelete.Enabled = true;
            }
        }

        private void ResetEditor()
        {
            _selectedCategoryId = 0;
            _lblFormMode.Text = "Add Category";
            _txtCategoryName.Clear();
            _txtDescription.Clear();
            _lblAssignedProductsInfo.Text = "Enter a unique category name.";
            _btnSave.Text = "Save Category";
            _btnDelete.Enabled = false;
            _dgvCategories.ClearSelection();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var category = new Category(_selectedCategoryId, _txtCategoryName.Text, _txtDescription.Text);
            if (!category.Validate(out string validationError))
            {
                MessageBox.Show(validationError, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtCategoryName.Focus();
                return;
            }

            try
            {
                if (_selectedCategoryId == 0)
                {
                    _categoryRepo.AddCategory(category);
                    MessageBox.Show($"Category '{category.CategoryName}' saved.", "Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _categoryRepo.UpdateCategory(category);
                    MessageBox.Show($"Category '{category.CategoryName}' updated.", "Updated",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                LoadGrid();
                ResetEditor();
                CategoriesChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save category (name must be unique):\n{ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedCategoryId <= 0) return;

            DialogResult confirm = MessageBox.Show(
                $"Delete category '{_txtCategoryName.Text}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                if (_categoryRepo.DeleteCategory(_selectedCategoryId, out string errorMsg))
                {
                    LoadGrid();
                    ResetEditor();
                    CategoriesChanged?.Invoke();
                }
                else
                {
                    MessageBox.Show(errorMsg, "Cannot Delete Category", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
