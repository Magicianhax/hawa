namespace Hawa.Core.Model;

public enum AirPodsModel : ushort
{
    Unknown = 0,
    AirPods1 = 0x0220,
    AirPods2 = 0x0F20,
    AirPods3 = 0x1320,
    AirPods4 = 0x1920,
    AirPods4Anc = 0x1B20,
    AirPodsPro = 0x0E20,
    AirPodsPro2 = 0x1420,
    AirPodsPro2UsbC = 0x2420,
    AirPodsMax = 0x0A20,
}

public static class AirPodsModelExtensions
{
    public static bool IsKnown(this AirPodsModel m) => m != AirPodsModel.Unknown && Enum.IsDefined(m);

    public static bool IsPro2(this AirPodsModel m) => m is AirPodsModel.AirPodsPro2 or AirPodsModel.AirPodsPro2UsbC;

    public static string DisplayName(this AirPodsModel m) => m switch
    {
        AirPodsModel.AirPods1 => "AirPods",
        AirPodsModel.AirPods2 => "AirPods (2nd generation)",
        AirPodsModel.AirPods3 => "AirPods (3rd generation)",
        AirPodsModel.AirPods4 => "AirPods 4",
        AirPodsModel.AirPods4Anc => "AirPods 4 (ANC)",
        AirPodsModel.AirPodsPro => "AirPods Pro",
        AirPodsModel.AirPodsPro2 => "AirPods Pro (2nd generation)",
        AirPodsModel.AirPodsPro2UsbC => "AirPods Pro (2nd generation, USB-C)",
        AirPodsModel.AirPodsMax => "AirPods Max",
        _ => "Unknown AirPods",
    };

    public static AirPodsModel FromRaw(ushort raw) => Enum.IsDefined(typeof(AirPodsModel), raw) ? (AirPodsModel)raw : AirPodsModel.Unknown;
}
