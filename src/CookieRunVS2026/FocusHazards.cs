using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private static readonly Color FocusDanger=Color.FromArgb(255,106,124),FocusGuide=Color.FromArgb(89,202,255);
    private static RectangleF FocusGroundHazardBounds(float centerX)=>new(centerX-87,GroundY-56,174,56);
    private static RectangleF FocusSweepHazardBounds(float centerY)=>new(0,centerY-14,W,28);
    private void DrawFocusHazards(Graphics g)
    {
        if(round==4)
        {
            DrawFocusRushHazards(g);
            DrawFocusLaserHazard(g);
        }
        if(round==5&&finalSpecialActive)DrawFocusFinalHazard(g);
        if((round==3||TruePurgaeEncounter)&&purgaeSkill2Active&&purgaeSkill2ThrowTimer<14&&purgaeSkill2NextBall<6)
        {
            float angle=-MathF.PI*.5f+purgaeSkill2NextBall*MathF.Tau/6;
            float x=bossX+77.5f+MathF.Cos(angle)*108,y=bossY+77.5f+MathF.Sin(angle)*108;
            DrawFocusBound(g,new(x,y,34,34),false,"");
            using var aim=new Pen(Color.FromArgb(110,FocusGuide),.8f){DashStyle=DashStyle.Dash};
            g.DrawLine(aim,x+17,y+17,px+PlayerWidth*.5f+17,py+PlayerHeight*.5f+17);
        }
        if((round==3||TruePurgaeEncounter)&&meteorPhase==1)
        {
            FocusHazardBox(g,new(W*.5f-28,GroundY-44,56,44),false,.8f);
            Label(g,"메테오 · 발차기로 위로 반사",new(W*.5f-170,GroundY-88,340,24),12,FocusGuide);
        }
    }
    private void FocusHazardBox(Graphics g,RectangleF bounds,bool live,float opacity=1)
    {
        opacity=Math.Clamp(opacity,0,1);Color color=live?FocusDanger:FocusGuide;
        using var fill=new SolidBrush(Color.FromArgb((int)((live?38:12)*opacity),color));g.FillRectangle(fill,bounds);
        using var outline=new Pen(Color.FromArgb((int)((live?250:185)*opacity),color),live?1.8f:1)
            {DashStyle=live?DashStyle.Solid:DashStyle.Dash};
        g.DrawRectangle(outline,bounds.X,bounds.Y,bounds.Width,bounds.Height);
    }
    private void DrawFocusLane(Graphics g,float left,float right,float halfWidth,bool live,float opacity=1)
    {
        opacity=Math.Clamp(opacity,0,1);Color c=live?FocusDanger:FocusGuide;
        // Damage is checked in vertical pixels (y +/- 17), rather than a
        // perpendicular distance from the slope, so both edges use that offset.
        using var band=new GraphicsPath();band.AddLines(new[]{new PointF(0,left-halfWidth),new PointF(W,right-halfWidth),new PointF(W,right+halfWidth),new PointF(0,left+halfWidth)});band.CloseFigure();
        using var fill=new SolidBrush(Color.FromArgb((int)((live?42:12)*opacity),c));g.FillPath(fill,band);
        using var edge=new Pen(Color.FromArgb((int)((live?250:185)*opacity),c),live?1.8f:1){DashStyle=live?DashStyle.Solid:DashStyle.Dash};
        g.DrawLine(edge,0,left-halfWidth,W,right-halfWidth);g.DrawLine(edge,0,left+halfWidth,W,right+halfWidth);
    }
    private void DrawFocusRushMarker(Graphics g,float x,float y)
    {
        // The damaging sweep is 34 px tall. The large photographic dash pose
        // is cosmetic; a narrow moving cursor shows the actual crossing time.
        FocusHazardBox(g,new(x-4,y-17,8,34),true);
        g.DrawLine(ParticlePen(Color.White,220,1),x-7,y,x+7,y);
    }
    private void DrawFocusRushHazards(Graphics g)
    {
        if(!dashActive)return;
        if(!fullRush)
        {
            float warning=DashWarning,travel=DashTravel,age=dashTimer-warning;
            bool live=age>=0&&age<=travel;
            float opacity=age>travel?Math.Clamp(1-(age-travel)/5,0,1):1;
            DrawFocusLane(g,DashLaneY(0),DashLaneY(W),17,live,opacity);
            if(live)
            {
                float t=Math.Clamp(age/travel,0,1),x=dashPass%2==0?W+100-t*(W+200):-100+t*(W+200);
                DrawFocusRushMarker(g,x,DashLaneY(x));
            }
            if(age<0)Label(g,$"돌진 {dashPass+1}/3 · 빨간 경로는 반사 불가",new(260,148,480,25),13,FocusGuide);
            return;
        }
        int revealed=RevealedRushCount,next=0;
        while(next<rushCount&&rushClock>RushLaunch(next)+CascadeTravel)next++;
        for(int i=0;i<revealed;i++)
        {
            float age=rushClock-RushLaunch(i);if(age>CascadeTravel+5||!rushRouteLocked[i])continue;
            var route=rushRoutes[i];bool live=age>=0&&age<=CascadeTravel;
            float opacity=age>CascadeTravel?Math.Clamp(1-(age-CascadeTravel)/5,0,1):i==next?1:.56f;
            DrawFocusLane(g,route.Left,route.Right,17,live,opacity);
            if(live)
            {
                float t=age/CascadeTravel,x=i%2==0?W+100-t*(W+200):-100+t*(W+200);
                DrawFocusRushMarker(g,x,route.Y(x));
            }
            else if(age<0&&i==next)
            {
                float x=px+26;
                Label(g,$"{i+1:00} · 반사 불가",new(x-72,route.Y(x)-46,144,22),11,FocusGuide);
            }
        }
        if(next<rushCount)Label(g,RushKindName(rushKinds[next]),new(300,148,400,24),13,FocusGuide);
    }
    private void DrawFocusFinalHazard(Graphics g)
    {
        float clock=finalSpecialClock;
        if(FinalBossKind==2)
        {
            if(clock<67)
            {
                bool live=clock>=56&&clock<59;
                FocusHazardBox(g,FocusSweepHazardBounds(finalSweepY),live,clock<59?1:Math.Clamp((67-clock)/8,0,1));
                if(clock<56)Label(g,finalSweepY>450?"↑ 점프 · 붉은 줄 반사 불가":"↓ 슬라이드 · 붉은 줄 반사 불가",new(285,147,430,25),13,FocusGuide);
            }
            return;
        }
        int pulses=FinalBossKind==1?2:1;
        bool remaining=false,livePulse=false;float opacity=0;
        for(int i=0;i<pulses;i++)
        {
            float age=clock-(50+i*22);
            if(age<0){remaining=true;opacity=Math.Max(opacity,.8f);}
            else if(age<3){livePulse=true;opacity=1;}
            else if(age<11)opacity=Math.Max(opacity,(11-age)/11);
        }
        if(opacity>0)FocusHazardBox(g,FocusGroundHazardBounds(finalSpecialX),livePulse,opacity);
        if(remaining)Label(g,FinalBossKind==1?"↑↑ 2단 점프 · 불기둥 2회 / 반사 불가":"↑ 점프 · 내려찍기 반사 불가",new(finalSpecialX-180,GroundY-105,360,25),12,FocusGuide);
    }
    private void DrawFocusLaserHazard(Graphics g)
    {
        bool charge=MelodyLaserApproaching;
        if(!laserActive&&!charge)return;
        bool reflected=laserActive&&laserReflected;
        PointF start=charge?melodyLaserOrigin:reflected?new(counterOriginX,counterOriginY):new(bossX+65,bossY+65);
        PointF end;
        if(charge)end=MelodyLaserTip;
        else if(reflected)end=new(counterTargetX,counterTargetY);
        else end=new(start.X+MathF.Cos(LiveLaserAngle)*1160,start.Y+MathF.Sin(LiveLaserAngle)*1160);
        float dx=end.X-start.X,dy=end.Y-start.Y,length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy));
        float nx=-dy/length,ny=dx/length,halfWidth=charge?5:reflected?9.5f:menheraMode?12.1667f:10.8333f;
        Color c=reflected?Color.FromArgb(178,255,231):FocusDanger;
        float opacity=charge?1:reflected?Math.Clamp((21-laserTimer)/6,0,1):Math.Clamp((58-laserTimer)/16,0,1);
        using var path=new GraphicsPath();path.AddLines(new[]{new PointF(start.X+nx*halfWidth,start.Y+ny*halfWidth),new PointF(end.X+nx*halfWidth,end.Y+ny*halfWidth),new PointF(end.X-nx*halfWidth,end.Y-ny*halfWidth),new PointF(start.X-nx*halfWidth,start.Y-ny*halfWidth)});path.CloseFigure();
        using var fill=new SolidBrush(Color.FromArgb((int)(16*opacity),c));g.FillPath(fill,path);
        using var edge=new Pen(Color.FromArgb((int)(210*opacity),c),reflected?1.8f:1.2f){DashStyle=charge?DashStyle.Dash:DashStyle.Solid};g.DrawPath(edge,path);
        using var aim=new Pen(Color.FromArgb((int)(180*opacity),c),.8f){DashStyle=DashStyle.Dash};g.DrawLine(aim,start,end);
        PointF contact=reflected?end:charge?end:new(laserContactX,laserContactY);
        FocusHazardBox(g,new(contact.X-13,contact.Y-17,26,34),!charge,opacity);
        if(charge)Label(g,"레이저가 범위에 닿으면 발차기",new(px-100,py-68,260,25),11,FocusGuide);
        else if(reflected)Label(g,"레이저 반사",new(px-38,py-47,180,23),12,c);
    }
}
