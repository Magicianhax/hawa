using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Media.Control;

namespace Hawa.Core.Media;

/// <summary>Global System Media Transport Controls wrapper. Not unit-tested.</summary>
public sealed class SmtcMediaSession : IMediaSession
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly ILogger _log;

    public SmtcMediaSession(ILogger<SmtcMediaSession>? logger = null) => _log = logger ?? NullLogger<SmtcMediaSession>.Instance;

    private async Task<GlobalSystemMediaTransportControlsSession?> CurrentAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return _manager.GetCurrentSession();
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            return _manager is not null;
        }
        catch { return false; }
    }

    public async Task<bool> IsPlayingAsync()
    {
        var s = await CurrentAsync();
        if (_manager is not null && _log.IsEnabled(LogLevel.Debug))
        {
            foreach (var session in _manager.GetSessions())
                _log.LogDebug("smtc session {App}: {Status}{Current}", session.SourceAppUserModelId, session.GetPlaybackInfo().PlaybackStatus,
                    session.SourceAppUserModelId == s?.SourceAppUserModelId ? " (current)" : "");
        }
        return s?.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    public async Task<bool> TryPauseAsync()
    {
        var s = await CurrentAsync();
        return s is not null && await s.TryPauseAsync();
    }

    public async Task<bool> TryPlayAsync()
    {
        var s = await CurrentAsync();
        return s is not null && await s.TryPlayAsync();
    }
}
