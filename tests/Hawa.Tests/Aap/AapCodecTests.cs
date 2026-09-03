using Hawa.Aap;

namespace Hawa.Tests.Aap;

public class AapCodecTests
{
    private static byte[] Hex(string s) => Convert.FromHexString(s.Replace(" ", ""));

    [Fact]
    public void Handshake_matches_documented_bytes() =>
        Assert.Equal(Hex("00 00 04 00 01 00 02 00 00 00 00 00 00 00 00 00"), AapCodec.Handshake());

    [Fact]
    public void EnableFeatures_matches_documented_bytes() =>
        Assert.Equal(Hex("04 00 04 00 4D 00 FF 00 00 00 00 00 00 00"), AapCodec.EnableFeatures());

    [Fact]
    public void RequestNotifications_matches_documented_bytes() =>
        Assert.Equal(Hex("04 00 04 00 0F 00 FF FF FF FF"), AapCodec.RequestNotifications());

    [Theory]
    [InlineData(NoiseMode.Off, 0x01)]
    [InlineData(NoiseMode.NoiseCancellation, 0x02)]
    [InlineData(NoiseMode.Transparency, 0x03)]
    [InlineData(NoiseMode.Adaptive, 0x04)]
    public void SetNoiseMode_encodes_mode_byte(NoiseMode mode, byte expected)
    {
        var packet = AapCodec.SetNoiseMode(mode);
        Assert.Equal(Hex("04 00 04 00 09 00 0D"), packet[..7]);
        Assert.Equal(expected, packet[7]);
        Assert.Equal(new byte[] { 0, 0, 0 }, packet[8..]);
    }

    [Fact]
    public void TryDecodeNoiseMode_round_trips()
    {
        Assert.Equal(NoiseMode.Transparency, AapCodec.TryDecodeNoiseMode(AapCodec.SetNoiseMode(NoiseMode.Transparency)));
        Assert.Null(AapCodec.TryDecodeNoiseMode(Hex("04 00 04 00 06 00 00 01")));
    }

    [Fact]
    public void TryDecodeEarDetection_parses_primary_and_secondary()
    {
        var n = AapCodec.TryDecodeEarDetection(Hex("04 00 04 00 06 00 00 02"));
        Assert.NotNull(n);
        Assert.Equal(EarState.InEar, n!.Primary);
        Assert.Equal(EarState.InCase, n.Secondary);
    }

    [Fact]
    public void TryDecodeEarDetection_rejects_other_prefix() =>
        Assert.Null(AapCodec.TryDecodeEarDetection(Hex("04 00 04 00 04 00 00 00")));

    [Fact]
    public void TryDecodeBattery_parses_three_components()
    {
        var n = AapCodec.TryDecodeBattery(Hex("04 00 04 00 04 00 03 02 01 64 01 01 04 01 5A 02 01 08 01 32 01 01"));
        Assert.NotNull(n);
        Assert.Collection(n!.Components,
            c => { Assert.Equal(BatteryComponentType.Right, c.Type); Assert.Equal(100, c.Level); Assert.True(c.Charging); },
            c => { Assert.Equal(BatteryComponentType.Left, c.Type); Assert.Equal(90, c.Level); Assert.False(c.Charging); },
            c => { Assert.Equal(BatteryComponentType.Case, c.Type); Assert.Equal(50, c.Level); Assert.True(c.Charging); });
    }

    [Fact]
    public void TryDecodeBattery_rejects_truncated() =>
        Assert.Null(AapCodec.TryDecodeBattery(Hex("04 00 04 00 04 00 03 02 01 64")));
}
