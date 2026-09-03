namespace Hawa.Aap;

public enum EarState : byte { InEar = 0, OutOfEar = 1, InCase = 2 }

public sealed record EarDetectionNotification(EarState Primary, EarState Secondary);

public enum BatteryComponentType : byte { Right = 2, Left = 4, Case = 8 }

public sealed record BatteryComponent(BatteryComponentType Type, int Level, bool Charging);

public sealed record BatteryNotification(IReadOnlyList<BatteryComponent> Components);
