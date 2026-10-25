namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    // Original orchestral performances are rendered offline from acoustic
    // instrument samples. No synthesis, resampling or reverb runs during play.
    private static byte[] MakeRoundScore(int key)
    {
        string file=Path.Combine(AppContext.BaseDirectory,"Assets","Audio",$"score-{key}.pcm");
        byte[] pcm=File.ReadAllBytes(file);
        int block=MusicChannels*2;
        if(pcm.Length<MusicSampleRate*block||pcm.Length%block!=0)
            throw new InvalidDataException($"Invalid orchestral score {key}: expected 44100 Hz stereo 16-bit PCM.");
        return pcm;
    }
}
