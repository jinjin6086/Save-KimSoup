namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Pause the first stage for one safe, repeatable reflection before the jail.
    private int roundOneParryPhase;
    private float roundOneParryClock;
    private Projectile? roundOneParryTrace;
    private bool roundOneParryMissed;
    private bool RoundOneParryTutorialActive=>round==1&&!practiceActive&&roundOneParryPhase is 1 or 2;

    private void ResetRoundOneParryTutorial()
    {
        roundOneParryPhase=0;roundOneParryClock=0;
        roundOneParryTrace=null;roundOneParryMissed=false;
    }
    private void BeginRoundOneParryTutorial()
    {
        roundOneParryPhase=1;roundOneParryClock=0;
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        bossX=W-202;bossY=GroundY-143;
        py=GroundY-PlayerHeight;vy=0;onGround=true;sliding=false;jumps=0;
        parryWindow=parryCooldown=0;warningTimer=0;ReleaseInputs();
    }
    private void FinishRoundOneParryTutorial()
    {
        roundOneParryPhase=3;roundOneParryTrace=null;
        projectiles.Clear();obstacleSpawnTimer=90;parryWindow=parryCooldown=0;
    }
    private bool HandleRoundOneParryTutorialKey(Keys key)
    {
        if(!RoundOneParryTutorialActive||key!=Keys.Enter)return false;
        FinishRoundOneParryTutorial();return true;
    }
    private bool UpdateRoundOneParryTutorial(float step)
    {
        if(!RoundOneParryTutorialActive)return false;
        // Neither stage progress nor health drain advances while learning.
        roundOneParryClock+=step;
        parryWindow=Math.Max(0,parryWindow-step);parryCooldown=Math.Max(0,parryCooldown-step);
        UpdatePlayer(step);
        if(roundOneParryPhase==2)
        {
            UpdateObjects(step);
            if(projectiles.RemoveAll(p=>p.IsParried&&BossHitBox.IntersectsWith(p.Rect))>0)
                Burst(bossX+65,bossY+65,Gold,14);
            if(roundOneParryClock>=100)FinishRoundOneParryTutorial();
            return true;
        }
        if(roundOneParryTrace==null&&roundOneParryClock>=75)
        {
            // The same black projectile used by Isangjin, with zero damage here.
            roundOneParryTrace=new Projectile(W-150,py+24,AttackType.BlackBall,28,-5,0,0);
            projectiles.Add(roundOneParryTrace);
        }
        UpdateObjects(step);UpdateParryHits();
        if(roundOneParryTrace is {IsParried:true})
        {
            roundOneParryPhase=2;roundOneParryClock=0;roundOneParryTrace=null;
            audio.Play("reward");
        }
        else if(roundOneParryTrace!=null&&roundOneParryTrace.X+roundOneParryTrace.W<KickHitBox.Left)
        {
            projectiles.Clear();roundOneParryTrace=null;roundOneParryClock=0;
            roundOneParryMissed=true;parryWindow=parryCooldown=0;
        }
        return true;
    }
    private void DrawRoundOneParryTutorial(Graphics g)
    {
        if(!RoundOneParryTutorialActive)return;
        PanelSurface(g,new(190,107,620,100),Gold);
        Label(g,roundOneParryPhase==2?"패링 성공!":"이상진의 흔적 · 패링 연습",new(210,115,580,28),17,Gold);
        string help=roundOneParryPhase==2?"좋아요! 이상진의 탄환도 같은 방법으로 되돌려주세요.":
            roundOneParryMissed?"괜찮아요. 검은 탄환이 금색 범위에 닿을 때 발차기하세요.":
            "이상진이 남긴 검은 탄환이 금색 범위에 닿으면 SPACE / SHIFT!";
        Label(g,help,new(208,148,584,27),12,Paper);
        Label(g,"체력 감소·피격 피해 없이 연습  ·  ENTER 건너뛰기",new(210,180,580,19),10,Steel);
        Label(g,"이상진의 흔적",new(W-221,GroundY-172,194,22),11,Steel);
        if(enemy3!=null)DrawSpectralBoss(g,enemy3,new(W-202,GroundY-143,125,125),Color.FromArgb(170,206,225),.28f);
    }
}
