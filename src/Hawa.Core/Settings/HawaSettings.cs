namespace Hawa.Core.Settings;

public sealed record HawaSettings
{
    public string? SelectedDeviceId { get; init; }
    public bool AutoPauseEnabled { get; init; } = true;
    public bool PauseOnlyWhenBothRemoved { get; init; } = false;
    public bool PopupOnConnect { get; init; } = true;
    public bool PopupOnLidOpen { get; init; } = true;
    public int PopupDurationSeconds { get; init; } = 5;
    public bool StartWithWindows { get; init; } = false;
    public int LowBatteryThreshold { get; init; } = 20;
    /// <summary>Reserved for v2 noise control. 0 = unset.</summary>
    public int LastNoiseMode { get; init; } = 0;
}
