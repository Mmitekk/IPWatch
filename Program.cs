using System.Runtime.InteropServices;
using System.Threading;

namespace IpWatch;

internal static class Program
{
    private static readonly int ShowWindowMessage = NativeMethods.RegisterWindowMessage("IPWatch.ShowWindow");

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\IPWatch.SingleInstance", out bool created);
        if (!created)
        {
            // Уже запущено — просим существующее окно показаться.
            NativeMethods.PostMessage(NativeMethods.HwndBroadcast, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Autostart.EnsureFirstRun();

        bool startHidden = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(startHidden));
    }
}

internal static class NativeMethods
{
    public static readonly IntPtr HwndBroadcast = new(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
