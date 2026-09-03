using NAudio.CoreAudioApi;

namespace Hawa.Core.Media;

/// <summary>Reads the default render endpoint's peak meter and name via NAudio. Not unit-tested.
/// <para>The meter reports the peak of the last device period, a few milliseconds, so a single
/// read taken at the moment a pod is removed lands in the gap between tracks and looks like
/// silence. A background timer samples every 100 ms into a ten-slot ring and
/// <see cref="PeakLevel"/> returns the maximum, i.e. the loudest moment of the last second.</para>
/// <para>The endpoint is cached rather than resolved per read, and re-resolved every five seconds
/// or after any failure, so a default-device change is picked up without a COM call per sample.</para></summary>
public sealed class NAudioProbe : IAudioProbe, IDisposable
{
    private const int Slots = 10;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DeviceLifetime = TimeSpan.FromSeconds(5);

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly object _gate = new();
    private readonly float[] _ring = new float[Slots];
    private readonly Timer _sampler;
    private MMDevice? _device;
    private DateTimeOffset _resolvedAt;
    private int _slot;
    private int _sampling;
    private bool _meterUnavailable = true;
    private bool _disposed;

    public NAudioProbe() => _sampler = new Timer(_ => Sample(), null, TimeSpan.Zero, SampleInterval);

    /// <summary>Loudest master peak seen in the last second, 0..1. Returns 1 while the meter
    /// cannot be read, so an unavailable meter never blocks pausing.</summary>
    public float PeakLevel
    {
        get
        {
            lock (_gate)
            {
                if (_meterUnavailable) return 1f;
                float max = 0f;
                foreach (var v in _ring)
                    if (v > max) max = v;
                return max;
            }
        }
    }

    public string DefaultRenderDeviceName
    {
        get
        {
            lock (_gate)
            {
                if (_disposed) return string.Empty;
                try { return Resolve().FriendlyName; }
                catch { Drop(); return string.Empty; }
            }
        }
    }

    private void Sample()
    {
        // A slow COM call must not let the next tick pile a second sampler onto the lock.
        if (Interlocked.Exchange(ref _sampling, 1) == 1) return;
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                try
                {
                    _ring[_slot] = Resolve().AudioMeterInformation.MasterPeakValue;
                    _slot = (_slot + 1) % Slots;
                    _meterUnavailable = false;
                }
                catch
                {
                    Drop();
                    _meterUnavailable = true;
                }
            }
        }
        finally { Interlocked.Exchange(ref _sampling, 0); }
    }

    /// <summary>Must be called while holding <see cref="_gate"/>.</summary>
    private MMDevice Resolve()
    {
        var now = DateTimeOffset.UtcNow;
        if (_device is null || now - _resolvedAt >= DeviceLifetime)
        {
            Drop();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _resolvedAt = now;
        }
        return _device;
    }

    /// <summary>Must be called while holding <see cref="_gate"/>.</summary>
    private void Drop()
    {
        try { _device?.Dispose(); } catch { }
        _device = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        // Outside the lock: an in-flight Sample holds it and would deadlock a blocking dispose.
        _sampler.Dispose();
        lock (_gate) { Drop(); }
        _enumerator.Dispose();
    }
}
