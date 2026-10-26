using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool bossBriefingActive,bossDefeatActive;
    private int briefingRound,storyDepartureFoot=-1;
    private float briefingClock,bossDefeatClock;
    private Bitmap? defeatPortrait;
    private float defeatX,defeatY;
    private float BossDefeatDuration=>round==4?(rodianExitClearPlayed?rodianExitClearClock+90:float.MaxValue):round==5?180:240;
    private float BossDefeatSkipFrame=>round==4?(rodianExitClearPlayed?rodianExitClearClock+42:float.MaxValue):round==5?120:204;
    private bool FinisherMonochromeActive=>bossDefeatActive&&bossDefeatClock<18;
    private void DisposeEncounterFlow(){DisposeIsangjinClockAssets();DisposeWolfPlayer();DisposeShowcaseAssets();DisposeStoryResources();bossTalkDepartureFrame?.Dispose();defeatPortrait?.Dispose();DisposeFinisherPortraits();}
    private void ResetEncounterFlow()
    {
        audio.StopDepartureSounds();CancelCinderFlight();
        ResetMelodyLastStand();ResetIsangjinClock();
        ResetTruePurgaeEncounter();
        bossBriefingActive=bossDefeatActive=false;briefingClock=bossDefeatClock=0;
        ResetBossConversation();
        defeatPortrait?.Dispose();defeatPortrait=null;
        DisposeFinisherPortraits();
    }
    private void BeginBossBriefing(int number)
    {
        if(number<2)return;
        bossBriefingActive=true;briefingRound=number;briefingClock=0;
        BeginBossConversation();
        ReleaseInputs();warningTimer=0;audio.Play("bossintro");
    }
    private bool UpdateEncounterFlow(float step)
    {
        if(bossBriefingActive)
        {
            briefingClock+=step;sceneFadeTimer=Math.Max(0,sceneFadeTimer-step);UpdateBossConversation(step);audio.Collect();return true;
        }
        if(!bossDefeatActive)return false;
        bossDefeatClock+=step;audio.Collect();
        if(round is 2 or 3&&bossDefeatClock>=168)
        {
            int foot=(int)((bossDefeatClock-168)/18);
            if(foot!=storyDepartureFoot){storyDepartureFoot=foot;if(audio.ActiveVoices<6)audio.Play($"step{(round==2?1:2)}{foot%2}");}
        }
        if(round is 2 or 3 && !independentBossExplosionPlayed && bossDefeatClock>=IndependentBossExplosionFrame){independentBossExplosionPlayed=true;audio.Play(round==2?"bossburst2":"bossburst3");}
        if(round==4&&!lastStandResolved&&!rodianDefeatExplosionPlayed&&bossDefeatClock>=RodianExplosionFrame)
        {
            rodianDefeatExplosionPlayed=true;audio.PlayClashExplosion();
        }
        if(round==4)UpdateRodianDeparture(step);
        if(bossDefeatClock>=BossDefeatDuration)FinishBossDefeat();
        return true;
    }
    private void FinishBossDefeat()
    {
        if(round==4&&practiceActive&&lastStandResolved)
        {
            bossDefeatActive=false;state=GameState.Round;lastStandPhase=7;lastStandClock=0;
            audio.StopClashSounds();audio.SetFinaleMix(1);ReleaseInputs();return;
        }
        bossDefeatActive=false;roundTransitionTimer=45;audio.SetFinaleMix(1);
        ReleaseInputs();
    }
    private bool HandleEncounterKey(Keys key)
    {
        if(bossBriefingActive)return HandleBossConversationKey(key);
        if(!bossDefeatActive)return false;
        if((key==Keys.Enter||key==Keys.Space)&&bossDefeatClock>=BossDefeatSkipFrame)FinishBossDefeat();
        return true;
    }
    private void BeginBossDefeat()
    {
        if(bossDefeatActive||storyDefeatTalk)return;
        if(!practiceActive&&round is 2 or 3&&!storyDefeatDone){BeginStoryDefeatTalk();return;}
        CancelCinderFlight();
        defeatPortrait?.Dispose();defeatPortrait=new Bitmap(288,288,PixelFormat.Format32bppPArgb);
        using(var capture=Graphics.FromImage(defeatPortrait))
        {
            capture.SmoothingMode=SmoothingMode.AntiAlias;capture.InterpolationMode=InterpolationMode.HighQualityBicubic;
            if(round==5)DrawFinalBossPortrait(capture,new(0,0,288,288));
            else
            {
                Image? source=round==4?(menheraMode?melodyMenhera:enemy5):CurrentBossImage;
                if(source!=null)capture.DrawImage(source,new Rectangle(10,10,268,268));
            }
        }
        defeatX=bossX;defeatY=bossY;
        PrepareFinisherPortraits();
        BeginRoundTransition(round<5?round+1:0,true,round!=4);
        bossDefeatActive=true;bossDefeatClock=0;storyDepartureFoot=-1;independentBossExplosionPlayed=false;rodianDefeatExplosionPlayed=round==4&&lastStandResolved;
        if(round==4)ResetRodianDeparture();
        burnTimer=0;parryWindow=0;counterFxTimer=0;ReleaseInputs();
        if(round!=4||!lastStandResolved)audio.Play("bossdefeat");
    }
    private void DrawBossDefeat(Graphics g)
    {
        if(round>=2&&round<=4){DrawDistinctBossFinisher(g);return;}
        DrawArenaGround(g);DrawPlayer(g);
        float t=bossDefeatClock,zoom=Math.Clamp(t/24,0,1),p=Math.Clamp((t-25)/115,0,1);
        float size=155+zoom*32,x=defeatX+(655-defeatX)*zoom,y=defeatY+(220-defeatY)*zoom;
        Color color=round==2?Color.FromArgb(170,206,225):round==3?Color.FromArgb(235,137,76):round==4?Color.FromArgb(231,160,199):FinalBossAccent;
        using var dim=new SolidBrush(Color.FromArgb((int)(100+70*zoom),5,6,8));g.FillRectangle(dim,0,0,W,H);
        using var vignette=new GraphicsPath();vignette.AddEllipse(x-115,y-100,size+230,size+200);
        using var haze=new PathGradientBrush(vignette){CenterColor=Color.FromArgb((int)(48*(1-p)),color),SurroundColors=new[]{Color.Transparent}};g.FillPath(haze,vignette);
        if(defeatPortrait!=null)
        {
            if(t<25)
            {
                float jolt=reducedEffects?0:MathF.Sin(t*2.3f)*(1-t/25)*3;
                DrawDefeatImage(g,defeatPortrait,new(x+jolt,y,size,size),1);
            }
            else if(round==4)
            {
                int echoes=reducedEffects?2:5;
                for(int i=echoes;i>=0;i--)
                {
                    float fade=(1-p)*(i==0?1:.16f),drift=p*(i*18+15);
                    DrawDefeatImage(g,defeatPortrait,new(x+MathF.Sin(i*1.8f)*drift,y-i*p*12,size,size),fade);
                }
                for(int i=0;i<(reducedEffects?8:28);i++)
                {
                    float a=i*2.399f,r=18+p*(50+i%4*18);
                    float sx=x+size*.5f+MathF.Cos(a)*r,sy=y+size*.5f+MathF.Sin(a)*r*.6f+p*45;
                    g.FillEllipse(ParticleBrush(color,(int)(190*(1-p))),sx,sy,1.5f+i%2,1.5f+i%2);
                }
            }
            else DrawDefeatFragments(g,defeatPortrait,x,y,size,p,color);
        }
        if(t<45&&!reducedEffects)
        {
            float k=1-t/45;
            using var glow=new Pen(Color.FromArgb((int)(75*k),color),8*k+1){EndCap=LineCap.Round};
            g.DrawBezier(glow,px+55,py+35,390,py-30,520,y+90,x+size*.45f,y+size*.5f);
        }
        float clear=Math.Clamp((t-108)/24,0,1);
        if(clear>0)
        {
            using var band=new LinearGradientBrush(new Rectangle(0,387,W,90),Color.FromArgb(0,0,0,0),Color.FromArgb((int)(200*clear),0,0,0),90);g.FillRectangle(band,0,387,W,90);
            SoulsHeading(g,$"라운드 {round} 클리어",new(180,393,640,50),32,Color.FromArgb((int)(255*clear),Gold));
            Label(g,RoundName(round)+" 처치",new(200,444,600,26),14,Color.FromArgb((int)(240*clear),Paper));
        }
        using var bars=new SolidBrush(Color.FromArgb(245,4,5,7));g.FillRectangle(bars,0,0,W,45);g.FillRectangle(bars,0,H-45,W,45);
        if(t>=120)Label(g,"ENTER / SPACE  다음",new(738,562,240,27),11,Steel,true,false);
    }
    private void DrawDefeatImage(Graphics g,Image image,RectangleF r,float alpha)
    {
        if(alpha<=0)return;
        readableMatrix.Matrix33=Math.Clamp(alpha,0,1);readableAlpha.SetColorMatrix(readableMatrix);
        g.DrawImage(image,Rectangle.Round(r),0,0,image.Width,image.Height,GraphicsUnit.Pixel,readableAlpha);
    }
    private void DrawDefeatFragments(Graphics g,Bitmap image,float x,float y,float size,float p,Color color)
    {
        int columns=reducedEffects?4:8,rows=4;
        float alpha=1-MathF.Pow(p,.75f);
        readableMatrix.Matrix33=alpha;readableAlpha.SetColorMatrix(readableMatrix);
        int cw=image.Width/columns,ch=image.Height/rows;
        for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
        {
            int n=row*columns+col;float side=col-(columns-1)*.5f;
            float sx=x+col*size/columns+side*p*(round==3?14:22),sy=y+row*size/rows+p*p*(25+n%5*19);
            float rotation=side*p*(round==2?23:12);
            var saved=g.Save();g.TranslateTransform(sx+size/columns*.5f,sy+size/rows*.5f);g.RotateTransform(rotation);
            g.DrawImage(image,new Rectangle((int)(-size/columns*.5f),(int)(-size/rows*.5f),(int)MathF.Ceiling(size/columns)+1,(int)MathF.Ceiling(size/rows)+1),col*cw,row*ch,cw,ch,GraphicsUnit.Pixel,readableAlpha);g.Restore(saved);
        }
        int embers=reducedEffects?10:45;
        for(int i=0;i<embers;i++)
        {
            float a=i*2.399f,r=10+p*(65+i%5*18),sx=x+size*.5f+MathF.Cos(a)*r,sy=y+size*.55f+MathF.Sin(a)*r*.55f+p*p*65;
            g.FillEllipse(ParticleBrush(color,(int)(200*(1-p))),sx,sy,1+i%3,1+i%3);
        }
    }
}
