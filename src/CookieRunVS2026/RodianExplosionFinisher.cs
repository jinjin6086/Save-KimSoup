namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private const float RodianExplosionFrame=48;
    private bool rodianDefeatExplosionPlayed;
    private const float RodianPantStart=12,RodianPantEnd=108;
    private const float RodianExitLineFrame=132,RodianExitRunFrame=198,RodianExitClearFrame=366;
    private float rodianExitStartX,rodianExitAirOffset,rodianExitClearClock;
    private int rodianExitFootstrike;
    private bool rodianExitPantPlayed,rodianExitLinePlayed,rodianExitClearPlayed;
    private float RodianExitBaseFrame=>lastStandResolved?0:RodianExplosionFrame+180;
    private float RodianExitClock=>bossDefeatClock-RodianExitBaseFrame;
    private bool RodianExitLineVisible=>RodianExitClock>=RodianExitLineFrame&&RodianExitClock<RodianExitRunFrame+54;

    private void ResetRodianDeparture()
    {
        rodianExitStartX=px;rodianExitAirOffset=Math.Min(0,py+PlayerHeight-GroundY);
        rodianExitPantPlayed=rodianExitLinePlayed=rodianExitClearPlayed=false;rodianExitClearClock=0;
        rodianExitFootstrike=(int)(animationClock/10);sliding=false;vy=0;
    }
    private void UpdateRodianDeparture(float step)
    {
        float t=RodianExitClock;if(t<0)return;
        py=GroundY-PlayerHeight+rodianExitAirOffset*(1-SmoothUnit(t/28));onGround=t>=28;
        if(t>=RodianPantStart&&!rodianExitPantPlayed){rodianExitPantPlayed=true;audio.Play("exitpant");}
        if(t>=RodianExitLineFrame&&!rodianExitLinePlayed){rodianExitLinePlayed=true;audio.Play("select");}
        if(t>=RodianExitRunFrame&&px<W+140)
        {
            float speed=7.2f*SmoothUnit((t-RodianExitRunFrame)/18);
            px+=speed*step;animationClock+=step*1.15f;
            int strike=(int)(animationClock/10);
            if(strike!=rodianExitFootstrike)
            {
                rodianExitFootstrike=strike;
                if(px<W+45&&audio.ActiveVoices<6)audio.Play($"step2{strike%2}");
            }
        }
        if(t>=RodianExitClearFrame&&px>W+70&&!rodianExitClearPlayed)
        {
            rodianExitClearPlayed=true;rodianExitClearClock=bossDefeatClock;
            audio.SetFinaleMix(1);audio.Play("round");
        }
    }

    private void DrawRodianExplosionFinisher(Graphics g)
    {
        DrawArenaGround(g);
        using(var dim=new SolidBrush(Color.FromArgb(175,3,5,10)))g.FillRectangle(dim,0,0,W,H);
        float t=bossDefeatClock;
        float x=defeatX-17,y=defeatY-3,size=185,cx=x+size*.50f,cy=y+size*.48f;
        if(!lastStandResolved)
        {
            float anticipation=SmoothUnit(t/RodianExplosionFrame);
            float vanish=SmoothUnit((t-RodianExplosionFrame)/20);
            if(defeatPortrait!=null&&vanish<1)
                DrawDefeatImage(g,finisherShadowPortrait??defeatPortrait,new(x,y,size,size),1-vanish);
            if(t<RodianExplosionFrame)
            {
                DrawLaserBloom(g,cx,cy,18+anticipation*32,26+anticipation*44,ClashPink,anticipation*.78f);
                DrawLaserBloom(g,cx,cy,4+anticipation*8,5+anticipation*14,Color.FromArgb(237,249,255),anticipation*.8f);
            }
            else
            {
                float p=Math.Clamp((t-RodianExplosionFrame)/110,0,1);
                DrawApotheosisDetonation(g,cx,cy,p,1);
                DrawFinaleDebris(g,cx,cy,p,1-SmoothUnit((p-.35f)/.65f));
            }
            DrawFinisherImpact(g,cx,cy,t);
        }
        // QTE already consumes the actual boss in its frame-126 explosion.
        // This tail never draws the portrait again, avoiding a second death.
        float ash=lastStandResolved?t+95:t-RodianExplosionFrame;
        if(ash>=0)DrawRodianAfterglow(g,cx,cy,ash);
        DrawRodianDeparturePlayer(g);
        if(RodianExitClock>=RodianPantStart&&RodianExitClock<RodianPantEnd)
        {
            float fade=SmoothUnit((RodianExitClock-RodianPantStart)/8)*(1-SmoothUnit((RodianExitClock-RodianPantEnd+14)/14));
            Label(g,"헉… 헉…",new(px-29,GroundY-185,175,35),20,Color.FromArgb((int)(220*fade),Paper));
        }
        if(RodianExitLineVisible)
        {
            const string line="저기 있다!";
            float clock=RodianExitClock-RodianExitLineFrame;
            int characters=Math.Clamp((int)(clock/3)+1,0,line.Length);
            float alpha=Math.Min(1,clock/5)*(1-SmoothUnit((RodianExitClock-RodianExitRunFrame-36)/18));
            float sx=Math.Clamp(px-35,12,W-210),sy=GroundY-190;
            using(var back=new SolidBrush(Color.FromArgb((int)(135*alpha),2,4,7)))g.FillRectangle(back,sx,sy,190,38);
            Label(g,line[..characters],new(sx,sy,190,38),23,Color.FromArgb((int)(255*alpha),Paper));
        }
        float clear=rodianExitClearPlayed?SmoothUnit((t-rodianExitClearClock)/24):0;
        if(clear>0)
        {
            SoulsHeading(g,"STAGE CLEAR",new(150,220,700,73),49,Color.FromArgb((int)(255*clear),Gold));
            DrawSoulsRule(g,330,311,340);
            Label(g,"라운드 4 · 강로디언 처치",new(150,332,700,31),17,Color.FromArgb((int)(235*clear),Paper));
        }
        using(var bars=new SolidBrush(Color.FromArgb(255,2,3,5))){g.FillRectangle(bars,0,0,W,45);g.FillRectangle(bars,0,H-45,W,45);}
        if(t>=BossDefeatSkipFrame&&lastStandPhase!=7)Label(g,"ENTER / SPACE  다음",new(738,562,240,27),11,Steel,true,false);
    }
    private void DrawRodianDeparturePlayer(Graphics g)
    {
        float t=RodianExitClock;
        if(t<0){DrawPlayer(g);return;}
        float footY=py+PlayerHeight;
        using(var floor=new SolidBrush(Color.FromArgb(125,0,0,0)))g.FillEllipse(floor,px-17,footY-3,106,9);
        if(selectedCookie!=0){DrawSimpleCookie(g,(int)px,(int)py,PlayerWidth,PlayerHeight);return;}
        if(t<RodianExitRunFrame)
        {
            if(t<10&&lastStandResolved&&finaleHeroPoses.Length>2)
                DrawPlayerSprite(g,finaleHeroPoses[2],new(px-40,footY-143,143,143),ClashBlue,1);
            else if(RodianExitClock<RodianPantEnd+12&&pantingPoses.Length>0)
            {
                int pose=((int)(Math.Max(0,RodianExitClock-RodianPantStart)/48*pantingPoses.Length))%pantingPoses.Length;
                DrawPlayerSprite(g,pantingPoses[pose],new(px-17,footY-143,96,143),Mint,1);
            }
            else if(endingIdle!=null)DrawPlayerSprite(g,endingIdle,new(px-17,footY-143,96,143),Mint,1);
            else if(motionFrames.Length>0)DrawPlayerSprite(g,motionFrames[0],new(px-24,footY-143,110,143),Mint,1);
            return;
        }
        float bob=MathF.Sin(animationClock*.58f)*1.7f;
        if(motionFrames.Length>3)DrawAnimatedPlayer(g,new Rectangle((int)px-24,(int)(footY-143+bob),110,143));
        if(!reducedEffects&&px<W+80)
            for(int i=0;i<8;i++)
            {
                float p=(animationClock*.065f+i*.137f)%1;
                g.FillEllipse(ParticleBrush(Color.FromArgb(138,128,116),(int)(90*(1-p))),px-14-p*43,footY-3-MathF.Sin(p*MathF.PI)*6,2+p*3,1+p*2);
            }
    }
    private void DrawRodianAfterglow(Graphics g,float x,float y,float age)
    {
        float fade=1-SmoothUnit(age/170);if(fade<=0)return;
        for(int i=0;i<(reducedEffects?12:58);i++)
        {
            float z=Scatter01(i*97+9),angle=Scatter01(i*31+4)*MathF.Tau;
            float travel=age*(.38f+z*.95f),xx=x+MathF.Cos(angle)*travel,yy=y+MathF.Sin(angle)*travel*.7f+age*age*.0015f;
            Color color=i%3==0?ClashPink:i%5==0?Color.FromArgb(229,248,255):ClashBlue;
            float size=.5f+z*1.1f;
            g.FillEllipse(ParticleBrush(color,(int)(180*fade*(.3f+z*.7f))),xx,yy,size,size);
        }
    }
}
