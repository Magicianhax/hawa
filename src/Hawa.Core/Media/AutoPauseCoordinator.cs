using Hawa.Core.Model;
using Hawa.Core.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hawa.Core.Media;

public sealed class AutoPauseCoordinator : IDisposable
{
    private readonly DeviceStateStore _store;
    private readonly IMediaController _media;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _log;
    private DateTimeOffset? _pausedAt;

    public AutoPauseCoordinator(DeviceStateStore store, IMediaController media, TimeProvider time, ILogger<AutoPauseCoordinator>? logger = null)
    {
        _store = store;
        _media = media;
        _time = time;
        _log = logger ?? NullLogger<AutoPauseCoordinator>.Instance;
        _store.TransitionOccurred += OnTransition;
    }

    public bool Enabled { get; set; } = true;
    public bool PauseOnlyWhenBothRemoved { get; set; }
    public TimeSpan ResumeWindow { get; init; } = TimeSpan.FromSeconds(60);
    public event Action<Exception>? Failed;

    private async void OnTransition(Transition t)
    {
        try { await HandleAsync(t); }
        catch (Exception ex) { Failed?.Invoke(ex); }
    }

    public async Task HandleAsync(Transition t)
    {
        if (!Enabled) { _log.LogDebug("auto-pause: {Transition} ignored, disabled", t); return; }
        await _gate.WaitAsync();
        try
        {
            switch (t)
            {
                case PodRemoved:
                    if (!_store.IsConnected) { _log.LogDebug("auto-pause: {Transition} ignored, classic link not connected", t); return; }
                    if (PauseOnlyWhenBothRemoved && _store.Snapshot is { } s && (s.LeftInEar || s.RightInEar)) { _log.LogDebug("auto-pause: {Transition} ignored, other pod still in ear", t); return; }
                    if (await _media.PauseIfPlayingAsync()) { _pausedAt = _time.GetUtcNow(); _log.LogInformation("auto-pause: paused on {Transition}", t); }
                    else _log.LogDebug("auto-pause: {Transition} but nothing to pause", t);
                    break;

                case PodInserted:
                    if (_pausedAt is not { } at) { _log.LogDebug("auto-pause: {Transition} ignored, we did not pause", t); return; }
                    _pausedAt = null;
                    if (_time.GetUtcNow() - at <= ResumeWindow)
                        _log.LogInformation("auto-pause: resume on {Transition} -> {Result}", t, await _media.ResumeIfWePausedAsync());
                    else _log.LogDebug("auto-pause: {Transition} outside resume window", t);
                    break;
            }
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        _store.TransitionOccurred -= OnTransition;
        _gate.Dispose();
    }
}
