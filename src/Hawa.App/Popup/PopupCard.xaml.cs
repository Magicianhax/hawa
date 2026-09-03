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

    private readonly AppServices _services;
    private readonly DispatcherTimer _hideTimer = new();
    private bool _visible;

    public PopupCard(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _hideTimer.Tick += (_, _) => HideCard();
        services.Store.StateChanged += () => Dispatcher.BeginInvoke(Refresh);
        MouseEnter += (_, _) => _hideTimer.Stop();
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
        Show();
        var slide = new DoubleAnimation(Top + 16, Top, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(TopProperty, slide);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    public void Toggle()
    {
        if (_visible) HideCard();
        else ShowFor(TimeSpan.FromSeconds(_services.Settings.PopupDurationSeconds));
    }

    public void HideCard()
    {
        if (!_visible) return;
        _visible = false;
        _hideTimer.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => { if (!_visible) Hide(); };
        BeginAnimation(OpacityProperty, fade);
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
