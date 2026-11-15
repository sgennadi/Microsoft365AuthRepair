using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// A localized, strictly offline lookup for an error CODE, not a login,
// password, token, screenshot or potentially sensitive raw log.
internal sealed class SignInErrorDialog : Form
{
    private readonly TextBox code = new()
    {
        Dock = DockStyle.Fill,
        MaxLength = 128,
        Margin = new Padding(0, 6, 10, 6),
        AccessibleName = "Microsoft 365 sign-in error code"
    };
    private readonly Button explain = UiTheme.CreateActionButton(
        UpdateUiText.T("Explain code", "Объяснить код", "הסבר קוד"), primary: true);
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private readonly Label title = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label source = UiTheme.Caption(string.Empty);
    private readonly Label diagnosis = UiTheme.Caption(string.Empty);
    private readonly Label next = UiTheme.Caption(string.Empty);
    private readonly Label privacy = UiTheme.Caption(string.Empty);
    private readonly ComboBox scenario = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Margin = new Padding(0, 6, 0, 6)
    };
    private readonly LinkLabel documentation = new()
    {
        AutoSize = true, Visible = false, TabStop = true,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 6, 0, 5)
    };
    private SignInHelpArticle? linkedArticle;
    internal bool HasVerifiedSource => linkedArticle is not null &&
        SignInKnowledgeBase.IsMicrosoftLearn(linkedArticle.SourceUrl);
    internal bool LookupIsOffline => true;
    internal bool HasResult => !string.IsNullOrWhiteSpace(diagnosis.Text);

    internal SignInErrorDialog(string? recommendedKey = null)
    {
        Text = UpdateUiText.T("Sign-in error guide",
            "Справочник ошибок входа", "מדריך שגיאות התחברות");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 650);
        MinimumSize = new Size(630, 500);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(900, 650),
            new Size(1150, 850), widthFraction: 0.76, heightFraction: 0.76);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
            Padding = new Padding(18), AutoScroll = true, BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        title.Text = UpdateUiText.T("What does this sign-in error mean?",
            "Что означает ошибка входа?", "מה משמעות שגיאת ההתחברות?");
        source.Text = UpdateUiText.T(
            "Enter an error code only, e.g. AADSTS53003, 1001 or 0x80090016. Never paste credentials or an unredacted log.",
            "Введите только код ошибки, например AADSTS53003, 1001 или 0x80090016. Не вставляйте пароль, токены или неочищенный журнал.",
            "הזן קוד שגיאה בלבד, למשל AADSTS53003, 1001 או 0x80090016. אל תדביק סיסמאות או יומנים.");
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(source, 0, 1);

        var search = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2,
            Margin = new Padding(0, 8, 0, 8)
        };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.Controls.Add(code, 0, 0);
        search.Controls.Add(explain, 1, 0);
        layout.Controls.Add(search, 0, 2);

        scenario.AccessibleName = UpdateUiText.T(
            "Choose a Microsoft Learn troubleshooting topic", "Выберите проблему из Microsoft Learn",
            "בחר בעיה ממאמרי Microsoft Learn");
        scenario.Items.Add(UpdateUiText.T(
            "Or choose a problem from Microsoft Learn…",
            "Или выберите проблему из Microsoft Learn…",
            "או בחר בעיה מתוך Microsoft Learn…"));
        var articles = SignInKnowledgeBase.All();
        foreach (var article in articles) scenario.Items.Add(article.Title);
        scenario.SelectedIndex = 0;
        layout.Controls.Add(scenario, 0, 3);

        var result = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            Padding = new Padding(14), ColumnCount = 1, RowCount = 2,
            BackColor = SystemColors.Window,
            Margin = new Padding(0, 8, 0, 8), BorderStyle = BorderStyle.FixedSingle
        };
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        result.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        result.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        result.Controls.Add(diagnosis, 0, 0);
        result.Controls.Add(next, 0, 1);
        layout.Controls.Add(result, 0, 4);

        privacy.Text = UpdateUiText.T(
            "Offline lookup. No report is sent. Conditional Access and MFA rules are enforced by Entra and cannot be bypassed here.",
            "Локальный справочник, без отправки данных. Правила MFA и Conditional Access задаются в Entra; программа их не обходит.",
            "מידע מקומי ללא שליחת נתונים. מדיניות MFA וגישת Entra לא ניתנת לעקיפה כאן.");
        documentation.Text = UpdateUiText.T(
            "Open source: Microsoft Learn", "Открыть первоисточник: Microsoft Learn",
            "פתח מקור: Microsoft Learn");
        documentation.AccessibleName = documentation.Text;
        layout.Controls.Add(documentation, 0, 5);
        layout.Controls.Add(privacy, 0, 6);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        actions.Controls.Add(close);
        layout.Controls.Add(actions, 0, 7);
        Controls.Add(ResponsiveLayout.Center(this, layout, 1010));

        layout.SizeChanged += (_, _) =>
        {
            var width = Math.Max(170, layout.ClientSize.Width - layout.Padding.Horizontal - 18);
            title.MaximumSize = new Size(width, 0);
            source.MaximumSize = new Size(width, 0);
            privacy.MaximumSize = new Size(width, 0);
        };
        result.SizeChanged += (_, _) =>
        {
            var width = Math.Max(160, result.ClientSize.Width - result.Padding.Horizontal - 20);
            diagnosis.MaximumSize = new Size(width, 0);
            next.MaximumSize = new Size(width, 0);
        };
        explain.Click += (_, _) => ShowExplanation(code.Text);
        scenario.SelectedIndexChanged += (_, _) =>
        {
            if (scenario.SelectedIndex > 0 && scenario.SelectedIndex <= articles.Count)
            {
                code.Clear();
                ShowArticle(articles[scenario.SelectedIndex - 1]);
            }
        };
        documentation.LinkClicked += (_, _) =>
        {
            var url = linkedArticle?.SourceUrl;
            if (url is null || !SignInKnowledgeBase.IsMicrosoftLearn(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                documentation.Text = UpdateUiText.T("Cannot open browser", "Не удалось открыть браузер",
                    "לא ניתן לפתוח דפדפן");
            }
        };
        close.Click += (_, _) => Close();
        CancelButton = close;
        AcceptButton = explain;
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(explain, UiGlyph.Search, primary: true);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        ShowExplanation("");
        if (!string.IsNullOrEmpty(recommendedKey))
        {
            var index = articles.ToList().FindIndex(article => article.Key == recommendedKey);
            if (index >= 0) scenario.SelectedIndex = index + 1;
        }
    }

    private void ShowArticle(SignInHelpArticle article)
    {
        linkedArticle = article;
        documentation.Visible = true;
        diagnosis.Text = article.Title + Environment.NewLine + Environment.NewLine +
            UpdateUiText.T("SYMPTOM", "СИМПТОМ", "תסמין") + Environment.NewLine +
            article.Symptom + Environment.NewLine + Environment.NewLine +
            UpdateUiText.T("POSSIBLE CAUSE", "ВОЗМОЖНАЯ ПРИЧИНА", "גורם אפשרי") +
            Environment.NewLine + article.Cause;
        next.Text = UpdateUiText.T("CHECK", "ПРОВЕРКА", "בדיקה") +
            Environment.NewLine + article.Check + Environment.NewLine + Environment.NewLine +
            UpdateUiText.T("SAFE ACTION", "БЕЗОПАСНОЕ ДЕЙСТВИЕ", "פעולה בטוחה") +
            Environment.NewLine + article.SafeAction;
    }

    internal void ShowExplanation(string input)
    {
        var issue = SignInIssueAdvisor.Explain(input);
        var domain = issue.Scope switch
        {
            SignInIssueScope.LocalRepair => UpdateUiText.T(
                "LOCAL REPAIR POSSIBLE", "ВОЗМОЖНО ЛОКАЛЬНОЕ ИСПРАВЛЕНИЕ", "ייתכן תיקון מקומי"),
            SignInIssueScope.UserAction => UpdateUiText.T(
                "USER ACTION", "ДЕЙСТВИЕ ПОЛЬЗОВАТЕЛЯ", "פעולה של משתמש"),
            SignInIssueScope.Administrator => UpdateUiText.T(
                "IT / ENTRA ADMIN", "АДМИНИСТРАТОР IT / ENTRA", "מנהל IT / ENTRA"),
            _ => UpdateUiText.T("INVESTIGATE", "ТРЕБУЕТ ПРОВЕРКИ", "נדרשת בדיקה")
        };
        diagnosis.Text = $"{domain} · {issue.Code}" + Environment.NewLine +
            issue.Title + Environment.NewLine + Environment.NewLine + issue.Cause;
        next.Text = UpdateUiText.T("NEXT STEP", "СЛЕДУЮЩЕЕ ДЕЙСТВИЕ", "השלב הבא") +
            Environment.NewLine + issue.NextStep;
        linkedArticle = SignInKnowledgeBase.ByCode(issue.Code);
        documentation.Visible = linkedArticle is not null;
        if (linkedArticle is not null)
        {
            next.Text += Environment.NewLine + Environment.NewLine +
                UpdateUiText.T("MICROSOFT CHECK", "ПРОВЕРКА ПО MICROSOFT LEARN", "בדיקה לפי Microsoft Learn") +
                Environment.NewLine + linkedArticle.Check;
        }
        // Input is neither saved nor logged. Only a code-shaped lookup is used.
    }

    internal static bool SelfTest()
    {
        using var form = new SignInErrorDialog();
        form.ShowExplanation("AADSTS53003");
        return form.LookupIsOffline && form.HasResult &&
            form.HasVerifiedSource &&
            form.diagnosis.Text.Contains("AADSTS53003", StringComparison.Ordinal) &&
            form.scenario.Items.Count >= 12 &&
            UiTheme.VerifyButtons(form, 2);
    }
}
