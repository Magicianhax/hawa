using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hawa.Core.Media;

/// <remarks>Not thread-safe: callers must serialize access to <see cref="PauseIfPlayingAsync"/> and
/// <see cref="ResumeIfWePausedAsync"/> (<see cref="AutoPauseCoordinator"/> does this).</remarks>
public sealed class MediaController : IMediaController
{
    private readonly IMediaSession _session;
    private readonly IAudioProbe _audio;
    private readonly Func<string?> _boundDeviceName;
    private readonly ILogger _log;
    private bool _wePaused;

    public MediaController(IMediaSession session, IAudioProbe audio, Func<string?> boundDeviceName, ILogger<MediaController>? logger = null)
    {
        _session = session;
        _audio = audio;
        _boundDeviceName = boundDeviceName;
        _log = logger ?? NullLogger<MediaController>.Instance;
    }

    public float PeakThreshold { get; init; } = 0.001f;

    public async Task<bool> PauseIfPlayingAsync()
    {
        bool playing = await _session.IsPlayingAsync();
        float peak = _audio.PeakLevel;
        _log.LogDebug("media: playing={Playing} peak={Peak:F4} threshold={Threshold}", playing, peak, PeakThreshold);
        if (!playing) return false;
        if (peak < PeakThreshold) return false;
        if (!await _session.TryPauseAsync()) { _log.LogWarning("media: TryPause returned false"); return false; }
        _wePaused = true;
        return true;
    }

    public async Task<bool> ResumeIfWePausedAsync()
    {
        if (!_wePaused) return false;
        var name = _boundDeviceName();
        var endpoint = _audio.DefaultRenderDeviceName;
        if (!string.IsNullOrEmpty(name) && !endpoint.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            _log.LogDebug("media: not resuming, default endpoint '{Endpoint}' is not '{Device}'", endpoint, name);
            return false;
        }
        if (!await _session.TryPlayAsync()) { _log.LogWarning("media: TryPlay returned false"); return false; }
        _wePaused = false;
        return true;
    }
}
