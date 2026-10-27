using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Own real-time clock: no player physics, damage ticks, Q timers or enemy attacks
    // advance while this encounter owns input. True Purgae keeps its original rules.
    private int isangjinClockPhase,isangjinClockBeat,isangjinCounterShard;
    private float isangjinClockTimer,isangjinClockCooldown=720,isangjinClockFeedback;
    private bool isangjinClockDamage,isangjinClockHitApplied,isangjinClockCounterApplied,isangjinRepelImpactPlayed;
    private Keys isangjinClockLastKey;
    private const float IsangjinClockTellDuration=96;
    private bool IsangjinClockTelegraphActive=>state==GameState.Round&&round==2&&isangjinClockPhase==0&&!bossBriefingActive&&!bossDefeatActive&&!PhaseIntroActive&&isangjinClockCooldown>0&&isangjinClockCooldown<=IsangjinClockTellDuration;
    private Bitmap? isangjinFrozenBackdrop;
    private readonly List<ClockShard> isangjinClockShards=new(64);
    private Bitmap[] isangjinShardSprites=Array.Empty<Bitmap>(),isangjinRepelPoses=Array.Empty<Bitmap>();
    private static readonly float[] IsangjinBeatTimes={36,60,82.5f,106.5f,127.5f,150};
    private readonly Keys[] IsangjinBeatKeys=new Keys[6];
    private static readonly int[] IsangjinClockPatterns=Enumerable.Range(0,64).Where(n=>System.Numerics.BitOperations.PopCount((uint)n)==3).ToArray();
    private int isangjinPreviousPattern=-1;
    private const float IsangjinBeatWindow=5;
    private static readonly Color ClockBlue=Color.FromArgb(155,218,245),ClockGold=Color.FromArgb(239,195,119);
    private bool IsangjinClockActive=>state==GameState.Round&&round==2&&isangjinClockPhase>0;
    private sealed class ClockShard
    {
        public PointF Source,Frozen,Exit;
        public float Angle,Length,Reveal;
        public int Sprite;
    }
    private void LoadIsangjinClockAssets()
    {
        string file=AssetPath("IsangjinShardAtlas.png");
        if(File.Exists(file))
        {
            using var sheet=new Bitmap(file);int cw=sheet.Width/4;
            isangjinShardSprites=new Bitmap[4];
            for(int i=0;i<4;i++)isangjinShardSprites[i]=ExtractMainSprite(sheet,new(i*cw,0,cw,sheet.Height),192,96);
        }
        file=AssetPath("PlayerClockMotion6.png");
        if(File.Exists(file))
        {
            using var sheet=new Bitmap(file);int cw=sheet.Width/3,ch=sheet.Height/2;
            isangjinRepelPoses=new Bitmap[6];
            // Remove disconnected generation residue once; feet stay bottom-anchored.
            for(int i=0;i<6;i++)
            {
                isangjinRepelPoses[i]=ExtractMainSprite(sheet,new(i%3*cw,i/3*ch,cw,ch),256,256);
            }
        }
    }
    private void DisposeIsangjinClockAssets()
    {
        isangjinFrozenBackdrop?.Dispose();isangjinFrozenBackdrop=null;
        foreach(var b in isangjinShardSprites)b.Dispose();foreach(var b in isangjinRepelPoses)b.Dispose();
        isangjinShardSprites=isangjinRepelPoses=Array.Empty<Bitmap>();
    }
    private void ResetIsangjinClock()
    {
        if(isangjinClockPhase>0)audio.StopTimeStopSounds();
        isangjinClockPhase=isangjinClockBeat=0;isangjinClockTimer=isangjinClockFeedback=0;isangjinClockCooldown=720;
        isangjinClockDamage=isangjinClockHitApplied=isangjinClockCounterApplied=false;isangjinClockShards.Clear();
        isangjinRepelImpactPlayed=false;
        isangjinFrozenBackdrop?.Dispose();isangjinFrozenBackdrop=null;
    }
    private void BeginIsangjinClock()
    {
        if(state!=GameState.Round||round!=2||bossBriefingActive||bossDefeatActive||PhaseIntroActive||IsangjinClockActive)return;
        isangjinClockPhase=1;isangjinClockTimer=0;isangjinClockBeat=0;isangjinClockFeedback=0;
        isangjinClockHitApplied=isangjinClockCounterApplied=false;isangjinClockLastKey=Keys.None;
        isangjinRepelImpactPlayed=false;
        RandomizeIsangjinClockPattern();
        // The rhythm sequence is exclusive: no stale contact attack may hit on release.
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        CancelAbilityEchoes();CancelCinderFlight();audio.StopSliding();ReleaseInputs();
        sliding=false;parryWindow=parryFxTimer=hitStopTimer=cinematicTimer=warningTimer=0;
        cameraShakeTimer=screenFlashTimer=0;gravityMirrorTimer=gravityCue=0;
        BuildIsangjinClockShards();
        isangjinFrozenBackdrop?.Dispose();float scale=Math.Clamp(nativeArtScale,1,2);
        isangjinFrozenBackdrop=new Bitmap((int)(W*scale),(int)(H*scale),PixelFormat.Format32bppRgb);
        using(var cg=Graphics.FromImage(isangjinFrozenBackdrop)){cg.ScaleTransform(scale,scale);DrawBackground(cg);DrawArenaGround(cg);}
        DesaturateScene(isangjinFrozenBackdrop);audio.Play("timestopstart");
    }
    private void RandomizeIsangjinClockPattern()
    {
        int next=rng.Next(IsangjinClockPatterns.Length-(isangjinPreviousPattern>=0?1:0));
        if(isangjinPreviousPattern>=0&&next>=isangjinPreviousPattern)next++;
        isangjinPreviousPattern=next;int mask=IsangjinClockPatterns[next];
        for(int i=0;i<IsangjinBeatKeys.Length;i++)IsangjinBeatKeys[i]=(mask&(1<<i))!=0?Keys.W:Keys.E;
    }
    private void BuildIsangjinClockShards()
    {
        isangjinClockShards.Clear();var center=new PointF(px+PlayerWidth*.5f,py+PlayerHeight*.45f);
        PointF boss=new(bossX+67,bossY+67);float nearest=float.MaxValue;
        for(int i=0;i<64;i++)
        {
            float u=(float)rng.NextDouble(),v=(float)rng.NextDouble();
            // Spread each source region rather than clamping a radial burst onto
            // the left edge / ground and stacking many shards in one pixel row.
            PointF frozen=(i%5) switch
            {
                0=>new(24+u*Math.Max(30,center.X-90),center.Y-90+v*150),
                1=>new(center.X+140+u*(W-center.X-190),center.Y-110+v*170),
                2=>new(center.X-60+u*120,115+v*Math.Max(30,center.Y-235)),
                3=>new(28+u*Math.Max(30,center.X-105),130+v*Math.Max(30,center.Y-260)),
                _=>new(center.X+125+u*390,130+v*Math.Max(30,center.Y-265))
            };
            frozen.Y=Math.Clamp(frozen.Y,100,GroundY-30);
            float a=MathF.Atan2(frozen.Y-center.Y,frozen.X-center.X);
            PointF origin=new(center.X+MathF.Cos(a)*1050,center.Y+MathF.Sin(a)*1050);
            PointF exit=new(center.X+MathF.Cos(a)*1550,center.Y+MathF.Sin(a)*1550);
            var shard=new ClockShard{Source=origin,Frozen=frozen,Exit=exit,Angle=MathF.Atan2(center.Y-frozen.Y,center.X-frozen.X)*180/MathF.PI,Length=28+(float)rng.NextDouble()*33,Sprite=i%4,Reveal=i*.9f};
            isangjinClockShards.Add(shard);
            float d=(frozen.X-boss.X)*(frozen.X-boss.X)+(frozen.Y-boss.Y)*(frozen.Y-boss.Y);
            if(d<nearest){nearest=d;isangjinCounterShard=i;}
        }
    }
    private bool UpdateIsangjinClock(float step)
    {
        if(!IsangjinClockActive)
        {
            if(state!=GameState.Round||round!=2||bossBriefingActive||bossDefeatActive||PhaseIntroActive||sceneFadeTimer>0)return false;
            if(practiceActive&&practicePreset is not (1 or 13))return false;
            if(hitStopTimer>0||cinematicTimer>0)return false;
            isangjinClockCooldown-=step;
            if(isangjinClockCooldown>0)return false;
            BeginIsangjinClock();return IsangjinClockActive;
        }
        isangjinClockTimer+=step;isangjinClockFeedback=Math.Max(0,isangjinClockFeedback-step);
        switch(isangjinClockPhase)
        {
            case 1:if(isangjinClockTimer>=66){isangjinClockPhase=2;isangjinClockTimer=0;audio.Play("timestopstone");}break;
            case 2:if(isangjinClockTimer>=108){isangjinClockPhase=3;isangjinClockTimer=0;}break;
            case 3:if(isangjinClockTimer>=84){isangjinClockPhase=4;isangjinClockTimer=0;ReleaseInputs();}break;
            case 4:
                if(isangjinClockBeat<IsangjinBeatTimes.Length&&isangjinClockTimer>IsangjinBeatTimes[isangjinClockBeat]+IsangjinBeatWindow)FailIsangjinClock();
                break;
            case 5:
                if(isangjinClockTimer>=22&&!isangjinRepelImpactPlayed){isangjinRepelImpactPlayed=true;audio.Play("timestoprelease");audio.Play("perfect");}
                if(isangjinClockTimer>=52&&!isangjinClockCounterApplied){isangjinClockCounterApplied=true;ApplyIsangjinClockCounter();}
                if(isangjinClockTimer>=92&&IsangjinClockActive)FinishIsangjinClock();
                break;
            case 6:
                if(isangjinClockTimer>=24&&!isangjinClockHitApplied)
                {
                    isangjinClockHitApplied=true;isangjinClockDamage=true;
                    try{ReceiveHit(3000,"시간정지 파편",Coral,true);}finally{isangjinClockDamage=false;}
                    if(state!=GameState.Round){ResetIsangjinClock();return true;}
                }
                if(isangjinClockTimer>=84)FinishIsangjinClock();
                break;
        }
        return true;
    }
    private bool HandleIsangjinClockKey(Keys key)
    {
        if(!IsangjinClockActive)return false;
        if(key==Keys.Escape)return false;
        if(isangjinClockPhase!=4||key is not (Keys.W or Keys.E))return true;
        int beat=isangjinClockBeat;
        if(beat>=IsangjinBeatTimes.Length)return true;
        if(key!=IsangjinBeatKeys[beat]||Math.Abs(isangjinClockTimer-IsangjinBeatTimes[beat])>IsangjinBeatWindow)
        {FailIsangjinClock();return true;}
        isangjinClockLastKey=key;isangjinClockBeat++;isangjinClockFeedback=18;audio.Play("clashtap");
        if(isangjinClockBeat==IsangjinBeatTimes.Length)
        {
            isangjinClockPhase=5;isangjinClockTimer=0;ReleaseInputs();
        }
        return true;
    }
    private void FailIsangjinClock()
    {
        if(isangjinClockPhase!=4)return;
        isangjinClockPhase=6;isangjinClockTimer=0;ReleaseInputs();audio.Play("timestoprelease");
    }
    private void ApplyIsangjinClockCounter()
    {
        if(round!=2||state!=GameState.Round)return;
        // A single returning shard deals exactly one damage; no Q conversion,
        // vulnerability multiplier, clone modifier, lifesteal or parry passive proc.
        bossDamage=Math.Min(bossHitRequired,bossDamage+1);bossRecoil=12;
        Burst(bossX+67,bossY+67,ClockBlue,24);audio.Play("impact");
        if(bossDamage>=bossHitRequired)
        {
            if(practiceActive){practiceClears++;bossDamage=0;bossParryHits=0;ResetCinderRound();}
            else{FinishIsangjinClock();BeginBossDefeat();}
        }
    }
    private void FinishIsangjinClock()
    {
        isangjinClockPhase=0;isangjinClockTimer=0;isangjinClockShards.Clear();
        isangjinClockCooldown=practiceActive&&practicePreset==13?180:1080;
        isangjinFrozenBackdrop?.Dispose();isangjinFrozenBackdrop=null;audio.StopTimeStopSounds();
        attackTimer=0;obstacleSpawnTimer=Math.Max(obstacleSpawnTimer,90);invuln=Math.Max(invuln,48);
        parryWindow=parryFxTimer=hitStopTimer=cameraShakeTimer=0;ReleaseInputs();
    }
    private void DrawIsangjinClockScene(Graphics g)
    {
        if(isangjinFrozenBackdrop!=null)g.DrawImage(isangjinFrozenBackdrop,new Rectangle(0,0,W,H));
        using(var wash=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(100,8,13,20),Color.FromArgb(165,5,8,14),90f))g.FillRectangle(wash,0,0,W,H);
        DrawBoss(g);DrawIsangjinRepelPlayer(g);DrawIsangjinShards(g);
        float t=isangjinClockTimer;
        if(isangjinClockPhase==1)
        {
            float k=Math.Clamp(t/66,0,1),r=30+k*820;
            using var wave=new Pen(Color.FromArgb((int)(190*(1-k)),ClockBlue),2+7*(1-k));
            g.DrawEllipse(wave,bossX+67-r,bossY+67-r,r*2,r*2);
            Label(g,"시간정지",new(350,191,300,50),30,Paper);
        }
        if(isangjinClockPhase is 2 or 3 or 4)DrawIsangjinClockRhythm(g);
        if(isangjinClockPhase==5)
        {
            DrawIsangjinRepelImpact(g);
            Label(g,t<22?"버텨낸다":"파편 반격!",new(350,190,300,36),t<22?20:26,ClockBlue);
        }
        if(isangjinClockPhase==6)
        {
            Label(g,t<24?"시간이 다시 흐른다":"실패  −3,000",new(300,210,400,42),25,Coral);
            if(t>=24){float a=1-Math.Clamp((t-24)/45,0,1);using var red=new SolidBrush(Color.FromArgb((int)(70*a),Coral));g.FillRectangle(red,0,0,W,H);}
        }
        using(var bars=new SolidBrush(Color.FromArgb(210,4,7,11))){g.FillRectangle(bars,0,99,W,28);g.FillRectangle(bars,0,548,W,52);}
        Label(g,"이상진  /  시간정지",new(28,101,285,23),12,Gold,false);
        Label(g,isangjinClockPhase is 3 or 4?"왼쪽은 W · 오른쪽은 E  /  중앙 시계와 겹칠 때 한 번씩":"정지된 시간 속에서는 발차기 대신 W / E 리듬으로 반격",new(100,557,800,26),14,Paper);
    }
    private void ApplyIsangjinClockWindup(Graphics g,float cx,float cy)
    {
        float k=1-Math.Clamp(isangjinClockCooldown/IsangjinClockTellDuration,0,1);
        float ease=k*k*(3-2*k),r=78+8*ease;
        // A restrained clock winding behind the original sprite makes the cast
        // readable without replacing Isangjin's character artwork.
        using var glow=new Pen(Color.FromArgb((int)(35+45*ease),ClockBlue),7);
        using var rim=new Pen(Color.FromArgb((int)(100+90*ease),ClockGold),1.3f);
        g.DrawEllipse(glow,cx-r,cy-r,r*2,r*2);g.DrawEllipse(rim,cx-r,cy-r,r*2,r*2);
        for(int i=0;i<12;i++)
        {
            float a=i*MathF.Tau/12-MathF.PI/2;
            g.DrawLine(rim,cx+MathF.Cos(a)*(r-6),cy+MathF.Sin(a)*(r-6),cx+MathF.Cos(a)*(r+3),cy+MathF.Sin(a)*(r+3));
        }
        float needle=-MathF.PI/2+ease*MathF.Tau;
        g.DrawLine(rim,cx,cy,cx+MathF.Cos(needle)*(r-13),cy+MathF.Sin(needle)*(r-13));
        g.TranslateTransform(cx-9*ease,cy-13*ease);
        g.RotateTransform(-7*ease);g.ScaleTransform(1-.035f*ease,1+.045f*ease);g.TranslateTransform(-cx,-cy);
    }
    private void DrawIsangjinRepelImpact(Graphics g)
    {
        float t=isangjinClockTimer,cx=px+26,cy=py+35;
        if(t<22)
        {
            float k=Math.Clamp(t/22,0,1);
            using var gather=new Pen(Color.FromArgb((int)(90*k),ClockBlue),1.5f);
            for(int i=0;i<14;i++)
            {
                float a=i*MathF.Tau/14,r=66-44*k;
                g.DrawLine(gather,cx+MathF.Cos(a)*r,cy+MathF.Sin(a)*r*.75f,cx+MathF.Cos(a)*(r+14),cy+MathF.Sin(a)*(r+14)*.75f);
            }
            return;
        }
        for(int j=0;j<3;j++)
        {
            float k=Math.Clamp((t-22-j*5)/30,0,1);if(k<=0||k>=1)continue;
            float r=20+480*(1-MathF.Pow(1-k,2)),alpha=(1-k)*(1-j*.18f);
            using var glow=new Pen(Color.FromArgb((int)(45*alpha),ClockBlue),12-7*k);
            using var wave=new Pen(Color.FromArgb((int)(220*alpha),ClockBlue),3.5f-2*k);
            g.DrawEllipse(glow,cx-r,cy-r*.67f,r*2,r*1.34f);g.DrawEllipse(wave,cx-r,cy-r*.67f,r*2,r*1.34f);
        }
        float burst=Math.Clamp((t-22)/28,0,1),fade=1-burst;
        for(int i=0;i<42;i++)
        {
            float a=i*2.39996f,r=18+(85+i%9*15)*burst;
            float x=cx+MathF.Cos(a)*r,y=cy+MathF.Sin(a)*r*.72f;
            using var spark=new Pen(Color.FromArgb((int)(210*fade),i%3==0?Color.White:ClockBlue),i%4==0?2.2f:1.2f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(spark,x,y,x+MathF.Cos(a)*(5+18*fade),y+MathF.Sin(a)*(5+18*fade));
        }
        if(t<32)
        {
            using var cut=new Pen(Color.FromArgb((int)(230*(1-(t-22)/10)),Color.White),2);
            g.DrawLine(cut,cx-70,cy+14,cx+94,cy-14);
        }
    }
    private void DrawIsangjinClockTelegraph(Graphics g)
    {
        if(!IsangjinClockTelegraphActive)return;
        PanelSurface(g,new(310,210,380,64),ClockGold);
        Label(g,$"시간정지 준비  ·  {isangjinClockCooldown/60:0.0}초",new(320,214,360,28),18,ClockGold);
        Label(g,"정지 후 W / E 6회  ·  빠른 시계 리듬",new(320,245,360,22),12,Paper);
        float width=354*Math.Clamp(1-isangjinClockCooldown/IsangjinClockTellDuration,0,1);
        using var bar=new SolidBrush(ClockBlue);g.FillRectangle(bar,323,270,width,2);
    }
    private void DrawIsangjinShards(Graphics g)
    {
        float t=isangjinClockTimer;var target=new PointF(px+26,py+35);
        for(int i=0;i<isangjinClockShards.Count;i++)
        {
            ClockShard s=isangjinClockShards[i];PointF p=s.Frozen;float alpha=1,angle=s.Angle;
            if(isangjinClockPhase==1)continue;
            if(isangjinClockPhase==2)
            {
                float k=Math.Clamp((t-s.Reveal)/46,0,1);if(k<=0)continue;
                k=1-MathF.Pow(1-k,3);p=LerpClockPoint(s.Source,s.Frozen,k);alpha=Math.Min(1,k*3);
            }
            else if(isangjinClockPhase==5)
            {
                float k=Math.Clamp((t-22)/(i==isangjinCounterShard?30:43),0,1);
                PointF dest=i==isangjinCounterShard?new(bossX+67,bossY+67):s.Exit;
                p=LerpClockPoint(s.Frozen,dest,k*k);angle=MathF.Atan2(dest.Y-s.Frozen.Y,dest.X-s.Frozen.X)*180/MathF.PI;
                if(i==isangjinCounterShard&&k>=1)continue;
                alpha=1-Math.Clamp((k-.8f)*5,0,1);
            }
            else if(isangjinClockPhase==6)
            {
                float k=Math.Clamp(t/24,0,1);p=LerpClockPoint(s.Frozen,target,k*k*k);alpha=1-Math.Clamp((t-22)/8,0,1);
            }
            if(alpha<=0)continue;
            if(isangjinClockPhase is 5 or 6)
            {
                float a=angle*MathF.PI/180;using var trail=new Pen(Color.FromArgb((int)(85*alpha),ClockBlue),1.5f);
                g.DrawLine(trail,p.X-MathF.Cos(a)*42,p.Y-MathF.Sin(a)*42,p.X,p.Y);
                if(isangjinClockPhase==5&&i==isangjinCounterShard&&t>=22)
                {
                    using var counter=new Pen(Color.FromArgb((int)(230*alpha),ClockGold),4){StartCap=LineCap.Round,EndCap=LineCap.Round};
                    g.DrawLine(counter,p.X-MathF.Cos(a)*80,p.Y-MathF.Sin(a)*80,p.X,p.Y);
                }
            }
            var saved=g.Save();g.TranslateTransform(p.X,p.Y);g.RotateTransform(angle);
            if(isangjinShardSprites.Length==4)DrawDefeatImage(g,isangjinShardSprites[s.Sprite],new(-s.Length/2,-s.Length/4,s.Length,s.Length/2),alpha);
            else{using var fill=new SolidBrush(Color.FromArgb((int)(235*alpha),ClockBlue));g.FillPolygon(fill,new[]{new PointF(s.Length/2,0),new PointF(-s.Length/2,-5),new PointF(-s.Length*.3f,5)});}
            g.Restore(saved);
        }
    }
    private static PointF LerpClockPoint(PointF a,PointF b,float t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
    private void DrawIsangjinRepelPlayer(Graphics g)
    {
        if(selectedCookie==1&&wolfPoses.Length==12){DrawWolfRepel(g);return;}
        if(selectedCookie!=0||isangjinRepelPoses.Length!=6)
        {
            DrawPlayer(g);
            if(isangjinClockPhase==5)
            {
                float open=Math.Clamp((isangjinClockTimer-20)/12,0,1);open=open*open*(3-2*open);
                float cx=px+26,cy=py+36;
                using var rim=new Pen(Color.White,9){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
                using var arm=new Pen(cloneActive?Color.FromArgb(64,216,106):Color.FromArgb(255,105,185),5){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
                PointF[] left={new(cx-17,cy),new(cx-8-open*26,cy+8-open*9),new(cx+8-open*53,cy-8)};
                PointF[] right={new(cx+17,cy),new(cx+8+open*26,cy+8-open*9),new(cx-8+open*53,cy-8)};
                g.DrawLines(rim,left);g.DrawLines(rim,right);g.DrawLines(arm,left);g.DrawLines(arm,right);
            }
            return;
        }
        float pose=0;
        if(isangjinClockPhase==5)
        {
            float t=isangjinClockTimer;
            pose=t<20?1+t/20:t<28?2+(t-20)/8*2:4+Math.Clamp((t-28)/12,0,1);
        }
        else if(isangjinClockPhase is 3 or 4)pose=1+MathF.Sin(isangjinClockTimer*.025f)*.06f;
        int a=Math.Clamp((int)pose,0,5),b=Math.Min(5,a+1);float mix=pose-a;
        var rect=new RectangleF(px+26-45,py+PlayerHeight-87,90,90);
        if(isangjinClockPhase==5)
        {
            float t=isangjinClockTimer,scale=t<22?1-.055f*Math.Clamp(t/22,0,1):1+.065f*MathF.Exp(-(t-22)/8);
            rect=new(rect.X-(scale-1)*45,py+PlayerHeight+3-90*scale,90*scale,90*scale);
        }
        DrawPlayerSprite(g,isangjinRepelPoses[a],rect,ClockBlue,1);
        if(mix>0&&a!=b)DrawPlayerSprite(g,isangjinRepelPoses[b],rect,ClockBlue,mix);
    }
    private void DrawIsangjinClockRhythm(Graphics g)
    {
        const float cx=500,cy=300,radius=50;
        using(var vignette=new LinearGradientBrush(new Rectangle(170,193,660,217),Color.FromArgb(0,7,12,18),Color.FromArgb(215,7,12,18),0f))
        {vignette.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(0,7,12,18),Color.FromArgb(225,7,12,18),Color.FromArgb(0,7,12,18)},Positions=new[]{0f,.5f,1f}};g.FillRectangle(vignette,170,193,660,217);}
        if(isangjinClockPhase is 2 or 3)
        {
            Label(g,"시계가 겹치는 순간",new(300,182,400,32),22,Paper);
            Label(g,"왼쪽 W    ·    오른쪽 E",new(300,224,400,26),16,ClockGold);
        }
        else Label(g,$"시간의 틈  {isangjinClockBeat} / {IsangjinBeatTimes.Length}",new(350,188,300,30),19,Paper);
        using(var face=new SolidBrush(Color.FromArgb(225,7,11,17)))g.FillEllipse(face,cx-radius,cy-radius,radius*2,radius*2);
        using(var rim=new Pen(Color.FromArgb(195,Gold),1.5f)){g.DrawEllipse(rim,cx-radius-7,cy-radius-7,(radius+7)*2,(radius+7)*2);g.DrawEllipse(rim,cx-radius+3,cy-radius+3,(radius-3)*2,(radius-3)*2);}
        for(int i=0;i<60;i++)
        {
            float a=i*MathF.Tau/60-MathF.PI/2;float r0=radius+(i%5==0?0:4),r1=radius+7;
            using var tick=new Pen(Color.FromArgb(i%5==0?200:80,Gold),i%5==0?1.6f:.8f);
            g.DrawLine(tick,cx+MathF.Cos(a)*r0,cy+MathF.Sin(a)*r0,cx+MathF.Cos(a)*r1,cy+MathF.Sin(a)*r1);
        }
        float hand=(isangjinClockPhase==4?isangjinClockTimer:0)*.07f-MathF.PI/2;
        using(var hour=new Pen(Color.FromArgb(100,ClockGold),2))g.DrawLine(hour,cx,cy,cx+MathF.Cos(hand)*31,cy+MathF.Sin(hand)*31);
        using(var divide=new Pen(Color.FromArgb(110,Gold),1))g.DrawLine(divide,cx,cy-30,cx,cy+30);
        Label(g,"W",new(cx-40,cy-16,38,32),23,isangjinClockFeedback>0&&isangjinClockLastKey==Keys.W?Color.White:ClockBlue);
        Label(g,"E",new(cx+2,cy-16,38,32),23,isangjinClockFeedback>0&&isangjinClockLastKey==Keys.E?Color.White:ClockGold);
        if(isangjinClockPhase==4)
        {
            for(int i=isangjinClockBeat;i<IsangjinBeatTimes.Length;i++)
            {
                float until=IsangjinBeatTimes[i]-isangjinClockTimer;
                if(until>42||until < -IsangjinBeatWindow)continue;
                bool left=IsangjinBeatKeys[i]==Keys.W;float x=cx+(left?-1:1)*Math.Max(0,until)*10.2f;
                Color color=left?ClockBlue:ClockGold;float opacity=Math.Clamp((42-until)/10,0,1);
                using var glow=new Pen(Color.FromArgb((int)(42*opacity),color),10);
                using var bright=new Pen(Color.FromArgb((int)(235*opacity),color),3);
                float start=left?90:-90;g.DrawArc(glow,x-radius,cy-radius,radius*2,radius*2,start,180);g.DrawArc(bright,x-radius,cy-radius,radius*2,radius*2,start,180);
                Label(g,left?"W":"E",new(x-12,cy+65,24,25),15,color);
            }
        }
        if(isangjinClockFeedback>0)
        {
            float k=1-isangjinClockFeedback/18;using var feedback=new Pen(Color.FromArgb((int)(200*(1-k)),ClockBlue),2);
            g.DrawEllipse(feedback,cx-radius-15*k,cy-radius-15*k,(radius+15*k)*2,(radius+15*k)*2);
        }
        for(int i=0;i<IsangjinBeatTimes.Length;i++)
        {
            float x=500-(IsangjinBeatTimes.Length-1)*9+i*18;using var dot=new SolidBrush(i<isangjinClockBeat?ClockBlue:Color.FromArgb(80,Gold));g.FillEllipse(dot,x,411,6,6);
        }
        PanelSurface(g,new(257,438,486,35),Gold);
        Label(g,"발차기 불가  ·  잘못 누르거나 놓치면 3,000 피해",new(265,442,470,26),13,Steel);
    }
}
