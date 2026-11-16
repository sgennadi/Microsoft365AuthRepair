using System.Drawing;
using System.Windows.Forms;

namespace Microsoft365AuthRepair;

// Read-only inspection by default. A separate explicit, narrowly scoped
// button can queue ONLY a confirmed failed WAM action for next sign-in.
internal sealed class AllUsersTaskStatusForm : Form
{
    private readonly DataGridView rows = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        MultiSelect = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToOrderColumns = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        ScrollBars = ScrollBars.Both,
        BorderStyle = BorderStyle.FixedSingle,
        BackgroundColor = SystemColors.Window,
        GridColor = SystemColors.ControlLight,
        Margin = new Padding(0, 9, 0, 9)
    };
    private readonly Label headline = UiTheme.Caption(string.Empty, heading: true);
    private readonly Label explanation = UiTheme.Caption(string.Empty);
    private readonly Label summary = UiTheme.Caption(string.Empty);
    private readonly Button refresh = UiTheme.CreateActionButton(
        UpdateUiText.T("Refresh status", "Обновить статусы", "רענון מצב"), primary: true);
    private readonly Button close = UiTheme.CreateActionButton(
        UpdateUiText.T("Close", "Закрыть", "סגור"));
    private readonly Button retry = UiTheme.CreateActionButton(
        UpdateUiText.T("Retry WAM at next sign-in", "Повторить WAM при следующем входе",
            "ניסיון חוזר בכניסה הבאה"));
    private bool checking;

    internal AllUsersTaskStatusForm()
    {
        Text = "Microsoft 365 Auth Repair v" + Repair.Version + " — " +
            UpdateUiText.T("All users task status", "Состояние заданий всех пользователей",
                "מצב משימות לכל המשתמשים");
        Font = UiTheme.BodyFont();
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : UiTheme.Canvas;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 480);
        Size = new Size(1130, 670);
        ResponsiveLayout.FitInitialWindow(this, new Size(1130, 670),
            new Size(1460, 930), widthFraction: 0.88, heightFraction: 0.76);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headline.Text = UpdateUiText.T("All users · Task status",
            "Все пользователи · Состояние заданий", "כל המשתמשים · מצב משימות");
        explanation.Text = UpdateUiText.T(
            "Viewing task status makes no changes. To retry an explicitly failed WAM task, select it and approve a new next-sign-in task. This never retries an Office reset. A worker success does not prove Office sign-in.",
            "Просмотр статусов ничего не изменяет. Для повторного запуска WAM выберите подтверждённо ошибочное задание и разрешите создание нового при следующем входе. Сброс Office не повторяется. Успех worker не доказывает вход в Office.",
            "הצגת מצב אינה משנה דבר. אפשר לנסות שוב רק תיקון WAM שנכשל, באישור מפורש, בכניסה הבאה. אין איפוס Office חוזר.");
        explanation.MaximumSize = new Size(1240, 0);
        summary.Text = UpdateUiText.T("No task status loaded.",
            "Состояние заданий ещё не загружено.", "מצב המשימות טרם נטען.");
        summary.MaximumSize = new Size(1240, 0);
        root.Controls.Add(headline, 0, 0);
        root.Controls.Add(explanation, 0, 1);
        root.Controls.Add(summary, 0, 2);

        void Add(string name, string header, float fill)
        {
            rows.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, FillWeight = fill,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = 75
            });
        }
        Add("User", UpdateUiText.T("User", "Пользователь", "משתמש"), 18f);
        Add("Action", UpdateUiText.T("Action / engine", "Действие / способ", "פעולה / מנוע"), 19f);
        Add("Time", UpdateUiText.T("Queued at", "Создано", "זמן רישום"), 16f);
        Add("State", UpdateUiText.T("State", "Состояние", "מצב"), 18f);
        Add("Evidence", UpdateUiText.T("Evidence", "Что известно", "מידע"), 29f);
        rows.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        rows.DefaultCellStyle.Padding = new Padding(5, 4, 5, 4);
        rows.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        rows.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        rows.EnableHeadersVisualStyles = false;
        rows.RowTemplate.MinimumHeight = 30;
        root.Controls.Add(rows, 0, 3);

        var footer = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0)
        };
        footer.Controls.Add(close);
        retry.Enabled = false;
        footer.Controls.Add(retry);
        footer.Controls.Add(refresh);
        root.Controls.Add(footer, 0, 4);
        root.SizeChanged += (_, _) =>
        {
            var width = Math.Max(190, root.ClientSize.Width - root.Padding.Horizontal - 20);
            explanation.MaximumSize = new Size(width, 0);
            summary.MaximumSize = new Size(width, 0);
        };
        Controls.Add(ResponsiveLayout.Center(this, root, 1360));
        UiIcons.SetWindowIcon(this);
        UiIcons.Assign(refresh, UiGlyph.Search, primary: true);
        UiIcons.Assign(close, UiGlyph.Reset);
        UiIcons.TrackDpi(this);
        AcceptButton = refresh;
        CancelButton = close;
        close.Click += (_, _) => Close();
        refresh.Click += async (_, _) => await RefreshAsync();
        rows.SelectionChanged += (_, _) => UpdateRetrySelection();
        retry.Click += async (_, _) => await RetrySelectedAsync();
        Shown += async (_, _) => await RefreshAsync();
        RightToLeft = UiStrings.Language == "he" ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = UiStrings.Language == "he";
    }

    private static string StateText(string state) => state switch
    {
        "WorkerReportedSuccess" => UpdateUiText.T("Worker reported success",
            "Worker сообщил об успехе", "ה-worker דיווח הצלחה"),
        "WorkerReportedFailure" => UpdateUiText.T("Worker reported failure",
            "Worker сообщил об ошибке", "ה-worker דיווח שגיאה"),
        "Running" => UpdateUiText.T("Running", "Выполняется", "פעיל"),
        "Queued" => UpdateUiText.T("Waiting / queued", "Ожидает запуска", "ממתין"),
        "Expired" => UpdateUiText.T("Expired", "Срок истёк", "פג תוקף"),
        "SchedulerNeedsAttention" => UpdateUiText.T("Task error",
            "Ошибка задания", "שגיאת משימה"),
        "AttemptedUnconfirmed" => UpdateUiText.T("Attempted · unknown result",
            "Запускался · результат неизвестен", "בוצע ניסיון · תוצאה לא ידועה"),
        "TaskFinishedUnconfirmed" => UpdateUiText.T("Finished · unconfirmed",
            "Завершён · не подтверждён", "הסתיים · לא מאומת"),
        _ => UpdateUiText.T("Not confirmed", "Не подтверждено", "לא מאומת")
    };

    private static string EvidenceText(string state) => state switch
    {
        "WorkerReportedSuccess" => UpdateUiText.T(
            "Local success receipt, not an Office sign-in test.",
            "Локальная отметка успеха, но вход в Office не проверен.",
            "אישור מקומי אינו בדיקת כניסה ל-Office."),
        "WorkerReportedFailure" => UpdateUiText.T(
            "Check the affected user's local repair log.",
            "Проверьте локальный журнал восстановления пользователя.",
            "בדוק יומן תיקון מקומי של המשתמש."),
        "Running" => UpdateUiText.T(
            "Task Scheduler currently reports running.",
            "Планировщик сообщает, что задание выполняется.",
            "מתזמן המשימות מדווח על פעולה."),
        "Queued" => UpdateUiText.T(
            "Registered, waiting for logon/start.",
            "Зарегистрировано, ожидает входа или запуска.",
            "נרשמה משימה וממתינה להפעלה."),
        "Expired" => UpdateUiText.T(
            "No verified worker success before expiry.",
            "До истечения срока успех не подтверждён.",
            "אין אישור הצלחה לפני תום הזמן."),
        "SchedulerNeedsAttention" => UpdateUiText.T(
            "Scheduler returned nonzero; inspect the task.",
            "Планировщик вернул ошибку; проверьте задание.",
            "המתזמן דיווח שגיאה."),
        "AttemptedUnconfirmed" => UpdateUiText.T(
            "Started once; final result was not recorded.",
            "Однократный запуск был; итог не зарегистрирован.",
            "התחיל אך התוצאה הסופית לא נשמרה."),
        "TaskFinishedUnconfirmed" => UpdateUiText.T(
            "Scheduler finished; worker success not verified.",
            "Планировщик завершил; успех worker не подтверждён.",
            "המתזמן סיים, אך הצלחת worker לא אומתה."),
        _ => UpdateUiText.T(
            "Missing task or receipt; no success claim.",
            "Нет задания или отметки; успех не подтверждён.",
            "משימה או אישור חסרים; אין הוכחת הצלחה.")
    };

    private async Task RefreshAsync()
    {
        if (checking || IsDisposed || Disposing) return;
        checking = true;
        refresh.Enabled = false;
        retry.Enabled = false;
        summary.Text = UpdateUiText.T("Reading protected local task records...",
            "Читаю локальную историю заданий...", "קורא את היסטוריית המשימות...");
        try
        {
            var data = await Task.Run(TaskRunHistory.Snapshot);
            if (IsDisposed || Disposing) return;
            rows.Rows.Clear();
            foreach (var item in data)
            {
                var action = item.Action == "full"
                    ? UpdateUiText.T("Office sign-in reset", "Сброс входа Office", "איפוס כניסה")
                    : UpdateUiText.T("WAM re-registration", "Восстановление WAM", "רישום WAM");
                var local = item.SubmittedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    ?? UpdateUiText.T("Older task", "Старое задание", "משימה ישנה");
                int index = rows.Rows.Add(item.User, action + " · " + item.Mode, local,
                    StateText(item.State), EvidenceText(item.State));
                rows.Rows[index].Tag = item;
            }
            UpdateRetrySelection();
            int successes = data.Count(x => x.State == "WorkerReportedSuccess");
            int pending = data.Count(x => x.State is "Queued" or "Running");
            int followUp = data.Count - pending - successes;
            summary.Text = UpdateUiText.T(
                $"Recent tasks: {data.Count} · waiting/running: {pending} · worker-reported success: {successes} · need review: {followUp}.",
                $"Последние задания: {data.Count} · ожидают/работают: {pending} · worker сообщил об успехе: {successes} · требуют проверки: {followUp}.",
                $"משימות אחרונות: {data.Count} · ממתינות/פעילות: {pending} · הצלחה מדווחת: {successes} · דורשות בדיקה: {followUp}.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                summary.Text = UpdateUiText.T("Status could not be read: ",
                    "Не удалось прочитать статусы: ", "קריאת המצב נכשלה: ") +
                    ex.GetType().Name;
        }
        finally
        {
            checking = false;
            if (!IsDisposed && !Disposing) refresh.Enabled = true;
        }
    }

    private TaskStatusRow? SelectedTask() =>
        rows.SelectedRows.Count == 1 ? rows.SelectedRows[0].Tag as TaskStatusRow : null;

    private void UpdateRetrySelection()
    {
        retry.Enabled = !checking && TaskRetryService.CanRetry(SelectedTask()) && Identity.IsElevated();
    }

    private async Task RetrySelectedAsync()
    {
        var selected = SelectedTask();
        if (checking || !TaskRetryService.CanRetry(selected) || selected is null) return;
        var approval = UpdateUiText.T(
            $"Queue ONE WAM re-registration for {selected.User} at their NEXT sign-in? No immediate launch; no Office cache deletion. This does not retry Office Reset. Continue?",
            $"Создать ОДНО задание перерегистрации WAM для {selected.User} при СЛЕДУЮЩЕМ входе? Без немедленного запуска и удаления кешей Office. Продолжить?",
            $"ליצור משימת WAM אחת עבור {selected.User} בכניסה הבאה? ללא הפעלה מיידית או ניקוי מטמון Office?");
        if (MessageBox.Show(this, approval,
                UpdateUiText.T("Confirm selected retry", "Подтвердите повтор для пользователя", "אישור ניסיון חוזר"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        checking = true;
        refresh.Enabled = retry.Enabled = false;
        try
        {
            var message = await Task.Run(() => TaskRetryService.QueueAfterApproval(selected));
            if (IsDisposed || Disposing) return;
            summary.Text = UpdateUiText.T("New WAM task registered for next sign-in. Refresh to review status.",
                "Новое задание WAM зарегистрировано для следующего входа. Обновите статусы.",
                "משימת WAM נרשמה לכניסה הבאה.") + Environment.NewLine + message;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                summary.Text = UpdateUiText.T("Retry was not confirmed: ",
                    "Повторное задание не подтверждено: ", "המשימה לא אושרה: ") + ex.GetType().Name;
        }
        finally
        {
            checking = false;
            if (!IsDisposed && !Disposing)
            {
                refresh.Enabled = true;
                UpdateRetrySelection();
            }
        }
    }

    internal bool SelfTest() =>
        rows.Columns.Count == 5 && rows.ReadOnly && !rows.AllowUserToAddRows &&
        !rows.AllowUserToDeleteRows && !string.IsNullOrWhiteSpace(headline.Text) &&
        UiTheme.VerifyButtons(this, 3);
}
