using NAudio.CoreAudioApi;

namespace Hawa.Core.Media;

/// <summary>Reads the default render endpoint's peak meter and name via NAudio. Not unit-tested.</summary>
public sealed class NAudioProbe : IAudioProbe, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public float PeakLevel
    {
        get
        {
            try
            {
                using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.AudioMeterInformation.MasterPeakValue;
            }
            catch { return 1f; } // if the meter is unavailable, do not block pausing
        }
    }

    public string DefaultRenderDeviceName
    {
        get
        {
            try
            {
                using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.FriendlyName;
            }
            catch { return string.Empty; }
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
