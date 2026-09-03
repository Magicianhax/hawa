using Hawa.Core.Bluetooth;
using Hawa.Core.Model;

namespace Hawa.Tests.Bluetooth;

public class ProximityParserTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Enc = " 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
    private static byte[] Hex(string s) => Convert.FromHexString(s.Replace(" ", ""));
    private static PodsSnapshot? Parse(string hex) => ProximityParser.Parse(Hex(hex), 0xAABBCCDDEEFF, -50, T0);

    // status 0x20: high nibble bit 0x2 set => not flipped; no in-ear
    // battery 0x98: right = high nibble 9, left = low nibble 8 (not flipped)
    // 0x45: charging nibble 0x4 = case charging; case battery 5
    // lid 0x01: bit 0x08 clear => open, counter 1
    private const string LidOpenNotFlipped = "07 19 01 14 20 20 98 45 01 00 00" + " 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

    // status 0x08: high nibble 0 => flipped; low nibble 0x8 => left in ear when flipped
    // battery 0xA7: flipped => left = high nibble 10, right = low nibble 7
    // 0x1F: charging nibble 0x1 => left charging when flipped; case battery 0xF unknown
    // lid 0x08: closed
    private const string LidClosedFlipped = "07 19 01 24 20 08 A7 1F 08 00 00" + " 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

    [Fact]
    public void Parses_lid_open_not_flipped()
    {
        var s = Parse(LidOpenNotFlipped)!;
        Assert.Equal(AirPodsModel.AirPodsPro2, s.Model);
        Assert.Equal(0xAABBCCDDEEFFUL, s.Address);
        Assert.Equal(80, s.LeftBattery);
        Assert.Equal(90, s.RightBattery);
        Assert.Equal(50, s.CaseBattery);
        Assert.False(s.LeftCharging);
        Assert.False(s.RightCharging);
        Assert.True(s.CaseCharging);
        Assert.False(s.LeftInEar);
        Assert.False(s.RightInEar);
        Assert.True(s.LidOpen);
        Assert.Equal(-50, s.Rssi);
        Assert.Equal(T0, s.ReceivedAt);
    }

    [Fact]
    public void Parses_lid_closed_flipped_with_left_in_ear_and_charging()
    {
        var s = Parse(LidClosedFlipped)!;
        Assert.Equal(AirPodsModel.AirPodsPro2UsbC, s.Model);
        Assert.Equal(100, s.LeftBattery);
        Assert.Equal(70, s.RightBattery);
        Assert.Null(s.CaseBattery);
        Assert.True(s.LeftCharging);
        Assert.False(s.RightCharging);
        Assert.False(s.CaseCharging);
        Assert.True(s.LeftInEar);
        Assert.False(s.RightInEar);
        Assert.False(s.LidOpen);
    }

    [Fact]
    public void Right_in_ear_when_not_flipped_uses_bit_8()
    {
        var s = Parse("07 19 01 14 20 28 98 05 08 00 00" + Enc)!;
        Assert.False(s.LeftInEar);
        Assert.True(s.RightInEar);
    }

    [Fact]
    public void Unknown_battery_nibbles_become_null()
    {
        var s = Parse("07 19 01 14 20 20 FF 0F 08 00 00" + Enc)!;
        Assert.Null(s.LeftBattery);
        Assert.Null(s.RightBattery);
        Assert.Null(s.CaseBattery);
        Assert.Null(s.LowestPodBattery);
    }

    [Fact]
    public void Unknown_model_is_tagged_not_rejected()
    {
        var s = Parse("07 19 01 99 99 20 98 45 01 00 00" + Enc)!;
        Assert.Equal(AirPodsModel.Unknown, s.Model);
        Assert.False(s.Model.IsKnown());
    }

    [Theory]
    [InlineData("0C 0E 00 00 00 00 00 00 00 00 00 00 00 00 00 00")] // Nearby Info, not proximity pairing
    [InlineData("07 19 01 14 20")]                                   // truncated
    [InlineData("")]
    public void Rejects_non_proximity_or_short_payloads(string hex) => Assert.Null(Parse(hex));

    [Fact]
    public void Side_helpers_map_correctly()
    {
        var s = Parse(LidOpenNotFlipped)!;
        Assert.Equal(80, s.Battery(Side.Left));
        Assert.Equal(90, s.Battery(Side.Right));
        Assert.Equal(50, s.Battery(Side.Case));
        Assert.Equal(80, s.LowestPodBattery);
    }
}
