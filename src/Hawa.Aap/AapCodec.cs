namespace Hawa.Aap;

/// <summary>Apple Accessory Protocol byte sequences as documented by the open-source community
/// (librepods, AirPodsWindows). Transport-agnostic.</summary>
public static class AapCodec
{
    private static readonly byte[] HeaderPrefix = { 0x04, 0x00, 0x04, 0x00 };
    private static readonly byte[] NoiseModePrefix = { 0x04, 0x00, 0x04, 0x00, 0x09, 0x00, 0x0D };
    private static readonly byte[] EarPrefix = { 0x04, 0x00, 0x04, 0x00, 0x06, 0x00 };
    private static readonly byte[] BatteryPrefix = { 0x04, 0x00, 0x04, 0x00, 0x04, 0x00 };

    public static byte[] Handshake() =>
        new byte[] { 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0, 0, 0, 0, 0, 0, 0, 0 };

    public static byte[] EnableFeatures() =>
        new byte[] { 0x04, 0x00, 0x04, 0x00, 0x4D, 0x00, 0xFF, 0x00, 0, 0, 0, 0, 0, 0 };

    public static byte[] RequestNotifications() =>
        new byte[] { 0x04, 0x00, 0x04, 0x00, 0x0F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };

    public static byte[] SetNoiseMode(NoiseMode mode)
    {
        var packet = new byte[NoiseModePrefix.Length + 4];
        NoiseModePrefix.CopyTo(packet, 0);
        packet[NoiseModePrefix.Length] = (byte)mode;
        return packet;
    }

    public static NoiseMode? TryDecodeNoiseMode(ReadOnlySpan<byte> data)
    {
        if (!data.StartsWith(NoiseModePrefix) || data.Length < NoiseModePrefix.Length + 1) return null;
        var value = data[NoiseModePrefix.Length];
        return Enum.IsDefined(typeof(NoiseMode), value) ? (NoiseMode)value : null;
    }

    public static EarDetectionNotification? TryDecodeEarDetection(ReadOnlySpan<byte> data)
    {
        if (!data.StartsWith(EarPrefix) || data.Length < EarPrefix.Length + 2) return null;
        return new EarDetectionNotification((EarState)data[EarPrefix.Length], (EarState)data[EarPrefix.Length + 1]);
    }

    public static BatteryNotification? TryDecodeBattery(ReadOnlySpan<byte> data)
    {
        if (!data.StartsWith(BatteryPrefix) || data.Length < BatteryPrefix.Length + 1) return null;
        int count = data[BatteryPrefix.Length];
        int offset = BatteryPrefix.Length + 1;
        const int entrySize = 5; // type, 0x01, level, status, 0x01
        if (data.Length < offset + count * entrySize) return null;

        var components = new List<BatteryComponent>(count);
        for (int i = 0; i < count; i++, offset += entrySize)
        {
            var type = (BatteryComponentType)data[offset];
            int level = data[offset + 2];
            bool charging = data[offset + 3] == 0x01;
            components.Add(new BatteryComponent(type, level, charging));
        }
        return new BatteryNotification(components);
    }
}
