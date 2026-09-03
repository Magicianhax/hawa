namespace Hawa.Aap;

/// <summary>v1 placeholder: no L2CAP transport exists in user mode on Windows.</summary>
public sealed class NullTransport : IAapTransport
{
    public bool IsAvailable => false;
    public event Action<byte[]>? Received { add { } remove { } }
    public Task ConnectAsync(CancellationToken cancellationToken) => throw new AapUnavailableException();
    public Task SendAsync(byte[] data, CancellationToken cancellationToken) => throw new AapUnavailableException();
    public Task DisconnectAsync() => Task.CompletedTask;
}
