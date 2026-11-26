using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using InventoryManagementSystem.Database;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Theme;

namespace InventoryManagementSystem.Forms
{
    public class ProductDetailsDialog : Form
    {
        private readonly Product _product;
        private readonly StockTransactionRepository _txRepo = new StockTransactionRepository();

        private PictureBox _picProduct = null!;
        private ListBox _lstTransactions = null!;
        private Label _lblStatusBadge = null!;

        public ProductDetailsDialog(Product product)
        {
            _product = product ?? throw new ArgumentNullException(nameof(product));

            Text = $"Product Details - {_product.SKU}";
            Size = new Size(640, 535);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = UITheme.CanvasBg;
            Font = UITheme.FontRegular;
            DoubleBuffered = true;

            InitializeControls();
            Load += ProductDetailsDialog_Load;
        }

        private void InitializeControls()
        {
            var banner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = UITheme.TopBarBg
            };
            banner.Paint += (s, e) =>
            {
                using (Pen line = new Pen(Color.FromArgb(80, 56, 139, 253), 1.5f))
                {
                    e.Graphics.DrawLine(line, 0, banner.Height - 1, banner.Width, banner.Height - 1);
                }
            };

            var lblBannerTitle = new Label
            {
                Text = _product.ProductName,
                Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(18, 10),
                AutoSize = true
            };

            var lblBannerSub = new Label
            {
                Text = $"SKU: {_product.SKU}  |  Category: {_product.CategoryName}  |  ID: {_product.ProductID}",
                Font = UITheme.FontSmall,
                ForeColor = UITheme.SidebarTextMuted,
                Location = new Point(20, 38),
                AutoSize = true
            };

            _lblStatusBadge = new Label
            {
                Text = _product.StockStatus,
                Font = UITheme.FontBold,
                ForeColor = Color.White,
                Location = new Point(500, 19),
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4)
            };
            ApplyStatusStyle(_lblStatusBadge, _product.StockStatus);

            banner.Controls.Add(lblBannerTitle);
            banner.Controls.Add(lblBannerSub);
            banner.Controls.Add(_lblStatusBadge);

            var grpDetails = new GroupBox
            {
                Text = "Product Information",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.CardSurface,
                Location = new Point(18, 80),
                Size = new Size(588, 206)
            };

            _picProduct = new PictureBox
            {
                Location = new Point(18, 28),
                Size = new Size(128, 128),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UITheme.InputSurface
            };
            DrawPlaceholderImage();

            var btnLoadPhoto = new Button
            {
                Text = "Choose Image...",
                Location = new Point(18, 162),
                Size = new Size(128, 30)
            };
            UITheme.StyleSecondaryButton(btnLoadPhoto);
            btnLoadPhoto.Click += BtnLoadPhoto_Click;

            int infoX = 168;
            int infoY = 24;
            grpDetails.Controls.Add(CreateDetailRow("Product ID:", $"{_product.ProductID}   (SKU: {_product.SKU})", infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Purchase Price:", UITheme.FormatCurrency(_product.PurchasePrice), infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Selling Price:", UITheme.FormatCurrency(_product.UnitPrice), infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Quantity:", $"{_product.QuantityInStock} (Min Stock: {_product.ReorderLevel})", infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Total Value:", UITheme.FormatCurrency(_product.TotalStockValue), infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Supplier:", string.IsNullOrWhiteSpace(_product.SupplierName) ? "-" : _product.SupplierName, infoX, infoY)); infoY += 24;
            grpDetails.Controls.Add(CreateDetailRow("Description:", string.IsNullOrWhiteSpace(_product.Description) ? "-" : _product.Description, infoX, infoY));

            grpDetails.Controls.Add(_picProduct);
            grpDetails.Controls.Add(btnLoadPhoto);

            var grpHistory = new GroupBox
            {
                Text = "Recent Stock History",
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextSecondary,
                BackColor = UITheme.CardSurface,
                Location = new Point(18, 296),
                Size = new Size(588, 148)
            };

            _lstTransactions = new ListBox
            {
                Font = UITheme.FontRegular,
                BackColor = UITheme.InputSurface,
                ForeColor = UITheme.TextPrimary,
                Location = new Point(16, 26),
                Size = new Size(556, 108),
                BorderStyle = BorderStyle.FixedSingle
            };
            grpHistory.Controls.Add(_lstTransactions);

            var btnClose = new GradientButton
            {
                Text = "Close",
                Location = new Point(496, 454),
                Size = new Size(110, 34),
                CornerRadius = 9,
                GradientStart = UITheme.ElectricBlue,
                GradientEnd = UITheme.PrimaryBlue,
                DialogResult = DialogResult.OK
            };

            Controls.Add(banner);
            Controls.Add(grpDetails);
            Controls.Add(grpHistory);
            Controls.Add(btnClose);
            AcceptButton = btnClose;
        }

        private Control CreateDetailRow(string caption, string value, int x, int y)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(400, 24),
                BackColor = Color.Transparent
            };

