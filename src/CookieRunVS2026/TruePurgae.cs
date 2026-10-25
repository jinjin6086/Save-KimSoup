using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private const int TrueStopStoneCount=36;
    private const float TrueStopDuration=156;
    private int trueStopPhase,trueStopBuilt,trueStopReleased;
    private float trueStopClock,trueStopCooldown,trueStopReleaseFlash;
    private float trueStopX,trueStopY;
    private bool trueStopParryQueued;
    private readonly List<TrueStopStone> trueStopStones=new(TrueStopStoneCount);
    private readonly HashSet<Projectile> trueStopBullets=new();
    private sealed class TrueStopStone
    {
        public float X,Y,FireAt,Speed=40;
        public bool Fired;
    }
    private bool TruePurgaeEncounter=>round==5&&FinalBossKind==0;
    private bool TruePurgaeTimeFrozen=>TruePurgaeEncounter&&trueStopPhase==2&&state==GameState.Round;
    private bool TruePurgaeStone(Projectile projectile)=>trueStopBullets.Contains(projectile);

    private void InitializeTruePurgaeEncounter()
    {
        ResetTruePurgaeEncounter();
        if(!TruePurgaeEncounter)return;
        purgaeTransformed=true;
        purgaeSkill1Cooldown=700;purgaeSkill2Cooldown=150;
        trueStopCooldown=285;
    }
    private void ResetTruePurgaeEncounter()
    {
        DisposeTrueStopSurfaces();
        audio.StopTimeStopSounds();
        trueStopPhase=trueStopBuilt=trueStopReleased=0;
        trueStopClock=trueStopReleaseFlash=0;trueStopCooldown=285;
        trueStopParryQueued=false;trueStopStones.Clear();trueStopBullets.Clear();trueStopLanes.Clear();trueStopPattern=trueStopCycle=0;
    }
    private void UpdateTruePurgaeBoss(float step)
    {
        trueStopReleaseFlash=Math.Max(0,trueStopReleaseFlash-step);
        trueStopBullets.RemoveWhere(p=>!projectiles.Contains(p));
        if(trueStopPhase==1)
        {
            trueStopClock+=step;
            if(trueStopClock>=48)BeginTruePurgaeFrozenTime();
            return;
        }
        if(trueStopPhase==2)return;
        if(trueStopPhase==3)
        {
            trueStopClock+=step;
            foreach(var stone in trueStopStones)
                if(!stone.Fired&&trueStopClock>=stone.FireAt)
                {
                    stone.Fired=true;trueStopReleased++;
                    float dx=trueStopX-stone.X,dy=trueStopY-stone.Y,length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy));
                    var shot=new Projectile(stone.X-13,stone.Y-13,AttackType.StoneChunk,26,dx/length*stone.Speed,dy/length*stone.Speed,450);
                    projectiles.Add(shot);trueStopBullets.Add(shot);
                    if(trueStopReleased%3==1)audio.Play("timestopstone");
                }
            UpdateTrueStopLanes();
            if(trueStopClock>=TrueStopReleaseDuration&&!projectiles.Any(p=>trueStopBullets.Contains(p)&&!p.IsParried))
            {
                trueStopPhase=0;trueStopClock=0;trueStopStones.Clear();
                trueStopCooldown=bossDamage>bossHitRequired*.5f?240:330;purgaeSkill2Cooldown=Math.Max(105,purgaeSkill2Cooldown);
            }
            return;
        }
        finalBossClock+=step;
        float targetX=734+MathF.Sin(finalBossClock*.029f)*39,targetY=205+MathF.Sin(finalBossClock*.041f)*32;
        bossX+=(targetX-bossX)*(1-MathF.Pow(.95f,step));bossY+=(targetY-bossY)*(1-MathF.Pow(.95f,step));
        if(MeteorSceneActive)return;
        finalShotClock+=step;
        if(finalShotClock>=84&&!purgaeSkill2Active)
        {finalShotClock=0;SpawnPurgaeBasicAttack();audio.Play("volley");}
        if(purgaeSkill2Active)UpdatePurgaeSkill2(step);
        else
        {
            purgaeSkill2Cooldown-=step;
            if(purgaeSkill2Cooldown<=0)StartPurgaeSkill2();
        }
        purgaeSkill1Cooldown-=step;trueStopCooldown-=step;
        if(!purgaeSkill2Active&&trueStopCooldown<=0)
        {BeginTruePurgaeTimeStop();return;}
        if(!purgaeSkill2Active&&purgaeSkill1Cooldown<=0)
        {StartMeteorStorm();purgaeSkill1Cooldown=920;}
    }
    private void BeginTruePurgaeTimeStop()
    {
        if(!TruePurgaeEncounter||trueStopPhase!=0||MeteorSceneActive)return;
        trueStopPattern=trueStopCycle++%4;trueStopLanes.Clear();
        trueStopPhase=1;trueStopClock=0;trueStopBuilt=trueStopReleased=0;trueStopParryQueued=false;
        trueStopStones.Clear();
        projectiles.RemoveAll(p=>!p.IsParried);obstacles.Clear();risingObstacles.Clear();
        purgaeSkill2Active=false;Array.Fill(purgaeSkill2BallVisible,false);
        encounterMessage="시간 정지 · "+TrueStopPatternName;warningTimer=48;
        bossMessage="멈춰.";bossShoutTimer=48;
        audio.Play("timestopstart");TriggerCameraShake(reducedEffects?0:3.5f,10);
    }
    private void BeginTruePurgaeFrozenTime()
    {
        trueStopPhase=2;trueStopClock=0;
        trueStopX=px+PlayerWidth*.5f;trueStopY=py+(sliding?51:PlayerHeight*.5f);
        parryWindow=0;parryCooldown=0;
        audio.PlayTimeStopHold();
        BuildTrueStopFormation();
    }
    // Called from the real-time presentation path before any gameplay update.
    // Returning true also consumes the release frame, so no actor gets one extra step.
    private bool UpdateTruePurgaeFrozenTime(float step)
    {
        if(!TruePurgaeTimeFrozen)return false;
        float previous=trueStopClock;
        trueStopClock=Math.Min(TrueStopDuration,trueStopClock+step);
        if((int)(previous/60)<(int)(trueStopClock/60))audio.PlayTimeStopHold();
        int built=Math.Min(TrueStopTotal,Math.Max(0,(int)((trueStopClock-13)/TrueStopBuildInterval)));
        if(built>trueStopBuilt){trueStopBuilt=built;if(built%3==1)audio.Play("timestopstone");}
        if(trueStopClock>=TrueStopDuration)
        {
            trueStopPhase=3;trueStopClock=0;trueStopBuilt=TrueStopTotal;trueStopReleaseFlash=22;
            DisposeTrueStopSurfaces();
            audio.StopTimeStopSounds();audio.Play("timestoprelease");parryWindow=parryCooldown=0;
            sliding=heldKeys.Contains(Keys.Down);
            if(trueStopParryQueued||heldKeys.Contains(Keys.Space)||heldKeys.Contains(Keys.ShiftKey))StartParry();
            trueStopParryQueued=false;bossMessage="시간이 흐른다.";bossShoutTimer=42;
        }
        return true;
    }
    private bool QueueTruePurgaeFrozenAction(Keys key)
    {
        if(!TruePurgaeTimeFrozen)return false;
        if(key is Keys.Space or Keys.ShiftKey)
        {if(trueStopClock>=TrueStopDuration-30)trueStopParryQueued=true;return true;}
        return key is Keys.Up or Keys.Down or Keys.Q;
    }
    private void DrawTruePurgaePortrait(Graphics g,RectangleF rect)
    {
        bool world=state==GameState.Round&&!bossBriefingActive&&!bossDefeatActive;
        var body=world?new RectangleF(rect.X+rect.Width*.5f-137,rect.Y-47,274,274):rect;
        DrawPurgaeActor(g,2,body);
        if(world&&purgaeSkill2Active)DrawPurgaeSkill2Balls(g,bossX+77.5f,bossY+77.5f);
    }
    private void DrawTruePurgaeTimeOverlay(Graphics g)
    {
        if(!TruePurgaeEncounter||trueStopPhase==0)return;
        bool frozen=trueStopPhase==2,released=trueStopPhase==3;
        float t=trueStopClock;
        if(!released){using var dim=new SolidBrush(Color.FromArgb(frozen?55:28,18,16,10));g.FillRectangle(dim,0,104,W,411);}
        // Deliberately basic prototype graphics: plain markers and lines.
        // Illustration and animation belong to the boss, not these attacks.
        if(trueStopStandSprite!=null&&!released)
            g.DrawImage(trueStopStandSprite,new RectangleF(bossX-88,bossY-55,118,118));
        int visible=frozen?trueStopBuilt:released?trueStopStones.Count:0;
        for(int i=0;i<visible;i++)
        {
            var stone=trueStopStones[i];if(stone.Fired)continue;
            using var fill=new SolidBrush(Color.FromArgb(235,47,43,34));using var rim=new Pen(Gold,1.2f);
            g.FillEllipse(fill,stone.X-13,stone.Y-13,26,26);g.DrawEllipse(rim,stone.X-13,stone.Y-13,26,26);
            if(i<3||released&&i<=trueStopReleased+2)Label(g,(i+1).ToString(),new(stone.X-11,stone.Y-29,22,18),9,Gold);
            if(frozen&&i<3){using var aim=new Pen(Color.FromArgb(90,Gold),1){DashStyle=DashStyle.Dash};g.DrawLine(aim,stone.X,stone.Y,trueStopX,trueStopY);}
        }
        DrawTrueStopLanes(g,frozen);
        Label(g,TrueStopPatternName+" · "+(frozen?"시간 정지":released?"시간 재개":"시간 정지 예고"),new(190,118,620,30),18,Gold);
        if(!released)
        {
            Label(g,frozen?$"해제까지 {(TrueStopDuration-t)/60:0.0}초 · 돌탄환 {TrueStopTotal}개":"정지 뒤 사방에서 탄환이 날아옵니다",new(190,155,620,24),12,Paper);
            if(frozen&&t>=TrueStopDuration-30)Label(g,"SPACE / SHIFT · 해제 순간 발차기 예약",new(235,185,530,24),12,Gold);
        }
        else Label(g,$"빠르게 연속 반사  {trueStopReleased} / {TrueStopTotal}"+(trueStopPattern>=2?" · 창은 점프 / 슬라이드":""),new(190,155,620,24),12,Paper);
    }
}
