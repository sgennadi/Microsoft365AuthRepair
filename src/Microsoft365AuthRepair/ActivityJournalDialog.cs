using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Read-only snapshot of the existing stage-only Activity journal. A separate
// dialog makes the View activity shortcut useful even when the page is at the
// bottom of its scroll range; opening it never re-runs a diagnostic.
internal sealed class ActivityJournalDialog : Form
{
    private readonly TextBox journal = new()
    {
        Multiline = true, ReadOnly = true, WordWrap = false,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 10f),
        BackColor = SystemColors.Window,
        ForeColor = SystemColors.WindowText,
        ShortcutsEnabled = true,
        AccessibleName = "Read-only local stage activity"
    };
    private readonly Button copy = UiTheme.CreateActionButton(string.Empty);
    private readonly Button close = UiTheme.CreateActionButton(string.Empty);

    internal ActivityJournalDialog(string stageOnlyText)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(580, 430);
        Size = new Size(860, 650);
        Text = UpdateUiText.T("Activity log - Microsoft 365 Auth Repair",
            "Журнал работы - Microsoft 365 Auth Repair",
            "יומן פעולות - Microsoft 365 Auth Repair");
        ResponsiveLayout.FitInitialWindow(this, new Size(860, 630),
            new Size(1150, 880), 0.82, 0.83);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 3,
            Padding = new Padding(18, 12, 18, 15)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hint = UiTheme.Caption(UpdateUiText.T(
            "Local read-only stages only - no account names, tokens, keys or raw WMI data.",
            "Только этапы локальной диагностики - без учётных записей, токенов, ключей и сырых данных WMI.",
            "יומן שלבים מקומי בלבד, ללא פרטי חשבונות, מפתחות או נתוני WMI גולמיים."));
        hint.Dock = DockStyle.Top;
        hint.MaximumSize = new Size(1050, 0);
        root.Controls.Add(hint, 0, 0);
        journal.Text = stageOnlyText ?? string.Empty;
        journal.SelectionStart = 0;
        journal.SelectionLength = 0;
        root.Controls.Add(journal, 0, 1);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true, Margin = new Padding(0, 9, 0, 0)
        };
        copy.Text = UpdateUiText.T("Copy log", "Копировать журнал", "העתק יומן");
        close.Text = UpdateUiText.T("Close", "Закрыть", "סגור");
        copy.AccessibleName = copy.Text;
        close.AccessibleName = close.Text;
        UiIcons.Assign(copy, UiGlyph.Save);
        UiIcons.Assign(close, UiGlyph.Reset);
        footer.Controls.Add(close);
        footer.Controls.Add(copy);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(ResponsiveLayout.Center(this, root, 1150));
        UiIcons.SetWindowIcon(this);
        UiIcons.TrackDpi(this);
        CancelButton = close;
        close.Click += (_, _) => Close();
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(journal.Text); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException
                or ThreadStateException or InvalidOperationException)
            {
                MessageBox.Show(this, UpdateUiText.T(
                    "Clipboard is busy. Select text and press Ctrl+C.",
                    "Буфер обмена занят. Выделите текст и нажмите Ctrl+C.",
                    "הלוח תפוס. בחר טקסט ולחץ Ctrl+C."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
    }

    internal static bool SelfTest()
    {
        using var dlg = new ActivityJournalDialog(
            "Microsoft 365 Auth Repair\r\n10:00:00 TPM / BitLocker · Finished");
        return dlg.journal.ReadOnly &&
            dlg.journal.Text.Contains("TPM / BitLocker", StringComparison.Ordinal) &&
            dlg.journal.ScrollBars == ScrollBars.Both &&
            dlg.AcceptButton is null &&
            dlg.CancelButton == dlg.close &&
            UiTheme.VerifyButtons(dlg, 2);
    }
}
