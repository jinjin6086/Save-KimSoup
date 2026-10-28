namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private double clearTimeSeconds;
    // Host elapsed time stays independent of gameplay slow-motion and acceleration.
    internal void AdvanceClearTimerFromHost(float seconds)=>AdvanceClearTimer(seconds);
    private void AdvanceClearTimer(double seconds)
    {
        if(seconds<=0||!double.IsFinite(seconds)||gamePaused||practiceActive||state!=GameState.Round)return;
        if(storyDefeatTalk||bossBriefingActive||bossDefeatActive||PhaseIntroActive||sceneFadeTimer>0||TruePurgaeTimeFrozen||RoundOneOnboardingActive)return;
        if(MelodyLastStandActive&&lastStandPhase!=4)return;
        if(IsangjinClockActive&&isangjinClockPhase!=4)return;
        if(round==4&&menheraIntro>0)return;
        clearTimeSeconds+=seconds;
    }
    private string ClearTimeText
    {
        get
        {
            long centiseconds=(long)Math.Floor(Math.Max(0,clearTimeSeconds)*100+1e-7);
            return $"{centiseconds/6000:00}:{centiseconds/100%60:00}.{centiseconds%100:00}";
        }
    }
}
