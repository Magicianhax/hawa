namespace Hawa.Core.Media;

public interface IMediaSession
{
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
