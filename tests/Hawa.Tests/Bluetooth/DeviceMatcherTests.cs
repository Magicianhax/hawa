using Hawa.Core.Bluetooth;
using Hawa.Core.Model;
using Microsoft.Extensions.Time.Testing;

namespace Hawa.Tests.Bluetooth;

public class DeviceMatcherTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    private PodsSnapshot Snap(ulong address, short rssi, AirPodsModel model = AirPodsModel.AirPodsPro2) =>
        new(model, address, 50, 50, 50, false, false, false, false, false, false, rssi, _time.GetUtcNow());

    [Fact]
    public void Single_device_is_always_accepted()
    {
        var m = new DeviceMatcher(_time);
        Assert.True(m.Accept(Snap(1, -60)));
        Assert.True(m.Accept(Snap(1, -70)));
        Assert.True(m.Accept(Snap(1, -55)));
    }

    [Fact]
    public void Unknown_model_is_rejected()
    {
        var m = new DeviceMatcher(_time);
        Assert.False(m.Accept(Snap(1, -40, AirPodsModel.Unknown)));
    }

    [Fact]
    public void Expected_model_mismatch_is_rejected()
    {
        var m = new DeviceMatcher(_time) { ExpectedModel = AirPodsModel.AirPodsPro2UsbC };
        Assert.False(m.Accept(Snap(1, -40, AirPodsModel.AirPodsPro2)));
        Assert.True(m.Accept(Snap(1, -40, AirPodsModel.AirPodsPro2UsbC)));
    }

    [Fact]
    public void Strongest_address_wins_and_weaker_is_rejected()
    {
        var m = new DeviceMatcher(_time);
        Assert.True(m.Accept(Snap(1, -50)));
        Assert.False(m.Accept(Snap(2, -80)));
        Assert.True(m.Accept(Snap(1, -52)));
    }

    [Fact]
    public void Stale_candidates_expire_after_window()
    {
        var m = new DeviceMatcher(_time);
        Assert.True(m.Accept(Snap(1, -50)));
        _time.Advance(TimeSpan.FromSeconds(4));
        Assert.True(m.Accept(Snap(2, -80)));
    }

    [Fact]
    public void Address_rotation_hands_over_to_new_strong_address()
    {
        var m = new DeviceMatcher(_time);
        Assert.True(m.Accept(Snap(1, -50)));
        Assert.True(m.Accept(Snap(2, -45)));
        Assert.False(m.Accept(Snap(1, -50)));
    }
}
