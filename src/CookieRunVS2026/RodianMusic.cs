namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool rodianMusicPrepared;
    private void UpdateRodianBackgroundMusic()
    {
        bool rodian=state==GameState.Round&&round==4;
        if(rodian&&!rodianMusicPrepared)
        {
            // Warm only the new score data, never change native/session volume.
            audio.PreloadMusic(12);audio.PreloadMusic(10);audio.PreloadMusic(9);
            rodianMusicPrepared=true;
        }
        bool hope=MelodyLastStandActive&&lastStandPhase is 8 or 9 or 4 or 5;
        bool dark=MelodyLastStandActive&&!hope;
        int musicRound=state==GameState.StoryScene?JourneyMusicKey:state==GameState.Ending?1:state==GameState.Round?
            (dark?10:rodian?12:round==3?13:round==5?(FinalBossKind==0?13:FinalBossKind==1?3:4):round):0;
        // The final chorus begins after the monologue and stays through the
        // successful explosion. Other rounds and optional bosses keep their score.
        audio.SetMusic(musicRound,false,false,hope);
    }
}
