using Hawa.Core.Media;
using Hawa.Core.Model;
using Hawa.Core.State;
using Microsoft.Extensions.Time.Testing;

namespace Hawa.Tests.Media;

public class AutoPauseCoordinatorTests
{
    private sealed class FakeMedia : IMediaController
    {
        public int Pauses, Resumes; public bool PauseResult = true;
        public Task<bool> PauseIfPlayingAsync() { Pauses++; return Task.FromResult(PauseResult); }
        public Task<bool> ResumeIfWePausedAsync() { Resumes++; return Task.FromResult(true); }
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    private PodsSnapshot Snap(bool left, bool right) =>
        new(AirPodsModel.AirPodsPro2, 1, 80, 80, 50, false, false, false, left, right, false, -50, _time.GetUtcNow());

    private (DeviceStateStore store, FakeMedia media, AutoPauseCoordinator coord) Make(bool connected = true)
    {
        var store = new DeviceStateStore(_time);
        var media = new FakeMedia();
        var coord = new AutoPauseCoordinator(store, media, _time);
        store.SetConnected(connected);
        store.Apply(Snap(true, true));
        return (store, media, coord);
    }

    [Fact]
    public async Task Pauses_when_one_pod_removed_while_connected()
    {
        var (store, media, _) = Make();
        store.Apply(Snap(false, true));
        await Task.Yield();
        Assert.Equal(1, media.Pauses);
    }

    [Fact]
    public async Task Ignores_removal_when_not_connected()
    {
        var (store, media, _) = Make(connected: false);
        store.Apply(Snap(false, true));
        await Task.Yield();
        Assert.Equal(0, media.Pauses);
    }

    [Fact]
    public async Task Ignores_removal_when_disabled()
    {
        var (store, media, coord) = Make();
        coord.Enabled = false;
        store.Apply(Snap(false, true));
        await Task.Yield();
        Assert.Equal(0, media.Pauses);
    }

    [Fact]
    public async Task Both_removed_setting_waits_for_second_pod()
    {
        var (store, media, coord) = Make();
        coord.PauseOnlyWhenBothRemoved = true;
        store.Apply(Snap(false, true));
        await Task.Yield();
        Assert.Equal(0, media.Pauses);
        store.Apply(Snap(false, false));
        await Task.Yield();
        Assert.Equal(1, media.Pauses);
    }

    [Fact]
    public async Task Resumes_on_insert_within_window_only_after_our_pause()
    {
        var (store, media, coord) = Make();
        await coord.HandleAsync(new PodInserted(Side.Left));
        Assert.Equal(0, media.Resumes);

        await coord.HandleAsync(new PodRemoved(Side.Left));
        _time.Advance(TimeSpan.FromSeconds(30));
        await coord.HandleAsync(new PodInserted(Side.Left));
        Assert.Equal(1, media.Resumes);

        await coord.HandleAsync(new PodInserted(Side.Left));
        Assert.Equal(1, media.Resumes);
    }

    [Fact]
    public async Task Does_not_resume_after_window_expires()
    {
        var (_, media, coord) = Make();
        await coord.HandleAsync(new PodRemoved(Side.Left));
        _time.Advance(TimeSpan.FromSeconds(61));
        await coord.HandleAsync(new PodInserted(Side.Left));
        Assert.Equal(0, media.Resumes);
    }

    [Fact]
    public async Task Failed_pause_does_not_arm_resume()
    {
        var (_, media, coord) = Make();
        media.PauseResult = false;
        await coord.HandleAsync(new PodRemoved(Side.Left));
        await coord.HandleAsync(new PodInserted(Side.Left));
        Assert.Equal(0, media.Resumes);
    }
}
