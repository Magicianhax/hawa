using Hawa.Core.Model;
using Hawa.Core.State;

namespace Hawa.Core.Media;

public sealed class AutoPauseCoordinator : IDisposable
{
    private readonly DeviceStateStore _store;
    private readonly IMediaController _media;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _pausedAt;

    public AutoPauseCoordinator(DeviceStateStore store, IMediaController media, TimeProvider time)
    {
        _store = store;
        _media = media;
        _time = time;
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
        if (!Enabled) return;
        await _gate.WaitAsync();
        try
        {
            switch (t)
            {
                case PodRemoved:
                    if (!_store.IsConnected) return;
                    if (PauseOnlyWhenBothRemoved && _store.Snapshot is { } s && (s.LeftInEar || s.RightInEar)) return;
                    if (await _media.PauseIfPlayingAsync()) _pausedAt = _time.GetUtcNow();
                    break;

                case PodInserted:
                    if (_pausedAt is not { } at) return;
                    _pausedAt = null;
                    if (_time.GetUtcNow() - at <= ResumeWindow) await _media.ResumeIfWePausedAsync();
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
