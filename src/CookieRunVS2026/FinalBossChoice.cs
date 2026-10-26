namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private int finalBossChoice;
    private float finalBossChoiceClock;
    private readonly Rectangle[] finalBossChoiceRects={new(116,218,372,205),new(512,218,372,205)};

    private void OpenFinalBossChoice()
    {
        state=GameState.FinalBossSelect;finalBossChoice=0;finalBossChoiceClock=0;
        // Preserve already-held keys so an Enter used to skip the preceding
        // clear screen cannot auto-confirm this new, optional route.
        sliding=false;audio.StopSliding();audio.StopClashSounds();
        sceneFadeTimer=cameraShakeTimer=screenFlashTimer=0;
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        audio.Play("reward");
    }
    private void UpdateFinalBossChoice(float step)=>finalBossChoiceClock+=step;
    private void ChooseFinalBossRoute(int choice)
    {
        if(state!=GameState.FinalBossSelect||finalBossChoiceClock<18||choice is <0 or >1)return;
        finalBossChoice=choice;audio.Play("select");ReleaseInputs();
        if(choice==0)
        {
            // Random selection happens only after accepting the extra fight.
            ChooseFinalBoss();BeginStageTravel(5);
        }
        else
        {
            ResetEncounterFlow();transitionNextRound=0;roundTransitionTimer=0;pendingDraft=0;
            BeginRescueEnding();sceneFadeTimer=SceneFadeFrames;
        }
    }
    private bool HandleFinalBossChoiceKey(Keys key)
    {
        if(state!=GameState.FinalBossSelect||key==Keys.Escape)return false;
        if(finalBossChoiceClock<18)return true;
        if(key is Keys.Left or Keys.Right or Keys.Up or Keys.Down){finalBossChoice=1-finalBossChoice;audio.Play("select");}
        else if(key is Keys.D1 or Keys.NumPad1)ChooseFinalBossRoute(0);
        else if(key is Keys.D2 or Keys.NumPad2)ChooseFinalBossRoute(1);
        else if(key is Keys.Enter or Keys.Space)ChooseFinalBossRoute(finalBossChoice);
        return true;
    }
    private void HandleFinalBossChoiceMouse(int x,int y)
    {
        for(int i=0;i<finalBossChoiceRects.Length;i++)if(finalBossChoiceRects[i].Contains(x,y)){ChooseFinalBossRoute(i);return;}
    }
    private void DrawFinalBossChoice(Graphics g)
    {
        DrawSoulsMenuBase(g,.92f);
        Label(g,"라운드 4 클리어",new(150,46,700,26),13,Gold);
        SoulsHeading(g,"종결 보스에 도전할까?",new(100,88,800,57),35,Paper);
        DrawSoulsRule(g,340,159,320);
        Label(g,"지금 김스프를 구하러 가도 됩니다. 추가 전투는 선택입니다.",new(120,174,760,28),13,Steel,true,false);
        for(int i=0;i<2;i++)
        {
            var r=finalBossChoiceRects[i];PanelSurface(g,r,Gold,finalBossChoice==i);
            Label(g,$"[ {i+1} ]",new(r.X+20,r.Y+18,r.Width-40,25),12,Gold);
            SoulsHeading(g,i==0?"종결 보스에 도전":"바로 김스프 구출",new(r.X+18,r.Y+58,r.Width-36,42),25,Paper);
            DrawSoulsRule(g,r.X+65,r.Y+113,r.Width-130);
            Label(g,i==0?"무작위 종결 보스와 한 번 더 전투\n처치 후 김스프 구출 엔딩으로 진행":"추가 전투 없이\n김스프 구출 엔딩으로 진행",new(r.X+22,r.Y+130,r.Width-44,55),14,Steel,true,false);
        }
        Label(g,"1 도전  /  2 구출  ·  카드 클릭  ·  방향키 + ENTER",new(120,465,760,30),13,Gold);
        Label(g,"자동으로 선택되지 않습니다.",new(120,502,760,25),12,Steel,true,false);
        Label(g,$"현재 기록  {ClearTimeText}",new(150,533,700,23),12,Gold);
    }
}
