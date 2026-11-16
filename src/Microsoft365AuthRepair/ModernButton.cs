using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// A single accessible WinForms button style shared by Repair, Updates,
// Diagnostics and their secondary actions. No dependency on icon fonts.
internal sealed class ModernButton : Button
{
    internal enum Kind { Primary, Secondary, Destructive }
    internal Kind Role { get; }

    private bool hovered;
    private bool pressed;

    internal ModernButton(Kind role)
    {
        Role = role;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        UseMnemonic = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        MinimumSize = new Size(110, 42);
        Margin = new Padding(0, 5, 10, 5);
        Padding = new Padding(15, 9, 15, 9);
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        TabStop = true;
        SetPalette();
    }

    internal void SetPalette()
    {
        if (SystemInformation.HighContrast)
        {
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
        }
        else
        {
            BackColor = Role switch
            {
                Kind.Primary => UiTheme.Positive,
                Kind.Destructive => Color.FromArgb(255, 241, 241),
                _ => Color.White
            };
            ForeColor = Role switch
            {
                Kind.Primary => Color.White,
                Kind.Destructive => Color.FromArgb(159, 41, 47),
                _ => UiTheme.Navy
            };
        }
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var dpi = IsHandleCreated ? DeviceDpi / 96f : 1f;
        int gap = (int)Math.Round(10 * dpi);
        int horizontal = (int)Math.Round(34 * dpi);
        int vertical = (int)Math.Round(20 * dpi);
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        var t = TextRenderer.MeasureText(Text ?? "", Font, new Size(1200, 200), flags);
        int imageWidth = Image?.Width ?? 0;
        int imageHeight = Image?.Height ?? 0;
        return new Size(
            Math.Max((int)Math.Round(110 * dpi), t.Width + horizontal + (imageWidth > 0 ? imageWidth + gap : 0)),
            Math.Max((int)Math.Round(42 * dpi), Math.Max(t.Height, imageHeight) + vertical));
    }

    private static Color Mix(Color a, Color b, float f)
    {
        f = Math.Clamp(f, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * f),
            (int)Math.Round(a.G + (b.G - a.G) * f),
            (int)Math.Round(a.B + (b.B - a.B) * f));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (SystemInformation.HighContrast)
        {
            base.OnPaint(e);
            return;
        }

        var g = e.Graphics;
        // Paint the entire rectangular surface first. The old rounded owner
        // paint omitted the four corners and left black pixels on some GPUs/DPI.
        var surround = Parent?.BackColor ?? UiTheme.Canvas;
        if (surround.A != 255) surround = UiTheme.Canvas;
        g.Clear(surround);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(1, 1, Math.Max(0, Width - 3), Math.Max(0, Height - 3));
        if (r.Width < 5 || r.Height < 5) return;
        float dpi = DeviceDpi / 96f;
        int radius = Math.Max(3, (int)Math.Round(4 * dpi)); // subtle Windows-style corners
        using var path = Rounded(r, radius);
        Color fill = BackColor;
        if (!Enabled) fill = Mix(fill, SystemColors.Control, .65f);
        else if (pressed) fill = Mix(fill, Color.Black, .14f);
        else if (hovered) fill = Mix(fill, Role == Kind.Primary ? Color.White : UiTheme.Accent, .09f);
        // A restrained embossed gradient gives the buttons depth, without
        // opaque unpainted corners or bitmap skins.
        using (var brush = new LinearGradientBrush(r,
            Mix(fill, Color.White, pressed ? .01f : .11f),
            Mix(fill, Color.Black, pressed ? .11f : .035f),
            LinearGradientMode.Vertical))
            g.FillPath(brush, path);

        var border = Role == Kind.Primary ? Mix(fill, Color.Black, .19f)
            : Role == Kind.Destructive ? Color.FromArgb(207, 148, 153)
            : Mix(UiTheme.Border, Color.Black, .15f);
        using (var pen = new Pen(border, Math.Max(1f, dpi)))
            g.DrawPath(pen, path);
        using (var highlight = new Pen(Mix(fill, Color.White, pressed ? .04f : .35f), Math.Max(1f, dpi)))
            g.DrawLine(highlight, r.Left + radius, r.Top + 2, r.Right - radius, r.Top + 2);
        using (var shade = new Pen(Mix(fill, Color.Black, pressed ? .14f : .09f), Math.Max(1f, dpi)))
            g.DrawLine(shade, r.Left + radius, r.Bottom - 2, r.Right - radius, r.Bottom - 2);

        var text = Text ?? string.Empty;
        int imageWidth = Image?.Width ?? 0;
        int imageHeight = Image?.Height ?? 0;
        int gap = imageWidth > 0 && text.Length > 0 ? (int)Math.Round(9 * dpi) : 0;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        var measured = TextRenderer.MeasureText(g, text, Font, new Size(Math.Max(100, Width * 2), Height), flags);
        int totalWidth = imageWidth + gap + measured.Width;
        int startX = Math.Max((int)Math.Round(12 * dpi), (Width - totalWidth) / 2);
        int iconX = RightToLeft == RightToLeft.Yes
            ? Math.Min(Width - imageWidth - (int)(11 * dpi), startX + measured.Width + gap) : startX;
        if (Image is not null)
            g.DrawImage(Image, new Rectangle(iconX, (Height - imageHeight) / 2, imageWidth, imageHeight));

        int textX = RightToLeft == RightToLeft.Yes ? startX : startX + imageWidth + gap;
        int rightPad = (int)Math.Round(12 * dpi);
        int textWidth = Math.Max(5, Width - textX - rightPad);
        var fg = Enabled ? ForeColor : Mix(ForeColor, fill, .50f);
        TextRenderer.DrawText(g, text, Font, new Rectangle(textX, 0, textWidth, Height), fg, flags);

        if (Focused && ShowFocusCues)
        {
            var focus = Rectangle.Inflate(r, -(int)Math.Round(4 * dpi), -(int)Math.Round(4 * dpi));
            ControlPaint.DrawFocusRectangle(g, focus, ForeColor, fill);
        }
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); PerformLayout(); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); PerformLayout(); Invalidate(); }
}
