using Hawa.Core.Media;

namespace Hawa.Tests.Media;

public class MediaControllerTests
{
    private sealed class FakeSession : IMediaSession
    {
        public bool Playing; public int Pauses; public int Plays;
        public Task<bool> IsPlayingAsync() => Task.FromResult(Playing);
        public Task<bool> TryPauseAsync() { Pauses++; Playing = false; return Task.FromResult(true); }
        public Task<bool> TryPlayAsync() { Plays++; Playing = true; return Task.FromResult(true); }
    }

    private sealed class FakeAudio : IAudioProbe
    {
        public float PeakLevel { get; set; } = 0.2f;
        public string DefaultRenderDeviceName { get; set; } = "Headphones (AirPods Pro)";
    }

    [Fact]
    public async Task Pauses_only_when_playing_with_audible_output()
    {
        var s = new FakeSession { Playing = true };
        var a = new FakeAudio();
        var c = new MediaController(s, a, () => "AirPods Pro");
        Assert.True(await c.PauseIfPlayingAsync());
        Assert.Equal(1, s.Pauses);
    }

    [Fact]
    public async Task Does_not_pause_silent_session()
    {
        var s = new FakeSession { Playing = true };
        var c = new MediaController(s, new FakeAudio { PeakLevel = 0f }, () => "AirPods Pro");
        Assert.False(await c.PauseIfPlayingAsync());
        Assert.Equal(0, s.Pauses);
    }

    [Fact]
    public async Task Does_not_pause_when_not_playing()
    {
        var s = new FakeSession { Playing = false };
        var c = new MediaController(s, new FakeAudio(), () => "AirPods Pro");
        Assert.False(await c.PauseIfPlayingAsync());
    }

    [Fact]
    public async Task Resumes_only_if_we_paused_and_airpods_are_default_endpoint()
    {
        var s = new FakeSession { Playing = true };
        var a = new FakeAudio();
        var c = new MediaController(s, a, () => "AirPods Pro");

        Assert.False(await c.ResumeIfWePausedAsync());
        await c.PauseIfPlayingAsync();

        a.DefaultRenderDeviceName = "Speakers (Realtek)";
        Assert.False(await c.ResumeIfWePausedAsync());
        Assert.Equal(0, s.Plays);

        a.DefaultRenderDeviceName = "Headphones (AirPods Pro)";
        Assert.True(await c.ResumeIfWePausedAsync());
        Assert.Equal(1, s.Plays);
        Assert.False(await c.ResumeIfWePausedAsync());
    }
}
