using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class ActivityHistoryForm : UserControl
    {
        private readonly ActivityLogRepository _activityRepo = new ActivityLogRepository();

        private TextBox _txtSearch = null!;
        private ComboBox _cboAction = null!;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;
        private Label _lblCount = null!;
        private DataGridView _dgvLogs = null!;
        private Button _btnReset = null!;
        private bool _suppress = false;

        public ActivityHistoryForm()
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var filterCard = UITheme.CreateCardPanel();
            filterCard.Dock = DockStyle.Fill;
            filterCard.Margin = new Padding(0, 0, 0, 12);

            filterCard.Controls.Add(new Label
            {
                Text = "Activity History",
                Font = UITheme.FontSectionHeader,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(16, 10),
                AutoSize = true
            });

            _lblCount = new Label
            {
                Text = "0 events",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.ElectricBlue,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(filterCard.Width - 140, 12),
                AutoSize = true
            };

            _txtSearch = new TextBox
            {
                PlaceholderText = "Search action, product, or details...",
                Location = new Point(16, 40),
                Size = new Size(210, 27)
            };
            UITheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => LoadGrid();

            _cboAction = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(236, 40),
                Size = new Size(165, 27)
            };
            UITheme.StyleComboBox(_cboAction);
            _cboAction.Items.AddRange(new object[]
            {
                "ALL",
                "Product Added",
                "Product Updated",
                "Product Deleted",
                "Stock Received",
                "Stock Released",
                "Category Added",
                "Category Updated",
                "Category Deleted",
                "Supplier Added",
                "Supplier Updated",
                "Supplier Deleted",
                "Backup Created",
                "Data Restored"
            });
            _cboAction.SelectedIndex = 0;
            _cboAction.SelectedIndexChanged += (s, e) => LoadGrid();

            filterCard.Controls.Add(new Label { Text = "From:", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(414, 44), AutoSize = true });
            _dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = UITheme.FontRegular, Value = DateTime.Now.AddDays(-30), Location = new Point(454, 40), Size = new Size(112, 27) };
            _dtpFrom.ValueChanged += (s, e) => LoadGrid();

            filterCard.Controls.Add(new Label { Text = "To:", Font = UITheme.FontSmall, ForeColor = UITheme.TextSecondary, BackColor = Color.Transparent, Location = new Point(576, 44), AutoSize = true });
            _dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = UITheme.FontRegular, Value = DateTime.Now, Location = new Point(602, 40), Size = new Size(112, 27) };
            _dtpTo.ValueChanged += (s, e) => LoadGrid();

            _btnReset = new Button { Text = "Reset", Location = new Point(724, 39), Size = new Size(76, 29) };
            UITheme.StyleSecondaryButton(_btnReset);
            _btnReset.Click += (s, e) =>
            {
                _suppress = true;
                _txtSearch.Clear();
                _cboAction.SelectedIndex = 0;
                _dtpFrom.Value = DateTime.Now.AddDays(-30);
                _dtpTo.Value = DateTime.Now;
                _suppress = false;
                LoadGrid();
            };

            filterCard.Controls.Add(_lblCount);
            filterCard.Controls.Add(_txtSearch);
            filterCard.Controls.Add(_cboAction);
            filterCard.Controls.Add(_dtpFrom);
            filterCard.Controls.Add(_dtpTo);
            filterCard.Controls.Add(_btnReset);

            var gridCard = UITheme.CreateCardPanel();
            gridCard.Dock = DockStyle.Fill;

            _dgvLogs = new DataGridView { Dock = DockStyle.Fill };
            UITheme.StyleDataGridView(_dgvLogs);
            gridCard.Controls.Add(_dgvLogs);

            mainLayout.Controls.Add(filterCard, 0, 0);
            mainLayout.Controls.Add(gridCard, 0, 1);
            Controls.Add(mainLayout);
        }

        public void ApplyTheme()
        {
            UITheme.ApplyThemeRecursive(this);
            Invalidate(true);
        }

        public void RefreshData()
        {
            LoadGrid();
        }

        private void LoadGrid()
        {
            if (_suppress) return;
            try
            {
                string action = _cboAction.SelectedItem?.ToString() ?? "ALL";
                List<ActivityLog> logs = _activityRepo.GetActivities(_dtpFrom.Value, _dtpTo.Value, action, _txtSearch.Text, 300);

                _dgvLogs.Rows.Clear();
                _dgvLogs.Columns.Clear();

                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "ActivityID",
                    HeaderText = "ID",
                    FillWeight = 8,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date & Time", FillWeight = 18 });
                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Action",
                    HeaderText = "Action",
                    FillWeight = 18,
                    DefaultCellStyle = new DataGridViewCellStyle { Font = UITheme.FontBold, ForeColor = UITheme.ElectricBlue }
                });
                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Related", HeaderText = "Related Record", FillWeight = 20 });
                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actor", HeaderText = "Performed By", FillWeight = 16 });
                _dgvLogs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", HeaderText = "Details", FillWeight = 30 });

                foreach (var log in logs)
                {
                    _dgvLogs.Rows.Add(
                        log.ActivityID,
                        log.ActivityDate.ToString("yyyy-MM-dd hh:mm tt"),
                        log.Action,
                        log.RelatedRecord,
                        string.IsNullOrWhiteSpace(log.PerformedBy) ? "System" : log.PerformedBy,
                        log.Details);
                }

                _lblCount.Text = $"{logs.Count} event(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading activity history:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
