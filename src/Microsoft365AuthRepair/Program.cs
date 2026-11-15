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
            if (args.Length == 1 && args[0] == "--ui-self-test")
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                using var form = new MainForm();
                form.CreateControl();
                return form.MinimumSize.Width >= 600 && form.Controls.Count > 0 ? 0 : 2;
            }
            if (args.Length > 0) return 2;
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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
