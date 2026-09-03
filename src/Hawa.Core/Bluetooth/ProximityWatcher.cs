using Hawa.Core.Model;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Radios;
using Windows.Storage.Streams;

namespace Hawa.Core.Bluetooth;

/// <summary>Listens for Apple (0x004C) BLE manufacturer adverts and emits parsed snapshots.
/// Restarts with exponential backoff when the watcher aborts.</summary>
public sealed class ProximityWatcher : IDisposable
{
    public const ushort AppleCompanyId = 0x004C;
    private const int MaxFailuresBeforeFault = 5;

    private readonly ILogger<ProximityWatcher> _logger;
    private readonly TimeProvider _time;
    private readonly ITimer _restartTimer;
    private readonly object _gate = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private int _failures;
    private bool _wantRunning;

    public ProximityWatcher(ILogger<ProximityWatcher> logger, TimeProvider time)
    {
        _logger = logger;
        _time = time;
        _restartTimer = time.CreateTimer(_ => StartCore(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public bool IsRunning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;
    public event Action<PodsSnapshot>? SnapshotReceived;
    public event Action<string>? Faulted;

    /// <summary>True only when an LE-capable adapter exists <em>and</em> its radio is switched on.
    /// The adapter still reports <c>IsLowEnergySupported</c> when the radio is off in Settings, so
    /// without the radio check the watcher starts, aborts, and backs off instead of reporting that
    /// Bluetooth is unavailable.</summary>
    public static async Task<bool> IsLowEnergySupportedAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is not { IsLowEnergySupported: true }) return false;
            var radio = await adapter.GetRadioAsync();
            return radio?.State == RadioState.On;
        }
        catch { return false; }
    }

    public void Start()
    {
        lock (_gate)
        {
            _wantRunning = true;
            _failures = 0;
        }
        StartCore();
    }

    public void Stop()
    {
        lock (_gate)
        {
            _wantRunning = false;
            _restartTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            TearDown();
        }
    }

    private void StartCore()
    {
        bool shouldFault = false;
        lock (_gate)
        {
            if (!_wantRunning) return;
            TearDown();
            try
            {
                var w = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
                w.AdvertisementFilter.Advertisement.ManufacturerData.Add(
                    new BluetoothLEManufacturerData(AppleCompanyId, new DataWriter().DetachBuffer()));
                w.Received += OnReceived;
                w.Stopped += OnStopped;
                w.Start();
                _watcher = w;
                _logger.LogInformation("BLE watcher started");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BLE watcher failed to start");
                shouldFault = ScheduleRestart();
            }
        }
        if (shouldFault) Faulted?.Invoke("Bluetooth scanning keeps failing. Check that Bluetooth is turned on.");
    }

    private void TearDown()
    {
        lock (_gate)
        {
            if (_watcher is null) return;
            _watcher.Received -= OnReceived;
            _watcher.Stopped -= OnStopped;
            try { _watcher.Stop(); } catch { }
            _watcher = null;
        }
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var md in args.Advertisement.ManufacturerData)
        {
            if (md.CompanyId != AppleCompanyId) continue;
            var data = new byte[md.Data.Length];
            using var reader = DataReader.FromBuffer(md.Data);
            reader.ReadBytes(data);
            if (data.Length == 0 || data[0] != ProximityParser.MessageType) continue;

            _logger.LogTrace("advert {Address:X12} rssi {Rssi} data {Hex}", args.BluetoothAddress, args.RawSignalStrengthInDBm, Convert.ToHexString(data));

            var snap = ProximityParser.Parse(data, args.BluetoothAddress, args.RawSignalStrengthInDBm, _time.GetUtcNow());
            if (snap is not null) SnapshotReceived?.Invoke(snap);
        }
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        bool shouldFault;
        lock (_gate)
        {
            if (!_wantRunning) return;
            _logger.LogWarning("BLE watcher stopped: {Error}", args.Error);
            shouldFault = ScheduleRestart();
        }
        if (shouldFault) Faulted?.Invoke("Bluetooth scanning keeps failing. Check that Bluetooth is turned on.");
    }

    /// <summary>Must be called while holding <see cref="_gate"/>. Returns true when the fault
    /// threshold was just reached; the caller raises <see cref="Faulted"/> after leaving the lock.</summary>
    private bool ScheduleRestart()
    {
        _failures++;
        bool shouldFault = _failures == MaxFailuresBeforeFault;
        var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(_failures - 1, 5))));
        _restartTimer.Change(delay, Timeout.InfiniteTimeSpan);
        return shouldFault;
    }

    public void Dispose()
    {
        Stop();
        _restartTimer.Dispose();
    }
}
