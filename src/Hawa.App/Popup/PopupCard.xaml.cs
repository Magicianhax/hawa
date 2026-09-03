using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Hawa.Core.Model;

namespace Hawa.App.Popup;

public partial class PopupCard : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    private readonly Microsoft.Extensions.Logging.ILogger _log;

    private readonly AppServices _services;
    private readonly DispatcherTimer _hideTimer = new();
    private bool _visible;
    private bool _pinned;

    public PopupCard(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _log = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<PopupCard>(services.Loggers);
        _hideTimer.Tick += (_, _) => HideCard();
        services.Store.StateChanged += () => Dispatcher.BeginInvoke(Refresh);
        MouseEnter += (_, _) =>
        {
            _hideTimer.Stop();
            if (!_visible && IsVisible)
            {
                _visible = true;
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
            }
        };
        MouseLeave += (_, _) => { if (_visible) _hideTimer.Start(); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GwlExStyle, GetWindowLong(h, GwlExStyle) | WsExNoActivate | WsExToolWindow);
        };
    }

    public void ShowFor(TimeSpan duration)
    {
        Refresh();
        Position();
        _hideTimer.Interval = duration;
        _hideTimer.Stop();
        _hideTimer.Start();
        if (_visible) return;
        _visible = true;
        LogForeground("before Show");
        Show();
        LogForeground("after Show");
        var probe = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        probe.Tick += (_, _) => { probe.Stop(); LogForeground("700 ms after Show"); };
        probe.Start();
        var slide = new DoubleAnimation(Top + 16, Top, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(TopProperty, slide);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    /// <summary>Hover show. A no-op while the card is already up, so moving the mouse over the
    /// tray icon never re-runs the slide animation and never steals the click's toggle state.</summary>
    public void ShowOnHover()
    {
        if (_visible) return;
        _pinned = false;
        ShowFor(TimeSpan.FromSeconds(3));
    }

    /// <summary>Left-click toggle. Only a click-pinned card is hidden by a click; clicking a card
    /// that hover put on screen pins it for the configured duration instead of dismissing it.</summary>
    public void Toggle()
    {
        if (_visible && _pinned)
        {
            HideCard();
        }
        else
        {
            _pinned = true;
            ShowFor(TimeSpan.FromSeconds(_services.Settings.PopupDurationSeconds));
        }
    }

    public void HideCard()
    {
        _pinned = false;
        if (!_visible) return;
        _visible = false;
        _hideTimer.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => { if (!_visible) Hide(); };
        BeginAnimation(OpacityProperty, fade);
    }

    private void LogForeground(string when)
    {
        var h = GetForegroundWindow();
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(h, sb, sb.Capacity);
        var ours = new WindowInteropHelper(this).Handle;
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(_log, "focus {When}: foreground=0x{Handle:X} '{Title}' (popup=0x{Ours:X})", when, h.ToInt64(), sb.ToString(), ours.ToInt64());
    }

    private void Position()
    {
        var wa = SystemParameters.WorkArea;
        BeginAnimation(TopProperty, null);
        Left = wa.Right - Width - 12;
        Top = wa.Bottom - Height - 12;
    }

    private void Refresh()
    {
        var snap = _services.Store.Snapshot;
        int low = _services.Settings.LowBatteryThreshold;
        TitleText.Text = _services.Paired.DeviceName ?? snap?.Model.DisplayName() ?? "AirPods";
        LeftRing.Update(snap?.LeftBattery, snap?.LeftCharging ?? false, low);
        RightRing.Update(snap?.RightBattery, snap?.RightCharging ?? false, low);
        CaseRing.Update(snap?.CaseBattery, snap?.CaseCharging ?? false, low);
    }
}
