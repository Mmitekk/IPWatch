using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IpWatch;

internal sealed class MainForm : Form
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int WM_NCLBUTTONDOWN = 0x00A1;

    private static readonly Color Bg = Color.FromArgb(22, 24, 29);
    private static readonly Color Card = Color.FromArgb(32, 35, 42);
    private static readonly Color Line = Color.FromArgb(48, 52, 61);
    private static readonly Color TextMain = Color.FromArgb(238, 240, 245);
    private static readonly Color TextDim = Color.FromArgb(140, 147, 160);
    private static readonly Color Accent = Color.FromArgb(88, 166, 255);
    private static readonly Color Good = Color.FromArgb(87, 207, 137);
    private static readonly Color Warn = Color.FromArgb(232, 190, 88);
    private static readonly Color Bad = Color.FromArgb(231, 110, 110);

    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;
    private Icon? _trayDynamic;

    private readonly Panel _header;
    private readonly PictureBox _flag;
    private readonly Label _countryLabel;
    private readonly Label _placeLabel;
    private readonly Label _ipLabel;
    private readonly Label _timeLabel;
    private readonly Label _tzLabel;
    private readonly Label _pingLabel;
    private readonly Label _gatewayLabel;
    private readonly Label _localIpLabel;
    private readonly Label _ispLabel;
    private readonly Label _updatedLabel;
    private readonly Label _statusLabel;
    private readonly Button _refreshButton;
    private readonly Button _copyButton;
    private readonly DarkCheckBox _autostartCheck;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private IpInfo? _ip;
    private NetworkSnapshot _net = NetworkSnapshot.Empty;
    private int? _ping;
    private int? _gatewayPing;
    private bool _busy;
    private bool _exiting;
    private CancellationTokenSource? _cts;
    private string _lastCountryCode = "";
    private string _trayIconKey = "";
    private int _lastTrayMinute = -1;
    private readonly Icon _emptyIcon;
    private readonly ToolTip _tips = new() { InitialDelay = 600, ReshowDelay = 200, AutoPopDelay = 15000 };

    private static readonly int WM_SHOW_IPWATCH = NativeMethods.RegisterWindowMessage("IPWatch.ShowWindow");

    public MainForm(bool startHidden)
    {
        Text = "IP Watch";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(384, 462);
        BackColor = Bg;
        DoubleBuffered = true;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        _appIcon = TrayIconFactory.Create("IP", Accent, Color.White);
        _emptyIcon = TrayIconFactory.Create("··", Color.FromArgb(80, 86, 98), Color.White);
        Icon = _appIcon;

        // ── заголовок ───────────────────────────────────────────────
        _header = new Panel { Bounds = new Rectangle(0, 0, 384, 46), BackColor = Bg };
        _header.MouseDown += DragMove;
        _header.MouseMove += DragMoveMove;

        var title = new Label
        {
            Text = "IP Watch",
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 11F),
            AutoSize = true,
            Location = new Point(16, 15),
            BackColor = Color.Transparent
        };
        title.MouseDown += DragMove;
        title.MouseMove += DragMoveMove;
        _header.Controls.Add(title);

        _statusLabel = new Label
        {
            Text = "обновление…",
            ForeColor = TextDim,
            Font = new Font("Segoe UI", 8F),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(180, 14, 158, 20),
            BackColor = Color.Transparent
        };
        _statusLabel.MouseDown += DragMove;
        _statusLabel.MouseMove += DragMoveMove;
        _header.Controls.Add(_statusLabel);

        var closeButton = MakeIconButton("✕", new Point(348, 11), 24);
        closeButton.Click += (_, _) => HideToTray();
        _header.Controls.Add(closeButton);
        Controls.Add(_header);

        // ── карточка с IP ───────────────────────────────────────────
        var ipCard = new Panel { Bounds = new Rectangle(16, 46, 352, 84), BackColor = Card };
        ipCard.Paint += (_, e) => RoundBorder(e.Graphics, ipCard.ClientRectangle, 12, Line);
        ipCard.MouseDown += DragMove;
        ipCard.MouseMove += DragMoveMove;

        ipCard.Controls.Add(new Label
        {
            Text = "ВНЕШНИЙ IP",
            ForeColor = TextDim,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 12),
            BackColor = Color.Transparent
        });

        _ipLabel = new Label
        {
            Text = "— . — . — . —",
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 19F),
            AutoSize = true,
            Location = new Point(12, 30),
            BackColor = Color.Transparent
        };
        _ipLabel.MouseDown += DragMove;
        _ipLabel.MouseMove += DragMoveMove;
        ipCard.Controls.Add(_ipLabel);

        _copyButton = MakeIconButton("⧉", new Point(312, 26), 28);
        _copyButton.Font = new Font("Segoe UI Symbol", 11F);
        _copyButton.Click += (_, _) => CopyIp();
        ipCard.Controls.Add(_copyButton);
        Controls.Add(ipCard);

        // ── страна ──────────────────────────────────────────────────
        _flag = new PictureBox
        {
            Bounds = new Rectangle(16, 140, 34, 24),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Bg
        };
        Controls.Add(_flag);

        _countryLabel = new Label
        {
            Text = "Определение…",
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 11.5F),
            AutoSize = true,
            Location = new Point(60, 140),
            BackColor = Bg
        };
        Controls.Add(_countryLabel);

        _placeLabel = new Label
        {
            Text = "",
            ForeColor = TextDim,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Location = new Point(60, 161),
            BackColor = Bg
        };
        Controls.Add(_placeLabel);

        var separator = new Panel { Bounds = new Rectangle(16, 190, 352, 1), BackColor = Line };
        Controls.Add(separator);

        // ── строки данных ───────────────────────────────────────────
        int y = 200;
        _timeLabel = AddRow("Время по IP", ref y, 11.5F, FontStyle.Bold);
        _tzLabel = AddRow("Часовой пояс", ref y);
        _pingLabel = AddRow("Пинг до интернета", ref y);
        _gatewayLabel = AddRow("Шлюз (VPN/сеть)", ref y);
        _localIpLabel = AddRow("Локальный IP", ref y);
        _ispLabel = AddRow("Провайдер", ref y);

        // ── подвал ──────────────────────────────────────────────────
        var footerLine = new Panel { Bounds = new Rectangle(16, 386, 352, 1), BackColor = Line };
        Controls.Add(footerLine);

        _autostartCheck = new DarkCheckBox
        {
            Text = "Запускать с Windows",
            Bounds = new Rectangle(15, 394, 220, 26),
            BackColor = Bg
        };
        _autostartCheck.Checked = Autostart.IsEnabled();
        _autostartCheck.CheckedChanged += (_, _) => ToggleAutostart();
        Controls.Add(_autostartCheck);

        _refreshButton = new Button
        {
            Text = "Обновить",
            Bounds = new Rectangle(268, 394, 100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Card,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 8.5F),
            Cursor = Cursors.Hand
        };
        _refreshButton.FlatAppearance.BorderColor = Line;
        _refreshButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 57);
        _refreshButton.Click += async (_, _) => await RefreshAsync(manual: true);
        Controls.Add(_refreshButton);

        _updatedLabel = new Label
        {
            Text = "ожидание данных",
            ForeColor = Color.FromArgb(105, 111, 124),
            Font = new Font("Segoe UI", 7.5F),
            AutoSize = true,
            Location = new Point(16, 430),
            BackColor = Bg
        };
        Controls.Add(_updatedLabel);

        // ── трей ────────────────────────────────────────────────────
        _tray = new NotifyIcon
        {
            Icon = _emptyIcon,
            Text = "IP Watch",
            Visible = true
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleWindow();
        };

        var menu = new ContextMenuStrip { ShowImageMargin = false };
        var openItem = new ToolStripMenuItem("Показать окно");
        openItem.Click += (_, _) => ShowWindow();
        var refreshItem = new ToolStripMenuItem("Обновить сейчас");
        refreshItem.Click += async (_, _) => await RefreshAsync(manual: true);
        var copyItem = new ToolStripMenuItem("Копировать IP");
        copyItem.Click += (_, _) => CopyIp();
        var autostartItem = new ToolStripMenuItem("Запускать с Windows")
        {
            CheckOnClick = true,
            Checked = Autostart.IsEnabled()
        };
        autostartItem.Click += (_, _) =>
        {
            Autostart.Set(autostartItem.Checked);
            _autostartCheck.Checked = autostartItem.Checked;
            UpdateTrayText();
        };
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.AddRange(new ToolStripItem[]
        {
            openItem, refreshItem, copyItem, new ToolStripSeparator(), autostartItem,
            new ToolStripSeparator(), exitItem
        });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();

        // ── таймеры ─────────────────────────────────────────────────
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync(manual: false);
        _refreshTimer.Start();

        Load += async (_, _) =>
        {
            PlaceNearTray();
            UpdateClock();
            await RefreshAsync(manual: false);
            if (!startHidden) ShowWindow();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkModeBefore20H1, ref on, sizeof(int));
        }
        catch (Exception)
        {
            // тёмная рамка не критична
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Line, 1f);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Rounded(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), 14f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Rounded(new RectangleF(0, 0, Width, Height), 14f);
        Region = new Region(path);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) HideToTray();
        base.OnKeyDown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    private Label AddRow(string caption, ref int y, float valueSize = 9.5F, FontStyle style = FontStyle.Regular)
    {
        var captionLabel = new Label
        {
            Text = caption,
            ForeColor = TextDim,
            Font = new Font("Segoe UI", 9F),
            AutoSize = false,
            Bounds = new Rectangle(16, y, 150, 22),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Bg
        };

        var valueLabel = new Label
        {
            Text = "—",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", valueSize, style),
            AutoSize = false,
            Bounds = new Rectangle(150, y, 218, 22),
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            BackColor = Bg
        };

        valueLabel.MouseEnter += (_, _) =>
        {
            var text = valueLabel.Tag as string ?? valueLabel.Text;
            if (!string.IsNullOrWhiteSpace(text) && text != "—")
                _tips.SetToolTip(valueLabel, text);
        };

        Controls.Add(captionLabel);
        Controls.Add(valueLabel);
        y += 30;
        return valueLabel;
    }

    private Button MakeIconButton(string glyph, Point location, int size)
    {
        var button = new Button
        {
            Text = glyph,
            Bounds = new Rectangle(location, new Size(size, size)),
            FlatStyle = FlatStyle.Flat,
            BackColor = Bg,
            ForeColor = TextDim,
            Font = new Font("Segoe UI", 9F),
            TabStop = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 55, 65);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 70, 82);
        return button;
    }

    private void PlaceNearTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
    }

    private void DragMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
    }

    private void DragMoveMove(object? sender, MouseEventArgs e)
    {
        // перетаскивание реализовано в DragMove
    }

    private void ToggleWindow()
    {
        if (Visible && WindowState != FormWindowState.Minimized) HideToTray();
        else ShowWindow();
    }

    private void ShowWindow()
    {
        if (!Visible)
        {
            PlaceNearTray();
            Show();
        }
        else
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
        BringToFront();
    }

    private void HideToTray() => Hide();

    private void UpdateTrayText()
    {
        string text;
        if (_ip == null)
        {
            text = "IP Watch · получение данных";
        }
        else
        {
            var time = _ip.LocalTimeNow().ToString("HH:mm");
            var ping = _ping is null ? "—" : $"{_ping} мс";
            text = $"{_ip.Ip} · {_ip.CountryCode} · {time} · {ping}";
        }

        if (text.Length > 62) text = text[..62];
        _tray.Text = text;
    }

    private void UpdateClock()
    {
        if (_ip == null)
        {
            _timeLabel.Text = "—";
            return;
        }

        var now = _ip.LocalTimeNow();
        _timeLabel.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var offset = _ip.OffsetNow();
        _tzLabel.Text = $"{IpTimeZone.FormatOffset(offset)} · {IpTimeZone.DescribeDifference(offset)}";

        if (now.Minute != _lastTrayMinute)
        {
            _lastTrayMinute = now.Minute;
            UpdateTrayText();
        }
    }

    private async Task RefreshAsync(bool manual)
    {
        if (_busy) return;
        _busy = true;

        if (manual || _ip == null) _statusLabel.Text = "обновление…";
        _refreshButton.Enabled = false;
        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = _cts.Token;

        try
        {
            var netTask = Task.Run(() => NetworkService.GetDefaultRoute(), ct);
            var ipTask = IpLookupService.GetAsync(ct);
            var pingTask = PingService.InternetAsync(ct);

            _net = await netTask.ConfigureAwait(true);
            _ip = await ipTask.ConfigureAwait(true);
            _ping = await pingTask.ConfigureAwait(true);

            _gatewayPing = _net.Gateway != null
                ? await PingService.IcmpAsync(_net.Gateway, ct).ConfigureAwait(true)
                : null;

            ApplyIp(_ip);
            ApplyPing(_ping);
            ApplyNetwork();

            var stamp = DateTime.Now.ToString("HH:mm:ss");
            _updatedLabel.Text = $"обновлено {stamp} · автообновление раз в 60 с";
            _statusLabel.Text = "актуально";
            UpdateTrayPresentation();
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "таймаут";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "ошибка сети";
            _updatedLabel.Text = ex.Message.Length > 90 ? ex.Message[..90] + "…" : ex.Message;
        }
        finally
        {
            _busy = false;
            _refreshButton.Enabled = true;
            UpdateClock();
        }
    }

    private void ApplyIp(IpInfo info)
    {
        _ipLabel.Text = info.Ip;
        _countryLabel.Text = IpTimeZone.RussianCountryName(info.CountryCode, info.CountryName);
        _placeLabel.Text = string.IsNullOrWhiteSpace(info.CountryCode)
            ? info.Place
            : $"{info.CountryCode} · {info.Place}";
        _ispLabel.Tag = info.Isp;
        SetFitText(_ispLabel, string.IsNullOrWhiteSpace(info.Isp) ? "—" : info.Isp);
        UpdateClock();

        if (!string.Equals(_lastCountryCode, info.CountryCode, StringComparison.OrdinalIgnoreCase))
        {
            _lastCountryCode = info.CountryCode;
            _flag.Image?.Dispose();
            _flag.Image = null;

            var code = info.CountryCode;
            _ = Task.Run(async () =>
            {
                var image = await FlagCache.GetAsync(code, CancellationToken.None).ConfigureAwait(false);
                if (image == null || IsDisposed) return;
                try
                {
                    BeginInvoke(() =>
                    {
                        if (IsDisposed || !string.Equals(code, _lastCountryCode, StringComparison.OrdinalIgnoreCase)) return;
                        _flag.Image?.Dispose();
                        _flag.Image = image;
                    });
                }
                catch (Exception)
                {
                    image.Dispose();
                }
            });
        }
    }

    private void ApplyPing(int? ping)
    {
        if (ping is null)
        {
            _pingLabel.Text = "нет ответа";
            _pingLabel.ForeColor = Bad;
            return;
        }

        _pingLabel.Text = $"{ping} мс";
        _pingLabel.ForeColor = ping < 80 ? Good : ping < 200 ? Warn : Bad;
    }

    private void ApplyNetwork()
    {
        _gatewayLabel.Text = _net.Gateway == null
            ? "не определён"
            : _gatewayPing != null ? $"{_net.Gateway} · {_gatewayPing} мс" : _net.Gateway;

        _localIpLabel.Text = string.IsNullOrWhiteSpace(_net.LocalIp) ? "—" : _net.LocalIp;
        _localIpLabel.Tag = string.IsNullOrWhiteSpace(_net.AdapterName) || _net.AdapterName == "—"
            ? _net.LocalIp
            : $"Адаптер: {_net.AdapterName}";
        _localIpLabel.ForeColor = TextMain;
    }

    /// <summary>Вписывает текст в метку, уменьшая шрифт, чтобы ничего не обрезалось.</summary>
    private static void SetFitText(Label label, string text)
    {
        if (label.Font is null) return;

        var baseFont = label.Tag as Font ?? (Font)label.Font.Clone();
        label.Tag = baseFont;

        var available = label.ClientSize.Width;
        var size = baseFont.Size;

        while (size > 6.5f)
        {
            using var probe = new Font(baseFont.FontFamily, size, baseFont.Style, GraphicsUnit.Point);
            var measured = TextRenderer.MeasureText(text, probe).Width;
            if (measured <= available)
            {
                label.Font = probe;
                label.Text = text;
                return;
            }

            size -= 0.5f;
        }

        label.Font = new Font(baseFont.FontFamily, 6.5f, baseFont.Style, GraphicsUnit.Point);
        label.Text = text;
    }

    private void UpdateTrayPresentation()
    {
        var code = string.IsNullOrWhiteSpace(_ip?.CountryCode) ? "··" : _ip!.CountryCode.ToUpperInvariant();
        var bucket = _ping switch
        {
            null => 0,
            < 80 => 1,
            < 200 => 2,
            _ => 3
        };

        var backColor = bucket switch
        {
            0 => Color.FromArgb(90, 96, 108),
            1 => Color.FromArgb(38, 132, 84),
            2 => Color.FromArgb(150, 116, 32),
            _ => Color.FromArgb(150, 56, 56)
        };

        var key = $"{code}:{bucket}";
        if (key != _trayIconKey || _trayDynamic == null)
        {
            var icon = TrayIconFactory.Create(code, backColor, Color.White);
            var previous = _trayDynamic;
            _trayDynamic = icon;
            _tray.Icon = icon;
            _trayIconKey = key;
            previous?.Dispose();
        }

        UpdateTrayText();
    }

    private void CopyIp()
    {
        if (_ip == null) return;
        try
        {
            Clipboard.SetText(_ip.Ip);
            _statusLabel.Text = "IP скопирован";
        }
        catch (Exception)
        {
            _statusLabel.Text = "буфер недоступен";
        }
    }

    private void ToggleAutostart()
    {
        Autostart.Set(_autostartCheck.Checked);
        UpdateTrayText();
    }

    private void ExitApp()
    {
        _exiting = true;
        _clockTimer.Stop();
        _refreshTimer.Stop();
        _cts?.Cancel();
        _tray.Visible = false;
        _tray.Dispose();
        _trayDynamic?.Dispose();
        _emptyIcon.Dispose();
        _tips.Dispose();
        Close();
        Application.Exit();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SHOW_IPWATCH)
        {
            ShowWindow();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.dispose_quiet();
        }

        base.Dispose(disposing);
    }

    private static void RoundBorder(Graphics g, Rectangle bounds, float radius, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(new RectangleF(0.5f, 0.5f, bounds.Width - 1, bounds.Height - 1), radius);
        using var pen = new Pen(color, 1f);
        g.DrawPath(pen, path);
    }

    internal static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

internal static class NotifyIconExtensions
{
    public static void dispose_quiet(this NotifyIcon icon)
    {
        try { icon.Visible = false; } catch (Exception) { }
        icon.Dispose();
    }
}

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IPWatch";

    public static void EnsureFirstRun()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key?.GetValue(ValueName) != null) return;
        }
        catch (Exception)
        {
            return;
        }

        Set(true);
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ??
                            Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;

            if (enabled)
            {
                key.SetValue(ValueName, $"{Quote(LaunchExe())} --tray", RegistryValueKind.String);
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception)
        {
            // нет прав — просто не включаем автозапуск
        }
    }

    private static string LaunchExe()
    {
        var path = Environment.ProcessPath ?? Application.ExecutablePath;
        var name = Path.GetFileName(path);

        if (name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return $"{Quote(path)} \"{Application.ExecutablePath}\"";

        return path;
    }

    private static string Quote(string value) => $"\"{value}\"";
}
