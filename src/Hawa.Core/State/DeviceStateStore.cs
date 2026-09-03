using Hawa.Core.Model;

namespace Hawa.Core.State;

/// <summary>Single source of truth for the AirPods state. Derives typed transitions from
/// successive snapshots and throttles <see cref="StateChanged"/> for UI consumers.</summary>
public sealed class DeviceStateStore : IDisposable
{
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly ITimer _throttleTimer;
    private readonly ITimer _staleTimer;
    private DateTimeOffset _lastRaised = DateTimeOffset.MinValue;
    private DateTimeOffset _lastApplied = DateTimeOffset.MinValue;
    private bool _raisePending;

    public DeviceStateStore(TimeProvider time)
    {
        _time = time;
        _throttleTimer = time.CreateTimer(_ => FlushThrottled(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _staleTimer = time.CreateTimer(_ => MarkStale(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public PodsSnapshot? Snapshot { get; private set; }
    public bool IsConnected { get; private set; }
    public bool IsStale { get; private set; }
    public int LowBatteryThreshold { get; set; } = 20;
    public TimeSpan ThrottleInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(30);

    public event Action? StateChanged;
    public event Action<Transition>? TransitionOccurred;

    public void Apply(PodsSnapshot next)
    {
        List<Transition> transitions;
        lock (_gate)
        {
            var prev = Snapshot;
            Snapshot = next;
            IsStale = false;
            _lastApplied = _time.GetUtcNow();
            _staleTimer.Change(StaleAfter, Timeout.InfiniteTimeSpan);
            transitions = Derive(prev, next);
        }
        foreach (var t in transitions) TransitionOccurred?.Invoke(t);
        RaiseThrottled();
    }

    public void SetConnected(bool connected)
    {
        Transition? t = null;
        lock (_gate)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            t = connected ? new Connected() : new Disconnected();
        }
        TransitionOccurred?.Invoke(t);
        RaiseThrottled();
    }

    private List<Transition> Derive(PodsSnapshot? prev, PodsSnapshot next)
    {
        var list = new List<Transition>();

        if (next.LidOpen && (prev is null || !prev.LidOpen)) list.Add(new LidOpened());

        foreach (var side in new[] { Side.Left, Side.Right })
        {
            // Ear transitions need a previous snapshot; the first snapshot only establishes state.
            if (prev is not null)
            {
                bool was = prev.InEar(side);
                bool now = next.InEar(side);
                if (was && !now) list.Add(new PodRemoved(side));
                if (!was && now) list.Add(new PodInserted(side));
            }

            int? before = prev?.Battery(side);
            int? after = next.Battery(side);
            bool wasLow = before is { } b && b <= LowBatteryThreshold;
            bool isLow = after is { } a && a <= LowBatteryThreshold;
            if (isLow && !wasLow) list.Add(new LowBattery(side, after!.Value));
        }
        return list;
    }

    private void RaiseThrottled()
    {
        bool fireNow = false;
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            var elapsed = now - _lastRaised;
            if (elapsed >= ThrottleInterval)
            {
                _lastRaised = now;
                fireNow = true;
            }
            else if (!_raisePending)
            {
                _raisePending = true;
                _throttleTimer.Change(ThrottleInterval - elapsed, Timeout.InfiniteTimeSpan);
            }
        }
        if (fireNow) StateChanged?.Invoke();
    }

    private void FlushThrottled()
    {
        lock (_gate)
        {
            _raisePending = false;
            _lastRaised = _time.GetUtcNow();
        }
        StateChanged?.Invoke();
    }

    private void MarkStale()
    {
        lock (_gate)
        {
            if (IsStale) return;
            if (Snapshot is null || _time.GetUtcNow() - _lastApplied < StaleAfter) return;
            IsStale = true;
            _lastRaised = _time.GetUtcNow();
        }
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        _throttleTimer.Dispose();
        _staleTimer.Dispose();
    }
}
