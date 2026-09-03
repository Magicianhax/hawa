using Hawa.Core.Model;

namespace Hawa.Core.Bluetooth;

/// <summary>Chooses which advertising address belongs to "our" AirPods. The first address seen
/// wins, and it keeps winning until it stops advertising for <see cref="Window"/> or another
/// candidate beats it by <see cref="Hysteresis"/> dB. Without that margin, RSSI jitter between
/// two pairs in the same room flips the accepted address several times a second and every flip
/// looks like a pod being removed or a lid opening. Resolvable private addresses rotate every
/// ~15 min; a rotation retires the old address by timeout and the new one takes over.</summary>
public sealed class DeviceMatcher
{
    private readonly TimeProvider _time;
    private readonly Dictionary<ulong, (short Rssi, DateTimeOffset Seen)> _candidates = new();
    private readonly object _gate = new();
    private ulong? _current;

    public DeviceMatcher(TimeProvider time) => _time = time;

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>How many dB a rival address must beat the current one by before it takes over.</summary>
    public short Hysteresis { get; init; } = 8;

    public AirPodsModel? ExpectedModel { get; set; }

    public bool Accept(PodsSnapshot s)
    {
        if (!s.Model.IsKnown()) return false;
        if (ExpectedModel is { } expected && s.Model != expected) return false;

        lock (_gate)
        {
            var now = _time.GetUtcNow();
            _candidates[s.Address] = (s.Rssi, now);

            foreach (var stale in _candidates.Where(kv => now - kv.Value.Seen > Window).Select(kv => kv.Key).ToList())
                _candidates.Remove(stale);

            var strongest = _candidates.MaxBy(kv => kv.Value.Rssi).Key;
            if (_current is not { } current || !_candidates.ContainsKey(current))
                _current = strongest;
            else if (strongest != current && _candidates[strongest].Rssi >= _candidates[current].Rssi + Hysteresis)
                _current = strongest;

            return s.Address == _current;
        }
    }
}
