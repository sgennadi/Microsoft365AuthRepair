using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// The only UI that can request a Volume key change. No action at startup:
// inventory is read-only, DNS lookup is opt-in, changing a key requires
// a selected licensed Office SKU, explicit Preview removal and a second dialog.
internal sealed class OfficeVolumeLicenseForm : Form
{
    private readonly Label title = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label info = UiTheme.Caption(string.Empty);
    private readonly Label selectedStatus = UiTheme.Caption(string.Empty);
    private readonly Label outcome = UiTheme.Caption(string.Empty);
    private readonly Label kmsStatus = UiTheme.Caption(string.Empty);
    private readonly ComboBox productList = new()
    {
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Select installed Office, Visio or Project volume license"
    };
    private readonly RadioButton kms = new() { AutoSize = true };
    private readonly RadioButton mak = new() { AutoSize = true };
    private readonly Label hostLabel = UiTheme.Caption(string.Empty);
    private readonly TextBox host = new()
    {
        Width = 275, MaxLength = 253, AccessibleName = "Optional KMS server"
    };
    private readonly NumericUpDown port = new()
    {
        Width = 85, Minimum = 1, Maximum = 65535, Value = 1688
    };
    private readonly TextBox makKey = new()
    {
        Width = 280, MaxLength = 29, UseSystemPasswordChar = true,
        AccessibleName = "MAK key: not saved or logged"
    };
    private readonly CheckBox removePreview = new()
    {
        AutoSize = true,
        Checked = false
    };
    private readonly Button refresh = UiTheme.CreateActionButton(
        UpdateUiText.T("Refresh products", "Обновить список", "רענן מוצרים"));
    private readonly Button discover = UiTheme.CreateActionButton(
        UpdateUiText.T("Find KMS", "Найти KMS", "מצא KMS"));
    private readonly Button apply = UiTheme.CreateActionButton(
        UpdateUiText.T("Review & activate", "Проверить и активировать", "בדוק והפעל"), primary: true);
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private readonly LinkLabel docs = new()
    {
        AutoSize = true, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline
    };
    private OfficeVolumeInventory? inventory;
    private OfficeVolumeProduct? selected;
    private bool busy;
    internal bool ReadOnlyUntilConfirmed => true;
    internal bool MakHidden => makKey.UseSystemPasswordChar;
    internal bool HasSelectedSupportedProduct =>
        selected is not null && inventory is not null &&
        OfficeVolumeLicensing.CanApply(selected, inventory);

