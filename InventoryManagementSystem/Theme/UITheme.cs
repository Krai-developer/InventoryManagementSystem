using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InventoryManagementSystem.Theme
{
    /// <summary>
    /// Double-buffered glass-style rounded card with subtle gradient fill and glowing electric-blue edge.
    /// Adapts automatically when Dark Mode is toggled.
    /// </summary>
    public class GlassCardPanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 14;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color GlowAccent { get; set; } = Color.FromArgb(56, 139, 253);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowTopGlowTint { get; set; } = false;

        public GlassCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle shadowRect = new Rectangle(3, 4, Math.Max(2, Width - 5), Math.Max(2, Height - 5));
            Rectangle rect = new Rectangle(1, 1, Math.Max(2, Width - 4), Math.Max(2, Height - 5));

            if (UITheme.IsDarkMode)
            {
                using (GraphicsPath shadowPath = UITheme.CreateRoundedPath(shadowRect, CornerRadius))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(65, 2, 6, 18)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }
            else
            {
                using (GraphicsPath shadowPath = UITheme.CreateRoundedPath(shadowRect, CornerRadius))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(20, 15, 23, 42)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            using (GraphicsPath path = UITheme.CreateRoundedPath(rect, CornerRadius))
            {
                if (UITheme.IsDarkMode)
                {
                    Color topFill = ShowTopGlowTint
                        ? UITheme.BlendColor(Color.FromArgb(18, 34, 66), GlowAccent, 0.16f)
                        : Color.FromArgb(16, 29, 56);
                    Color bottomFill = Color.FromArgb(9, 18, 38);

                    using (LinearGradientBrush fill = new LinearGradientBrush(rect, topFill, bottomFill, 135f))
                    {
                        g.FillPath(fill, path);
                    }

                    // Soft top glass sheen
                    Rectangle sheenRect = new Rectangle(rect.X + 1, rect.Y + 1, Math.Max(2, rect.Width - 2), Math.Max(6, rect.Height / 3));
                    using (GraphicsPath sheenPath = UITheme.CreateRoundedPath(sheenRect, CornerRadius))
                    using (LinearGradientBrush sheen = new LinearGradientBrush(sheenRect, Color.FromArgb(16, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    {
                        g.FillPath(sheen, sheenPath);
                    }

                    // Outer subtle glow rim
                    Color borderColor = ShowTopGlowTint
                        ? Color.FromArgb(135, GlowAccent)
                        : Color.FromArgb(88, 56, 139, 253);
                    using (Pen borderPen = new Pen(borderColor, 1.25f))
                    {
                        g.DrawPath(borderPen, path);
                    }

                    // Crisp subtle top inner highlight line
                    Rectangle innerTop = new Rectangle(rect.X + CornerRadius, rect.Y + 1, Math.Max(1, rect.Width - CornerRadius * 2), 1);
                    Color highlightCol = ShowTopGlowTint
                        ? Color.FromArgb(130, GlowAccent)
                        : Color.FromArgb(65, 147, 197, 253);
                    using (Pen highlightPen = new Pen(highlightCol, 1.2f))
                    {
                        g.DrawLine(highlightPen, innerTop.Left, innerTop.Top, innerTop.Right, innerTop.Top);
                    }
                }
                else
                {
                    using (SolidBrush fill = new SolidBrush(Color.White))
                    using (Pen borderPen = new Pen(Color.FromArgb(216, 224, 236), 1f))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(borderPen, path);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rounded gradient button with hover glow, used for active sidebar pills and Quick Action tiles.
    /// </summary>
    public class GradientButton : Button
    {
        private bool _isHovered = false;
        private bool _isPressed = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color GradientStart { get; set; } = Color.FromArgb(37, 99, 235);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color GradientEnd { get; set; } = Color.FromArgb(29, 78, 216);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 10;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsOutline { get; set; } = false;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IconKind { get; set; } = string.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActiveNav { get; set; } = false;

        public GradientButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = UITheme.FontBold;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _isPressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Paint parent background so corners are clean
            if (Parent != null)
            {
                using (SolidBrush bg = new SolidBrush(Parent.BackColor == Color.Transparent ? UITheme.CardSurface : Parent.BackColor))
                {
                    g.FillRectangle(bg, ClientRectangle);
                }
            }

            Rectangle rect = new Rectangle(1, 1, Math.Max(2, Width - 3), Math.Max(2, Height - 3));
            using (GraphicsPath path = UITheme.CreateRoundedPath(rect, CornerRadius))
            {
                if (!Enabled)
                {
                    using (SolidBrush dis = new SolidBrush(Color.FromArgb(35, 48, 72)))
                    {
                        g.FillPath(dis, path);
                    }
                }
                else if (IsOutline)
                {
                    Color fillC = _isHovered
                        ? (UITheme.IsDarkMode ? Color.FromArgb(24, 46, 86) : Color.FromArgb(236, 243, 255))
                        : (UITheme.IsDarkMode ? Color.FromArgb(14, 26, 52) : Color.FromArgb(245, 248, 252));
                    Color penC = _isHovered
                        ? Color.FromArgb(140, 56, 189, 248)
                        : (UITheme.IsDarkMode ? Color.FromArgb(52, 56, 139, 253) : Color.FromArgb(203, 213, 225));
                    using (SolidBrush fill = new SolidBrush(fillC))
                    using (Pen pen = new Pen(penC, 1.1f))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    Color c1 = _isPressed ? ControlPaint.Dark(GradientStart, 0.1f)
                             : _isHovered ? ControlPaint.Light(GradientStart, 0.12f)
                             : GradientStart;
                    Color c2 = _isPressed ? ControlPaint.Dark(GradientEnd, 0.1f)
                             : _isHovered ? ControlPaint.Light(GradientEnd, 0.12f)
                             : GradientEnd;

                    using (LinearGradientBrush grad = new LinearGradientBrush(rect, c1, c2, 115f))
                    {
                        g.FillPath(grad, path);
                    }

                    using (Pen rim = new Pen(Color.FromArgb(_isHovered ? 190 : 110, Color.White), 1f))
                    {
                        g.DrawPath(rim, path);
                    }

                    if (IsActiveNav)
                    {
                        using (SolidBrush bar = new SolidBrush(Color.FromArgb(224, 242, 254)))
                        {
                            g.FillRectangle(bar, rect.X + 3, rect.Y + 6, 3, Math.Max(8, rect.Height - 12));
                        }
                    }
                }
            }

            Color textCol = !Enabled
                ? Color.FromArgb(130, 148, 175)
                : (IsOutline ? (UITheme.IsDarkMode ? Color.FromArgb(219, 234, 254) : UITheme.TextPrimary) : ForeColor);

            bool hasIcon = !string.IsNullOrWhiteSpace(IconKind);
            if (hasIcon && TextAlign == ContentAlignment.MiddleLeft)
            {
                Color iconCol = IsOutline
                    ? (_isHovered ? Color.FromArgb(56, 189, 248) : Color.FromArgb(147, 197, 253))
                    : Color.White;
                UITheme.DrawNavIcon(g, IconKind, new RectangleF(14, (Height - 16) / 2f, 16, 16), iconCol);
            }

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (TextAlign == ContentAlignment.MiddleLeft)
            {
                flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                int leftPad = hasIcon ? 38 : Padding.Left;
                Rectangle textRect = new Rectangle(leftPad, 0, Width - leftPad - 8, Height);
                TextRenderer.DrawText(g, Text, Font, textRect, textCol, flags);
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, textCol, flags);
            }
        }
    }

    /// <summary>
    /// Smooth GDI+ pill toggle switch for the Dark Mode toggle in the sidebar.
    /// </summary>
    public class ToggleSwitch : Control
    {
        private bool _checked = true;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(44, 22);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = UITheme.CreateRoundedPath(track, track.Height / 2))
            {
                if (_checked)
                {
                    using (LinearGradientBrush bg = new LinearGradientBrush(track, Color.FromArgb(14, 165, 233), Color.FromArgb(37, 99, 235), 0f))
                    {
                        g.FillPath(bg, path);
                    }
                }
                else
                {
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    {
                        g.FillPath(bg, path);
                    }
                }
            }

            int knobSize = Height - 6;
            int knobX = _checked ? Width - knobSize - 4 : 3;
            using (SolidBrush knob = new SolidBrush(Color.White))
            {
                g.FillEllipse(knob, knobX, 3, knobSize, knobSize);
            }
        }
    }

    /// <summary>
    /// Custom anti-aliased GDI+ Donut Chart displaying In Stock, Low Stock, and Out of Stock proportions.
    /// </summary>
    public class DonutChartControl : Control
    {
        private int _inStock;
        private int _lowStock;
        private int _outOfStock;

        public DonutChartControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            Size = new Size(138, 138);
            BackColor = Color.Transparent;
        }

        public void SetData(int inStock, int lowStock, int outOfStock)
        {
            _inStock = Math.Max(0, inStock);
            _lowStock = Math.Max(0, lowStock);
            _outOfStock = Math.Max(0, outOfStock);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int pad = 12;
            int dia = Math.Max(40, Math.Min(Width, Height) - pad * 2);
            Rectangle arcRect = new Rectangle((Width - dia) / 2, (Height - dia) / 2, dia, dia);
            float thickness = 16f;

            int total = _inStock + _lowStock + _outOfStock;

            using (Pen trackPen = new Pen(UITheme.IsDarkMode ? Color.FromArgb(30, 48, 84) : Color.FromArgb(226, 232, 240), thickness))
            {
                g.DrawEllipse(trackPen, arcRect);
            }

            if (total > 0)
            {
                float startAngle = -90f;

                void DrawSlice(int value, Color color)
                {
                    if (value <= 0) return;
                    float sweep = 360f * value / total;
                    if (sweep > 359.5f) sweep = 359.5f;
                    using (Pen slicePen = new Pen(color, thickness))
                    {
                        slicePen.StartCap = LineCap.Flat;
                        slicePen.EndCap = LineCap.Flat;
                        g.DrawArc(slicePen, arcRect, startAngle, sweep);
                    }
                    startAngle += sweep;
                }

                DrawSlice(_inStock, Color.FromArgb(14, 165, 233));    // Electric Blue
                DrawSlice(_lowStock, Color.FromArgb(245, 158, 11));   // Amber Orange
                DrawSlice(_outOfStock, Color.FromArgb(239, 68, 68));  // Crimson Red
            }

            // Center label: Total Count + "Products"
            string numText = total.ToString("N0");
            using (Font numFont = new Font("Segoe UI", 16F, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 8F, FontStyle.Regular))
            using (SolidBrush numBrush = new SolidBrush(UITheme.TextPrimary))
            using (SolidBrush subBrush = new SolidBrush(UITheme.TextMuted))
            {
                SizeF numSz = g.MeasureString(numText, numFont);
                SizeF subSz = g.MeasureString("Products", subFont);
                float centerY = Height / 2f;

                g.DrawString(numText, numFont, numBrush, (Width - numSz.Width) / 2f, centerY - numSz.Height + 6);
                g.DrawString("Products", subFont, subBrush, (Width - subSz.Width) / 2f, centerY + 3);
            }
        }
    }

    public class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => UITheme.TopBarBg;
        public override Color MenuStripGradientEnd => UITheme.TopBarBg;
        public override Color MenuItemSelected => Color.FromArgb(30, 58, 110);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(30, 58, 110);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(30, 58, 110);
        public override Color MenuItemPressedGradientBegin => UITheme.PrimaryBlue;
        public override Color MenuItemPressedGradientEnd => UITheme.PrimaryBlue;
        public override Color MenuItemBorder => Color.FromArgb(56, 139, 253);
        public override Color MenuBorder => Color.FromArgb(40, 76, 136);
        public override Color ToolStripDropDownBackground => Color.FromArgb(12, 22, 44);
        public override Color ImageMarginGradientBegin => Color.FromArgb(12, 22, 44);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(12, 22, 44);
        public override Color ImageMarginGradientEnd => Color.FromArgb(12, 22, 44);
        public override Color SeparatorDark => Color.FromArgb(36, 56, 94);
        public override Color SeparatorLight => Color.Transparent;
    }

    /// <summary>
    /// Centralized Dark Navy & Electric-Blue Glass Theme matching the reference design,
    /// with live Dark/Light mode switching support.
    /// </summary>
    public static class UITheme
    {
        public static bool IsDarkMode { get; set; } = true;

        // Dynamic theme-aware colors
        public static Color TopBarBg          => IsDarkMode ? Color.FromArgb(8, 15, 31)     : Color.FromArgb(24, 34, 54);
        public static Color CanvasBg          => IsDarkMode ? Color.FromArgb(9, 17, 36)     : Color.FromArgb(240, 244, 250);
        public static Color SidebarBg         => IsDarkMode ? Color.FromArgb(10, 19, 40)    : Color.FromArgb(20, 31, 54);
        public static Color CardSurface       => IsDarkMode ? Color.FromArgb(14, 25, 49)    : Color.White;
        public static Color InputSurface      => IsDarkMode ? Color.FromArgb(18, 32, 62)    : Color.White;
        public static Color BorderSubtle      => IsDarkMode ? Color.FromArgb(40, 74, 130)   : Color.FromArgb(216, 224, 236);

        public static Color TextPrimary       => IsDarkMode ? Color.White                   : Color.FromArgb(15, 23, 42);
        public static Color TextSecondary     => IsDarkMode ? Color.FromArgb(191, 219, 254) : Color.FromArgb(51, 65, 85);
        public static Color TextMuted         => IsDarkMode ? Color.FromArgb(147, 174, 212) : Color.FromArgb(100, 116, 139);

        public static readonly Color SidebarHover      = Color.FromArgb(24, 46, 86);
        public static readonly Color SidebarTextMuted  = Color.FromArgb(165, 190, 224);
        public static readonly Color SidebarTextActive = Color.White;

        // Core Accents matching reference screenshot
        public static readonly Color PrimaryBlue       = Color.FromArgb(37, 99, 235);    // #2563EB
        public static readonly Color ElectricBlue      = Color.FromArgb(14, 165, 233);   // #0EA5E9
        public static readonly Color SuccessGreen      = Color.FromArgb(22, 163, 74);    // #16A34A
        public static readonly Color TealGlow          = Color.FromArgb(16, 185, 129);   // #10B981
        public static readonly Color WarningAmber      = Color.FromArgb(217, 119, 6);    // #D97706
        public static readonly Color DangerRed         = Color.FromArgb(220, 38, 38);    // #DC2626
        public static readonly Color PurpleAccent      = Color.FromArgb(124, 58, 237);   // #7C3AED

        public static readonly Color SuccessSoftBg     = Color.FromArgb(22, 163, 74);
        public static readonly Color WarningSoftBg     = Color.FromArgb(217, 119, 6);
        public static readonly Color DangerSoftBg      = Color.FromArgb(220, 38, 38);

        // Typography
        public static readonly Font FontBrandTitle     = new Font("Segoe UI", 13F, FontStyle.Bold);
        public static readonly Font FontBrandSub       = new Font("Segoe UI", 8.25F, FontStyle.Regular);
        public static readonly Font FontWelcome        = new Font("Segoe UI", 17F, FontStyle.Bold);
        public static readonly Font FontSectionHeader  = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        public static readonly Font FontCardValue      = new Font("Segoe UI", 19F, FontStyle.Bold);
        public static readonly Font FontCardLabel      = new Font("Segoe UI", 8.75F, FontStyle.Regular);
        public static readonly Font FontNavButton      = new Font("Segoe UI Semibold", 9.75F, FontStyle.Regular);
        public static readonly Font FontRegular        = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        public static readonly Font FontBold           = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        public static readonly Font FontSmall          = new Font("Segoe UI", 8.5F, FontStyle.Regular);

        public static string FormatCurrency(decimal amount) => $"₱{amount:N2}";

        public static Color BlendColor(Color baseColor, Color accent, float ratio)
        {
            int r = (int)(baseColor.R * (1 - ratio) + accent.R * ratio);
            int g = (int)(baseColor.G * (1 - ratio) + accent.G * ratio);
            int b = (int)(baseColor.B * (1 - ratio) + accent.B * ratio);
            return Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
        }

        public static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0 || rect.Width <= 2 || rect.Height <= 2)
            {
                path.AddRectangle(rect);
                return path;
            }

            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Paints the deep navy gradient + subtle electric blue ambient light waves on main surfaces.
        /// </summary>
        public static void PaintAmbientBackground(Graphics g, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            if (!IsDarkMode)
            {
                using (SolidBrush lightBg = new SolidBrush(CanvasBg))
                {
                    g.FillRectangle(lightBg, bounds);
                }
                return;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush bg = new LinearGradientBrush(
                bounds,
                Color.FromArgb(7, 13, 29),
                Color.FromArgb(12, 24, 52),
                135f))
            {
                g.FillRectangle(bg, bounds);
            }

            // Subtle electric-blue top-right ambient glow
            Rectangle glow1 = new Rectangle(bounds.Width - 420, -120, 520, 340);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(glow1);
                using (PathGradientBrush pgb = new PathGradientBrush(gp))
                {
                    pgb.CenterColor = Color.FromArgb(46, 37, 130, 246);
                    pgb.SurroundColors = new[] { Color.FromArgb(0, 14, 165, 233) };
                    g.FillPath(pgb, gp);
                }
            }

            // Subtle bottom-left blue glow
            Rectangle glow2 = new Rectangle(-120, bounds.Height - 240, 420, 300);
            using (GraphicsPath gp2 = new GraphicsPath())
            {
                gp2.AddEllipse(glow2);
                using (PathGradientBrush pgb2 = new PathGradientBrush(gp2))
                {
                    pgb2.CenterColor = Color.FromArgb(34, 14, 165, 233);
                    pgb2.SurroundColors = new[] { Color.FromArgb(0, 14, 165, 233) };
                    g.FillPath(pgb2, gp2);
                }
            }
        }

        /// <summary>
        /// Draws the 3D glowing isometric cube icon shown in the reference screenshot.
        /// </summary>
        public static void DrawIsometricCube(Graphics g, RectangleF bounds, bool glowing = true)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float r = Math.Min(bounds.Width, bounds.Height) * 0.44f;

            if (glowing)
            {
                RectangleF glowRect = new RectangleF(cx - r * 1.3f, cy - r * 1.3f, r * 2.6f, r * 2.6f);
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(glowRect);
                    using (PathGradientBrush pgb = new PathGradientBrush(gp))
                    {
                        pgb.CenterColor = Color.FromArgb(85, 56, 189, 248);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, 56, 189, 248) };
                        g.FillPath(pgb, gp);
                    }
                }
            }

            PointF top = new PointF(cx, cy - r);
            PointF right = new PointF(cx + r * 0.88f, cy - r * 0.45f);
            PointF bottomRight = new PointF(cx + r * 0.88f, cy + r * 0.48f);
            PointF bottom = new PointF(cx, cy + r);
            PointF bottomLeft = new PointF(cx - r * 0.88f, cy + r * 0.48f);
            PointF left = new PointF(cx - r * 0.88f, cy - r * 0.45f);
            PointF center = new PointF(cx, cy + r * 0.04f);

            // Top face (bright cyan-blue)
            using (SolidBrush topBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
            {
                g.FillPolygon(topBrush, new[] { top, right, center, left });
            }
            // Left face (medium electric blue)
            using (SolidBrush leftBrush = new SolidBrush(Color.FromArgb(37, 99, 235)))
            {
                g.FillPolygon(leftBrush, new[] { left, center, bottom, bottomLeft });
            }
            // Right face (deep royal blue)
            using (SolidBrush rightBrush = new SolidBrush(Color.FromArgb(29, 78, 216)))
            {
                g.FillPolygon(rightBrush, new[] { center, right, bottomRight, bottom });
            }
            // Crisp highlight edges
            using (Pen edgePen = new Pen(Color.FromArgb(180, 186, 230, 253), 1.1f))
            {
                g.DrawLine(edgePen, left, center);
                g.DrawLine(edgePen, right, center);
                g.DrawLine(edgePen, center, bottom);
            }
        }

        public static void StyleTextBox(TextBox txt)
        {
            txt.BackColor = InputSurface;
            txt.ForeColor = TextPrimary;
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.Font = FontRegular;
        }

        public static void StyleComboBox(ComboBox cbo)
        {
            cbo.BackColor = InputSurface;
            cbo.ForeColor = TextPrimary;
            cbo.FlatStyle = FlatStyle.Flat;
            cbo.Font = FontRegular;
        }

        public static void StyleNumericUpDown(NumericUpDown num)
        {
            num.BackColor = InputSurface;
            num.ForeColor = TextPrimary;
            num.BorderStyle = BorderStyle.FixedSingle;
            num.Font = FontRegular;
        }

        public static void StyleDataGridView(DataGridView dgv)
        {
            // Enable double-buffering on DataGridView for smooth, flicker-free scrolling
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null,
                dgv,
                new object[] { true });

            dgv.BackgroundColor = IsDarkMode ? Color.FromArgb(11, 20, 40) : Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.GridColor = IsDarkMode ? Color.FromArgb(24, 42, 76) : Color.FromArgb(234, 238, 245);
            dgv.EnableHeadersVisualStyles = false;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.ReadOnly = true;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 40;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = IsDarkMode ? Color.FromArgb(17, 32, 62) : Color.FromArgb(241, 245, 249);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = IsDarkMode ? Color.FromArgb(147, 197, 253) : Color.FromArgb(51, 65, 85);
            dgv.ColumnHeadersDefaultCellStyle.Font = FontBold;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);

            dgv.RowTemplate.Height = 42;
            dgv.DefaultCellStyle.BackColor = IsDarkMode ? Color.FromArgb(11, 20, 40) : Color.White;
            dgv.DefaultCellStyle.ForeColor = IsDarkMode ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            dgv.DefaultCellStyle.Font = FontRegular;
            dgv.DefaultCellStyle.SelectionBackColor = IsDarkMode ? Color.FromArgb(24, 54, 108) : Color.FromArgb(219, 234, 254);
            dgv.DefaultCellStyle.SelectionForeColor = IsDarkMode ? Color.White : Color.FromArgb(23, 37, 84);
            dgv.DefaultCellStyle.Padding = new Padding(10, 0, 10, 0);

            dgv.AlternatingRowsDefaultCellStyle.BackColor = IsDarkMode ? Color.FromArgb(14, 25, 48) : Color.FromArgb(248, 250, 253);
        }

        public static void StyleButton(Button btn, Color bgColor, Color? fgColor = null)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bgColor;
            btn.ForeColor = fgColor ?? Color.White;
            btn.Font = FontBold;
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bgColor, 0.08f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bgColor, 0.1f);
            ApplyRoundedRegion(btn, 8);
        }

        public static void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = IsDarkMode ? Color.FromArgb(56, 98, 168) : Color.FromArgb(203, 213, 225);
            btn.BackColor = IsDarkMode ? Color.FromArgb(18, 33, 62) : Color.White;
            btn.ForeColor = IsDarkMode ? Color.FromArgb(219, 234, 254) : Color.FromArgb(51, 65, 85);
            btn.Font = FontBold;
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.FlatAppearance.MouseOverBackColor = IsDarkMode ? Color.FromArgb(28, 52, 96) : Color.FromArgb(241, 245, 249);
            btn.Region = null;
        }

        private static void ApplyRoundedRegion(Button btn, int radius)
        {
            if (btn.Width > 4 && btn.Height > 4)
            {
                using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, btn.Width, btn.Height), radius))
                {
                    btn.Region = new Region(path);
                }
            }
            btn.Resize -= Btn_ResizeRegion;
            btn.Resize += Btn_ResizeRegion;
        }

        private static void Btn_ResizeRegion(object? sender, EventArgs e)
        {
            if (sender is Button b && b.Width > 4 && b.Height > 4)
            {
                using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, b.Width, b.Height), 8))
                {
                    b.Region = new Region(path);
                }
            }
        }

        public static GlassCardPanel CreateCardPanel(Color? glowAccent = null, bool topTint = false)
        {
            return new GlassCardPanel
            {
                GlowAccent = glowAccent ?? Color.FromArgb(56, 139, 253),
                ShowTopGlowTint = topTint
            };
        }

        /// <summary>
        /// Draws crisp 16x16 GDI+ vector navigation icons for sidebar buttons.
        /// </summary>
        public static void DrawNavIcon(Graphics g, string kind, RectangleF r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using SolidBrush brush = new SolidBrush(color);

            float x = r.X, y = r.Y, w = r.Width, h = r.Height;

            switch (kind.ToUpperInvariant())
            {
                case "DASHBOARD":
                    g.DrawRectangle(pen, x + 1, y + 1, 5.5f, 5.5f);
                    g.DrawRectangle(pen, x + 9, y + 1, 5.5f, 5.5f);
                    g.DrawRectangle(pen, x + 1, y + 9, 5.5f, 5.5f);
                    g.DrawRectangle(pen, x + 9, y + 9, 5.5f, 5.5f);
                    break;
                case "PRODUCTS":
                    g.DrawPolygon(pen, new[] { new PointF(x + w / 2, y + 1), new PointF(x + w - 1, y + 4.5f), new PointF(x + w / 2, y + 8), new PointF(x + 1, y + 4.5f) });
                    g.DrawLine(pen, x + 1, y + 4.5f, x + 1, y + h - 4);
                    g.DrawLine(pen, x + w - 1, y + 4.5f, x + w - 1, y + h - 4);
                    g.DrawLine(pen, x + w / 2, y + 8, x + w / 2, y + h - 1);
                    g.DrawLine(pen, x + 1, y + h - 4, x + w / 2, y + h - 1);
                    g.DrawLine(pen, x + w - 1, y + h - 4, x + w / 2, y + h - 1);
                    break;
                case "CATEGORIES":
                    g.DrawPolygon(pen, new[] { new PointF(x + 2, y + 2), new PointF(x + 8.5f, y + 2), new PointF(x + w - 1, y + 9), new PointF(x + 9, y + h - 1), new PointF(x + 2, y + 8.5f) });
                    g.FillEllipse(brush, x + 4.5f, y + 4.5f, 2.5f, 2.5f);
                    break;
                case "SUPPLIERS":
                    g.DrawRectangle(pen, x + 1, y + 3, 9, 8);
                    g.DrawPolygon(pen, new[] { new PointF(x + 10, y + 5), new PointF(x + 13.5f, y + 5), new PointF(x + w - 1, y + 8), new PointF(x + w - 1, y + 11), new PointF(x + 10, y + 11) });
                    g.DrawEllipse(pen, x + 2.5f, y + 11, 3, 3);
                    g.DrawEllipse(pen, x + 10.5f, y + 11, 3, 3);
                    break;
                case "STOCK":
                case "STOCKIN":
                    g.DrawLine(pen, x + 3, y + 5, x + w - 3, y + 5);
                    g.DrawLine(pen, x + w - 6, y + 2, x + w - 3, y + 5);
                    g.DrawLine(pen, x + w - 6, y + 8, x + w - 3, y + 5);
                    g.DrawLine(pen, x + w - 3, y + 11, x + 3, y + 11);
                    g.DrawLine(pen, x + 6, y + 8, x + 3, y + 11);
                    g.DrawLine(pen, x + 6, y + 14, x + 3, y + 11);
                    break;
                case "STOCKOUT":
                    g.DrawLine(pen, x + 2, y + h / 2, x + w - 2, y + h / 2);
                    g.DrawLine(pen, x + w - 6, y + 4, x + w - 2, y + h / 2);
                    g.DrawLine(pen, x + w - 6, y + h - 4, x + w - 2, y + h / 2);
                    break;
                case "HISTORY":
                case "ACTIVITY":
                    g.DrawEllipse(pen, x + 1.5f, y + 1.5f, w - 3, h - 3);
                    g.DrawLine(pen, x + w / 2, y + 4.5f, x + w / 2, y + h / 2);
                    g.DrawLine(pen, x + w / 2, y + h / 2, x + w - 4.5f, y + h / 2 + 2);
                    break;
                case "REPORTS":
                    g.DrawLine(pen, x + 2, y + h - 2, x + w - 2, y + h - 2);
                    g.DrawLine(pen, x + 4, y + h - 2, x + 4, y + 9);
                    g.DrawLine(pen, x + 8, y + h - 2, x + 8, y + 4);
                    g.DrawLine(pen, x + 12, y + h - 2, x + 12, y + 6.5f);
                    break;
                case "USERS":
                case "PROFILE":
                    g.DrawEllipse(pen, x + 5, y + 1.5f, 6, 6);
                    g.DrawArc(pen, x + 2, y + 9, 12, 9, 180, 180);
                    break;
                case "SETTINGS":
                    g.DrawEllipse(pen, x + 5.5f, y + 5.5f, 5, 5);
                    g.DrawEllipse(pen, x + 2, y + 2, 12, 12);
                    break;
                default:
                    g.FillEllipse(brush, x + 5, y + 5, 6, 6);
                    break;
            }
        }

        /// <summary>
        /// Paints a crisp 1px glowing border around frameless application windows.
        /// </summary>
        public static void PaintFramelessWindowBorder(Graphics g, Rectangle clientRect)
        {
            using Pen border = new Pen(IsDarkMode ? Color.FromArgb(95, 56, 139, 253) : Color.FromArgb(180, 203, 213, 225), 1.5f);
            g.DrawRectangle(border, 0, 0, Math.Max(1, clientRect.Width - 1), Math.Max(1, clientRect.Height - 1));
        }

        /// <summary>
        /// Custom-paints a modern dark-glass rounded status pill with a glowing status dot
        /// ("In Stock", "Low Stock", "Out of Stock") inside a DataGridView cell.
        /// </summary>
        public static void PaintStatusPillCell(DataGridViewCellPaintingEventArgs e, string? status)
        {
            if (string.IsNullOrEmpty(status)) return;

            e.PaintBackground(e.ClipBounds, true);

            Color accent = SuccessGreen;
            Color pillFill = IsDarkMode ? Color.FromArgb(16, 64, 48) : Color.FromArgb(220, 252, 231);
            Color textCol = IsDarkMode ? Color.FromArgb(134, 239, 172) : Color.FromArgb(22, 101, 52);

            if (status.Contains("Out", StringComparison.OrdinalIgnoreCase))
            {
                accent = DangerRed;
                pillFill = IsDarkMode ? Color.FromArgb(78, 22, 32) : Color.FromArgb(254, 226, 226);
                textCol = IsDarkMode ? Color.FromArgb(252, 165, 165) : Color.FromArgb(153, 27, 27);
            }
            else if (status.Contains("Low", StringComparison.OrdinalIgnoreCase))
            {
                accent = WarningAmber;
                pillFill = IsDarkMode ? Color.FromArgb(78, 48, 16) : Color.FromArgb(254, 243, 199);
                textCol = IsDarkMode ? Color.FromArgb(253, 224, 71) : Color.FromArgb(146, 64, 14);
            }

            int pillW = Math.Min(e.CellBounds.Width - 12, 102);
            int pillH = 24;
            Rectangle pillRect = new Rectangle(
                e.CellBounds.X + (e.CellBounds.Width - pillW) / 2,
                e.CellBounds.Y + (e.CellBounds.Height - pillH) / 2,
                Math.Max(20, pillW),
                pillH);

            e.Graphics!.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = CreateRoundedPath(pillRect, 12))
            using (SolidBrush fill = new SolidBrush(pillFill))
            using (Pen border = new Pen(Color.FromArgb(140, accent), 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            using (SolidBrush dot = new SolidBrush(accent))
            {
                e.Graphics.FillEllipse(dot, pillRect.X + 9, pillRect.Y + 9, 6, 6);
            }

            Rectangle textRect = new Rectangle(pillRect.X + 16, pillRect.Y, pillRect.Width - 18, pillRect.Height);
            TextRenderer.DrawText(
                e.Graphics,
                status,
                FontSmall,
                textRect,
                textCol,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Preserve bottom gridline
            using (Pen gridPen = new Pen(IsDarkMode ? Color.FromArgb(24, 42, 76) : Color.FromArgb(234, 238, 245)))
            {
                e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            e.Handled = true;
        }
    }
}
