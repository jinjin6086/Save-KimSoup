namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    private readonly SoftwareAudioMixer softwareMixer=new();
    private readonly Dictionary<string,PreparedSound> soundResources=new();
    private SteadyWaveOutput? audioOutput;
    internal SoftwareAudioMixer MixerForTest=>softwareMixer;
    internal SteadyWaveOutput? OutputForTest=>audioOutput;
    private void PrepareSoundResources()
    {
        // One-time 2:1 interpolation preserves the authored 22050 Hz pitch and
        // duration. No allocation, file access, resampling or device-open in Play.
        foreach(var clip in clips)soundResources[clip.Key]=PreparedSound.FromMono22050(clip.Key,clip.Value);
    }
}
