namespace Hawa.Aap;

public sealed class NoiseControlService
{
    private readonly IAapTransport _transport;

    public NoiseControlService(IAapTransport transport)
    {
        _transport = transport;
        _transport.Received += OnReceived;
    }

    public bool IsAvailable => _transport.IsAvailable;
    public NoiseMode? CurrentMode { get; private set; }
    public event Action<NoiseMode>? ModeChanged;

    public Task SetModeAsync(NoiseMode mode, CancellationToken cancellationToken)
    {
        if (!_transport.IsAvailable) throw new AapUnavailableException();
        return _transport.SendAsync(AapCodec.SetNoiseMode(mode), cancellationToken);
    }

    private void OnReceived(byte[] data)
    {
        if (AapCodec.TryDecodeNoiseMode(data) is { } mode && mode != CurrentMode)
        {
            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
