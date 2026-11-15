using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

internal static class UiTheme
{
    internal static readonly Color Navy = Color.FromArgb(20, 46, 82);
    internal static readonly Color Accent = Color.FromArgb(25, 100, 180);
    internal static readonly Color Positive = Color.FromArgb(15, 118, 92);
    internal static readonly Color Canvas = Color.FromArgb(245, 248, 252);
    internal static readonly Color Muted = Color.FromArgb(76, 91, 110);
    internal static readonly Color Border = Color.FromArgb(216, 226, 237);
    internal static Font BodyFont() => SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

    // The ONLY button factory for all application dialogs. Styles, focus,
    // accessibility, DPI and hover feedback stay consistent project-wide.
    internal static Button CreateActionButton(string title, bool primary = false, bool destructive = false) =>
        new ModernButton(destructive ? ModernButton.Kind.Destructive
            : primary ? ModernButton.Kind.Primary : ModernButton.Kind.Secondary)
        {
            Text = title,
            AccessibleName = title
        };

    // CI smoke-tests all action buttons for a coherent layout/keyboard theme.
    internal static bool VerifyButtons(Control root, int minimumCount)
    {
        int count = 0;
        bool valid = true;
        void Walk(Control node)
        {
            if (node is Button b)
            {
                count++;
                if (b is not ModernButton || b.UseMnemonic ||
                    b.MinimumSize.Height < 42 || b.Font.Size < 9f ||
                    b.GetPreferredSize(Size.Empty).Height < 42)
                    valid = false;
            }
            foreach (Control child in node.Controls) Walk(child);
        }
        Walk(root);
        return valid && count >= minimumCount;
    }

    internal static Label Caption(string text, bool heading = false)
    {
        return new Label
        {
            AutoSize = true, Text = text, Margin = new Padding(0, 5, 0, 5),
            ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : heading ? Navy : Muted,
            Font = heading ? new Font("Segoe UI Semibold", 14f, FontStyle.Bold) : BodyFont()
        };
    }
}
