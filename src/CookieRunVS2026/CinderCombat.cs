using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private readonly List<CinderKunai> cinderKunai=new(32);
    private int cinderVulnerability,cinderKunaiHits;
    private float cinderBrandPulse;
    private bool cinderKunaiImpact;
    private sealed class CinderKunai{public float X,Y,OldX,OldY,Age,Angle;}
    // Frozen time stops interactions, not an already active transformation.
    private bool CinderVisualActive=>state==GameState.Round&&activeSkill==ActivePower.Cinder&&activeTimer>0&&!PhaseIntroActive&&!bossBriefingActive&&!MelodyLastStandActive;
    private bool CinderBurning=>CinderVisualActive&&!TruePurgaeTimeFrozen;
    private void ResetCinderRound(){CancelCinderFlight();cinderVulnerability=cinderKunaiHits=0;cinderBrandPulse=0;}
    private void CancelCinderFlight(){cinderKunai.Clear();audio.StopCinderFire();}
    private void QueueCinderKunai()
    {
        if(round<2||bossHitRequired<=0||cinderKunai.Count>=32||bossDefeatActive||MelodyLastStandActive)return;
        cinderKunai.Add(new(){X=px+49,Y=py+34,OldX=px+49,OldY=py+34});audio.Play("cinderthrow");
    }
    private void UpdateCinderCombat(float step)
    {
        cinderBrandPulse=Math.Max(0,cinderBrandPulse-step);

        for(int i=cinderKunai.Count-1;i>=0;i--)
        {
            var p=cinderKunai[i];p.Age+=step;p.OldX=p.X;p.OldY=p.Y;
            // The dart crosses real distance. It tracks the visible boss while
            // a dash is intangible and cannot register a delayed phantom hit.
            float dx=bossX+67-p.X,dy=bossY+80-p.Y,d=MathF.Sqrt(dx*dx+dy*dy);
            p.Angle=MathF.Atan2(dy,dx);
            if(p.Age>180){cinderKunai.RemoveAt(i);continue;}
            if(d<24*step+16&&!dashActive)
            {
                cinderKunai.RemoveAt(i);
                int priorClears=practiceClears;
                pendingBossDamage=1f;cinderKunaiImpact=true;
                try{RegisterBossParryHit(1200,true);}finally{cinderKunaiImpact=false;}
                if(state==GameState.Round&&!PhaseIntroActive&&!MelodyLastStandActive&&!bossDefeatActive&&practiceClears==priorClears)
                    if(++cinderKunaiHits>=2){cinderKunaiHits=0;cinderVulnerability++;cinderBrandPulse=35;audio.Play("cinderbrand");}
                if(state!=GameState.Round||bossDefeatActive||PhaseIntroActive||MelodyLastStandActive||practiceClears!=priorClears)return;
                augmentNotice=$"취약 {cinderVulnerability}  ·  반사 피해 +{cinderVulnerability*.5f:0.0}  ·  쿠나이 {cinderKunaiHits}/2";augmentFlashTimer=70;
            }
            else if(d>1){p.X+=dx/d*24*step;p.Y+=dy/d*24*step;}
        }
    }
    private void DrawCinderProjectiles(Graphics g)
    {
        foreach(var p in cinderKunai)
        {
            var st=g.Save();g.TranslateTransform(p.X,p.Y);g.RotateTransform(p.Angle*180/MathF.PI);
            if(cinderKunaiFrames[0]!=null)DrawEchoFrame(g,cinderKunaiFrames[(int)(p.Age*1.4f)%16],new(-69,-19.5f,111,39),1);
            else g.DrawLine(ParticlePen(Color.OrangeRed,255,3),-20,0,12,0);
            if(!reducedEffects)for(int i=0;i<12;i++)
            {
                float t=(animationClock*.08f+i*.1618f)%1,x=-18-t*68,y=MathF.Sin(i*2.4f+animationClock*.19f)*t*14;
                g.FillEllipse(ParticleBrush(i%2==0?Color.FromArgb(255,214,103):CinderOrange,(int)(190*(1-t))),x,y,1.8f,1.8f);
            }
            g.Restore(st);
        }
    }
    private void DrawCinderBrand(Graphics g,RectangleF body,float alpha)
    {
        if(cinderVulnerability<=0||cinderBrandArt==null||alpha<=0)return;
        float severity=Math.Min(10,cinderVulnerability),pulse=.90f+.10f*MathF.Sin(animationClock*.10f),flare=cinderBrandPulse/35;
        float size=body.Width*(.17f+MathF.Sqrt(severity)*.075f),x=body.X+body.Width*.51f,y=body.Y+body.Height*.61f;
        DrawLaserBloom(g,x,y,size*.7f,size*.7f,CinderOrange,alpha*(.14f+severity*.027f+flare*.24f));
        DrawEchoFrame(g,cinderBrandArt,new(x-size*.53f,y-size*.55f,size*1.06f,size*1.10f),alpha*(.65f+severity*.025f)*pulse);
        // Each added stack opens further scorched fissures. Brightness and
        // spread saturate for readability; the damage counter has no stack cap.
        if(!reducedEffects)for(int i=0;i<Math.Min(18,cinderVulnerability*2);i++)
        {
            float t=(animationClock*.018f+i*.618f)%1,a=i*2.39996f;
            float xx=x+MathF.Cos(a)*size*(.18f+t*.46f),yy=y-size*t*.8f+MathF.Sin(a)*size*.22f;
            float s=.6f+(i%3)*.27f;
            g.FillEllipse(ParticleBrush(Color.FromArgb(255,188,67),(int)(185*MathF.Sin(t*MathF.PI)*alpha)),xx-s,yy-s,s*2,s*2);
        }
    }
    private void DrawCinderStatus(Graphics g)
    {
        if(round<2||cinderVulnerability==0&&activeSkill!=ActivePower.Cinder)return;
        Label(g,$"취약 {cinderVulnerability}  ·  반사 +{cinderVulnerability*.5f:0.0}  ·  쿠나이 {cinderKunaiHits}/2",new(601,501,367,20),11,Color.FromArgb(255,184,92),false);
    }
    private int DraftCardCount=>pendingDraft==1?ActivePowerCount:PassivePowerCount;
    private bool DraftCardAvailable(int index)=>index>=0&&index<DraftCardCount;
    private Rectangle DraftCardRect(int i)=>pendingDraft==2&&i>=3?new(i==3?199:509,338,292,190):augmentCardRects[i];
    private int NextDraftCard(int offset)
    {
        int p=skillCardFocus;for(int i=0;i<DraftCardCount;i++){p=(p+offset+DraftCardCount)%DraftCardCount;if(DraftCardAvailable(p))return p;}return skillCardFocus;
    }
}
