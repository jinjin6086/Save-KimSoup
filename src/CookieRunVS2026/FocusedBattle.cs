using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool menheraMode, pendingMenheraCleanup;
    private float menheraIntro;
    private int dashPattern;
    private float DashWarning=>dashPass==0?34:18;
    private float DashTravel=>menheraMode?5:8;
    private float DashLaneY(float x)=>dashY+(x-176)*dashSlope;
    private float meteorBarrageTimer,meteorSpawnTimer;
    private int meteorShotsRemaining;
    private readonly List<MeteorImpact> meteorImpacts=new();
    private sealed class MeteorImpact{public float X,Life=22;}
    private void ResetEncore()
    {
        rushCount=0;rushClock=0;
        menheraMode=false;fullRush=false;melodyBurstActive=false;melodyBurstRemaining=0;pendingMenheraCleanup=false;menheraIntro=0;dashPattern=0;
        meteorBarrageTimer=meteorSpawnTimer=0;meteorShotsRemaining=0;meteorImpacts.Clear();
    }
    private void UpdateEncorePresentation(float step)
    {
        menheraIntro=Math.Max(0,menheraIntro-step);
        for(int i=meteorImpacts.Count-1;i>=0;i--){meteorImpacts[i].Life-=step;if(meteorImpacts[i].Life<=0)meteorImpacts.RemoveAt(i);}
    }
    private void EnterMenheraMode()
    {
        menheraMode=true;menheraIntro=0;
        BeginPhaseIntro(2);
        bossMessage="가질 수 없다면 부숴버리겠어..";bossShoutTimer=170;
        dashActive=false;laserActive=false;melodyStep=-1;hasGate=false;
        pendingMenheraCleanup=true;obstacles.Clear();risingObstacles.Clear();
        attackTimer=0;dashCooldown=180;melodySequenceCooldown=100;obstacleSpawnTimer=35;
        audio.CancelLaserCharge();audio.Play("menhera");
        Burst(bossX+65,bossY+65,Coral,55);TriggerCameraShake(6,12);
        warningTimer=0;
    }
    private void SpawnMelodyNote()
    {
        float x=bossX-8,y=bossY+55,dx=px+25-x,dy=py+32-y;
        float length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy)),speed=menheraMode?12.6f:10.2f;
        projectiles.Add(new(x,y,AttackType.MelodyNote,menheraMode?34:32,dx/length*speed,dy/length*speed,menheraMode?425:300));
        audio.Play("volley");
    }
    private void UpdateMeteorBarrage(float step)
    {
        UpdateMeteorStorm(step);
    }
    private void SpawnMeteorImpact(float x)
    {
        if(TruePurgaeEncounter){audio.Play("meteorhit");return;}
        if(meteorImpacts.Count>=12)meteorImpacts.RemoveAt(0);
        meteorImpacts.Add(new(){X=x});Burst(x,GroundY-5,Color.FromArgb(255,169,74),10,2);
        audio.Play("meteorhit");TriggerCameraShake(2.5f,4);
    }
    private void DrawFallingMeteor(Graphics g,Projectile p)
    {
        float cx=p.X+p.W*.5f,cy=p.Y+p.H*.5f;
        float vx=p.VelocityX,vy=p.VelocityY;
        if(p.IsParried&&!p.IsMeteorUpward){vx=bossX+65-cx;vy=bossY+65-cy;float l=Math.Max(1,MathF.Sqrt(vx*vx+vy*vy));vx=vx/l*20;vy=vy/l*20;}
        float speed=Math.Max(1,MathF.Sqrt(vx*vx+vy*vy)),dx=vx/speed,dy=vy/speed;
        Color flame=p.IsParried?Mint:Color.FromArgb(255,143,53);
        // A continuous tapered flame follows the real velocity, with an independently fluttering tip.
        var tailState=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(MathF.Atan2(dy,dx)*180/MathF.PI-90);
        float length=speed*3.3f+20,w=p.W*.44f,flutter=MathF.Sin(frame*.85f+p.FocusX)*8;
        DrawCachedMeteorTail(g,p,length);
        g.Restore(tailState);
        var saved=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform((frame*7+p.FocusX*2)%360);
        float size=p.W*1.18f;g.DrawImage(meteorArt!,new RectangleF(-size/2,-size/2,size,size));g.Restore(saved);
        using var rim=new Pen(Color.FromArgb(110,flame),2);g.DrawArc(rim,cx-p.W*.48f,cy-p.W*.48f,p.W*.96f,p.W*.96f,frame*8,150);
    }
    private void DrawEncoreEffects(Graphics g)
    {
        foreach(var impact in meteorImpacts)
        {
            float t=1-impact.Life/22,r=12+t*77,life=1-t;
            using var wave=new Pen(Color.FromArgb((int)(210*life),255,183,100),3*life+1);
            g.DrawEllipse(wave,impact.X-r,GroundY-5-r*.17f,r*2,r*.34f);
            using var crack=new Pen(Color.FromArgb((int)(190*life),255,124,62),2);
            for(int i=0;i<5;i++){float dir=(i-2)*.45f;g.DrawLine(crack,impact.X,GroundY-3,impact.X+MathF.Sin(dir)*r,GroundY+4+MathF.Cos(dir)*r*.2f);}
        }
        if(dashActive&&fullRush)DrawRushCascade(g);
        if(dashActive&&!fullRush)
        {
            float y1=DashLaneY(0),y2=DashLaneY(W),warning=DashWarning,travel=DashTravel;
            if(dashTimer<warning)
            {
                using var lane=new Pen(Color.FromArgb(39,255,65,78),34);
                using var edge=new Pen(Color.FromArgb(215,255,90,98),1.5f){DashStyle=DashStyle.Dash};
                g.DrawLine(lane,0,y1,W,y2);g.DrawLine(edge,0,y1-17,W,y2-17);g.DrawLine(edge,0,y1+17,W,y2+17);
                Label(g,$"{(fullRush?"RAMPAGE":"TRIPLE RUSH")}  {dashPass+1} / {(fullRush?rushCount:3)}",new(355,162,290,28),16,Coral);
                using var timer=new Pen(Coral,3);g.DrawLine(timer,430,201,430+140*Math.Clamp(dashTimer/warning,0,1),201);
            }
            else if(dashTimer<warning+travel+4)
            {
                float f=Math.Clamp((dashTimer-warning)/travel,0,1),x=dashPass%2==0?W+100-f*(W+200):-100+f*(W+200);
                float y=DashLaneY(x);
                using var bloom=new Pen(Color.FromArgb(125,255,60,72),39);using var core=new Pen(Color.FromArgb(245,255,220,214),5);
                g.DrawLine(bloom,0,y1,W,y2);g.DrawLine(core,0,y1,W,y2);
                DrawMelodyRushBody(g,x,y);
            }
        }
        if(menheraIntro>0)
        {
            using var dim=new SolidBrush(Color.FromArgb(125,25,5,17));g.FillRectangle(dim,0,104,W,397);
            Label(g,"MENHERA MODE",new(170,206,660,35),20,Coral);
            Label(g,"가질 수 없다면 부숴버리겠어..",new(90,260,820,62),32,Paper);
            Label(g,"모든 패턴이 강화됩니다",new(200,335,600,31),15,Gold);
        }
        else if(menheraMode)
        {
            using var danger=new Pen(Color.FromArgb(65,255,65,113),2);g.DrawLine(danger,3,110,3,496);g.DrawLine(danger,W-3,110,W-3,496);
        }
    }
}


