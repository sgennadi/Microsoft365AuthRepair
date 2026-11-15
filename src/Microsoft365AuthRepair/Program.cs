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
            if (args.Length == 1 && args[0] == "--ui-self-test")
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using var form = new MainForm();
                using var updater = new UpdateCenter();
                using var reports = new DiagnosticsCenter(uiSelfTest: true);
                using var sendReview = new ReportSendDialog(
                    new PreparedDiagnostic("00000000-0000-4000-8000-000000000001", "{}"), null);
                using var wizard = new AllUsersWizard(ScheduledWorkerMode.Exe,
                    initialScheduleOnly: false, elevated: false);
                using var signInGuide = new SignInErrorDialog();
                using var volumeLicensing = new OfficeVolumeLicenseForm();
                form.CreateControl();
                updater.CreateControl();
                reports.CreateControl();
                sendReview.CreateControl();
                wizard.CreateControl();
                signInGuide.CreateControl();
                volumeLicensing.CreateControl();
                // On a headless Windows CI runner the GUI has no console.
                // Record boolean test names only; never log user identities.
                var tests = new (string Name, Func<bool> Run)[]
                {
                    ("Main minimum size", () => form.MinimumSize.Width >= 600),
                    ("Responsive Setup and Language header", form.HeaderLayoutSelfTest),
                    ("Direct current-user / wizard-only all-users mode", form.ScopeWorkflowSelfTest),
                    ("Main icon", () => UiIcons.Verify(form)),
                    ("Updater icon", () => UiIcons.Verify(updater)),
                    ("Reports icon", () => UiIcons.Verify(reports)),
                    ("Local report queue and repair logs", () => reports.HasLocalUtilities),
                    ("Send review icon", () => UiIcons.Verify(sendReview)),
                    ("Wizard icon", () => UiIcons.Verify(wizard)),
                    ("Sign-in guide window icon", () => UiIcons.HasBrandIcon &&
                        signInGuide.Icon is not null && signInGuide.ShowIcon),
                    ("Volume licensing icon", () => UiIcons.Verify(volumeLicensing)),
                    ("Responsive geometry", ResponsiveLayout.SelfTest),
                    ("Updater channel precedence", UpdaterIntegration.SelfTest),
                    ("Updater default channel", () => updater.IncludesPreviews == UpdaterIntegration.InstalledIsPreview),
                    ("Update notes layout", () => updater.ChangeListHasSeparateLines),
                    ("Review approval default", () => sendReview.ConsentInitiallyChecked &&
                        sendReview.ConfirmedIssue is null),
                    ("All users wizard", AllUsersWizard.SelfTest),
                    ("Sign-in error codes", SignInIssueAdvisor.SelfTest),
                    ("Sign-in guide", SignInErrorDialog.SelfTest),
                    ("Microsoft Learn knowledge base", SignInKnowledgeBase.SelfTest),
                    ("Native vNext license checks", OfficeLicenseDiagnostics.SelfTest),
                    ("Office 2024 Volume product identity", OfficeVolumeLicensing.SelfTest),
                    ("Volume licensing UI", OfficeVolumeLicenseForm.SelfTest),
                    ("WAM health analysis", WamHealthAnalysis.SelfTest),
                    ("WAM result card", DiagnosticResultCard.SelfTest),
                    ("What's New format", UpdateUiText.SelfTest),
                    ("Diagnostic payload", M365DiagnosticPayload.SelfTest),
                    ("Updater helper", UpdateHandoff.SelfTest),
                    ("User scope", UserScopeSupport.SelfTest),
                    ("Scheduled worker resource", Scheduling.SelfTest),
                    ("Protected worker maintenance rules", ScheduledWorkerMaintenance.SelfTest),
                    ("Legacy logon cleanup", FirstLogonDeployment.SelfTest),
                    ("Main buttons", () => UiTheme.VerifyButtons(form, 5)),
                    ("Updater buttons", () => UiTheme.VerifyButtons(updater, 4)),
                    ("Reports buttons", () => UiTheme.VerifyButtons(reports, 3)),
                    ("Review buttons", () => UiTheme.VerifyButtons(sendReview, 2)),
                    ("Wizard buttons", () => UiTheme.VerifyButtons(wizard, 3)),
                    ("Sign-in guide buttons", () => UiTheme.VerifyButtons(signInGuide, 1)),
                    ("Volume licensing buttons", () => UiTheme.VerifyButtons(volumeLicensing, 2))
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
            bool hasWamReview = allOptions.Contains("--review-wam", StringComparer.Ordinal);
            bool hasResetReview = allOptions.Contains("--review-reset", StringComparer.Ordinal);
            bool adminAll = args.Length >= 1 && args[0] == "--all-users"
                && allOptions.Length <= 3
                && allOptions.All(x => x is "--powershell-tasks" or "--schedule-only"
                    or "--review-wam" or "--review-reset")
                && allOptions.Distinct(StringComparer.Ordinal).Count() == allOptions.Length
                && !(hasWamReview && hasResetReview)
                && !(hasResetReview && allOptions.Contains("--powershell-tasks", StringComparer.Ordinal));
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
                    scheduleOnlyAtLogon: onlySchedule,
                    pendingAction: adminAll && Identity.IsElevated()
                        ? hasResetReview ? AllUsersWizardAction.ResetOffice
                          : hasWamReview ? AllUsersWizardAction.RepairWam
                          : AllUsersWizardAction.SaveOnly
                        : AllUsersWizardAction.SaveOnly));
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
