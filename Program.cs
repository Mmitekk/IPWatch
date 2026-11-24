using System.Runtime.InteropServices;
using System.Threading;

namespace IpWatch;

internal static class Program
{
    private const string ShowEventName = @"Local\IPWatch.ShowEvent";
    private static readonly int ShowWindowMessage = NativeMethods.RegisterWindowMessage("IPWatch.ShowWindow");

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\IPWatch.SingleInstance", out bool created);
        if (!created)
        {
            // Уже запущено — просим существующее окно показаться.
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
                showEvent.Set();
            }
            catch (Exception)
            {
                var hwnd = NativeMethods.FindWindow(null, "IP Watch");
                if (hwnd != IntPtr.Zero)
                    NativeMethods.PostMessage(hwnd, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
                else
                    NativeMethods.PostMessage(NativeMethods.HwndBroadcast, ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
            }

            return;
        }

        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Autostart.EnsureFirstRun();

        bool startHidden = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(startHidden, showSignal));
    }
}

internal static class NativeMethods
{
    public static readonly IntPtr HwndBroadcast = new(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? className, string windowName);
}