    internal OfficeVolumeLicenseForm()
    {
        Text = UpdateUiText.T("Office / Visio / Project licensing",
            "Лицензирование Office / Visio / Project",
            "רישוי Office / Visio / Project");
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        Size = new Size(900, 650);
        MinimumSize = new Size(620, 490);
        ResponsiveLayout.FitInitialWindow(this, new Size(900, 650),
            new Size(1160, 850), widthFraction: 0.77, heightFraction: 0.76);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            ColumnCount = 1, RowCount = 9,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        title.Text = UpdateUiText.T("Office LTSC 2024 / Visio / Project",
            "Office LTSC 2024 / Visio / Project",
            "Office LTSC 2024 / Visio / Project");
        info.Text = UpdateUiText.T(
            "Read-only WMI inventory. Only a verified Volume 2024 license can be changed. Subscription, Retail and unknown 2026 Preview editions are protected. Removing a verified 2024 Preview key is an optional destructive step.",
            "Инвентаризация WMI — без изменений. Менять ключ разрешено только у проверенной Volume-редакции 2024. Подписки, Retail и неизвестные Preview 2026 защищены. Удаление подтверждённого ключа Preview 2024 требует отдельного согласия.",
            "בדיקת WMI בלבד. שינוי מפתחות רק למהדורת Volume 2024 מאומתת. מחיקת Preview מחייבת אישור.");
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(info, 0, 1);

        var productRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true,
            Margin = new Padding(0, 12, 0, 8)
        };
        productRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        productRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        productRow.Controls.Add(productList, 0, 0);
        productRow.Controls.Add(refresh, 1, 0);
        root.Controls.Add(productRow, 0, 2);
        root.Controls.Add(selectedStatus, 0, 3);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 10, 0, 8)
        };
        kms.Text = UpdateUiText.T("KMS (Microsoft GVLK)", "KMS (ключ Microsoft GVLK)", "KMS (מפתח GVLK)");
        mak.Text = UpdateUiText.T("MAK (organization key)", "MAK (ключ организации)", "MAK (מפתח ארגוני)");
        options.Controls.Add(kms);
        options.Controls.Add(mak);
        root.Controls.Add(options, 0, 4);

        var entry = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 4, 0, 8)
        };
        hostLabel.Text = UpdateUiText.T("KMS server (optional):", "KMS-сервер (необязательно):",
            "שרת KMS (לא חובה):");
        entry.Controls.Add(hostLabel);
        entry.Controls.Add(host);
        entry.Controls.Add(UiTheme.Caption(UpdateUiText.T("Port:", "Порт:", "יציאה:")));
        entry.Controls.Add(port);
        entry.Controls.Add(discover);
        entry.Controls.Add(UiTheme.Caption("MAK:"));
        entry.Controls.Add(makKey);
        root.Controls.Add(entry, 0, 5);

        removePreview.Text = UpdateUiText.T(
            "I approve removing the identified Office 2024 Preview key BEFORE installation of the new key.",
            "Разрешаю удалить найденный ключ Office 2024 Preview ДО установки нового ключа.",
            "אני מאשר להסיר מפתח Preview 2024 לפני התקנת מפתח חדש.");
        root.Controls.Add(removePreview, 0, 6);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            ColumnCount = 1, RowCount = 3,
            Margin = new Padding(0, 6, 0, 6)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.Controls.Add(kmsStatus, 0, 0);
        statusPanel.Controls.Add(outcome, 0, 1);
        docs.Text = UpdateUiText.T("Microsoft Learn — Official Office KMS keys",
            "Microsoft Learn — официальные KMS-ключи Office",
            "Microsoft Learn — מפתחות KMS רשמיים");
        statusPanel.Controls.Add(docs, 0, 2);
        root.Controls.Add(statusPanel, 0, 7);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(apply);
        root.Controls.Add(buttons, 0, 8);
        Controls.Add(ResponsiveLayout.Center(this, root, 1110));
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(apply, UiGlyph.Save, primary: true);
        UiIcons.Assign(refresh, UiGlyph.Updates);
        UiIcons.Assign(discover, UiGlyph.Search);
        UiIcons.TrackDpi(this);
        CancelButton = close;

        root.SizeChanged += (_, _) =>
        {
            var max = new Size(Math.Max(170, root.ClientSize.Width - root.Padding.Horizontal - 32), 0);
            info.MaximumSize = selectedStatus.MaximumSize =
                kmsStatus.MaximumSize = outcome.MaximumSize =
                removePreview.MaximumSize = max;
        };
        productList.SelectedIndexChanged += (_, _) => SelectProduct();
        kms.CheckedChanged += (_, _) => UpdateControls();
        mak.CheckedChanged += (_, _) => UpdateControls();
        removePreview.CheckedChanged += (_, _) => UpdateControls();
        refresh.Click += async (_, _) => await RefreshAsync();
        discover.Click += async (_, _) => await DiscoverAsync();
        apply.Click += async (_, _) => await ApplyAsync();
        close.Click += (_, _) => Close();
        docs.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(OfficeVolumeLicensing.GvlkSource)
                    { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception) { /* may be blocked */ }
        };

        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        kms.Checked = true;
        UpdateControls();
        Shown += async (_, _) => await RefreshAsync();
    }

    private void UpdateControls()
    {
        bool canChange = HasSelectedSupportedProduct && Identity.IsElevated();
        bool isPreview = selected is not null && inventory is not null &&
            OfficeVolumeLicensing.ShouldRemoveOldKey(selected, inventory);
        host.Enabled = port.Enabled = kms.Checked && !busy;
        discover.Enabled = kms.Checked && selected is not null && !busy;
        makKey.Enabled = mak.Checked && !busy;
        removePreview.Enabled = isPreview && !busy;
        apply.Enabled = canChange && !busy && (!isPreview || removePreview.Checked);
        if (selected is not null && !Identity.IsElevated())
            outcome.Text = UpdateUiText.T(
                "Changes require elevation. Read-only inventory is available without administrator rights.",
                "Изменение ключа требует прав администратора. Просмотр доступен без повышения прав.",
                "לשינוי מפתח נדרשת הרשאת מנהל.");
    }

    private void SetBusy(bool flag)
    {
        busy = flag;
        productList.Enabled = refresh.Enabled = kms.Enabled = mak.Enabled = !flag;
        UpdateControls();
        UseWaitCursor = flag;
    }

    private void SelectProduct()
    {
        selected = inventory is not null && productList.SelectedIndex >= 0 &&
            productList.SelectedIndex < inventory.Products.Count
            ? inventory.Products[productList.SelectedIndex] : null;
        removePreview.Checked = false;
        if (selected is null)
        {
            selectedStatus.Text = UpdateUiText.T(
                "No selected installed volume license.",
                "Не выбрана установленная корпоративная лицензия.",
                "לא נבחר רישיון ארגוני.");
            UpdateControls();
            return;
        }
        var product = selected;
        bool isPreview = inventory is not null &&
            OfficeVolumeLicensing.ShouldRemoveOldKey(product, inventory);
        selectedStatus.Text = product.Name + Environment.NewLine +
            "License: " + product.Status +
            (product.HasKey ? "  ·  *****-" + product.PartialKey : "  ·  no key") +
            (product.IsPreview ? "  ·  Preview" : "") + Environment.NewLine +
            (isPreview
                ? UpdateUiText.T("Verified Office LTSC 2024 Preview key. It must be removed first for replacement.",
                    "Подтверждён ключ Office LTSC 2024 Preview. Для замены сначала требуется его удалить.",
                    "זוהה מפתח Preview 2024. יש להסיר אותו לפני החלפה.")
                : HasSelectedSupportedProduct
                    ? UpdateUiText.T("Supported Office LTSC 2024 Volume key",
                        "Поддерживаемая корпоративная лицензия Office LTSC 2024",
                        "רישיון Volume 2024 נתמך")
                    : UpdateUiText.T(
                        "Read-only: product/Preview cannot be verified as a supported Volume 2024 SKU.",
                        "Только просмотр: продукт/Preview не подтверждён как поддерживаемый Volume 2024.",
                        "קריאה בלבד: המוצר לא אומת."));
        UpdateControls();
    }

    private async Task RefreshAsync()
    {
        if (busy) return;
        SetBusy(true);
        outcome.Text = UpdateUiText.T("Reading Office licensing via WMI…",
            "Чтение лицензирования Office через WMI…",
            "קורא פרטי רישוי באמצעות WMI…");
        try
        {
            var result = await Task.Run(OfficeVolumeLicensing.Read);
            if (IsDisposed || Disposing) return;
            inventory = result;
            productList.Items.Clear();
            foreach (var item in result.Products)
                productList.Items.Add(item.Name + " · " + item.Status +
                    (item.HasKey ? " · *****-" + item.PartialKey : "") +
                    (item.IsPreview ? " · Preview" : ""));
            if (productList.Items.Count > 0)
                productList.SelectedIndex = 0;
            else
                SelectProduct();
            var unverified = result.ProductReleaseIds
                .Where(OfficeVolumeLicensing.IsUnverified2026).ToArray();
            outcome.Text = result.Warning ??
                (unverified.Length != 0
                    ? UpdateUiText.T(
                        "Unverified Office/Visio/Project 2026 Preview detected. Do not install 2024 keys.",
                        "Обнаружена непроверенная Office/Visio/Project 2026 Preview. Ключи 2024 не устанавливаются.",
                        "זוהתה גרסת Preview 2026 לא מאומתת. אין להתקין מפתחות 2024.")
                    : UpdateUiText.T(
                        "Inventory complete. Find KMS or enter your own authorized server.",
                        "Инвентаризация завершена. Найдите KMS или укажите собственный сервер.",
                        "סריקה הושלמה. מצא שרת KMS או הזן ידנית."));
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                outcome.Text = "Read-only WMI inventory failed: " + ex.GetType().Name;
        }
        finally
        {
            if (!IsDisposed && !Disposing) SetBusy(false);
        }
    }

    private async Task DiscoverAsync()
    {
        if (busy || selected is null) return;
        var product = selected;
        SetBusy(true);
        kmsStatus.Text = UpdateUiText.T("Checking Office settings and DNS _vlmcs._tcp…",
            "Проверяю настройки Office и DNS _vlmcs._tcp…",
            "בודק הגדרות Office ורשומות DNS…");
        try
        {
            var endpoints = await Task.Run(() => OfficeVolumeLicensing.DiscoverKms(product));
            if (IsDisposed || Disposing) return;
            if (endpoints.Count > 0)
            {
                var match = endpoints[0];
                host.Text = match.Host;
                port.Value = match.Port;
                kmsStatus.Text = "KMS: " + match.Host + ":" + match.Port +
                    " (" + match.Source + ") — not yet contacted or verified.";
            }
            else
                kmsStatus.Text = UpdateUiText.T(
                    "KMS not found. Enter a permitted KMS host and port manually (default 1688).",
                    "KMS не найден. Укажите разрешённый сервер и порт вручную (по умолчанию 1688).",
                    "לא נמצא KMS. הזן שרת ויציאה ידנית.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                kmsStatus.Text = "KMS lookup: " + ex.GetType().Name;
        }
        finally
        {
            if (!IsDisposed && !Disposing) SetBusy(false);
        }
    }

    private async Task ApplyAsync()
    {
        if (busy || !HasSelectedSupportedProduct || inventory is null ||
            selected is null || !Identity.IsElevated()) return;
        var product = selected;
        var snapshot = inventory;
        bool isPreview = OfficeVolumeLicensing.ShouldRemoveOldKey(product, snapshot);
        if (isPreview && !removePreview.Checked) return;
        var isMak = mak.Checked;
        var provided = isMak ? makKey.Text.Trim().ToUpperInvariant() : null;
        // Never send a malformed or empty key to the global Windows licensing
        // provider, especially before removing an existing Preview license.
        if (isMak && (provided is null ||
            !System.Text.RegularExpressions.Regex.IsMatch(
                provided, @"^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$")))
        {
            outcome.Text = UpdateUiText.T(
                "Enter a valid 25-character Office 2024 Volume MAK before removing any Preview key.",
                "Введите корректный 25-символьный Office 2024 Volume MAK до удаления Preview-ключа.",
                "הזן מפתח MAK תקין של Office 2024 לפני הסרת Preview.");
            return;
        }
        OfficeKmsEndpoint? server = null;
        if (!isMak && !string.IsNullOrWhiteSpace(host.Text))
        {
            var valid = OfficeVolumeLicensing.ValidateHost(host.Text);
            if (valid is null)
            {
                outcome.Text = UpdateUiText.T("Invalid KMS hostname.",
                    "Неверное имя сервера KMS.", "שם שרת KMS לא תקין.");
                return;
            }
            server = new OfficeKmsEndpoint(valid, (int)port.Value, "Manual selection");
        }
        if (isPreview && !isMak && server is null)
        {
            outcome.Text = UpdateUiText.T(
                "Before removing Preview key, choose Find KMS or enter the organization's approved server and port.",
                "Перед удалением Preview-ключа нажмите «Найти KMS» либо укажите разрешённый KMS-сервер и порт.",
                "לפני הסרת Preview מצא שרת KMS או הזן שרת ארגוני מאושר.");
            return;
        }

        var text = UpdateUiText.T(
            $"Product: {product.Name}\nCurrent key ends in: {product.PartialKey}\n" +
            (isPreview ? "The selected Preview key will be REMOVED BEFORE installing the new key. If activation fails, Office may remain unlicensed.\n" : "") +
            (isMak ? "Install private MAK and activate this product?" : "Install official 2024 GVLK and activate this product?"),
            $"Продукт: {product.Name}\nПоследние 5 символов ключа: {product.PartialKey}\n" +
            (isPreview ? "Ключ Preview будет УДАЛЁН ДО установки нового. При ошибке активации Office может остаться без лицензии.\n" : "") +
            (isMak ? "Установить корпоративный MAK и активировать этот продукт?" : "Установить официальный GVLK 2024 и активировать этот продукт?"),
            $"מוצר: {product.Name}\nחמש ספרות אחרונות: {product.PartialKey}\n" +
            (isPreview ? "מפתח Preview יוסר לפני התקנת מפתח חדש. ייתכן שהמוצר יישאר ללא רישוי.\n" : "") +
            (isMak ? "להתקין מפתח MAK ולהפעיל?" : "להתקין מפתח GVLK ולהפעיל?"));
        if (MessageBox.Show(this, text,
                UpdateUiText.T("Confirm licensing change", "Подтверждение изменения лицензии", "אישור שינוי רישוי"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes)
        {
            makKey.Clear();
            return;
        }

        SetBusy(true);
        makKey.Clear(); // Never store MAK in settings, logs, or a report.
        outcome.Text = UpdateUiText.T("Applying selected Office license…",
            "Применение выбранной лицензии Office…",
            "מחיל רישיון Office נבחר…");
        try
        {
            var result = await Task.Run(() => OfficeVolumeLicensing.ReplaceKey(
                product, snapshot, isMak, provided, server, removePreviewApproved: isPreview && removePreview.Checked));
            if (IsDisposed || Disposing) return;
            outcome.Text = result.Stage + ": " + result.Status +
                (result.PreviewKeyRemoved ? Environment.NewLine + "Previous Preview key removed." : "") +
                (result.ActivationVerified ? Environment.NewLine + "Office licensed." : "");
            // Preserve the final activation result until the user chooses
            // Refresh. Never hide a partially completed key replacement.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                outcome.Text = "Office licensing operation: " + ex.GetType().Name;
        }
        finally
        {
            provided = null;
            if (!IsDisposed && !Disposing) SetBusy(false);
        }
    }

    internal static bool SelfTest()
    {
        using var form = new OfficeVolumeLicenseForm();
        return form.ReadOnlyUntilConfirmed && form.MakHidden &&
            form.IsUnsupportedPreviewBlocked &&
            !form.apply.Enabled && !form.removePreview.Checked &&
            UiTheme.VerifyButtons(form, 4);
    }
}
