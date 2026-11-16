using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Local, read-only inspector: never exposes recovery keys, protectors, TPM IDs
// or the raw Win32_Tpm/Win32_EncryptableVolume objects.
internal sealed class TpmBitLockerDialog : Form
{
    private readonly Label heading = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label safety = UiTheme.Caption(string.Empty);
    private readonly Label status = UiTheme.Caption(string.Empty);
    private readonly TextBox details = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        WordWrap = true, ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 9.5f),
        BackColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly ComboBox errorCode = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 208,
        AccessibleName = "Reported Office sign-in error code"
    };
    private readonly Label errorDetails = UiTheme.Caption(string.Empty);
    private readonly Button refresh = UiTheme.CreateActionButton(
        UpdateUiText.T("Refresh", "Обновить", "רענן"), primary: true);
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private bool running;

    internal TpmBitLockerDialog(TpmBitLockerSnapshot? initial = null)
    {
        Text = "Microsoft 365 Auth Repair — " +
            UpdateUiText.T("TPM & BitLocker (read-only)", "TPM и BitLocker (только чтение)",
                "TPM ו-BitLocker (קריאה בלבד)");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 480);
        Size = new Size(920, 730);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(920, 730),
            new Size(1240, 920), widthFraction: 0.84, heightFraction: 0.78);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            RowCount = 7, ColumnCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        heading.Text = UpdateUiText.T("Device protection · read-only",
            "Защита устройства · только чтение", "הגנת מכשיר · קריאה בלבד");
        safety.Text = UpdateUiText.T(
            "Office WAM, DPAPI and Autopilot TPM attestation are different checks. Never clear TPM, delete BrokerPlugin/NGC or disable BitLocker automatically. Recovery keys and protector IDs are never read. UNKNOWN can mean access is restricted.",
            "WAM, DPAPI и TPM Attestation — разные проверки. Программа не очищает TPM, не удаляет BrokerPlugin/NGC и не отключает BitLocker. Ключи восстановления не читаются. UNKNOWN может означать недостаточно прав.",
            "אין ניקוי TPM, ביטול BitLocker או מחיקת נתוני כניסה. מפתחות שחזור אינם נקראים. UNKNOWN עשוי להעיד על חוסר הרשאות.");
        status.Text = UpdateUiText.T("No device snapshot loaded.",
            "Данные устройства ещё не считаны.", "נתוני המכשיר טרם נטענו.");
        root.Controls.Add(heading, 0, 0);
        root.Controls.Add(safety, 0, 1);
        root.Controls.Add(status, 0, 2);
        root.Controls.Add(details, 0, 3);
        var errorRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            WrapContents = true, Margin = new Padding(0, 12, 0, 6)
        };
        errorCode.Items.AddRange([
            "No error selected", "0x80090016 / -2146893802",
            "0x80090030", "0x80090031", "0x80090034",
            "0x80090035", "0xC0090016",
            "0x80070490 (AIK)", "0x800705B4 (timeout)"
        ]);
        errorCode.SelectedIndex = 0;
        errorRow.Controls.Add(UiTheme.Caption(UpdateUiText.T(
            "Office error:", "Ошибка Office:", "שגיאת Office:")));
        errorRow.Controls.Add(errorCode);
        root.Controls.Add(errorRow, 0, 4);
        errorDetails.MaximumSize = new Size(1030, 0);
        errorDetails.Margin = new Padding(0, 5, 0, 9);
        root.Controls.Add(errorDetails, 0, 5);
        var footer = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true, Dock = DockStyle.Fill
        };
        footer.Controls.Add(close);
        footer.Controls.Add(refresh);
        root.Controls.Add(footer, 0, 6);
        Controls.Add(ResponsiveLayout.Center(this, root, 1170));
        root.SizeChanged += (_, _) =>
        {
            int w = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 24);
            heading.MaximumSize = new Size(w, 0);
            safety.MaximumSize = new Size(w, 0);
            status.MaximumSize = new Size(w, 0);
            errorDetails.MaximumSize = new Size(w, 0);
        };
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(refresh, UiGlyph.Search, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        AcceptButton = refresh;
        CancelButton = close;
        close.Click += (_, _) => Close();
        refresh.Click += async (_, _) => await RefreshAsync();
        errorCode.SelectedIndexChanged += (_, _) => DescribeSelectedCode();
        DescribeSelectedCode();
        if (initial is not null) Render(initial);
        else Shown += async (_, _) => await RefreshAsync();
    }

    private void DescribeSelectedCode()
    {
        string code = errorCode.SelectedIndex switch
        {
            1 => "0x80090016", 2 => "0x80090030",
            3 => "0x80090031", 4 => "0x80090034",
            5 => "0x80090035", 6 => "0xC0090016",
            7 => "0x80070490", 8 => "0x800705B4", _ => ""
        };
        var suffix = TpmBitLockerSnapshot.DescribeError(code);
        errorDetails.Text = UpdateUiText.T("Interpretation (not a device diagnosis): ",
            "Объяснение кода (не диагноз устройства): ",
            "פירוש הקוד (לא אבחון התקן): ") + suffix;
    }

    private static string YesNoUnknown(bool? x) =>
        x is null ? "UNKNOWN" : x.Value ? "YES" : "NO";

    private void Render(TpmBitLockerSnapshot x)
    {
        status.Text = x.NeedsTpmReview
            ? UpdateUiText.T("TPM state needs IT review. Do not clear TPM.",
                "Состояние TPM требует проверки IT. Не очищайте TPM.",
                "יש לבדוק את ה-TPM. אין לנקות אותו.")
            : x.NeedsAttestationReview
                ? UpdateUiText.T("TPM attestation needs review (not evidence of broken TPM).",
                    "Требуется проверка аттестации TPM; неисправность оборудования не доказана.",
                    "נדרשת בדיקת אימות TPM.")
                : x.NeedsBitLockerReview
                ? UpdateUiText.T("Encrypted OS volume reports protection OFF. Review with IT.",
                    "Системный раздел зашифрован, но защита OFF — проверьте с IT.",
                    "מחיצת מערכת מוצפנת אך ההגנה כבויה.")
                : UpdateUiText.T("Device state collected; UNKNOWN is not a failure.",
                    "Данные получены; UNKNOWN не означает неисправность.",
                    "הנתונים נאספו. UNKNOWN אינה שגיאה.");
        var newline = Environment.NewLine;
        details.Text =
            $"TPM present: {x.TpmPresent}{newline}" +
            $"TPM specification: {x.TpmVersion}{newline}" +
            $"TPM ready: {YesNoUnknown(x.TpmReady)}{newline}" +
            $"TPM enabled: {YesNoUnknown(x.TpmEnabled)}{newline}" +
            $"TPM activated: {YesNoUnknown(x.TpmActivated)}{newline}" +
            $"TPM owned: {YesNoUnknown(x.TpmOwned)}{newline}" +
            $"TPM manufacturer class: {x.TpmManufacturer}{newline}" +
            $"TPM firmware version (local only): {x.TpmFirmwareVersion}{newline}" +
            $"Windows release: {x.WindowsRelease}{newline}" +
            $"AIK enrollment last error: {x.AikEnrollmentError}{newline}" +
            $"EK certificate readiness indicator: {x.EkCertStore}{newline}" +
            $"{newline}OS volume BitLocker protection: {x.OsVolumeProtection}{newline}" +
            $"OS volume conversion: {x.OsVolumeConversion}{newline}" +
            $"{newline}Memory Integrity configured: {x.MemoryIntegrityConfigured}{newline}" +
            $"Memory Integrity running: {x.MemoryIntegrityRuntime}{newline}" +
            $"DPAPI ProtectionPolicy: {x.DpapiProtectionPolicy}{newline}" +
            $"Domain joined: {x.DomainJoinStatus}{newline}" +
            $"Writable DC discovery: {x.WritableDcStatus}{newline}{newline}" +
            "These values do not reveal stored BitLocker recovery keys, " +
            "do not identify TPM hardware failure, and do not prove Office authentication.";
    }

    private async Task RefreshAsync()
    {
        if (running || IsDisposed || Disposing) return;
        running = true;
        refresh.Enabled = false;
        status.Text = UpdateUiText.T("Reading Windows security state...",
            "Читаю состояние защиты Windows...", "קורא את מצב האבטחה...");
        try
        {
            var snapshot = await Task.Run(TpmBitLockerProbe.Capture);
            if (!IsDisposed && !Disposing) Render(snapshot);
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                status.Text = UpdateUiText.T("Status unavailable: ",
                    "Состояние недоступно: ", "המצב אינו זמין: ") + ex.GetType().Name;
        }
        finally
        {
            running = false;
            if (!IsDisposed && !Disposing) refresh.Enabled = true;
        }
    }

    internal bool SelfTest() =>
        errorCode.Items.Count == 9 &&
        details.ReadOnly && !string.IsNullOrWhiteSpace(safety.Text) &&
        UiTheme.VerifyButtons(this, 2);
}
