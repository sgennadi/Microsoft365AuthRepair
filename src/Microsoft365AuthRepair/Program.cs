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
                form.CreateControl();
                updater.CreateControl();
                reports.CreateControl();
                return form.MinimumSize.Width >= 600
                    && UiIcons.Verify(form)
                    && UiIcons.Verify(updater)
                    && UiIcons.Verify(reports)
                    && UpdateUiText.SelfTest()
                    && M365DiagnosticPayload.SelfTest()
                    && UpdateHandoff.SelfTest()
                    && UserScopeSupport.SelfTest()
                    && Scheduling.SelfTest()
                    && FirstLogonDeployment.SelfTest()
                    && PortableSelfCleanup.SelfTest()
                    && UiTheme.VerifyButtons(form, 6)
                    && UiTheme.VerifyButtons(updater, 5)
                    && UiTheme.VerifyButtons(reports, 5)
                    ? 0 : 2;
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
            if (args.Length == 0) MessageBox.Show(ex.Message, "Microsoft 365 Auth Repair", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }
}
