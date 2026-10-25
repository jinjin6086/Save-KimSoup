using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly Queue<AugmentType> augmentBag = new();
    private float resonance, resonanceGain=1, guardBonus, shockwavePower;
    private float skillFxTimer, perfectFxTimer, shockCooldown, gateCooldown=420, gateTimer;
    private bool hasGate, restartingCheckpoint;
    private float encounterTime, warningTimer, incomingWarning;
    private string encounterMessage="", lastReward="", transitionReward="";
    private readonly List<ParrySlice> parrySlices = new();
    private RunCheckpoint? runCheckpoint;
    private float bossRecoil;
    private bool gamePaused;

    public void PauseForFocusLoss()
    {
        ReleaseInputs();
        if(state==GameState.Round||state==GameState.RoundTransition)SetPaused(true);
    }

    private void SetPaused(bool paused)
    {
        gamePaused=paused;sliding=false;audio.StopSliding();slideSoundActive=false;audio.SetPaused(paused);
    }

    private void DrawPause(Graphics g)
    {
        if(!gamePaused||renderingScene)return;
        DrawSoulsMenuBase(g,.96f);
        Label(g,"PAUSED",new(200,101,600,25),12,Gold,true,false);
        SoulsHeading(g,"일시정지",new(150,146,700,60),35,Paper);
        DrawSoulsRule(g,320,231,360);
        Label(g,"P    게임 계속",new(200,253,600,44),22,Gold);
        Label(g,"↑ 2단 점프   ↓ 슬라이드   SPACE / SHIFT 발차기   Q 능력",new(100,323,800,35),15,Paper,true,false);
        Label(g,$"Q  {ActiveName}   ·   특성  {PassiveName}",new(100,374,800,32),14,Steel,true,false);
        Label(g,$"발차기 {GetParryCooldownSeconds():0.0}초  ·  반사 범위 +{parryRangeBonus:0}  ·  반사 적중 시 체력 {lifeStealPercent*100:0}% 회복",new(100,416,800,26),12,Gold,true,false);
        Label(g,"M 소리  ·  F 효과 강도  ·  ESC 종료",new(100,463,800,28),12,Steel,true,false);
    }
    private sealed record RunCheckpoint(float ParryCd,float Leech,float Range,float Gain,float Guard,float Shock,int Score,AugmentType[] Bag);
    private sealed class ParrySlice { public float X,Y,Angle,Life; public bool Perfect; }

    private void PrepareCheckpoint()
    {
        if(restartingCheckpoint){restartingCheckpoint=false;return;}
        SavePremiumCheckpoint();
        runCheckpoint=new(parryCooldownReduction,lifeStealPercent,parryRangeBonus,resonanceGain,guardBonus,shockwavePower,score,augmentBag.ToArray());
    }

    private void RetryRound()
    {
        if(runCheckpoint is { } c)
        {
            parryCooldownReduction=c.ParryCd;lifeStealPercent=c.Leech;parryRangeBonus=c.Range;
            resonanceGain=c.Gain;guardBonus=c.Guard;shockwavePower=c.Shock;score=c.Score;
            augmentBag.Clear();foreach(var type in c.Bag)augmentBag.Enqueue(type);
        }
        RestorePremiumCheckpoint();
        restartingCheckpoint=true;StartRound(round);sceneFadeTimer=SceneFadeFrames;audio.Play("round");
    }

    private void ResetRebuild(bool newRun)
    {
        SetPaused(false);
        resonance=0;skillFxTimer=perfectFxTimer=shockCooldown=gateTimer=encounterTime=incomingWarning=0;
        hasGate=false;gateCooldown=round==2?390:690;
        warningTimer=165;encounterMessage=round switch{1=>"↑  두 번 점프  ·  ↓  슬라이드",2=>"날아오는 탄환을 반사하세요",3=>"초록 구슬 → 각성 후 화상",5=>"바닥·붉은 줄은 피하세요",_=>"‘돼!’에 맞춰 레이저를 반사하세요"};
        parrySlices.Clear();bossRecoil=0;ResetKickCombat();
        if(newRun){resonanceGain=1;guardBonus=shockwavePower=0;augmentBag.Clear();}
    }

    private AugmentType DrawRandomAugment()
    {
        if(augmentBag.Count==0)
        {
            var choices=Enum.GetValues<AugmentType>();
            for(int i=choices.Length-1;i>0;i--){int j=rng.Next(i+1);(choices[i],choices[j])=(choices[j],choices[i]);}
            foreach(var type in choices)augmentBag.Enqueue(type);
        }
        return augmentBag.Dequeue();
    }

    private void GrantRandomAugment()
    {
        AugmentType type=DrawRandomAugment();
        switch(type)
        {
            case AugmentType.ParryCooldownReduction:parryCooldownReduction=Math.Min(48,parryCooldownReduction+12);break;
            case AugmentType.LifeSteal:lifeStealPercent=Math.Min(.17f,lifeStealPercent+.02f);break;
            case AugmentType.ParryRange:parryRangeBonus=Math.Min(70,parryRangeBonus+14);break;
            case AugmentType.Resonance:resonanceGain=Math.Min(2,resonanceGain+.2f);break;
            case AugmentType.LandingPulse:shockwavePower=Math.Min(3,shockwavePower+1);break;
            case AugmentType.Recovery:guardBonus=Math.Min(24,guardBonus+6);break;
        }
        score+=250;augmentCountdown=AugmentInterval;augmentFlashTimer=190;
        lastReward=GetAugmentName(type);augmentNotice="랜덤 증강  ·  "+lastReward;
        audio.Play("reward");Burst(px+26,py+25,Color.FromArgb(255,208,130),18);
    }

    private void UpdateRebuildFx(float step)
    {
        skillFxTimer=Math.Max(0,skillFxTimer-step);perfectFxTimer=Math.Max(0,perfectFxTimer-step);
        warningTimer=Math.Max(0,warningTimer-step);
        incomingWarning=Math.Max(0,incomingWarning-step);bossRecoil=Math.Max(0,bossRecoil-step);
        for(int i=parrySlices.Count-1;i>=0;i--){parrySlices[i].Life-=step;if(parrySlices[i].Life<=0)parrySlices.RemoveAt(i);}
    }

    private void UpdateRebuild(float step)
    {
        shockCooldown=Math.Max(0,shockCooldown-step);encounterTime+=step;hasGate=false;
    }

    private float GateX=>W+80-Math.Max(0,gateTimer-52)/60*(W+220);
    private bool EncounterBreathing=>!MelodySequenceActive&&!purgaeSkill2Active&&(encounterTime%510)>405;

    private void StartRuleGate(bool blue)
    {
        hasGate=false;
    }

    private void TriggerResonance()
    {
        if(state!=GameState.Round||resonance<100||sceneFadeTimer>0)return;
        resonance=0;skillFxTimer=32;invuln=Math.Max(invuln,40);audio.Play("reverse");
        int reflected=0;
        foreach(var p in projectiles.OrderBy(p=>Math.Abs(p.X-px)))
            if(!p.IsParried&&Math.Abs(p.X-px)<440&&reflected<6){p.ReflectToBoss();reflected++;}
        obstacles.RemoveAll(o=>Math.Abs(o.X-px)<260);risingObstacles.RemoveAll(o=>Math.Abs(o.X-px)<260);
        if(hasGate){hasGate=false;gateCooldown=540;}
        Burst(px+26,py+38,Mint,45);TriggerCameraShake(4,9);
        // Does not change the random rhythm clock or grant a free rhythm reflection.
        augmentNotice="역류  ·  근처 공격 반사";augmentFlashTimer=70;
    }

    private void RegisterParryStyle(bool perfect)
    {
        resonance=Math.Min(100,resonance+(perfect?34:22)*resonanceGain);
        if(perfect){perfectFxTimer=28;score+=300;}
        parrySlices.Add(new(){X=px+32,Y=py+38,Angle=-.30f+(rng.NextSingle()-.5f)*.45f,Life=perfect?25:20,Perfect=perfect});
        if(parrySlices.Count>8)parrySlices.RemoveAt(0);
        OnPremiumParry();
    }

    private void ApplyLandingPulse()
    {
        if(shockwavePower<=0||shockCooldown>0)return;
        shockCooldown=150;
        float range=85+shockwavePower*30;
        int removed=obstacles.RemoveAll(o=>o.Kind==ObstacleKind.Ground&&Math.Abs(o.X-px)<range);
        removed+=risingObstacles.RemoveAll(o=>Math.Abs(o.X-px)<range);
        if(removed>0){score+=removed*120;resonance=Math.Min(100,resonance+removed*8);Burst(px+26,GroundY-6,Paper,24,2);}
    }

    private void DrawRebuild(Graphics g)
    {
        DrawKickFeedback(g);
        if(perfectFxTimer>0&&counterFxTimer<=0)Label(g,"정확한 반사",new(px-40,py-40,150,22),11,Gold);
        if(skillFxTimer>0)
        {
            float progress=1-skillFxTimer/32;
            using var wave=new Pen(Color.FromArgb((int)(180*(1-progress)),Mint),6*(1-progress)+1);
            float r=30+progress*440;g.DrawEllipse(wave,px+26-r,py+38-r,r*2,r*2);
        }
        if(warningTimer>0&&!MelodySequenceActive&&!(TruePurgaeEncounter&&trueStopPhase!=0))
        {
            Color color=Paper;
            using var shade=new SolidBrush(Color.FromArgb(130,9,15,27));g.FillRoundRectangle(shade,new Rectangle(225,113,550,43),12,12);
            Label(g,encounterMessage,new(237,118,526,32),17,color);
        }
        if(incomingWarning>0&&!hasGate&&!MelodySequenceActive)
        {
            float radius=20+(incomingWarning/24)*22;
            using var aim=new Pen(Color.FromArgb(150,Accent(round)),2);
            g.DrawEllipse(aim,bossX+62-radius,bossY+65-radius,radius*2,radius*2);
        }
    }

    private void SpawnBossFan(int count,float spread,float speed)
    {
        float x=bossX-8,y=bossY+55;
        float aim=MathF.Atan2(py+32-y,px-x);
        for(int i=0;i<count;i++)
        {
            float angle=aim+(i-(count-1)/2f)*spread;
            projectiles.Add(new(x,y,AttackType.BlackBall,29,MathF.Cos(angle)*speed,MathF.Sin(angle)*speed));
        }
        audio.Play("volley");
    }
}








