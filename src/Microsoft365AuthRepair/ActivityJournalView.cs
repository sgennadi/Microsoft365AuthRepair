using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Only fixed stage/outcome enums enter the clipboard-visible journal.
// Never append raw PowerShell, WMI, dsregcmd, file paths, user/tenant IDs,
// credentials or exception messages.
internal enum ActivityStep
{
    Ready, ReadOnlyCheck, WamEntra, OfficeLicensing, TpmBitLocker,
    NetworkPreflight, TpmWmi, BitLockerWmi, DeviceGuardWmi,
    DomainDc, TpmLockout, DpapiAttestation, SecureBoot, VirtualMachine,
    Preflight,
    WamRegistration, OfficeReset, CredentialCleanup, RegistryCleanup,
    CacheCleanup, BrokerVerification, UserTaskScheduling
}

internal enum ActivityOutcome
{
    Started, Finished, NotVerified, Partial, Failed, Cancelled, TimeLimit,
    Scheduled
}

internal sealed class ActivityJournalView : TableLayoutPanel
{
    private sealed record Entry(DateTimeOffset Time, ActivityStep Step,
        ActivityOutcome Outcome, int ElapsedSeconds);

    private readonly List<Entry> entries = new();
    private readonly Label heading = UiTheme.Caption(string.Empty, true);
    private readonly LinkLabel copyLog = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Anchor = AnchorStyles.Right,
        Margin = new Padding(6, 9, 9, 5)
    };
    // Compact navigation-style cancellation, not a full-height action
    // button in the status banner. Visible only during a read-only check.
    private readonly LinkLabel cancelCheck = new()
    {
        AutoSize = true, TabStop = true, Visible = false,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(6, 9, 12, 5)
    };
    internal event EventHandler? CancelRequested;
    private readonly TextBox lines = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true, ReadOnly = true,
        WordWrap = true,
        ShortcutsEnabled = true,
        ScrollBars = ScrollBars.Vertical,
        BorderStyle = BorderStyle.None,
        TabStop = true,
        Margin = new Padding(12, 5, 12, 11),
        BackColor = SystemInformation.HighContrast
            ? SystemColors.Window : Color.FromArgb(249, 251, 253),
        ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Navy
    };

    private DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    internal ActivityJournalView()
    {
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 12, 0, 5);
        Padding = new Padding(8, 5, 8, 0);
        MinimumSize = new Size(0, 164);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        ColumnCount = 1;
        RowCount = 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3,
            RowCount = 1, Margin = new Padding(5, 2, 3, 3),
            BackColor = BackColor
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        heading.ForeColor = SystemInformation.HighContrast
            ? SystemColors.WindowText : UiTheme.Navy;
        heading.Margin = new Padding(4, 5, 8, 5);
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(cancelCheck, 1, 0);
        header.Controls.Add(copyLog, 2, 0);
        cancelCheck.LinkClicked += (_, _) =>
        {
            if (cancelCheck.Enabled)
                CancelRequested?.Invoke(this, EventArgs.Empty);
        };
        var cancelHint = new ToolTip { AutoPopDelay = 10000 };
        cancelHint.SetToolTip(cancelCheck,
            "Stop waiting for this read-only diagnostic; no Office, TPM or BitLocker data will be modified.");
        Controls.Add(header, 0, 0);
        Controls.Add(lines, 0, 1);

        lines.AccessibleName = "Local activity log";
        copyLog.LinkClicked += (_, _) =>
        {
            if (entries.Count == 0) return;
            try { Clipboard.SetText(CopyText); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException
                or ThreadStateException or InvalidOperationException)
            {
                MessageBox.Show(FindForm(),
                    UpdateUiText.T("Clipboard is busy. Select the log and press Ctrl+C.",
                        "Буфер обмена занят. Выделите журнал и нажмите Ctrl+C.",
                        "הלוח תפוס. בחר את היומן והקש Ctrl+C."),
                    UpdateUiText.T("Copy log", "Копировать журнал", "העתק יומן"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        Add(ActivityStep.Ready, ActivityOutcome.Finished);
    }

    private static string StepLabel(ActivityStep step) => step switch
    {
        ActivityStep.Ready => UpdateUiText.T("Application", "Программа", "יישום"),
        ActivityStep.ReadOnlyCheck => UpdateUiText.T("Check & Diagnose (read-only)",
            "Проверка и диагностика (без изменений)", "בדיקה ואבחון לקריאה בלבד"),
        ActivityStep.WamEntra => "WAM / Entra",
        ActivityStep.OfficeLicensing => UpdateUiText.T("Office licensing",
            "Лицензирование Office", "רישוי Office"),
        ActivityStep.TpmBitLocker => "TPM / BitLocker",
        ActivityStep.TpmWmi => "TPM (WMI)",
        ActivityStep.BitLockerWmi => "BitLocker (WMI)",
        ActivityStep.DeviceGuardWmi => "Device Guard / HVCI (WMI)",
        ActivityStep.DomainDc => UpdateUiText.T("Domain controller",
            "Контроллер домена", "בקר תחום"),
        ActivityStep.TpmLockout => UpdateUiText.T("TPM lockout",
            "Блокировка TPM", "נעילת TPM"),
        ActivityStep.DpapiAttestation => "DPAPI / AIK",
        ActivityStep.SecureBoot => "Secure Boot / UEFI",
        ActivityStep.VirtualMachine => UpdateUiText.T("Virtual machine detection",
            "Определение виртуальной машины", "זיהוי מכונה וירטואלית"),
        ActivityStep.NetworkPreflight => UpdateUiText.T("Network / preflight",
            "Сеть / предварительная проверка", "רשת / בדיקה מקדימה"),
        ActivityStep.Preflight => UpdateUiText.T("Safety preflight",
            "Предварительная проверка безопасности", "בדיקת בטיחות"),
        ActivityStep.WamRegistration => UpdateUiText.T("Re-register WAM",
            "Перерегистрация WAM", "רישום WAM מחדש"),
        ActivityStep.OfficeReset => UpdateUiText.T("Reset Office sign-in",
            "Сброс входа Office", "איפוס כניסה ל-Office"),
        ActivityStep.CredentialCleanup => UpdateUiText.T("Credential Manager",
            "Диспетчер учётных данных", "מנהל אישורים"),
        ActivityStep.RegistryCleanup => UpdateUiText.T("Office identity registry",
            "Реестр идентификации Office", "רישום זהויות Office"),
        ActivityStep.CacheCleanup => UpdateUiText.T("Office sign-in caches",
            "Кеши входа Office", "מטמון כניסה ל-Office"),
        ActivityStep.BrokerVerification => UpdateUiText.T("Broker registration verification",
            "Проверка регистрации Broker", "אימות רישום Broker"),
        ActivityStep.UserTaskScheduling => UpdateUiText.T("Per-user task scheduling",
            "Планирование заданий пользователей", "תזמון משימות משתמשים"),
        _ => UpdateUiText.T("Operation", "Операция", "פעולה")
    };

    private static string OutcomeLabel(ActivityOutcome status) => status switch
    {
        ActivityOutcome.Started => UpdateUiText.T("Started", "Начато", "התחיל"),
        ActivityOutcome.Finished => UpdateUiText.T("Finished", "Завершено", "הושלם"),
        ActivityOutcome.NotVerified => UpdateUiText.T("Not verified",
            "Не проверено", "לא אומת"),
        ActivityOutcome.Partial => UpdateUiText.T("Partial results",
            "Частичный результат", "תוצאות חלקיות"),
        ActivityOutcome.Failed => UpdateUiText.T("Failed", "Ошибка", "נכשל"),
        ActivityOutcome.Cancelled => UpdateUiText.T("Cancelled", "Отменено", "בוטל"),
        ActivityOutcome.TimeLimit => UpdateUiText.T("Time limit reached",
            "Истекло время ожидания", "תם הזמן"),
        ActivityOutcome.Scheduled => UpdateUiText.T("Queued, not yet executed",
            "Назначено, ещё не выполнено", "בתור, טרם בוצע"),
        _ => UpdateUiText.T("Unknown", "Неизвестно", "לא ידוע")
    };

    internal void Add(ActivityStep step, ActivityOutcome status)
    {
        if (IsDisposed || Disposing) return;
        if (step is ActivityStep.ReadOnlyCheck or ActivityStep.WamRegistration
            or ActivityStep.OfficeReset or ActivityStep.UserTaskScheduling &&
            status == ActivityOutcome.Started)
            startedAt = DateTimeOffset.UtcNow;
        var now = DateTimeOffset.Now;
        var elapsed = Math.Max(0, (int)(DateTimeOffset.UtcNow - startedAt).TotalSeconds);
        entries.Add(new(now, step, status, elapsed));
        // Cap retained text without file persistence or secret-bearing logs.
        if (entries.Count > 160) entries.RemoveRange(0, entries.Count - 160);
        RefreshLanguage();
    }

    private static string Format(Entry x) =>
        x.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " +
        StepLabel(x.Step) + " · " + OutcomeLabel(x.Outcome) +
        (x.Step == ActivityStep.Ready ? string.Empty : $"  (+{x.ElapsedSeconds}s)");

    internal string CopyText =>
        "Microsoft 365 Auth Repair v" + Repair.Version + Environment.NewLine +
        "Local stage-only activity; no user identity or raw diagnostic data" +
        Environment.NewLine + Environment.NewLine +
        string.Join(Environment.NewLine, entries.Select(Format));

    internal void RefreshLanguage()
    {
        if (IsDisposed || Disposing) return;
        heading.Text = UpdateUiText.T("ACTIVITY", "ХОД ВЫПОЛНЕНИЯ", "מהלך הביצוע");
        copyLog.Text = UpdateUiText.T("Copy log", "Копировать журнал", "העתק יומן");
        copyLog.AccessibleName = copyLog.Text;
        cancelCheck.Text = UpdateUiText.T("Cancel", "Отмена", "ביטול");
        cancelCheck.AccessibleName = UpdateUiText.T("Cancel the read-only check",
            "Отменить диагностику (без изменений)", "ביטול בדיקה לקריאה בלבד");
        lines.Text = entries.Count == 0 ?
            UpdateUiText.T("No activity yet.", "Нет событий.", "אין אירועים.") :
            string.Join(Environment.NewLine, entries.Select(Format));
        lines.SelectionStart = lines.TextLength;
        lines.ScrollToCaret();
    }

    // A WinForms control can report Visible=false while its owner Form is
    // unshown during CI. Validate the real copy control's wiring/properties.
    internal void SetCancellationAvailable(bool available)
    {
        cancelCheck.Visible = available;
        cancelCheck.Enabled = available;
    }

    internal bool HasCompactCancellation => cancelCheck.Parent is TableLayoutPanel &&
        !string.IsNullOrWhiteSpace(cancelCheck.Text) &&
        cancelCheck is LinkLabel && cancelCheck is not Button;

    internal bool HasCopyControl => copyLog.Enabled &&
        !string.IsNullOrWhiteSpace(copyLog.Text) && lines.ReadOnly;

    internal static bool SelfTest()
    {
        using var view = new ActivityJournalView();
        view.Add(ActivityStep.ReadOnlyCheck, ActivityOutcome.Started);
        view.Add(ActivityStep.WamEntra, ActivityOutcome.Finished);
        view.Add(ActivityStep.TpmBitLocker, ActivityOutcome.NotVerified);
        view.Add(ActivityStep.TpmWmi, ActivityOutcome.TimeLimit);
        view.Add(ActivityStep.BitLockerWmi, ActivityOutcome.Finished);
        view.Add(ActivityStep.SecureBoot, ActivityOutcome.Finished);
        view.Add(ActivityStep.VirtualMachine, ActivityOutcome.NotVerified);
        view.SetCancellationAvailable(true);
        if (!view.HasCompactCancellation || !view.cancelCheck.Enabled)
            return false;
        view.SetCancellationAvailable(false);
        if (view.cancelCheck.Enabled)
            return false;
        return view.HasCopyControl && view.CopyText.Contains("WAM / Entra",
            StringComparison.Ordinal) &&
            view.CopyText.Contains("TPM / BitLocker", StringComparison.Ordinal) &&
            !view.CopyText.Contains("TenantId", StringComparison.OrdinalIgnoreCase) &&
            !view.CopyText.Contains("UserSid", StringComparison.OrdinalIgnoreCase) &&
            view.entries.Count == 8;
    }
}
