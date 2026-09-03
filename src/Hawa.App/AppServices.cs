using Hawa.Aap;
using Hawa.Core.Bluetooth;
using Hawa.Core.Media;
using Hawa.Core.Settings;
using Hawa.Core.State;
using Microsoft.Extensions.Logging;

namespace Hawa.App;

/// <summary>Composition root. Owns every long-lived service and wires them together.</summary>
public sealed class AppServices : IDisposable
{
    private readonly ILogger<AppServices> _log;

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
        Media = new MediaController(new SmtcMediaSession(), AudioProbe, () => Paired.DeviceName);
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
    public IReadOnlyList<PairedDeviceInfo> PairedAirPods { get; private set; } = Array.Empty<PairedDeviceInfo>();

    public event Action<HawaSettings>? SettingsChanged;
    public event Action<string>? WatcherFaulted;

    public async Task InitializeAsync()
    {
        BluetoothSupported = await ProximityWatcher.IsLowEnergySupportedAsync();
        _log.LogInformation("Bluetooth LE supported: {Supported}", BluetoothSupported);
        if (!BluetoothSupported) return;

        await RefreshPairedAsync();
        Watcher.Start();
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
        AutoPause.Enabled = next.AutoPauseEnabled;
        AutoPause.PauseOnlyWhenBothRemoved = next.PauseOnlyWhenBothRemoved;
        SettingsChanged?.Invoke(next);
    }

    public void Dispose()
    {
        AutoPause.Dispose();
        Watcher.Dispose();
        Paired.Dispose();
        AudioProbe.Dispose();
        Store.Dispose();
        Loggers.Dispose();
    }
}
