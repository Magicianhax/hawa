using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hawa.Core.Bluetooth;
using Hawa.Core.Model;
using Hawa.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Hawa.App.Settings;

public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppServices _services;
    private readonly ILogger<SettingsViewModel> _log;
    private readonly Action _onStateChanged;
    private readonly Action<HawaSettings> _onSettingsChanged;
    private readonly Action _onBluetoothAvailabilityChanged;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        _log = services.Loggers.CreateLogger<SettingsViewModel>();
        _onStateChanged = () => System.Windows.Application.Current.Dispatcher.BeginInvoke(RaiseDeviceInfo);
        _onSettingsChanged = _ => System.Windows.Application.Current.Dispatcher.BeginInvoke(RaiseAll);
        _onBluetoothAvailabilityChanged = () => System.Windows.Application.Current.Dispatcher.BeginInvoke(
            () => OnPropertyChanged(nameof(BluetoothSupported)));
        services.Store.StateChanged += _onStateChanged;
        services.SettingsChanged += _onSettingsChanged;
        services.BluetoothAvailabilityChanged += _onBluetoothAvailabilityChanged;
    }

    public void Dispose()
    {
        _services.Store.StateChanged -= _onStateChanged;
        _services.SettingsChanged -= _onSettingsChanged;
        _services.BluetoothAvailabilityChanged -= _onBluetoothAvailabilityChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private HawaSettings S => _services.Settings;
    private void Apply(HawaSettings next, [CallerMemberName] string? name = null)
    {
        _services.UpdateSettings(next);
        OnPropertyChanged(name);
    }

    public bool AutoPauseEnabled { get => S.AutoPauseEnabled; set => Apply(S with { AutoPauseEnabled = value }); }
    public bool PauseOnlyWhenBothRemoved { get => S.PauseOnlyWhenBothRemoved; set => Apply(S with { PauseOnlyWhenBothRemoved = value }); }
    public bool PopupOnConnect { get => S.PopupOnConnect; set => Apply(S with { PopupOnConnect = value }); }
    public bool PopupOnLidOpen { get => S.PopupOnLidOpen; set => Apply(S with { PopupOnLidOpen = value }); }
    public int PopupDurationSeconds { get => S.PopupDurationSeconds; set => Apply(S with { PopupDurationSeconds = Math.Clamp(value, 1, 60) }); }
    public int LowBatteryThreshold { get => S.LowBatteryThreshold; set => Apply(S with { LowBatteryThreshold = Math.Clamp(value, 5, 50) }); }

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set { StartupRegistration.Set(value); Apply(S with { StartWithWindows = value }); }
    }

    public IReadOnlyList<PairedDeviceInfo> PairedDevices => _services.PairedAirPods;
    public PairedDeviceInfo? SelectedDevice
    {
        get => PairedDevices.FirstOrDefault(p => p.Id == S.SelectedDeviceId);
        set
        {
            if (value is null || value.Id == S.SelectedDeviceId) return;
            Apply(S with { SelectedDeviceId = value.Id });
            BindSelectedAsync(value.Id);
        }
    }

    private async void BindSelectedAsync(string id)
    {
        try
        {
            var bound = await _services.Paired.BindAsync(id);
            if (!bound) _log.LogWarning("Failed to bind selected device {DeviceId}", id);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to bind selected device {DeviceId}", id);
        }
        finally
        {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(RaiseDeviceInfo);
        }
    }

    public bool BluetoothSupported => _services.BluetoothSupported;
    public bool MediaUnavailable => !_services.MediaAvailable;
    public bool HasPairedDevices => PairedDevices.Count > 0;
    public string DeviceName => _services.Paired.DeviceName ?? "No AirPods paired";
    public string ModelName => _services.Store.Snapshot?.Model.DisplayName() ?? "Unknown";
    public string ConnectionState => _services.Paired.IsConnected ? "Connected" : "Not connected";
    public string LastSeen => _services.Store.Snapshot is { } s ? (_services.Store.IsStale ? "Out of range" : $"Seen {s.ReceivedAt.ToLocalTime():HH:mm:ss}") : "Never";
    public string NoiseControlNote => _services.NoiseControl.IsAvailable ? string.Empty : "Needs the Hawa AAP driver, coming in v2";
    public bool NoiseControlAvailable => _services.NoiseControl.IsAvailable;
    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task RefreshDevicesAsync()
    {
        await _services.RefreshPairedAsync();
        RaiseAll();
    }

    private void RaiseDeviceInfo()
    {
        foreach (var n in new[] { nameof(ModelName), nameof(ConnectionState), nameof(LastSeen), nameof(DeviceName) }) OnPropertyChanged(n);
    }

    private void RaiseAll()
    {
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
