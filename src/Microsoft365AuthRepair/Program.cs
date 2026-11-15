using System.Windows.Forms;

namespace Microsoft365AuthRepair;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return 2;
            if (args.Length == 5 && args[0] == "--worker")
                return Scheduling.Worker(args[1], args[2], args[3], args[4]);
            if (args.Length == 2 && args[0] == "--update-helper")
                return UpdateHandoff.RunHelper(args[1]);
            // First-logon auto-repair was removed: users without an existing
            // Windows profile have no previous WAM cache to recover.
            if (args.Length == 2 && args[0] == "--cleanup-helper")
                return PortableSelfCleanup.RunHelper(args[1]);
            if (args.Length == 1 && args[0] == "--ui-self-test")
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using var form = new MainForm();
                using var updater = new UpdateCenter();
                using var reports = new DiagnosticsCenter(uiSelfTest: true);
                using var settings = new SettingsForm(ScheduledWorkerMode.PowerShell,
                    initialScheduleOnly: true, initialCleanup: true, canCleanup: false,
                    target: "Test desktop user", elevated: false);
                using var sendReview = new ReportSendDialog(
                    new PreparedDiagnostic("00000000-0000-4000-8000-000000000001", "{}"), null);
                form.CreateControl();
                updater.CreateControl();
                reports.CreateControl();
                settings.CreateControl();
                sendReview.CreateControl();
                // On a headless Windows CI runner the GUI has no console.
                // Record boolean test names only; never log user identities.
                var tests = new (string Name, Func<bool> Run)[]
                {
                    ("Main minimum size", () => form.MinimumSize.Width >= 600),
                    ("Main icon", () => UiIcons.Verify(form)),
                    ("Updater icon", () => UiIcons.Verify(updater)),
                    ("Reports icon", () => UiIcons.Verify(reports)),
                    ("Settings icon", () => UiIcons.Verify(settings)),
                    ("Send review icon", () => UiIcons.Verify(sendReview)),
                    ("Settings state", () => settings.TaskMethod == ScheduledWorkerMode.PowerShell &&
                        settings.ScheduleOnly && !settings.CleanupAfterRepair),
                    ("Review approval default", () => sendReview.ConsentInitiallyChecked &&
                        sendReview.ConfirmedIssue is null),
                    ("WAM health analysis", WamHealthAnalysis.SelfTest),
                    ("What's New format", UpdateUiText.SelfTest),
                    ("Diagnostic payload", M365DiagnosticPayload.SelfTest),
                    ("Updater helper", UpdateHandoff.SelfTest),
                    ("User scope", UserScopeSupport.SelfTest),
                    ("Scheduled worker resource", Scheduling.SelfTest),
                    ("Legacy logon cleanup", FirstLogonDeployment.SelfTest),
                    ("Portable cleanup", PortableSelfCleanup.SelfTest),
                    ("Main buttons", () => UiTheme.VerifyButtons(form, 7)),
                    ("Updater buttons", () => UiTheme.VerifyButtons(updater, 5)),
                    ("Reports buttons", () => UiTheme.VerifyButtons(reports, 4)),
                    ("Settings buttons", () => UiTheme.VerifyButtons(settings, 2)),
                    ("Review buttons", () => UiTheme.VerifyButtons(sendReview, 2))
                };
                var outcomes = new List<string>();
                bool passed = true;
                foreach (var test in tests)
                {
                    try
                    {
                        bool ok = test.Run();
                        outcomes.Add(test.Name + ": " + (ok ? "PASS" : "FAIL"));
                        passed &= ok;
                    }
                    catch (Exception error)
                    {
                        outcomes.Add(test.Name + ": EXCEPTION " + error.GetType().Name);
                        passed = false;
                    }
                }
                File.WriteAllLines(Path.Combine(Path.GetTempPath(), "Microsoft365AuthRepair-UiTest.txt"), outcomes);
                return passed ? 0 : 2;
            }
            // Post-UAC launch selects All users only. It never starts repair
            // or full reset automatically; the operator must confirm again.
            // UAC option propagation is restricted to these literal flags;
            // no arbitrary command line is accepted for bulk user actions.
            var allOptions = args.Length >= 1 && args[0] == "--all-users"
                ? args.Skip(1).ToArray() : [];
            bool adminAll = args.Length >= 1 && args[0] == "--all-users"
                && allOptions.Length <= 2
                && allOptions.All(x => x is "--powershell-tasks" or "--schedule-only")
                && allOptions.Distinct(StringComparer.Ordinal).Count() == allOptions.Length;
            bool usePsTask = adminAll && allOptions.Contains("--powershell-tasks", StringComparer.Ordinal);
            bool onlySchedule = adminAll && allOptions.Contains("--schedule-only", StringComparer.Ordinal);
            bool privilegedUpdate = args.Length == 1 && args[0] is
                ("--update-elevated-stable" or "--update-elevated-preview");
            if (args.Length > 0 && !adminAll && !privilegedUpdate) return 2;
            if (privilegedUpdate && !Identity.IsElevated()) return 2;
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UpdateHandoff.CleanupOldHelpers();
            if (privilegedUpdate)
                Application.Run(new UpdateCenter(autoUpdate: true,
                    includePreviews: args[0] == "--update-elevated-preview"));
            else
                Application.Run(new MainForm(
                    preferAllUsers: adminAll && Identity.IsElevated(),
                    usePowerShellTasks: usePsTask,
                    scheduleOnlyAtLogon: onlySchedule));
            return 0;
        }
        catch (Exception ex)
        {
            // Avoid outputting account identities or raw diagnostic data.
            if (args.Length == 1 && args[0] == "--ui-self-test")
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "Microsoft365AuthRepair-UiTest.txt"),
                        "GUI construction: EXCEPTION " + ex.GetType().Name);
                }
                catch { /* Read-only CI probe; never block startup on logging. */ }
            }
            if (args.Length == 0) MessageBox.Show(ex.Message, "Microsoft 365 Auth Repair", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }
}
