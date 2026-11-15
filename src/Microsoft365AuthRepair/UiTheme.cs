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

    internal static Button CreateActionButton(string title, bool primary = false, bool destructive = false)
    {
        var button = new Button
        {
            Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(92, 34), Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(0, 4, 9, 4), FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        if (SystemInformation.HighContrast)
        {
            button.BackColor = SystemColors.Control;
            button.ForeColor = SystemColors.ControlText;
        }
        else
        {
            button.BackColor = destructive ? Color.FromArgb(255, 238, 237) : primary ? Positive : Color.White;
            button.ForeColor = primary ? Color.White : destructive ? Color.FromArgb(155, 34, 34) : Navy;
            button.FlatAppearance.BorderColor = primary ? Positive : Border;
        }
        return button;
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
