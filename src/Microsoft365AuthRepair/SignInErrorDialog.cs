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
        MaxLength = 64,
        Margin = new Padding(0, 6, 0, 6),
        AccessibleName = "Microsoft 365 sign-in error code"
    };
    private readonly ListBox matches = new()
    {
        Dock = DockStyle.Fill, IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
        Visible = false, Height = 118,
        Margin = new Padding(0, 0, 0, 7),
        AccessibleName = "Live matching sign-in error codes and Microsoft Learn topics"
    };
    private readonly List<string> suggestedCodes = [];
    private readonly List<SignInHelpArticle> suggestedArticles = [];
    private bool selectingSuggestion;
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
        Size = new Size(860, 580);
        MinimumSize = new Size(605, 440);
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        ResponsiveLayout.FitInitialWindow(this, new Size(860, 580),
            new Size(1090, 760), widthFraction: 0.74, heightFraction: 0.70);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9,
            Padding = new Padding(18), AutoScroll = true, BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        title.Text = UpdateUiText.T("What does this sign-in error mean?",
            "Что означает ошибка входа?", "מה משמעות שגיאת ההתחברות?");
        source.Text = UpdateUiText.T(
            "Type an error code (AADSTS53003, 1001, 0x80090016) or a problem name. Results appear instantly. Do not paste passwords or logs.",
            "Введите код (AADSTS53003, 1001, 0x80090016) или название проблемы — результат появится сразу. Не вставляйте пароли и журналы.",
            "הקלד קוד שגיאה או תיאור בעיה — התוצאות יופיעו מיד. אל תדביק סיסמאות או יומנים.");
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(source, 0, 1);

        code.PlaceholderText = UpdateUiText.T(
            "Search code or problem…", "Поиск кода или проблемы…", "חפש קוד או בעיה…");
        code.AccessibleDescription = UpdateUiText.T(
            "Find the cause and next steps automatically as you type",
            "Причина и дальнейшие действия появляются во время ввода",
            "הסבר ושלבים הבאים מוצגים בעת ההקלדה");
        layout.Controls.Add(code, 0, 2);
        layout.Controls.Add(matches, 0, 3);

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
        layout.Controls.Add(scenario, 0, 4);

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
        layout.Controls.Add(result, 0, 5);

        privacy.Text = UpdateUiText.T(
            "Offline lookup. No report is sent. Conditional Access and MFA rules are enforced by Entra and cannot be bypassed here.",
            "Локальный справочник, без отправки данных. Правила MFA и Conditional Access задаются в Entra; программа их не обходит.",
            "מידע מקומי ללא שליחת נתונים. מדיניות MFA וגישת Entra לא ניתנת לעקיפה כאן.");
        documentation.Text = UpdateUiText.T(
            "Open source: Microsoft Learn", "Открыть первоисточник: Microsoft Learn",
            "פתח מקור: Microsoft Learn");
        documentation.AccessibleName = documentation.Text;
        layout.Controls.Add(documentation, 0, 6);
        layout.Controls.Add(privacy, 0, 7);
        var actions = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        actions.Controls.Add(close);
        layout.Controls.Add(actions, 0, 8);
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
        code.TextChanged += (_, _) =>
        {
            if (!selectingSuggestion) SearchAsYouType(code.Text, articles);
        };
        code.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Down && matches.Items.Count > 0)
            {
                matches.Focus();
                matches.SelectedIndex = 0;
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
            else if (args.KeyCode == Keys.Enter)
            {
                AcceptSuggestion(articles);
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        };
        matches.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                AcceptSuggestion(articles);
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
            else if (args.KeyCode == Keys.Escape)
            {
                code.Focus();
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        };
        matches.MouseDoubleClick += (_, _) => AcceptSuggestion(articles);
        scenario.SelectedIndexChanged += (_, _) =>
        {
            if (scenario.SelectedIndex > 0 && scenario.SelectedIndex <= articles.Count)
            {
                selectingSuggestion = true;
                try { code.Clear(); }
                finally { selectingSuggestion = false; }
                matches.Items.Clear();
                matches.Visible = false;
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
        UiIcons.SetWindowIcon(this);
        UiIcons.TrackDpi(this);
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
        ShowPrompt();
        if (!string.IsNullOrEmpty(recommendedKey))
        {
            var index = articles.ToList().FindIndex(article => article.Key == recommendedKey);
            if (index >= 0) scenario.SelectedIndex = index + 1;
        }
    }

    private void ShowPrompt()
    {
        linkedArticle = null;
        documentation.Visible = false;
        diagnosis.Text = UpdateUiText.T(
            "Type an error code to see what it means, or search by problem name.",
            "Начните вводить код ошибки или название проблемы.",
            "התחל להקליד קוד שגיאה או שם בעיה.");
        next.Text = "";
    }

    private void SearchAsYouType(string input, IReadOnlyList<SignInHelpArticle> articles)
    {
        var trimmed = input.Trim();
        suggestedCodes.Clear();
        suggestedArticles.Clear();
        matches.BeginUpdate();
        try
        {
            matches.Items.Clear();
            if (trimmed.Length == 0)
            {
                matches.Visible = false;
                ShowPrompt();
                return;
            }
            // Exact recognized codes are explained on the very keystroke that
            // completes them. Unknown AADSTS codes show a neutral explanation.
            var extracted = SignInIssueAdvisor.ExtractCode(trimmed);
            var candidates = SignInIssueAdvisor.MatchingCodes(trimmed);
            if (extracted.Length > 0 &&
                (candidates.Contains(extracted) || trimmed.StartsWith("AADSTS", StringComparison.OrdinalIgnoreCase)))
            {
                matches.Visible = false;
                if (scenario.SelectedIndex != 0) scenario.SelectedIndex = 0;
                ShowExplanation(trimmed);
                return;
            }

            foreach (var found in candidates.Take(6))
            {
                suggestedCodes.Add(found);
                matches.Items.Add(found + " — " + SignInIssueAdvisor.Explain(found).Title);
            }
            // A text search also covers article titles and symptoms, useful
            // when the user knows "Outlook", "MFA" or "activation" but not
            // the precise code.
            if (trimmed.Length >= 2 && trimmed.Length <= 48 &&
                !trimmed.Contains('@') && !trimmed.Contains('\\') &&
                !trimmed.Contains('/'))
            {
                foreach (var article in articles
                    .Where(x => x.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                        x.Symptom.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                    .Take(Math.Max(0, 6 - suggestedCodes.Count)))
                {
                    suggestedArticles.Add(article);
                    matches.Items.Add(article.Title);
                }
            }
            matches.Height = Math.Min(148, Math.Max(29,
                matches.Items.Count * (Font.Height + 9) + 7));
            matches.Visible = matches.Items.Count > 0;
            if (scenario.SelectedIndex != 0) scenario.SelectedIndex = 0;
            if (matches.Items.Count == 0 && extracted.Length > 0)
                ShowExplanation(trimmed);
            else if (matches.Items.Count > 0)
            {
                linkedArticle = null;
                documentation.Visible = false;
                diagnosis.Text = UpdateUiText.T(
                    "Matches are shown above. Continue typing or select one.",
                    "Совпадения показаны выше. Продолжайте ввод или выберите результат.",
                    "התאמות מוצגות למעלה. המשך להקליד או בחר תוצאה.");
                next.Text = "";
            }
            else ShowPrompt();
        }
        finally { matches.EndUpdate(); }
    }

    private void AcceptSuggestion(IReadOnlyList<SignInHelpArticle> articles)
    {
        int index = matches.SelectedIndex;
        if (index < 0 && matches.Items.Count == 1) index = 0;
        if (index < 0) return;
        if (index < suggestedCodes.Count)
        {
            var chosen = suggestedCodes[index];
            selectingSuggestion = true;
            try { code.Text = chosen; }
            finally { selectingSuggestion = false; }
            matches.Visible = false;
            matches.Items.Clear();
            if (scenario.SelectedIndex != 0) scenario.SelectedIndex = 0;
            ShowExplanation(chosen);
            code.Focus();
            code.SelectionStart = code.TextLength;
        }
        else
        {
            var articleIndex = index - suggestedCodes.Count;
            if (articleIndex < 0 || articleIndex >= suggestedArticles.Count) return;
            var article = suggestedArticles[articleIndex];
            selectingSuggestion = true;
            try { code.Clear(); }
            finally { selectingSuggestion = false; }
            var selectedArticleIndex = articles.ToList().FindIndex(x => x.Key == article.Key);
            if (selectedArticleIndex >= 0) scenario.SelectedIndex = selectedArticleIndex + 1;
            matches.Items.Clear();
            matches.Visible = false;
            ShowArticle(article);
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
        form.code.Text = "530";
        var hasMatches = form.matches.Items.Count >= 1 &&
            form.matches.Items.Cast<string>().Any(x => x.StartsWith("AADSTS53003", StringComparison.Ordinal));
        form.code.Text = "53003";
        var liveExact = form.diagnosis.Text.Contains("AADSTS53003", StringComparison.Ordinal) &&
            form.HasVerifiedSource;
        form.code.Text = "1001";
        var wam = form.diagnosis.Text.Contains("1001", StringComparison.Ordinal) &&
            form.HasVerifiedSource;
        form.code.Text = "activation";
        var bySubject = form.matches.Items.Count > 0;
        form.code.Text = "some.person@example.org";
        var noSensitiveEcho = form.matches.Items.Count == 0 &&
            !form.diagnosis.Text.Contains("some.person", StringComparison.Ordinal);
        form.ShowExplanation("AADSTS53003");
        return hasMatches && liveExact && wam && bySubject && noSensitiveEcho &&
            form.LookupIsOffline && form.HasResult && form.HasVerifiedSource &&
            form.scenario.Items.Count >= 12 && UiTheme.VerifyButtons(form, 1);
    }
}
