using Windows.Media.Control;

namespace Hawa.Core.Media;

/// <summary>Global System Media Transport Controls wrapper. Not unit-tested.</summary>
public sealed class SmtcMediaSession : IMediaSession
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

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
