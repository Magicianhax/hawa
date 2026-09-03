namespace Hawa.Core.Media;

public interface IMediaController
{
    /// <summary>Pauses the current session if it is playing audibly. Returns true if Hawa paused it.</summary>
    Task<bool> PauseIfPlayingAsync();
    /// <summary>Resumes only if the last pause was Hawa's and the AirPods are still the default output.</summary>
    Task<bool> ResumeIfWePausedAsync();
}