            panel.Controls.Add(new Label
            {
                Text = caption,
                Font = UITheme.FontSmall,
                ForeColor = UITheme.TextMuted,
                BackColor = Color.Transparent,
                Location = new Point(0, 3),
                Size = new Size(100, 18)
            });

            panel.Controls.Add(new Label
            {
                Text = value,
                Font = UITheme.FontBold,
                ForeColor = UITheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(104, 2),
                AutoSize = true
            });

            return panel;
        }

        private void ApplyStatusStyle(Label badge, string status)
        {
            if (status.Contains("Out", StringComparison.OrdinalIgnoreCase))
                badge.BackColor = UITheme.DangerRed;
            else if (status.Contains("Low", StringComparison.OrdinalIgnoreCase))
                badge.BackColor = UITheme.WarningAmber;
            else
                badge.BackColor = UITheme.SuccessGreen;
        }

        private void DrawPlaceholderImage()
        {
            if (!string.IsNullOrWhiteSpace(_product.ImagePath) && File.Exists(_product.ImagePath))
            {
                try
                {
                    using var stream = new FileStream(_product.ImagePath, FileMode.Open, FileAccess.Read);
                    _picProduct.Image = Image.FromStream(stream);
                    return;
                }
                catch
                {
                }
            }

            Bitmap bmp = new Bitmap(128, 128);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(14, 26, 52));
                UITheme.DrawIsometricCube(g, new RectangleF(28, 18, 72, 72), glowing: true);
                using (Font subF = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold))
                using (Brush subB = new SolidBrush(Color.FromArgb(191, 219, 254)))
                {
                    SizeF skuSz = g.MeasureString(_product.SKU, subF);
                    g.DrawString(_product.SKU, subF, subB, Math.Max(6, (128 - skuSz.Width) / 2f), 98);
                }
            }
            _picProduct.Image = bmp;
        }

        private void BtnLoadPhoto_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select Product Photo";
                ofd.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        using var stream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read);
                        _picProduct.Image = Image.FromStream(stream);
                        _product.ImagePath = ofd.FileName;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not load image:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void ProductDetailsDialog_Load(object? sender, EventArgs e)
        {
            try
            {
                List<StockTransaction> transactions = _txRepo.GetTransactions(searchKeyword: _product.SKU, maxRows: 50);
                _lstTransactions.Items.Clear();

                int matchCount = 0;
                foreach (var tx in transactions)
                {
                    if (tx.ProductID == _product.ProductID)
                    {
                        string sign = tx.TransactionType == "IN" ? "+" : "-";
                        _lstTransactions.Items.Add(
                            $"{tx.TransactionDate:yyyy-MM-dd HH:mm}  |  {tx.TransactionType,-3} {sign}{tx.Quantity}  (Stock: {tx.PreviousStock} -> {tx.NewStock})  Ref: {tx.ReferenceNo}");
                        matchCount++;
                    }
                }

                if (matchCount == 0)
                {
                    _lstTransactions.Items.Add("No stock history recorded for this product.");
                }
            }
            catch (Exception ex)
            {
                _lstTransactions.Items.Add($"Unable to load history: {ex.Message}");
            }
        }
    }
}
