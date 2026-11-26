using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class SuppliersForm : UserControl
    {
        private readonly SupplierRepository _supplierRepo = new SupplierRepository();

        public event Action? SuppliersChanged;

        private TextBox _txtSearch = null!;
        private Label _lblTotalSuppliers = null!;
        private DataGridView _dgvSuppliers = null!;

        private int _selectedSupplierId = 0;
        private Label _lblFormMode = null!;
        private TextBox _txtSupplierName = null!;
        private TextBox _txtContactPerson = null!;
        private TextBox _txtPhone = null!;
        private TextBox _txtEmail = null!;
        private TextBox _txtAddress = null!;
        private Label _lblLinkedInfo = null!;

        private GradientButton _btnSave = null!;
        private GradientButton _btnDelete = null!;
        private Button _btnClear = null!;
        private Button _btnResetSearch = null!;

        public SuppliersForm()
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
            rootSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 368F));

            // Left: Search + Suppliers Table
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

            searchCard.Controls.Add(new Label
            {
                Text = "Supplier Management",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            });

            _lblTotalSuppliers = new Label
            {
                Text = "0 suppliers",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(searchCard.Width - 135, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search supplier name, contact, phone, email...",
                Location = new Point(16, 38),
                Size = new Size(360, 27)
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => LoadGrid();

            _btnResetSearch = new Button
            {
                Text = "Reset",
                Location = new Point(386, 36),
                Size = new Size(84, 29)
            };
            UITheme.StyleSecondaryButton(_btnResetSearch);
            _btnResetSearch.Click += (s, e) =>
            {
                _txtSearch.Clear();
                LoadGrid();
            };

            searchCard.Controls.Add(_lblTotalSuppliers);
            searchCard.Controls.Add(_txtSearch);
            searchCard.Controls.Add(_btnResetSearch);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            _dgvSuppliers = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvSuppliers);
            _dgvSuppliers.SelectionChanged += DgvSuppliers_SelectionChanged;
            gridCard.Controls.Add(_dgvSuppliers);

            leftLayout.Controls.Add(searchCard, 0, 0);
            leftLayout.Controls.Add(gridCard, 0, 1);

            // Right: Supplier Editor Card
            var formCard = UITheme.CreateCardPanel(UITheme.ElectricBlue, topTint: true);
            formCard.Dock = DockStyle.Fill;

            int y = 14;
            _lblFormMode = new Label
            {
                Text = "Add Supplier",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                AutoSize = true
            };
            formCard.Controls.Add(_lblFormMode);
            y += 34;

            formCard.Controls.Add(CreateFieldLabel("Supplier Name *", 16, y)); y += 20;
            _txtSupplierName = new TextBox { Location = new Point(16, y), Size = new Size(326, 27), PlaceholderText = "Company or vendor name" };
            UITheme.StyleTextBox(_txtSupplierName);
            formCard.Controls.Add(_txtSupplierName);
            y += 36;

            formCard.Controls.Add(CreateFieldLabel("Contact Person", 16, y)); y += 20;
            _txtContactPerson = new TextBox { Location = new Point(16, y), Size = new Size(326, 27), PlaceholderText = "Primary representative" };
            UITheme.StyleTextBox(_txtContactPerson);
            formCard.Controls.Add(_txtContactPerson);
            y += 36;

            formCard.Controls.Add(CreateFieldLabel("Phone Number", 16, y)); y += 20;
            _txtPhone = new TextBox { Location = new Point(16, y), Size = new Size(326, 27), PlaceholderText = "e.g., +63 912 345 6789" };
            UITheme.StyleTextBox(_txtPhone);
            formCard.Controls.Add(_txtPhone);
            y += 36;

            formCard.Controls.Add(CreateFieldLabel("Email Address", 16, y)); y += 20;
            _txtEmail = new TextBox { Location = new Point(16, y), Size = new Size(326, 27), PlaceholderText = "sales@supplier.com" };
            UITheme.StyleTextBox(_txtEmail);
            formCard.Controls.Add(_txtEmail);
            y += 36;

            formCard.Controls.Add(CreateFieldLabel("Business Address", 16, y)); y += 20;
            _txtAddress = new TextBox { Location = new Point(16, y), Size = new Size(326, 62), Multiline = true, PlaceholderText = "Street, City, Province" };
            UITheme.StyleTextBox(_txtAddress);
            formCard.Controls.Add(_txtAddress);
            y += 72;

            _lblLinkedInfo = new Label
            {
                Text = "Enter supplier details to register a vendor.",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(16, y),
                Size = new Size(326, 26)
            };
            formCard.Controls.Add(_lblLinkedInfo);
            y += 32;

            _btnSave = new GradientButton
            {
                Text = "Save Supplier",
                Location = new Point(16, y),
                Size = new Size(326, 38),
                CornerRadius = 9,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue
            };
            _btnSave.Click += BtnSave_Click;
            formCard.Controls.Add(_btnSave);
            y += 45;

            _btnClear = new Button { Text = "Clear", Location = new Point(16, y), Size = new Size(156, 34) };
            UITheme.StyleSecondaryButton(_btnClear);
            _btnClear.Click += (s, e) => ResetEditor();
            formCard.Controls.Add(_btnClear);

            _btnDelete = new GradientButton
            {
                Text = "Delete",
                Location = new Point(186, y),
                Size = new Size(156, 34),
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
                List<Supplier> suppliers = _supplierRepo.GetAllSuppliers(_txtSearch.Text);

                _dgvSuppliers.SelectionChanged -= DgvSuppliers_SelectionChanged;
                _dgvSuppliers.Rows.Clear();
                _dgvSuppliers.Columns.Clear();

                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "SupplierID",
                    HeaderText = "ID",
                    FillWeight = 8,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierName", HeaderText = "Supplier Name", FillWeight = 24 });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContactPerson", HeaderText = "Contact", FillWeight = 18 });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "PhoneNumber", HeaderText = "Phone", FillWeight = 16 });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmailAddress", HeaderText = "Email", FillWeight = 20 });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "BusinessAddress", HeaderText = "Address", FillWeight = 22 });
                _dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Products",
                    HeaderText = "Products",
                    FillWeight = 11,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = UITheme.FontBold }
                });

                foreach (var s in suppliers)
                {
                    int idx = _dgvSuppliers.Rows.Add(
                        s.SupplierID,
                        s.SupplierName,
                        s.ContactPerson,
                        s.PhoneNumber,
                        s.EmailAddress,
                        s.BusinessAddress,
                        s.ProductCount);
                    _dgvSuppliers.Rows[idx].Tag = s;
                }

                _lblTotalSuppliers.Text = $"{suppliers.Count} supplier(s)";
                _dgvSuppliers.ClearSelection();
                _dgvSuppliers.SelectionChanged += DgvSuppliers_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvSuppliers_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvSuppliers.CurrentRow?.Tag is Supplier s)
            {
                _selectedSupplierId = s.SupplierID;
                _lblFormMode.Text = $"Edit Supplier #{s.SupplierID}";
                _txtSupplierName.Text = s.SupplierName;
                _txtContactPerson.Text = s.ContactPerson;
                _txtPhone.Text = s.PhoneNumber;
                _txtEmail.Text = s.EmailAddress;
                _txtAddress.Text = s.BusinessAddress;
                _lblLinkedInfo.Text = $"Linked to {s.ProductCount} product(s).";
                _btnSave.Text = "Update Supplier";
                _btnDelete.Enabled = true;
            }
        }

        private void ResetEditor()
        {
            _selectedSupplierId = 0;
            _lblFormMode.Text = "Add Supplier";
            _txtSupplierName.Clear();
            _txtContactPerson.Clear();
            _txtPhone.Clear();
            _txtEmail.Clear();
            _txtAddress.Clear();
            _lblLinkedInfo.Text = "Enter supplier details to register a vendor.";
            _btnSave.Text = "Save Supplier";
            _btnDelete.Enabled = false;
            _dgvSuppliers.ClearSelection();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var supplier = new Supplier(
                _selectedSupplierId,
                _txtSupplierName.Text,
                _txtContactPerson.Text,
                _txtPhone.Text,
                _txtEmail.Text,
                _txtAddress.Text);

            if (!supplier.Validate(out string validationError))
            {
                MessageBox.Show(validationError, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSupplierName.Focus();
                return;
            }

            try
            {
                if (_selectedSupplierId == 0)
                {
                    _supplierRepo.AddSupplier(supplier);
                    MessageBox.Show($"Supplier '{supplier.SupplierName}' saved.", "Supplier Added",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _supplierRepo.UpdateSupplier(supplier);
                    MessageBox.Show($"Supplier '{supplier.SupplierName}' updated.", "Supplier Updated",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                LoadGrid();
                ResetEditor();
                SuppliersChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save supplier:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedSupplierId <= 0) return;

            DialogResult confirm = MessageBox.Show(
                $"Delete supplier '{_txtSupplierName.Text}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                if (_supplierRepo.DeleteSupplier(_selectedSupplierId, out string errorMsg))
                {
                    LoadGrid();
                    ResetEditor();
                    SuppliersChanged?.Invoke();
                }
                else
                {
                    MessageBox.Show(errorMsg, "Cannot Delete Supplier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
