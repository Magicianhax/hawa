using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Hawa.Core.Model;

namespace Hawa.App.Tray;

public sealed class TrayController : IDisposable
{
    private readonly AppServices _services;
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _autoPauseItem;
    private readonly MenuItem _startupItem;
    private readonly Action _onStateChanged;
    private readonly Action<Core.Settings.HawaSettings> _onSettingsChanged;
    private readonly Action<string> _onWatcherFaulted;
    private System.Drawing.Icon? _current;

    public TrayController(AppServices services, Action showSettings, Action togglePopup, Action showPopupHover)
    {
        _services = services;

        _autoPauseItem = new MenuItem { Header = "Auto-pause on removal", IsCheckable = true, IsChecked = services.Settings.AutoPauseEnabled };
        _autoPauseItem.Click += (_, _) => services.UpdateSettings(services.Settings with { AutoPauseEnabled = _autoPauseItem.IsChecked });

        _startupItem = new MenuItem { Header = "Start with Windows", IsCheckable = true, IsChecked = services.Settings.StartWithWindows };
        _startupItem.Click += (_, _) =>
        {
            StartupRegistration.Set(_startupItem.IsChecked);
            services.UpdateSettings(services.Settings with { StartWithWindows = _startupItem.IsChecked });
        };

        var noise = new MenuItem { Header = "Noise control", IsEnabled = false, ToolTip = "Needs the Hawa AAP driver, coming in v2" };
        foreach (var name in new[] { "Off", "Noise Cancellation", "Transparency", "Adaptive" })
            noise.Items.Add(new MenuItem { Header = name, IsEnabled = false });

        var settingsItem = new MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => showSettings();
        var quitItem = new MenuItem { Header = "Quit Hawa" };
        quitItem.Click += (_, _) => Application.Current.Shutdown();

        var menu = new ContextMenu();
        menu.Items.Add(settingsItem);
        menu.Items.Add(noise);
        menu.Items.Add(_autoPauseItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);

        _icon = new TaskbarIcon { ContextMenu = menu, ToolTipText = "Hawa" };
        _icon.TrayLeftMouseUp += (_, _) => togglePopup();
        _icon.TrayMouseMove += (_, _) => showPopupHover();
        _icon.ForceCreate();

        _onStateChanged = () => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        _onSettingsChanged = s => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _autoPauseItem.IsChecked = s.AutoPauseEnabled;
            _startupItem.IsChecked = s.StartWithWindows;
            Refresh();
        });
        _onWatcherFaulted = msg => Application.Current?.Dispatcher.BeginInvoke(() =>
            _icon.ShowNotification("Hawa", msg, NotificationIcon.Warning));

        services.Store.StateChanged += _onStateChanged;
        services.SettingsChanged += _onSettingsChanged;
        services.WatcherFaulted += _onWatcherFaulted;

        Refresh();
    }

    private void Refresh()
    {
        var store = _services.Store;
        var snap = store.Snapshot;
        TrayIconState state;
        int? percent = snap?.LowestPodBattery;
        string tip;

        if (!_services.BluetoothSupported)
        {
            state = TrayIconState.NoAdapter;
            tip = "Hawa: no Bluetooth LE adapter";
        }
        else if (_services.PairedAirPods.Count == 0)
        {
            state = TrayIconState.Disconnected;
            tip = "Hawa: no AirPods paired";
        }
        else if (snap is null || store.IsStale)
        {
            state = TrayIconState.Disconnected;
            tip = $"Hawa: {_services.Paired.DeviceName ?? "AirPods"} not in range";
        }
        else
        {
            state = percent is { } p && p <= _services.Settings.LowBatteryThreshold ? TrayIconState.Low : TrayIconState.Normal;
            tip = $"{_services.Paired.DeviceName ?? snap.Model.DisplayName()}\nL {Fmt(snap.LeftBattery)}  R {Fmt(snap.RightBattery)}  Case {Fmt(snap.CaseBattery)}";
        }

        var next = TrayIconRenderer.Render(state, percent);
        _icon.Icon = next;
        _current?.Dispose();
        _current = next;
        _icon.ToolTipText = tip;
    }

    private static string Fmt(int? p) => p is { } v ? $"{v}%" : "–";

    public void Dispose()
    {
        // Unsubscribe first: a BLE advert arriving mid-shutdown would otherwise marshal
        // Refresh onto an already-shut-down dispatcher from a thread-pool thread.
        _services.Store.StateChanged -= _onStateChanged;
        _services.SettingsChanged -= _onSettingsChanged;
        _services.WatcherFaulted -= _onWatcherFaulted;
        _icon.Dispose();
        _current?.Dispose();
    }
}
