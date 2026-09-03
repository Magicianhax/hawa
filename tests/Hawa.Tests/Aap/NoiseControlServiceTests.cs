using Hawa.Aap;

namespace Hawa.Tests.Aap;

public class NoiseControlServiceTests
{
    private sealed class FakeTransport : IAapTransport
    {
        public List<byte[]> Sent = new();
        public bool IsAvailable => true;
        public event Action<byte[]>? Received;
        public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SendAsync(byte[] data, CancellationToken ct) { Sent.Add(data); return Task.CompletedTask; }
        public Task DisconnectAsync() => Task.CompletedTask;
        public void Push(byte[] data) => Received?.Invoke(data);
    }

    [Fact]
    public async Task NullTransport_reports_unavailable_and_throws()
    {
        var svc = new NoiseControlService(new NullTransport());
        Assert.False(svc.IsAvailable);
        await Assert.ThrowsAsync<AapUnavailableException>(() => svc.SetModeAsync(NoiseMode.Adaptive, CancellationToken.None));
    }

    [Fact]
    public async Task SetMode_sends_packet_and_updates_on_echo()
    {
        var t = new FakeTransport();
        var svc = new NoiseControlService(t);
        NoiseMode? raised = null;
        svc.ModeChanged += m => raised = m;

        await svc.SetModeAsync(NoiseMode.Transparency, CancellationToken.None);
        Assert.Equal(AapCodec.SetNoiseMode(NoiseMode.Transparency), t.Sent.Single());

        t.Push(AapCodec.SetNoiseMode(NoiseMode.Transparency));
        Assert.Equal(NoiseMode.Transparency, svc.CurrentMode);
        Assert.Equal(NoiseMode.Transparency, raised);
    }
}
