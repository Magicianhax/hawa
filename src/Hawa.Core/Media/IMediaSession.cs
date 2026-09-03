namespace Hawa.Core.Media;

public interface IMediaSession
{
    /// <summary>True when the system media transport controls can be reached at all. Probed once
    /// at startup so the UI can say why auto-pause is off instead of failing silently per event.</summary>
    Task<bool> IsAvailableAsync();
    Task<bool> IsPlayingAsync();
    Task<bool> TryPauseAsync();
    Task<bool> TryPlayAsync();
}

public interface IAudioProbe
{
    /// <summary>Master peak level of the default render endpoint, 0..1.</summary>
    float PeakLevel { get; }
    string DefaultRenderDeviceName { get; }
}
