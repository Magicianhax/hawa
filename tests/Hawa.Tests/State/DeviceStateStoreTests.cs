using Hawa.Core.Model;
using Hawa.Core.State;
using Microsoft.Extensions.Time.Testing;

namespace Hawa.Tests.State;

public class DeviceStateStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    private PodsSnapshot Snap(bool lidOpen = false, bool leftInEar = false, bool rightInEar = false, int? left = 80, int? right = 80, ulong address = 1) =>
        new(AirPodsModel.AirPodsPro2, address, left, right, 50, false, false, false, leftInEar, rightInEar, lidOpen, -50, _time.GetUtcNow());

    private (DeviceStateStore store, List<Transition> transitions, Counter changes) Make()
    {
        var store = new DeviceStateStore(_time);
        var t = new List<Transition>();
        var c = new Counter();
        store.TransitionOccurred += t.Add;
        store.StateChanged += () => c.Value++;
        return (store, t, c);
    }

    private sealed class Counter { public int Value; }

    [Fact]
    public void First_snapshot_raises_StateChanged_immediately()
    {
        var (store, _, changes) = Make();
        store.Apply(Snap());
        Assert.Equal(1, changes.Value);
        Assert.NotNull(store.Snapshot);
    }

    [Fact]
    public void Bursts_are_throttled_to_one_per_interval()
    {
        var (store, _, changes) = Make();
        store.Apply(Snap());
        store.Apply(Snap(left: 70));
        store.Apply(Snap(left: 60));
        Assert.Equal(1, changes.Value);
        _time.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Equal(2, changes.Value);
        Assert.Equal(60, store.Snapshot!.LeftBattery);
    }

    [Fact]
    public void LidOpened_fires_once_on_rising_edge()
    {
        var (store, t, _) = Make();
        store.Apply(Snap(lidOpen: false));
        store.Apply(Snap(lidOpen: true));
        store.Apply(Snap(lidOpen: true));
        Assert.Single(t.OfType<LidOpened>());
    }

    [Fact]
    public void First_snapshot_with_lid_open_fires_LidOpened()
    {
        var (store, t, _) = Make();
        store.Apply(Snap(lidOpen: true));
        Assert.Single(t.OfType<LidOpened>());
    }

    [Fact]
    public void Pod_removed_and_inserted_transitions()
    {
        var (store, t, _) = Make();
        store.Apply(Snap(leftInEar: true, rightInEar: true));
        store.Apply(Snap(leftInEar: false, rightInEar: true));
        store.Apply(Snap(leftInEar: true, rightInEar: true));
        Assert.Equal(new Transition[] { new PodRemoved(Side.Left), new PodInserted(Side.Left) }, t.ToArray());
    }

    [Fact]
    public void Address_change_does_not_fire_ear_transitions()
    {
        var (store, t, _) = Make();
        store.Apply(Snap(leftInEar: true, rightInEar: true));

        // A different address is a different advertiser: its ear state is not a transition of ours.
        store.Apply(Snap(address: 2, leftInEar: false, rightInEar: false));
        Assert.Empty(t.OfType<PodRemoved>());

        // Once the new address is established, its own changes do produce transitions.
        store.Apply(Snap(address: 2, leftInEar: true, rightInEar: true));
        Assert.Equal(2, t.OfType<PodInserted>().Count());
    }

    [Fact]
    public void Connection_transitions_fire_on_change_only()
    {
        var (store, t, changes) = Make();
        store.SetConnected(true);
        store.SetConnected(true);
        store.SetConnected(false);
        Assert.Equal(new Transition[] { new Connected(), new Disconnected() }, t.ToArray());
        Assert.True(changes.Value >= 1);
    }

    [Fact]
    public void LowBattery_fires_when_crossing_threshold_downward()
    {
        var (store, t, _) = Make();
        store.LowBatteryThreshold = 20;
        store.Apply(Snap(left: 30, right: 80));
        store.Apply(Snap(left: 20, right: 80));
        store.Apply(Snap(left: 10, right: 80));
        store.Apply(Snap(left: 30, right: 80));
        store.Apply(Snap(left: 20, right: 80));
        Assert.Equal(2, t.OfType<LowBattery>().Count());
        Assert.All(t.OfType<LowBattery>(), lb => Assert.Equal(Side.Left, lb.Side));
    }

    [Fact]
    public void Becomes_stale_after_30s_without_adverts()
    {
        var (store, _, changes) = Make();
        store.Apply(Snap());
        Assert.False(store.IsStale);
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.True(store.IsStale);
        Assert.Equal(2, changes.Value);
        store.Apply(Snap());
        Assert.False(store.IsStale);
    }
}
