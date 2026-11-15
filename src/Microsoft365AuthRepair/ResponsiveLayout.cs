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

    // Pure calculations verified by CI independently of physical display size.
    internal static bool SelfTest() =>
        TargetWidth(1920, 1000, 96) == 1000 &&
        TargetWidth(3800, 1000, 192) == 2000 &&
        TargetWidth(700, 1000, 96) == 672 &&
        TargetWidth(960, 900, 144) == 918 &&
        TargetWidth(0, 900, 96) == 0;
}
