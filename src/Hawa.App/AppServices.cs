using Hawa.Aap;
using Hawa.Core.Bluetooth;
using Hawa.Core.Media;
using Hawa.Core.Settings;
using Hawa.Core.State;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Windows.Foundation;

namespace Hawa.App;

/// <summary>Composition root. Owns every long-lived service and wires them together.</summary>
public sealed class AppServices : IDisposable
{
    private readonly ILogger<AppServices> _log;
    private readonly IMediaSession _mediaSession;
    private TypedEventHandler<Radio, object>? _onRadioStateChanged;
    private Radio? _radio;
    private bool _leCapable;

    public AppServices(ILoggerFactory loggers)
    {
        Loggers = loggers;
        _log = loggers.CreateLogger<AppServices>();
        Time = TimeProvider.System;
        SettingsStore = new SettingsStore(SettingsStore.DefaultPath);
        Settings = SettingsStore.Load();

        Store = new DeviceStateStore(Time) { LowBatteryThreshold = Settings.LowBatteryThreshold };
        Matcher = new DeviceMatcher(Time);
        Watcher = new ProximityWatcher(loggers.CreateLogger<ProximityWatcher>(), Time);
        Paired = new PairedDeviceService();
        AudioProbe = new NAudioProbe();
        _mediaSession = new SmtcMediaSession();
        Media = new MediaController(_mediaSession, AudioProbe, () => Paired.DeviceName);
        AutoPause = new AutoPauseCoordinator(Store, Media, Time)
        {
            Enabled = Settings.AutoPauseEnabled,
            PauseOnlyWhenBothRemoved = Settings.PauseOnlyWhenBothRemoved,
        };
        NoiseControl = new NoiseControlService(new NullTransport());

        Watcher.SnapshotReceived += snap => { if (Matcher.Accept(snap)) Store.Apply(snap); };
        Watcher.Faulted += msg => WatcherFaulted?.Invoke(msg);
        Paired.ConnectionChanged += connected => Store.SetConnected(connected);
        AutoPause.Failed += ex => _log.LogError(ex, "Auto-pause failed");
    }

    public ILoggerFactory Loggers { get; }
    public TimeProvider Time { get; }
    public SettingsStore SettingsStore { get; }
    public HawaSettings Settings { get; private set; }
    public DeviceStateStore Store { get; }
    public DeviceMatcher Matcher { get; }
    public ProximityWatcher Watcher { get; }
    public PairedDeviceService Paired { get; }
    public NAudioProbe AudioProbe { get; }
    public MediaController Media { get; }
    public AutoPauseCoordinator AutoPause { get; }
    public NoiseControlService NoiseControl { get; }
    public bool BluetoothSupported { get; private set; }

    /// <summary>False when the system media transport controls could not be reached at startup.
    /// Auto-pause is forced off in that case and the Behaviour page says so.</summary>
    public bool MediaAvailable { get; private set; }
    public IReadOnlyList<PairedDeviceInfo> PairedAirPods { get; private set; } = Array.Empty<PairedDeviceInfo>();

    public event Action<HawaSettings>? SettingsChanged;
    public event Action<string>? WatcherFaulted;

    /// <summary>Raised when the Bluetooth radio is switched on or off, after
    /// <see cref="BluetoothSupported"/> has been updated and the watcher started or stopped.</summary>
    public event Action? BluetoothAvailabilityChanged;

    public async Task InitializeAsync()
    {
        // Probed before the Bluetooth check so the media warning is correct even on a machine
        // with no adapter, where the rest of initialisation returns early.
        MediaAvailable = await _mediaSession.IsAvailableAsync();
        _log.LogInformation("Media session manager available: {Available}", MediaAvailable);
        if (!MediaAvailable) AutoPause.Enabled = false;

        BluetoothSupported = await ProximityWatcher.IsLowEnergySupportedAsync();
        _log.LogInformation("Bluetooth LE supported: {Supported}", BluetoothSupported);

        await AttachRadioAsync();
        if (!BluetoothSupported) return;

        await RefreshPairedAsync();
        Watcher.Start();
    }

    /// <summary>Follows the Bluetooth radio so turning it off in Settings shows the red slash
    /// immediately, and turning it back on resumes scanning without a restart.</summary>
    private async Task AttachRadioAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is not { IsLowEnergySupported: true }) return;
            _leCapable = true;

            _radio = await adapter.GetRadioAsync();
            if (_radio is null) return;
            _onRadioStateChanged = (radio, _) => OnRadioStateChanged(radio.State);
            _radio.StateChanged += _onRadioStateChanged;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not subscribe to Bluetooth radio state changes");
        }
    }

    private void OnRadioStateChanged(RadioState state)
    {
        bool available = _leCapable && state == RadioState.On;
        if (available == BluetoothSupported) return;

        BluetoothSupported = available;
        _log.LogInformation("Bluetooth radio state is now {State}; LE available: {Available}", state, available);
        try
        {
            if (available)
            {
                Watcher.Start();
                // A PC that booted with the radio off enumerated nothing at startup, so without
                // this the tray stays on "no AirPods paired" for the rest of the session.
                _ = RefreshPairedAsync();
            }
            else
            {
                Watcher.Stop();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to apply Bluetooth radio state change");
        }
        BluetoothAvailabilityChanged?.Invoke();
    }

    public async Task RefreshPairedAsync()
    {
        try
        {
            PairedAirPods = await PairedDeviceService.FindAirPodsAsync();
            _log.LogInformation("Paired AirPods: {Count}", PairedAirPods.Count);

            var chosen = PairedAirPods.FirstOrDefault(p => p.Id == Settings.SelectedDeviceId) ?? PairedAirPods.FirstOrDefault();
            if (chosen is not null)
            {
                await Paired.BindAsync(chosen.Id);
                if (chosen.Id != Settings.SelectedDeviceId) UpdateSettings(Settings with { SelectedDeviceId = chosen.Id });
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to enumerate paired devices");
        }
    }

    public void UpdateSettings(HawaSettings next)
    {
        Settings = next;
        SettingsStore.Save(next);
        Store.LowBatteryThreshold = next.LowBatteryThreshold;
        // Without media transport controls there is nothing to pause, so the setting stays off
        // however the user leaves the toggle.
        AutoPause.Enabled = next.AutoPauseEnabled && MediaAvailable;
        AutoPause.PauseOnlyWhenBothRemoved = next.PauseOnlyWhenBothRemoved;
        SettingsChanged?.Invoke(next);
    }

    public void Dispose()
    {
        if (_radio is not null && _onRadioStateChanged is not null) _radio.StateChanged -= _onRadioStateChanged;
        AutoPause.Dispose();
        Watcher.Dispose();
        Paired.Dispose();
        AudioProbe.Dispose();
        Store.Dispose();
        Loggers.Dispose();
    }
}
