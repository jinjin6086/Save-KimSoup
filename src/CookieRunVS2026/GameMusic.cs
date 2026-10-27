namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    private const int MusicSampleRate=44100,MusicChannels=2;
    private readonly HashSet<int> failedScores=new();
    private readonly Dictionary<int,byte[]> musicClips=new();
    private readonly Dictionary<int,Task<byte[]>> loadingScores=new();
    private int requestedMusic,musicKey;
    private const double MusicCrossfadeSeconds=.48;
    // Calibrated against the pre-action legacy output: this Windows waveOut
    // stack applied its .30/.26 control twice (.09/.0676 linear amplitude).
    // Other rounds retain their calibrated idle level. Rodian's battle is
    // 1.5x that level; both QTE movements are 2x their previous .0676 gain.
    // These fixed software gains never write to the Windows audio session.
    private const float MusicMasterGain=.09f,RodianBattleMusicGain=.135f,ClashMusicGain=.1352f;
    public void SetMusic(int round,bool intense,bool duck,bool clash=false)
    {
        // Dialogue, actions, phase changes and finale accents never change gain.
        // The arguments remain API-compatible with the existing scene director.
        requestedMusic=clash?9:round is 10 or 12 or 13 or 14?round:round is >=1 and <=4?round:0;
        if(requestedMusic==0){StopMusic();return;}
        if(!Muted&&!DeviceUnavailable&&!paused&&musicKey!=requestedMusic)StartMusic(requestedMusic);
    }
    internal void PreloadMusic(int key)
    {
        if(!musicClips.ContainsKey(key)&&!loadingScores.ContainsKey(key)&&!failedScores.Contains(key))
            loadingScores[key]=Task.Run(()=>LoadScore(key));
    }
    private void StartMusic(int key)
    {
        if(Muted||paused||DeviceUnavailable||key==0||failedScores.Contains(key)||musicKey==key)return;
        if(!musicClips.TryGetValue(key,out var pcm))
        {
            if(!loadingScores.TryGetValue(key,out var task))
                loadingScores[key]=task=Task.Run(()=>LoadScore(key));
            if(!task.IsCompleted)return;
            if(task.IsFaulted||task.IsCanceled){failedScores.Add(key);loadingScores.Remove(key);return;}
            musicClips[key]=pcm=task.Result;loadingScores.Remove(key);
        }
        softwareMixer.SetMusic(key,pcm,key is 9 or 10?ClashMusicGain:key==12?RodianBattleMusicGain:MusicMasterGain,MusicCrossfadeSeconds);
        musicKey=key;
    }
    private static byte[] LoadScore(int key)=>MakeRoundScore(key);
    private void UpdateMusic()
    {
        if(!paused&&!Muted&&requestedMusic>0&&!DeviceUnavailable&&musicKey!=requestedMusic)StartMusic(requestedMusic);
    }
    private void StopMusic(){softwareMixer.StopMusic();musicKey=0;}
}
