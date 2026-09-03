using Hawa.Core.Model;

namespace Hawa.Core.Bluetooth;

/// <summary>Chooses which advertising address belongs to "our" AirPods: the strongest RSSI among
/// candidates seen within <see cref="Window"/>. Resolvable private addresses rotate every ~15 min;
/// a rotation simply produces a new winning candidate.</summary>
public sealed class DeviceMatcher
{
    private readonly TimeProvider _time;
    private readonly Dictionary<ulong, (short Rssi, DateTimeOffset Seen)> _candidates = new();
    private readonly object _gate = new();

    public DeviceMatcher(TimeProvider time) => _time = time;

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(3);
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

            var best = _candidates.MaxBy(kv => kv.Value.Rssi).Key;
            return best == s.Address;
        }
    }
}
