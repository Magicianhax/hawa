using Hawa.Core.Model;

namespace Hawa.Core.Bluetooth;

/// <summary>Parses the Apple Continuity "Proximity Pairing" message (type 0x07) from the
/// manufacturer-data payload that follows company ID 0x004C. Layout per furiousMAC/OpenPods:
/// [0]=0x07 type, [1]=length, [2]=prefix, [3..4]=model BE, [5]=status, [6]=pod batteries,
/// [7]=charging|case battery, [8]=lid, [9]=colour, [10]=0x00, [11..]=encrypted.</summary>
public static class ProximityParser
{
    public const byte MessageType = 0x07;
    private const int MinimumLength = 11;

    public static PodsSnapshot? Parse(ReadOnlySpan<byte> data, ulong address, short rssi, DateTimeOffset receivedAt)
    {
        if (data.Length < MinimumLength || data[0] != MessageType) return null;

        var model = AirPodsModelExtensions.FromRaw((ushort)((data[3] << 8) | data[4]));
        byte status = data[5];
        bool flipped = (status & 0x20) == 0;

        int hi = data[6] >> 4, lo = data[6] & 0x0F;
        int leftRaw = flipped ? hi : lo;
        int rightRaw = flipped ? lo : hi;

        int chargeBits = data[7] >> 4;
        bool leftCharging = (chargeBits & (flipped ? 0x1 : 0x2)) != 0;
        bool rightCharging = (chargeBits & (flipped ? 0x2 : 0x1)) != 0;
        bool caseCharging = (chargeBits & 0x4) != 0;
        int caseRaw = data[7] & 0x0F;

        int earBits = status & 0x0F;
        bool leftInEar = (earBits & (flipped ? 0x8 : 0x2)) != 0;
        bool rightInEar = (earBits & (flipped ? 0x2 : 0x8)) != 0;

        bool lidOpen = (data[8] & 0x08) == 0;

        return new PodsSnapshot(
            model, address,
            ToPercent(leftRaw), ToPercent(rightRaw), ToPercent(caseRaw),
            leftCharging, rightCharging, caseCharging,
            leftInEar, rightInEar, lidOpen,
            rssi, receivedAt);
    }

    private static int? ToPercent(int raw) => raw is >= 0 and <= 10 ? raw * 10 : null;
}
