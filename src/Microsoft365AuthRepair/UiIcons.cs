using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

internal enum UiGlyph
{
    Repair, Reset, Search, Updates, Reports, Logs, Save, Folder,
    Download, Link, Info, Success, Error, Working
}

// Vector drawing instead of font glyphs: WinForms buttons remain native,
// keyboard-accessible, DPI aware and correctly mirrored for RTL layouts.
internal static class UiIcons
{
    private sealed record ActionIcon(UiGlyph Glyph, bool Primary, bool Destructive);
    private static readonly ConditionalWeakTable<Button, ActionIcon> Actions = new();
    private static readonly ConditionalWeakTable<PictureBox, object> Pictures = new();
    private static readonly Lazy<Icon?> AppIcon = new(() =>
    {
        try
        {
            using var stream = typeof(UiIcons).Assembly.GetManifestResourceStream("Microsoft365AuthRepair.BrandIcon");
            if (stream is null) return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        catch { return null; }
    });

    internal static void SetWindowIcon(Form form)
    {
        if (AppIcon.Value is not null) form.Icon = AppIcon.Value;
        form.ShowIcon = true;
    }

    internal static bool HasBrandIcon => AppIcon.Value is not null;

    internal static void Assign(Button button, UiGlyph glyph, bool primary = false, bool destructive = false)
    {
        Actions.Add(button, new ActionIcon(glyph, primary, destructive));
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        Update(button);
    }

    internal static PictureBox CreateBrand()
    {
        var picture = new PictureBox
        {
            Width = 54, Height = 54, Margin = new Padding(0, 0, 12, 0),
            SizeMode = PictureBoxSizeMode.Zoom, TabStop = false,
            AccessibleName = "Microsoft 365 Auth Repair",
            AccessibleDescription = "Application brand icon"
        };
        Pictures.Add(picture, true);
        Update(picture);
        return picture;
    }

    internal static PictureBox CreateStatus()
    {
        var picture = new PictureBox
        {
            Width = 26, Height = 26, Margin = new Padding(0, 0, 10, 0),
            SizeMode = PictureBoxSizeMode.CenterImage, TabStop = false,
            AccessibleName = "Operation status", AccessibleDescription = "Ready"
        };
        Pictures.Add(picture, UiGlyph.Info);
        Update(picture);
        return picture;
    }

    internal static void SetStatus(PictureBox picture, UiGlyph kind, string description)
    {
        Pictures.Remove(picture);
        Pictures.Add(picture, kind);
        picture.AccessibleDescription = description;
        Update(picture);
    }

    internal static void TrackDpi(Form form)
    {
        form.DpiChanged += (_, _) => Refresh(form);
        form.Shown += (_, _) => Refresh(form);
        Refresh(form);
    }

    internal static bool Verify(Form form)
    {
        var count = 0;
        var valid = true;
        Walk(form, control =>
        {
            if (control is Button b && Actions.TryGetValue(b, out _))
            {
                count++;
                if (b.Image is null || b.Image.Width < 16) valid = false;
            }
            if (control is PictureBox p && Pictures.TryGetValue(p, out _) && p.Image is null)
                valid = false;
        });
        return count > 0 && valid && HasBrandIcon;
    }

    private static void Walk(Control control, Action<Control> action)
    {
        action(control);
        foreach (Control child in control.Controls) Walk(child, action);
    }

    private static void Refresh(Control root) => Walk(root, control =>
    {
        if (control is Button button && Actions.TryGetValue(button, out _)) Update(button);
        if (control is PictureBox picture && Pictures.TryGetValue(picture, out _)) Update(picture);
    });

    private static int Pixels(Control control, int logical)
    {
        var dpi = control.IsHandleCreated ? control.DeviceDpi : 96;
        return Math.Clamp((int)Math.Round(logical * dpi / 96.0), 16, 192);
    }

    private static void Update(Button button)
    {
        if (!Actions.TryGetValue(button, out var info)) return;
        Color color = SystemInformation.HighContrast ? SystemColors.ControlText
            : info.Primary ? Color.White
            : info.Destructive ? Color.FromArgb(158, 38, 38)
            : UiTheme.Navy;
        var prior = button.Image;
        button.Image = Draw(info.Glyph, Pixels(button, 20), color);
        prior?.Dispose();
    }

    private static void Update(PictureBox picture)
    {
        if (!Pictures.TryGetValue(picture, out var value)) return;
        bool brand = value is bool;
        var old = picture.Image;
        if (brand)
        {
            int pixel = Pixels(picture, 52);
            picture.Image = Brand(pixel);
        }
        else
        {
            var glyph = (UiGlyph)value;
            Color color = SystemInformation.HighContrast ? SystemColors.WindowText
                : glyph == UiGlyph.Success ? UiTheme.Positive
                : glyph == UiGlyph.Error ? Color.FromArgb(172, 40, 42)
                : UiTheme.Accent;
            picture.Image = Draw(glyph, Pixels(picture, 22), color);
        }
        old?.Dispose();
    }

    private static Bitmap Brand(int pixels)
    {
        var bitmap = new Bitmap(pixels, pixels);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(pixels / 100f, pixels / 100f);
        using var panel = new GraphicsPath();
        panel.AddArc(2, 2, 26, 26, 180, 90);
        panel.AddArc(72, 2, 26, 26, 270, 90);
        panel.AddArc(72, 72, 26, 26, 0, 90);
        panel.AddArc(2, 72, 26, 26, 90, 90);
        panel.CloseFigure();
        using var baseBrush = new LinearGradientBrush(new Point(0, 0), new Point(100, 100),
            Color.FromArgb(18, 45, 80), Color.FromArgb(34, 125, 187));
        g.FillPath(baseBrush, panel);
        using var shield = new GraphicsPath();
        shield.AddLines([
            new Point(50, 17), new Point(78, 27), new Point(77, 57),
            new Point(70, 72), new Point(50, 88), new Point(30, 72),
            new Point(23, 57), new Point(22, 27)
        ]);
        shield.CloseFigure();
        using var white = new SolidBrush(Color.White);
        g.FillPath(white, shield);
        using var inner = new GraphicsPath();
        inner.AddLines([
            new Point(50, 24), new Point(71, 32), new Point(70, 56),
            new Point(64, 67), new Point(50, 79), new Point(36, 67),
            new Point(30, 56), new Point(29, 32)
        ]);
        inner.CloseFigure();
        using var blue = new SolidBrush(Color.FromArgb(25, 92, 146));
        g.FillPath(blue, inner);
        using var check = new Pen(Color.FromArgb(88, 233, 187), 8)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        g.DrawLines(check, [new Point(38, 52), new Point(47, 61), new Point(64, 42)]);
        return bitmap;
    }

    private static Bitmap Draw(UiGlyph glyph, int pixels, Color color)
    {
        var bitmap = new Bitmap(pixels, pixels);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(pixels / 24f, pixels / 24f);
        using var p = new Pen(color, 2.1f)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        PointF Q(float x, float y) => new(x, y);
        switch (glyph)
        {
            case UiGlyph.Repair:
                g.DrawArc(p, 3, 3, 18, 18, 40, 290);
                g.DrawLines(p, [Q(14, 3), Q(20, 5), Q(19, 10)]);
                break;
            case UiGlyph.Reset:
                g.DrawArc(p, 3, 3, 18, 18, 50, 285);
                g.DrawLines(p, [Q(15, 3), Q(21, 5), Q(20, 11)]);
                g.DrawLine(p, 9, 9, 15, 15);
                g.DrawLine(p, 15, 9, 9, 15);
                break;
            case UiGlyph.Search:
                g.DrawEllipse(p, 3, 3, 13, 13);
                g.DrawLine(p, 14.5f, 14.5f, 21, 21);
                break;
            case UiGlyph.Updates:
                g.DrawArc(p, 3, 3, 18, 18, -140, 220);
                g.DrawLines(p, [Q(20, 9), Q(20.5f, 3.5f), Q(15.5f, 4)]);
                g.DrawArc(p, 3, 3, 18, 18, 40, 108);
                g.DrawLines(p, [Q(3.5f, 15), Q(3.5f, 20), Q(9, 20)]);
                break;
            case UiGlyph.Reports:
                g.DrawLines(p, [Q(6, 2), Q(15, 2), Q(20, 7), Q(20, 22), Q(6, 22), Q(6, 2)]);
                g.DrawLines(p, [Q(15, 2), Q(15, 7), Q(20, 7)]);
                g.DrawLine(p, 9, 12, 17, 12);
                g.DrawLine(p, 9, 16, 16, 16);
                break;
            case UiGlyph.Logs:
                g.DrawLines(p, [Q(5, 3), Q(19, 3), Q(19, 21), Q(5, 21), Q(5, 3)]);
                g.DrawLine(p, 8, 9, 16, 9);
                g.DrawLine(p, 8, 13, 16, 13);
                g.DrawLine(p, 8, 17, 14, 17);
                break;
            case UiGlyph.Save:
                g.DrawLines(p, [Q(3, 3), Q(18, 3), Q(21, 6), Q(21, 21), Q(3, 21), Q(3, 3)]);
                g.DrawRectangle(p, 7, 3, 9, 7);
                g.DrawRectangle(p, 7, 14, 10, 7);
                break;
            case UiGlyph.Folder:
                g.DrawLines(p, [Q(2, 7), Q(2, 20), Q(22, 20), Q(22, 9), Q(11, 9), Q(8, 5), Q(2, 5), Q(2, 7)]);
                break;
            case UiGlyph.Download:
                g.DrawLine(p, 12, 3, 12, 15);
                g.DrawLines(p, [Q(7, 11), Q(12, 16), Q(17, 11)]);
                g.DrawLines(p, [Q(4, 17), Q(4, 21), Q(20, 21), Q(20, 17)]);
                break;
            case UiGlyph.Link:
                g.DrawLines(p, [Q(12, 4), Q(20, 4), Q(20, 12)]);
                g.DrawLine(p, 20, 4, 10, 14);
                g.DrawLines(p, [Q(18, 15), Q(18, 20), Q(4, 20), Q(4, 6), Q(9, 6)]);
                break;
            case UiGlyph.Info:
                g.DrawEllipse(p, 3, 3, 18, 18);
                g.FillEllipse(new SolidBrush(color), 11, 6, 2, 2);
                g.DrawLine(p, 12, 11, 12, 17);
                break;
            case UiGlyph.Success:
                g.DrawEllipse(p, 3, 3, 18, 18);
                g.DrawLines(p, [Q(7, 12), Q(10.5f, 15.5f), Q(17, 8.5f)]);
                break;
            case UiGlyph.Error:
                g.DrawEllipse(p, 3, 3, 18, 18);
                g.DrawLine(p, 9, 9, 15, 15);
                g.DrawLine(p, 15, 9, 9, 15);
                break;
            case UiGlyph.Working:
                g.DrawArc(p, 4, 4, 16, 16, -90, 285);
                g.DrawLines(p, [Q(3, 15), Q(4.5f, 20), Q(10, 19)]);
                break;
        }
        return bitmap;
    }
}
