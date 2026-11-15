using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Keep useful content at a readable line length on 4K/maximized windows,
// but let the active work area use the full available height.
// On smaller screens the inner content (not the whole desktop) can scroll.
internal static class ResponsiveLayout
{
    private static int TargetWidth(int viewport, int maximumLogical, int dpi)
    {
        if (viewport <= 0) return 0;
        double scale = Math.Max(96, dpi) / 96d;
        int gutter = (int)Math.Round(14 * scale);
        int capped = (int)Math.Round(maximumLogical * scale);
        return Math.Max(1, Math.Min(Math.Max(1, viewport - 2 * gutter), capped));
    }

    internal static TableLayoutPanel Center(Form owner, Control inner, int maxLogicalWidth)
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = owner.BackColor
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 700));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        inner.Dock = DockStyle.Fill;
        shell.Controls.Add(inner, 1, 0);

        void ResizeMiddle()
        {
            int computed = TargetWidth(shell.ClientSize.Width, maxLogicalWidth,
                owner.IsHandleCreated ? owner.DeviceDpi : 96);
            if (computed > 0 && Math.Abs(shell.ColumnStyles[1].Width - computed) >= 1)
                shell.ColumnStyles[1].Width = computed;
        }
        shell.SizeChanged += (_, _) => ResizeMiddle();
        owner.DpiChanged += (_, _) => ResizeMiddle();
        owner.Shown += (_, _) => ResizeMiddle();
        ResizeMiddle();
        return shell;
    }

    // Give the main window and Update Center a spacious but not full-screen
    // first appearance. Pixel sizes are calculated from the actual working
    // area and the active monitor's DPI, not a fixed 890/1030-pixel window.
    internal static Size InitialWindowSize(Size workArea, int dpi,
        Size preferredLogical, Size maximumLogical,
        double widthFraction = 0.90, double heightFraction = 0.90)
    {
        double scale = Math.Max(96, dpi) / 96d;
        int gutter = (int)Math.Round(18 * scale);
        int usableWidth = Math.Max(1, workArea.Width - gutter * 2);
        int usableHeight = Math.Max(1, workArea.Height - gutter * 2);

        static int Dimension(int available, double scale, int preferred, int maximum, double fraction)
        {
            var target = Math.Max(preferred * scale, available * fraction);
            return Math.Max(1, (int)Math.Min(available, Math.Min(maximum * scale, Math.Round(target))));
        }

        return new Size(
            Dimension(usableWidth, scale, preferredLogical.Width, maximumLogical.Width, widthFraction),
            Dimension(usableHeight, scale, preferredLogical.Height, maximumLogical.Height, heightFraction));
    }

    internal static void FitInitialWindow(Form form, Size preferredLogical, Size maximumLogical,
        double widthFraction = 0.90, double heightFraction = 0.90)
    {
        // Load occurs after DPI is known but before the window is painted.
        // Preserve subsequent user resizing and maximization; fit only once.
        form.Load += (_, _) =>
        {
            if (form.WindowState != FormWindowState.Normal) return;
            var area = form.Owner is not null
                ? Screen.FromControl(form.Owner).WorkingArea
                : Screen.FromPoint(Cursor.Position).WorkingArea;
            var target = InitialWindowSize(area.Size, form.DeviceDpi,
                preferredLogical, maximumLogical, widthFraction, heightFraction);
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, target.Width),
                Math.Min(form.MinimumSize.Height, target.Height));
            form.Size = target;
            form.Location = new Point(
                area.Left + (area.Width - form.Width) / 2,
                area.Top + (area.Height - form.Height) / 2);
        };
    }

    // Pure calculations verified by CI independently of physical display size.
    internal static bool SelfTest() =>
        TargetWidth(1920, 1000, 96) == 1000 &&
        TargetWidth(3800, 1000, 192) == 2000 &&
        TargetWidth(700, 1000, 96) == 672 &&
        TargetWidth(960, 900, 144) == 918 &&
        TargetWidth(0, 900, 96) == 0 &&
        InitialWindowSize(new Size(1920, 1080), 96,
            new Size(1050, 740), new Size(1450, 940)) == new Size(1450, 940) &&
        InitialWindowSize(new Size(1366, 768), 96,
            new Size(1050, 740), new Size(1450, 940)) == new Size(1197, 732) &&
        InitialWindowSize(new Size(3840, 2160), 192,
            new Size(1050, 740), new Size(1450, 940)) == new Size(2900, 1880) &&
        InitialWindowSize(new Size(640, 480), 96,
            new Size(1050, 740), new Size(1450, 940)) == new Size(604, 444);
}
