using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly Dictionary<float,Font> soulsHeadingFonts=new();

    private void DisposeSoulsInterface()
    {
        foreach(var font in soulsHeadingFonts.Values)font.Dispose();
        soulsHeadingFonts.Clear();
    }
    private Font SoulsHeadingFont(float size)
    {
        if(!soulsHeadingFonts.TryGetValue(size,out var font))
            soulsHeadingFonts[size]=font=new Font("Batang",size,FontStyle.Regular,GraphicsUnit.Pixel);
        return font;
    }
    private void SoulsHeading(Graphics g,string text,RectangleF bounds,float size,Color color,bool centered=true)
    {
        float fitted=size;
        while(fitted>11&&g.MeasureString(text,SoulsHeadingFont(fitted)).Width>bounds.Width-6)fitted--;
        using var brush=new SolidBrush(color);
        using var shadow=new SolidBrush(Color.FromArgb(195,0,0,0));
        var shade=bounds;shade.Offset(0,1);
        g.DrawString(text,SoulsHeadingFont(fitted),shadow,shade,centered?centerLabel:leftLabel);
        g.DrawString(text,SoulsHeadingFont(fitted),brush,bounds,centered?centerLabel:leftLabel);
    }
    private static GraphicsPath SoulsPanelPath(RectangleF r,float cut)
    {
        var path=new GraphicsPath();
        path.AddLines(new[]{new PointF(r.Left+cut,r.Top),new PointF(r.Right-cut,r.Top),new PointF(r.Right,r.Top+cut),new PointF(r.Right,r.Bottom-cut),new PointF(r.Right-cut,r.Bottom),new PointF(r.Left+cut,r.Bottom),new PointF(r.Left,r.Bottom-cut),new PointF(r.Left,r.Top+cut)});
        path.CloseFigure();return path;
    }
    private void DrawSoulsRule(Graphics g,float x,float y,float width)
    {
        if(width<=0)return;
        using var left=new LinearGradientBrush(new RectangleF(x,y,width*.5f,1),Color.Transparent,Color.FromArgb(151,Gold),0f);
        using var right=new LinearGradientBrush(new RectangleF(x+width*.5f,y,width*.5f,1),Color.FromArgb(151,Gold),Color.Transparent,0f);
        g.FillRectangle(left,x,y,width*.5f,1);g.FillRectangle(right,x+width*.5f,y,width*.5f,1);
    }
    private void DrawSoulsKey(Graphics g,RectangleF r,string key,bool ready)
    {
        using var plate=new SolidBrush(Color.FromArgb(120,9,10,10));g.FillRectangle(plate,r);
        using var edge=new Pen(Color.FromArgb(ready?155:66,Gold),.8f);g.DrawRectangle(edge,r.X,r.Y,r.Width,r.Height);
        Label(g,key,new(r.X+1,r.Y+1,r.Width-2,r.Height-2),key.Length>1?8.5f:16,ready?Gold:Steel,true,false);
    }
    private void DrawSoulsGauge(Graphics g,RectangleF r,float value,float trail,Color low,Color high)
    {
        using var empty=new SolidBrush(Color.FromArgb(235,8,9,9));g.FillRectangle(empty,r);
        float pad=r.Height>8?2:1;
        RectangleF inside=new(r.X+pad,r.Y+pad,r.Width-pad*2,r.Height-pad*2);
        if(trail>value)
        {
            using var delayed=new SolidBrush(Color.FromArgb(157,117,87));
            g.FillRectangle(delayed,inside.X,inside.Y,inside.Width*Math.Clamp(trail,0,1),inside.Height);
        }
        if(value>0)
        {
            using var filled=new LinearGradientBrush(inside,low,high,0f);
            g.FillRectangle(filled,inside.X,inside.Y,inside.Width*Math.Clamp(value,0,1),inside.Height);
            using var sheen=new Pen(Color.FromArgb(63,240,224,192),.6f);
            g.DrawLine(sheen,inside.Left,inside.Top,inside.Left+inside.Width*Math.Clamp(value,0,1),inside.Top);
        }
        using var rim=new Pen(Color.FromArgb(127,Gold),.8f);g.DrawRectangle(rim,r.X,r.Y,r.Width,r.Height);
    }
    private void DrawSoulsMenuBase(Graphics g,float darkness)
    {
        DrawArenaGround(g);
        using var smoke=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb((int)(255*darkness),13,14,14),Color.FromArgb((int)(245*darkness),5,6,7),90f);
        g.FillRectangle(smoke,0,0,W,H);
        using var lower=new LinearGradientBrush(new Rectangle(0,360,W,240),Color.Transparent,Color.FromArgb(164,0,0,0),90f);g.FillRectangle(lower,0,360,W,240);
        if(!reducedEffects)
            for(int i=0;i<17;i++)
            {
                float x=(i*157.73f+MathF.Sin(animationClock*.008f+i)*24)%W;
                float y=H-((animationClock*.14f+i*51.7f)%(H+30));
                float fade=Math.Clamp((H-y)/130,0,1)*Math.Clamp(y/100,0,1);
                g.FillEllipse(ParticleBrush(Gold,(int)(fade*(i%3==0?85:45))),x,y,i%4==0?2:1,2);
            }
        DrawSoulsRule(g,27,25,946);
        Label(g,$"M 소리 {(audio.Muted?"OFF":"ON")}  ·  F 효과 {(reducedEffects?"약":"기본")}",new(735,554,235,24),10,Steel,true,false);
    }
    private void DrawCinderSeal(Graphics g,float x,float y,float alpha)
    {
        // A small engraved ember gives a sealed card a quiet focal point.
        using var glow=new SolidBrush(Color.FromArgb((int)(15*alpha),Gold));g.FillEllipse(glow,x-40,y-43,80,86);
        using var ring=new Pen(Color.FromArgb((int)(91*alpha),Gold),.8f);g.DrawEllipse(ring,x-40,y-43,80,86);
        using var etch=new Pen(Color.FromArgb((int)(184*alpha),Gold),1.2f);
        g.DrawBezier(etch,x,y-34,x-5,y-10,x-25,y-6,x-17,y+14);
        g.DrawBezier(etch,x-17,y+14,x-9,y+30,x+17,y+30,x+20,y+10);
        g.DrawBezier(etch,x+20,y+10,x+24,y-3,x+8,y-13,x,y-34);
        g.DrawBezier(etch,x,y-3,x-10,y+10,x-5,y+23,x+5,y+19);
        using var foot=new Pen(Color.FromArgb((int)(110*alpha),Gold),.7f);
        g.DrawLine(foot,x-24,y+35,x+24,y+35);
    }
    private void DrawSoulsForge(Graphics g)
    {
        DrawSoulsMenuBase(g,.94f);
        Label(g,"라운드 3 클리어",new(100,34,800,26),12,Gold);
        SoulsHeading(g,"발차기 동시 강화",new(100,70,800,49),32,Paper);
        Label(g,$"강화 +{forgeLevel}   ·   세 효과가 함께 오릅니다",new(100,128,800,30),16,Gold);
        string[] names={"발차기 재사용","반사 범위","체력 회복"};
        string[] values={$"{Math.Max(12,60-forgeLevel*6)/60f:0.0}초",$"기본 +{forgeLevel*8}",$"{5+forgeLevel}%"};
        string[] details={"성공마다 −0.1초 / 최소 0.2초","성공마다 좌우 +8 / 최대 +120","성공마다 +1%p / 최대 20%"};
        for(int i=0;i<3;i++)
        {
            Rectangle r=new(100+i*270,184,260,151);PanelSurface(g,r,Gold);
            Label(g,names[i],new(r.X+12,r.Y+18,r.Width-24,27),16,Steel);
            SoulsHeading(g,values[i],new(r.X+12,r.Y+54,r.Width-24,43),30,Paper);
            Label(g,details[i],new(r.X+10,r.Y+114,r.Width-20,22),10,Steel);
        }
        Label(g,forgeFailures>=3?"실패 3회 · 강화 종료":forgeLevel>=ForgeLimit?"최대 강화 도달":$"다음 성공 확률  {ForgeChance*100:0}%",new(100,349,800,29),18,Gold);
        Label(g,$"실패 시 세 효과가 기본으로 초기화됩니다  ·  실패 {forgeFailures} / 3",new(100,380,800,22),11,Steel);
        PanelSurface(g,forgeRollRect,Gold,forgeFailures<3&&forgeLevel<ForgeLimit);PanelSurface(g,forgeLeaveRect,Gold);
        Label(g,forgeFailures>=3?"강화 기회 종료":forgeLevel>=ForgeLimit?"최대 강화":"SPACE  동시 강화",forgeRollRect,17,forgeFailures>=3||forgeLevel>=ForgeLimit?Steel:Gold);
        Label(g,"E  이 수치로 출전",forgeLeaveRect,17,Paper);
        Label(g,forgeMessage,new(80,485,840,27),13,forgeFlash>0?Gold:Steel);
        Label(g,"100 → 90 → 70 → 50 → 35 → 20 → 19 … → 14%   /   실패 세 번까지",new(70,535,860,24),11,Steel);
    }
}
