namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Narrative-only shortcut. Never completes a fight, rhythm input or laser QTE.
    private bool TrySkipStory()
    {
        if(state==GameState.StoryScene)
        {
            if(journeyKind==3){ResetStoryJourney();BeginRoundTransition(2,false);}
            else FinishStageTravel();
            ReleaseInputs();return true;
        }
        if(state==GameState.Ending)
        {
            endingScene=3;endingLine=EndingWords[3].Length-1;endingPendingScene=-1;
            endingSceneClock=EndingRescueCompleteFrame+1;endingTextRevealed=true;
            endingFinal=true;endingFinalClock=0;endingInputLock=10;ReleaseInputs();return true;
        }
        if(state!=GameState.Round&&state!=GameState.RoundTransition)return false;
        if(PhaseIntroActive)
        {
            phaseIntroClock=PhaseIntroDuration;UpdatePhaseIntro(0);
            if(!bossBriefingActive){ReleaseInputs();return true;}
            // Continue into the phase dialogue's normal exit below.
        }
        if(bossBriefingActive)
        {
            bossTalkHelp=false;bossTalkInputLock=0;
            // The mechanics briefing remains accessible before the battle.
            if(!bossTalkIsPhase&&!storyDefeatTalk&&bossTalkPhase!=3)
            {bossTalkPhase=3;bossTalkClock=0;ReleaseInputs();return true;}
            bossTalkPhase=2;bossTalkClock=36;UpdateBossConversation(0);
            ReleaseInputs();return true;
        }
        if(bossDefeatActive){FinishBossDefeat();return true;}
        return false;
    }
    private void DrawStorySkipHint(Graphics g)
    {
        bool narrative=state is GameState.StoryScene or GameState.Ending||bossBriefingActive||bossDefeatActive||PhaseIntroActive;
        if(!narrative)return;
        Label(g,"S  스토리 건너뛰기",new(758,9,220,23),11,Color.FromArgb(210,219,207,184),false,true);
    }
}
