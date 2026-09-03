namespace Hawa.Core.Media;

public sealed class MediaController : IMediaController
{
    private readonly IMediaSession _session;
    private readonly IAudioProbe _audio;
    private readonly Func<string?> _boundDeviceName;
    private bool _wePaused;

    public MediaController(IMediaSession session, IAudioProbe audio, Func<string?> boundDeviceName)
    {
        _session = session;
        _audio = audio;
        _boundDeviceName = boundDeviceName;
    }

    public float PeakThreshold { get; init; } = 0.001f;

    public async Task<bool> PauseIfPlayingAsync()
    {
        if (!await _session.IsPlayingAsync()) return false;
        if (_audio.PeakLevel < PeakThreshold) return false;
        if (!await _session.TryPauseAsync()) return false;
        _wePaused = true;
        return true;
    }

    public async Task<bool> ResumeIfWePausedAsync()
    {
        if (!_wePaused) return false;
        var name = _boundDeviceName();
        if (!string.IsNullOrEmpty(name) &&
            !_audio.DefaultRenderDeviceName.Contains(name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!await _session.TryPlayAsync()) return false;
        _wePaused = false;
        return true;
    }
}
