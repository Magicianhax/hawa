namespace Hawa.Core.Model;

public sealed record PodsSnapshot(
    AirPodsModel Model,
    ulong Address,
    int? LeftBattery,
    int? RightBattery,
    int? CaseBattery,
    bool LeftCharging,
    bool RightCharging,
    bool CaseCharging,
    bool LeftInEar,
    bool RightInEar,
    bool LidOpen,
    short Rssi,
    DateTimeOffset ReceivedAt)
{
    public int? Battery(Side side) => side switch
    {
        Side.Left => LeftBattery,
        Side.Right => RightBattery,
        _ => CaseBattery,
    };

    public bool Charging(Side side) => side switch
    {
        Side.Left => LeftCharging,
        Side.Right => RightCharging,
        _ => CaseCharging,
    };

    public bool InEar(Side side) => side == Side.Left ? LeftInEar : side == Side.Right && RightInEar;

    public int? LowestPodBattery => (LeftBattery, RightBattery) switch
    {
        (null, null) => null,
        (null, var r) => r,
        (var l, null) => l,
        (var l, var r) => Math.Min(l.Value, r.Value),
    };
}
