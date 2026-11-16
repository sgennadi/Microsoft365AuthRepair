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
    // Public Microsoft GVLK, not an organization's confidential MAK.
    // Selected automatically by exact Office 2024 Volume SKU, copyable with Ctrl+C.
    // Label, complete 29-character key, then description on separate rows:
    // never place the help text beside the key where it can clip at 200% DPI.
    private readonly TableLayoutPanel gvlkRow = new()
    {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
        Margin = new Padding(0, 5, 0, 9)
    };
    private readonly Label gvlkCaption = UiTheme.Caption(string.Empty);
    private readonly TextBox gvlkValue = new()
    {
        Dock = DockStyle.Fill, MinimumSize = new Size(320, 0),
        ReadOnly = true, TabStop = true, ShortcutsEnabled = true,
        Font = new Font("Consolas", 10.2f),
        Margin = new Padding(0, 5, 0, 5),
        AccessibleName = "Microsoft published KMS client setup key for selected Office product"
    };
    private readonly Label gvlkHelp = UiTheme.Caption(string.Empty);
    private readonly TableLayoutPanel previewPanel = new()
    {
        AutoSize = true, Dock = DockStyle.Fill, Visible = false,
        ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 6, 0, 9)
    };
    private readonly Label previewInfo = UiTheme.Caption(string.Empty);
    private readonly LinkLabel previewAction = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 5, 0, 6)
    };
    private OfficeVolumeProduct? selectedPreviewForAction;
    private bool canRemoveLegacyPreview;
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
    private readonly LinkLabel refreshLink = new()
    {
        AutoSize = true, TabStop = true, LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(12, 12, 4, 5)
    };
    private readonly LinkLabel kmsLookupLink = new()
    {
        AutoSize = true, TabStop = true, LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(12, 6, 0, 5)
    };
    private readonly LinkLabel otherRecordsLink = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 5, 0, 7)
    };
    private readonly Label portCaption = UiTheme.Caption(string.Empty);
    private readonly Label makCaption = UiTheme.Caption("MAK:");
    private IReadOnlyList<OfficeVolumeProduct> displayedProducts = [];
    private bool showTechnicalRows;
    private bool populatingProducts;
    private bool manualKmsHost;
    private bool settingKmsHost;
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
    internal string? SuggestedKmsKey =>
        selected is not null && inventory is not null
            ? OfficeVolumeLicensing.SuggestGvlk(selected, inventory)?.Key
            : null;

    internal OfficeVolumeLicenseForm()
    {
        Text = UpdateUiText.T("Office / Visio / Project licensing",
            "Лицензирование Office / Visio / Project",
            "רישוי Office / Visio / Project");
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiTheme.BodyFont();
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        Size = new Size(900, 545);
        MinimumSize = new Size(620, 425);
        ResponsiveLayout.FitInitialWindow(this, new Size(900, 545),
            new Size(1120, 720), widthFraction: 0.77, heightFraction: 0.61);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            ColumnCount = 1, RowCount = 11,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // All rows are content-sized; no giant unused footer on tall screens.
        for (int i = 0; i < 11; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        title.Text = UpdateUiText.T("Office LTSC 2024 / Visio / Project",
            "Office LTSC 2024 / Visio / Project",
            "Office LTSC 2024 / Visio / Project");
        info.Text = UpdateUiText.T(
            "Installed product first. Additional licensing records can be expanded below. Activated products are protected against unnecessary key changes.",
            "Сначала установленный продукт. Дополнительные записи лицензирования можно раскрыть ниже. Активированные продукты защищены от ненужной замены ключей.",
            "מוצרים מותקנים מוצגים ראשונים. ניתן לפתוח רשומות נוספות בהמשך. רישיון פעיל מוגן משינוי.");
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(info, 0, 1);

        var productRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true,
            Margin = new Padding(0, 12, 0, 8)
        };
        productRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        productRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        refreshLink.Text = UpdateUiText.T("Refresh", "Обновить", "רענן");
        refreshLink.AccessibleName = UpdateUiText.T(
            "Refresh installed Office products", "Обновить список установленного Office",
            "רענן מוצרי Office מותקנים");
        productRow.Controls.Add(productList, 0, 0);
        productRow.Controls.Add(refreshLink, 1, 0);
        root.Controls.Add(productRow, 0, 2);
        root.Controls.Add(selectedStatus, 0, 3);
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        previewPanel.Controls.Add(previewInfo, 0, 0);
        previewPanel.Controls.Add(previewAction, 0, 1);
        root.Controls.Add(previewPanel, 0, 4);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 10, 0, 8)
        };
        kms.Text = UpdateUiText.T("KMS (Microsoft GVLK)", "KMS (ключ Microsoft GVLK)", "KMS (מפתח GVLK)");
        mak.Text = UpdateUiText.T("MAK (organization key)", "MAK (ключ организации)", "MAK (מפתח ארגוני)");
        options.Controls.Add(kms);
        options.Controls.Add(mak);
        root.Controls.Add(options, 0, 5);

        gvlkCaption.Text = UpdateUiText.T(
            "Suggested KMS client key (GVLK):",
            "Предлагаемый KMS-ключ (GVLK):",
            "מפתח KMS מומלץ (GVLK):");
        gvlkHelp.Text = UpdateUiText.T(
            "Official Microsoft key; inserted automatically at activation. Copy with Ctrl+C. Does not replace a KMS server.",
            "Официальный ключ Microsoft. Подставится автоматически при активации. Ctrl+C — копировать. Не заменяет KMS-сервер.",
            "מפתח רשמי של Microsoft. יוזן אוטומטית בהפעלה. להעתקה Ctrl+C. לא מחליף שרת KMS.");
        gvlkValue.AccessibleDescription = gvlkHelp.Text;
        gvlkRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            gvlkRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gvlkRow.Controls.Add(gvlkCaption, 0, 0);
        gvlkRow.Controls.Add(gvlkValue, 0, 1);
        gvlkRow.Controls.Add(gvlkHelp, 0, 2);
        gvlkRow.Visible = false;
        root.Controls.Add(gvlkRow, 0, 6);

        var entry = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 4, 0, 8)
        };
        hostLabel.Text = UpdateUiText.T("KMS server (optional):", "KMS-сервер (необязательно):",
            "שרת KMS (לא חובה):");
        portCaption.Text = UpdateUiText.T("Port:", "Порт:", "יציאה:");
        kmsLookupLink.Text = UpdateUiText.T("Find again", "Найти снова", "חפש שוב");
        kmsLookupLink.AccessibleName = UpdateUiText.T(
            "Rediscover KMS host via Office and DNS",
            "Повторить поиск KMS через Office и DNS",
            "חיפוש KMS מחדש דרך Office ו-DNS");
        entry.Controls.Add(hostLabel);
        entry.Controls.Add(host);
        entry.Controls.Add(portCaption);
        entry.Controls.Add(port);
        entry.Controls.Add(kmsLookupLink);
        entry.Controls.Add(makCaption);
        entry.Controls.Add(makKey);
        root.Controls.Add(entry, 0, 7);

        removePreview.Text = UpdateUiText.T(
            "I approve removing the identified Office 2024 Preview key BEFORE installation of the new key.",
            "Разрешаю удалить найденный ключ Office 2024 Preview ДО установки нового ключа.",
            "אני מאשר להסיר מפתח Preview 2024 לפני התקנת מפתח חדש.");
        removePreview.Visible = false;
        root.Controls.Add(removePreview, 0, 8);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            ColumnCount = 1, RowCount = 4,
            Margin = new Padding(0, 6, 0, 6)
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.Controls.Add(otherRecordsLink, 0, 0);
        statusPanel.Controls.Add(kmsStatus, 0, 1);
        statusPanel.Controls.Add(outcome, 0, 2);
        docs.Text = UpdateUiText.T("Microsoft Learn — Official Office KMS keys",
            "Microsoft Learn — официальные KMS-ключи Office",
            "Microsoft Learn — מפתחות KMS רשמיים");
        statusPanel.Controls.Add(docs, 0, 3);
        root.Controls.Add(statusPanel, 0, 9);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(apply);
        root.Controls.Add(buttons, 0, 10);
        Controls.Add(ResponsiveLayout.Center(this, root, 1110));
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(apply, UiGlyph.Save, primary: true);
        UiIcons.TrackDpi(this);
        CancelButton = close;

        root.SizeChanged += (_, _) =>
        {
            var max = new Size(Math.Max(170, root.ClientSize.Width - root.Padding.Horizontal - 32), 0);
            info.MaximumSize = selectedStatus.MaximumSize =
                kmsStatus.MaximumSize = outcome.MaximumSize =
                removePreview.MaximumSize = max;
            gvlkHelp.MaximumSize = previewInfo.MaximumSize =
                new Size(Math.Max(170, max.Width - 24), 0);
        };
        productList.SelectedIndexChanged += (_, _) =>
        {
            if (!populatingProducts) SelectProduct();
        };
        kms.CheckedChanged += (_, _) => UpdateControls();
        mak.CheckedChanged += (_, _) => UpdateControls();
        removePreview.CheckedChanged += (_, _) => UpdateControls();
        refreshLink.LinkClicked += async (_, _) => await RefreshAsync();
        kmsLookupLink.LinkClicked += async (_, _) => await DiscoverAsync();
        otherRecordsLink.LinkClicked += (_, _) =>
        {
            showTechnicalRows = !showTechnicalRows;
            PopulateProductList(selected?.ActivationId);
        };
        previewAction.LinkClicked += async (_, _) => await HandlePreviewActionAsync();
        host.TextChanged += (_, _) =>
        {
            if (!settingKmsHost) manualKmsHost = true;
        };
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
        host.Visible = hostLabel.Visible = portCaption.Visible =
            port.Visible = kmsLookupLink.Visible = kms.Checked;
        makKey.Visible = makCaption.Visible = mak.Checked;
        host.Enabled = port.Enabled = kms.Checked && !busy;
        kmsLookupLink.Enabled = kms.Checked && selected is not null && !busy;
        makKey.Enabled = mak.Checked && !busy;
        removePreview.Visible = isPreview;
        removePreview.Enabled = isPreview && !busy;
        // No extra "Choose key" button: only an exact 2024 Volume match
        // exposes the public GVLK, which is used by ReplaceKey automatically.
        var suggestion = selected is not null && inventory is not null
            ? OfficeVolumeLicensing.SuggestGvlk(selected, inventory) : null;
        gvlkValue.Text = suggestion?.Key ?? string.Empty;
        gvlkRow.Visible = kms.Checked && suggestion is not null;
        apply.Enabled = canChange && !busy && (!isPreview || removePreview.Checked) &&
            (!kms.Checked || suggestion is not null);
        gvlkHelp.Text = selected is not null && OfficeVolumeLicensing.IsActive(selected)
            ? UpdateUiText.T(
                "Already activated. Public KMS key shown for reference only; replacement is disabled.",
                "Уже активирован. Публичный KMS-ключ показан для справки; замена отключена.",
                "הרישיון פעיל. המפתח מוצג לעיון בלבד.")
            : UpdateUiText.T(
                "The Microsoft GVLK is selected automatically. A KMS server is still required.",
                "Ключ Microsoft GVLK выбирается автоматически. Сервер KMS всё равно необходим.",
                "מפתח Microsoft GVLK נבחר אוטומטית. עדיין נדרש שרת KMS.");
        if (selected is not null && !Identity.IsElevated() &&
            !OfficeVolumeLicensing.IsActive(selected))
            outcome.Text = UpdateUiText.T(
                "Changes require elevation. Read-only inventory is available without administrator rights.",
                "Изменение ключа требует прав администратора. Просмотр доступен без повышения прав.",
                "לשינוי מפתח נדרשת הרשאת מנהל.");
    }

    private void SetBusy(bool flag)
    {
        busy = flag;
        productList.Enabled = refreshLink.Enabled = otherRecordsLink.Enabled =
            kms.Enabled = mak.Enabled = !flag;
        UpdateControls();
        UseWaitCursor = flag;
    }

    private void SelectProduct()
    {
        selected = productList.SelectedIndex >= 0 &&
            productList.SelectedIndex < displayedProducts.Count
            ? displayedProducts[productList.SelectedIndex] : null;
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
        selectedStatus.Text = OfficeVolumeLicensing.FriendlyName(product.ProductId) +
            "  ·  " + product.Status +
            (product.HasKey ? "  ·  *****-" + product.PartialKey : "") +
            (product.IsPreview ? "  ·  Preview" : "") + Environment.NewLine +
            (OfficeVolumeLicensing.IsActive(product)
                ? UpdateUiText.T(
                    "Office is already activated. No license change is needed.",
                    "Office уже активирован. Менять лицензию не требуется.",
                    "Office כבר מופעל. אין צורך להחליף רישיון.")
                : isPreview
                    ? UpdateUiText.T(
                        "Verified 2024 Preview key: separate approval required before replacement.",
                        "Обнаружен Preview-ключ 2024: для замены потребуется отдельное подтверждение.",
                        "זוהה מפתח Preview 2024; נדרש אישור נפרד.")
                    : HasSelectedSupportedProduct
                        ? UpdateUiText.T(
                            "Installed 2024 Volume edition: review KMS or MAK activation.",
                            "Установлена Volume-редакция 2024: можно проверить KMS/MAK-активацию.",
                            "זוהתה מהדורת Volume 2024; ניתן לבדוק KMS או MAK.")
                        : UpdateUiText.T(
                            "Read-only: installed edition or license identity could not be verified.",
                            "Только просмотр: не удалось подтвердить установленную редакцию или лицензию.",
                            "מידע בלבד: לא ניתן לאמת רישיון או מהדורה."));
        UpdateControls();
    }

    private string DisplayName(OfficeVolumeProduct product)
    {
        var productName = OfficeVolumeLicensing.FriendlyName(product.ProductId);
        if (productName == "Unrecognized Office edition") productName = product.Name;
        var current = OfficeVolumeLicensing.IsActive(product)
            ? UpdateUiText.T("Activated", "Активирован", "מופעל")
            : !product.HasKey
                ? UpdateUiText.T("WMI template — no key", "Шаблон WMI — нет ключа",
                    "תבנית WMI ללא מפתח")
                : product.Status == "Unlicensed"
                    ? UpdateUiText.T("Not activated", "Не активирован", "לא מופעל")
                    : product.Status;
        return productName + " — " + current +
            (product.HasKey ? " · *****-" + product.PartialKey : "") +
            (product.IsPreview ? " · Preview" : "");
    }

    private void PopulateProductList(string? preferredId)
    {
        if (inventory is null) return;
        var preferred = OfficeVolumeLicensing.PreferredProducts(inventory);
        displayedProducts = showTechnicalRows
            ? OfficeVolumeLicensing.AllProductRecords(inventory)
            : preferred; // Main view lists one installed edition, not every WMI license row.
        populatingProducts = true;
        try
        {
            productList.BeginUpdate();
            productList.Items.Clear();
            foreach (var item in displayedProducts)
                productList.Items.Add(DisplayName(item));
            int ix = string.IsNullOrEmpty(preferredId) ? -1 :
                displayedProducts.ToList().FindIndex(x =>
                    x.ActivationId.Equals(preferredId, StringComparison.OrdinalIgnoreCase));
            productList.SelectedIndex = ix >= 0 ? ix :
                displayedProducts.Count > 0 ? 0 : -1;
        }
        finally
        {
            productList.EndUpdate();
            populatingProducts = false;
        }
        int hidden = Math.Max(0, inventory.Products.Count - preferred.Count);
        otherRecordsLink.Visible = hidden > 0;
        otherRecordsLink.Text = showTechnicalRows
            ? UpdateUiText.T("Hide WMI details", "Скрыть записи WMI",
                "הסתר פרטי WMI")
            : UpdateUiText.T("Show WMI details", "Показать записи WMI",
                "הצג פרטי WMI") + " (" + hidden + ")";
        // Keep the clickable text compact at 200% DPI; explanation is
        // available below and to screen readers.
        otherRecordsLink.AccessibleName = otherRecordsLink.Text;
        otherRecordsLink.AccessibleDescription = UpdateUiText.T(
            "Technical WMI licensing entries are not additional Office installations.",
            "Технические записи лицензий WMI не означают отдельные установки Office.",
            "רשומות WMI נוספות אינן התקנות Office נוספות.");
        SelectProduct();
    }

    private void UpdatePreviewPanel()
    {
        selectedPreviewForAction = null;
        canRemoveLegacyPreview = false;
        if (inventory is null)
        {
            previewInfo.Text = "";
            previewAction.Text = "";
            previewAction.Visible = previewPanel.Visible = false;
            return;
        }

        // Preview WMI entries must remain visible in the summary even if the
        // main selector correctly prioritizes an activated production Office.
        var found = inventory.Products.Where(x => x.IsPreview).ToArray();
        if (found.Length == 0)
        {
            previewInfo.Text = "";
            previewAction.Text = "";
            previewAction.Visible = previewPanel.Visible = false;
            return;
        }

        previewPanel.Visible = true;
        var keyed = OfficeVolumeLicensing.PreviewKeyRecords(inventory);
        if (keyed.Count == 0)
        {
            previewInfo.Text = UpdateUiText.T(
                $"Preview licensing entries detected: {found.Length}. No Preview key is installed in those entries; nothing needs removing.",
                $"Найдено записей лицензирования Preview: {found.Length}. В них нет установленного Preview-ключа — удалять нечего.",
                $"נמצאו {found.Length} רשומות Preview ללא מפתח מותקן; אין מה להסיר.");
        }
        else
        {
            previewInfo.Text = UpdateUiText.T(
                $"Preview entries: {found.Length}, with a partial key: {keyed.Count}. Only verified 2024 keys can be removed.",
                $"Записей Preview: {found.Length}, с установленным ключом: {keyed.Count}. Удалять можно только подтверждённые ключи 2024.",
                $"רשומות Preview: {found.Length}; עם מפתח: {keyed.Count}. ניתן להסיר רק מפתחות 2024 מאומתים.");
            var obsolete = keyed.FirstOrDefault(x =>
                OfficeVolumeLicensing.CanRemoveLegacyPreview(x, inventory));
            if (obsolete is not null)
            {
                selectedPreviewForAction = obsolete;
                canRemoveLegacyPreview = true;
                previewAction.Text = UpdateUiText.T(
                    "Review removal of verified obsolete Preview key…",
                    "Проверить удаление подтверждённого старого Preview-ключа…",
                    "בדוק הסרת מפתח Preview ישן ומאומת…");
                previewInfo.Text += Environment.NewLine + UpdateUiText.T(
                    "A separate active production Office license was verified.",
                    "Отдельная действующая лицензия Office подтверждена.",
                    "זוהה רישיון Office פעיל ונפרד.");
            }
            else
            {
                selectedPreviewForAction = keyed.FirstOrDefault(x =>
                    OfficeVolumeLicensing.CanApply(x, inventory));
                if (selectedPreviewForAction is not null)
                {
                    previewAction.Text = UpdateUiText.T(
                        "Review Preview-to-KMS/MAK replacement…",
                        "Проверить замену Preview на KMS/MAK…",
                        "בדוק החלפת Preview ב-KMS/MAK…");
                    previewInfo.Text += Environment.NewLine + UpdateUiText.T(
                        "No separate active production license. Replacement requires a new key and two confirmations.",
                        "Нет отдельной действующей лицензии. Для замены нужны новый ключ и два подтверждения.",
                        "אין רישיון פעיל נפרד; להחלפה נדרשים מפתח ואישורים.");
                }
                else
                {
                    previewInfo.Text += Environment.NewLine + UpdateUiText.T(
                        "No safe automatic action for these Preview keys. Ask IT to verify the edition and license.",
                        "Для этих Preview-ключей нет безопасного автоматического действия. Требуется проверка IT.",
                        "אין פעולה אוטומטית בטוחה; נדרשת בדיקת IT.");
                    previewAction.Text = "";
                }
            }
        }
        previewAction.Visible = selectedPreviewForAction is not null;
        previewAction.AccessibleName = previewAction.Text;
    }

    private async Task HandlePreviewActionAsync()
    {
        var preview = selectedPreviewForAction;
        var original = inventory;
        if (busy || preview is null || original is null) return;

        if (!canRemoveLegacyPreview)
        {
            // Select the exact Preview record for the normal explicit
            // Preview-to-Volume workflow. This selection does NOT remove it.
            showTechnicalRows = true;
            PopulateProductList(preview.ActivationId);
            outcome.Text = UpdateUiText.T(
                "Review the selected Preview license, choose KMS/MAK, approve old-key removal, then confirm activation. No changes made.",
                "Проверьте выбранный Preview-ключ, выберите KMS/MAK, разрешите удаление и подтвердите активацию. Пока ничего не изменено.",
                "בדוק רישיון Preview, בחר KMS/MAK ואשר את הפעולה. לא בוצע שינוי.");
            return;
        }

        if (!Identity.IsElevated())
        {
            outcome.Text = UpdateUiText.T(
                "To remove an obsolete Preview key, run the app as administrator. Nothing changed.",
                "Для удаления старого Preview-ключа запустите программу от администратора. Изменений нет.",
                "להסרת Preview נדרשות הרשאות מנהל. לא בוצע שינוי.");
            return;
        }
        var warning = UpdateUiText.T(
            $"Remove ONLY old Preview key *****-{preview.PartialKey} from {OfficeVolumeLicensing.FriendlyName(preview.ProductId)}? " +
            "A separate activated non-Preview license exists. The program will recheck both licenses before deletion. This cannot be undone using only the last five characters. Continue?",
            $"Удалить ТОЛЬКО старый Preview-ключ *****-{preview.PartialKey} из {OfficeVolumeLicensing.FriendlyName(preview.ProductId)}? " +
            "Найдена отдельная действующая лицензия без Preview. Перед удалением обе лицензии будут проверены повторно. По последним пяти символам восстановить ключ нельзя. Продолжить?",
            $"להסיר רק מפתח Preview *****-{preview.PartialKey}? " +
            "קיים רישיון פעיל נפרד. שתי הרשומות ייבדקו שוב לפני ההסרה. להמשיך?");
        if (MessageBox.Show(this, warning,
                UpdateUiText.T("Confirm old Preview key cleanup",
                    "Подтвердите удаление старого Preview-ключа",
                    "אישור הסרת מפתח Preview"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        SetBusy(true);
        try
        {
            var result = await Task.Run(() =>
                OfficeVolumeLicensing.RemoveLegacyPreviewKey(preview, original));
            if (IsDisposed || Disposing) return;
            outcome.Text = result.Stage + ": " + result.Status;
            // Refresh only via the read-only inventory path, even after an
            // unsuccessful delete; never retry a partial operation silently.
            SetBusy(false);
            await RefreshAsync();
            if (!IsDisposed && !Disposing)
                outcome.Text = result.Stage + ": " + result.Status;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                outcome.Text = "Preview cleanup: " + ex.GetType().Name;
        }
        finally
        {
            if (!IsDisposed && !Disposing) SetBusy(false);
        }
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
            var previousId = selected?.ActivationId;
            inventory = result;
            PopulateProductList(previousId);
            UpdatePreviewPanel();
            var unverified = result.ProductReleaseIds
                .Where(OfficeVolumeLicensing.IsUnverified2026).ToArray();
            outcome.Text = result.Warning ??
                (unverified.Length != 0
                    ? UpdateUiText.T(
                        "Unverified Office/Visio/Project 2026 Preview detected. Do not install 2024 keys.",
                        "Обнаружена непроверенная Office/Visio/Project 2026 Preview. Ключи 2024 не устанавливаются.",
                        "זוהתה גרסת Preview 2026 לא מאומתת. אין להתקין מפתחות 2024.")
                    : result.Products.Any(OfficeVolumeLicensing.IsActive)
                        ? UpdateUiText.T(
                            "Office activation detected. Extra WMI records are licensing variants/templates, NOT additional Office installations.",
                            "Лицензия Office активирована. Дополнительные записи WMI — варианты/шаблоны лицензирования, НЕ отдельные установки Office.",
                            "Office מופעל. רשומות WMI נוספות אינן התקנות נוספות של Office.")
                        : UpdateUiText.T(
                            "Office inventory complete. KMS discovery starts automatically.",
                            "Инвентаризация Office завершена. Поиск KMS запускается автоматически.",
                            "בדיקת Office הושלמה. גילוי KMS מתחיל אוטומטית."));
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
        if (!IsDisposed && !Disposing && selected is not null && kms.Checked)
            await DiscoverAsync();
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
                if (!manualKmsHost || string.IsNullOrWhiteSpace(host.Text))
                {
                    settingKmsHost = true;
                    try
                    {
                        host.Text = match.Host;
                        port.Value = match.Port;
                    }
                    finally { settingKmsHost = false; }
                }
                kmsStatus.Text = "KMS detected: " + match.Host + ":" + match.Port +
                    " (" + match.Source + ") — discovery only, activation not verified.";
            }
            else
                kmsStatus.Text = UpdateUiText.T(
                    "KMS not found automatically. Enter a permitted host and port (default 1688).",
                    "KMS автоматически не найден. Введите разрешённый сервер и порт (по умолчанию 1688).",
                    "לא נמצא KMS אוטומטית. הזן שרת מורשה ויציאה.");
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
        if (!isMak && OfficeVolumeLicensing.SuggestGvlk(product, snapshot) is null)
        {
            outcome.Text = UpdateUiText.T(
                "No verified Microsoft 2024 KMS key for this edition. No changes made.",
                "Для этой редакции не найден подтверждённый ключ Microsoft KMS 2024. Изменения не выполнены.",
                "לא נמצא מפתח KMS מאומת עבור מהדורה זו. לא בוצעו שינויים.");
            return;
        }
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
        var initial = form.ReadOnlyUntilConfirmed && form.MakHidden &&
            form.SuggestedKmsKey is null && !form.HasSelectedSupportedProduct &&
            !form.apply.Enabled && !form.removePreview.Checked &&
            form.refreshLink.TabStop && form.kmsLookupLink.TabStop;
        var visio = new OfficeVolumeProduct(Guid.Empty.ToString("D"),
            "Office24VisioPro2024VL_KMS_Client", "", "VisioPro2024Volume",
            "ABCDE", "Unlicensed", false, true, null, null, null, null);
        form.inventory = new OfficeVolumeInventory([visio], ["VisioPro2024Volume"], null)
            { InstalledProductsVerified = true };
        form.selected = visio;
        form.kms.Checked = true;
        form.UpdateControls();
        bool automatic = form.SuggestedKmsKey == "B7TN8-FJ8V3-7QYCP-HQPMV-YY89G" &&
            form.gvlkValue.Text == form.SuggestedKmsKey && form.gvlkValue.ReadOnly;
        form.mak.Checked = true;
        form.UpdateControls();
        // When MAK is selected the recommendation remains in state but is not
        // used; the input is an independent private key.
        bool makMode = form.mak.Checked && !form.kms.Checked &&
            form.gvlkValue.ReadOnly && form.makKey.Enabled;
        form.selected = visio with { ProductId = "ProPlus2024Volume" };
        form.UpdateControls();
        bool wrongEdition = form.SuggestedKmsKey is null &&
            form.gvlkValue.Text.Length == 0;
        var activated = visio with
        {
            ActivationId = Guid.NewGuid().ToString("D"),
            PartialKey = "YY89G",
            Status = "Licensed"
        };
        form.inventory = new OfficeVolumeInventory([visio, activated],
            ["VisioPro2024Volume"], null) { InstalledProductsVerified = true };
        form.showTechnicalRows = false;
        form.PopulateProductList(null);
        var activeFirst = form.selected?.ActivationId == activated.ActivationId &&
            form.displayedProducts.Count == 1 &&
            !string.IsNullOrWhiteSpace(form.otherRecordsLink.Text) && !form.apply.Enabled &&
            form.selectedStatus.Text.Contains("Licensed", StringComparison.Ordinal);
        form.showTechnicalRows = true;
        form.PopulateProductList(null);
        var allRecords = form.displayedProducts.Count == 2 &&
            form.selected?.ActivationId == activated.ActivationId;
        var preview = visio with
        {
            ActivationId = Guid.NewGuid().ToString("D"),
            Name = "Office24VisioPro2024PreviewVL_KMS_Client_AE",
            PartialKey = "WXGKQ", IsPreview = true, IsSupported2024Volume = false,
            Status = "Unlicensed"
        };
        form.inventory = new OfficeVolumeInventory([visio, activated, preview],
            ["VisioPro2024Volume"], null) { InstalledProductsVerified = true };
        form.UpdatePreviewPanel();
        bool oldPreview = form.canRemoveLegacyPreview &&
            form.selectedPreviewForAction?.ActivationId == preview.ActivationId &&
            form.previewAction.Text.Contains("Preview", StringComparison.Ordinal);
        form.inventory = new OfficeVolumeInventory([visio, activated,
            preview with { PartialKey = "" }], ["VisioPro2024Volume"], null)
            { InstalledProductsVerified = true };
        form.UpdatePreviewPanel();
        bool emptyPreview = !form.canRemoveLegacyPreview &&
            form.selectedPreviewForAction is null &&
            form.previewInfo.Text.Length > 15;
        return initial && automatic && makMode && wrongEdition &&
            activeFirst && allRecords && oldPreview && emptyPreview &&
            UiTheme.VerifyButtons(form, 2);
    }
}
