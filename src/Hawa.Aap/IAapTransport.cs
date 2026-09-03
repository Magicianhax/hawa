namespace Hawa.Aap;

public interface IAapTransport
{
    bool IsAvailable { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task SendAsync(byte[] data, CancellationToken cancellationToken);
    Task DisconnectAsync();
    event Action<byte[]>? Received;
}

public sealed class AapUnavailableException : InvalidOperationException
{
    public AapUnavailableException()
        : base("The AAP transport is not available. Noise control needs the Hawa AAP driver (v2).") { }
}
