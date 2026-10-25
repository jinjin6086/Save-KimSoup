using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private const int IndependentBossExplosionFrame=64;
    private bool independentBossExplosionPlayed;
    private readonly Bitmap[][] independentBossBursts={new Bitmap[36],new Bitmap[36]};
    private float IndependentBossAlpha=>1-SmoothUnit((bossDefeatClock-IndependentBossExplosionFrame)/14);
    private void PrepareIndependentBossExplosions()
    {
        for(int kind=0;kind<2;kind++)for(int f=0;f<36;f++)
        {
            if(detonationVolume[f]==null)continue;var b=new Bitmap(384,384,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(b);using var tint=new ImageAttributes();
            // The same volumetric shock structure gets two distinct palettes:
            // cold steel/cyan for Isangjin, hot ember/orange for Purgae.
            tint.SetColorMatrix(kind==0?new ColorMatrix(new[]{new[]{.32f,.30f,.33f,0,0},new[]{.29f,.36f,.40f,0,0},new[]{.24f,.32f,.40f,0,0},new[]{0f,0,0,1f,0},new[]{0f,0,0,0,1f}}):new ColorMatrix(new[]{new[]{.55f,.15f,.01f,0,0},new[]{.40f,.45f,.04f,0,0},new[]{.40f,.18f,.025f,0,0},new[]{0f,0,0,1f,0},new[]{0f,0,0,0,1f}}));
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(detonationVolume[f],new Rectangle(0,0,384,384),0,0,detonationVolume[f].Width,detonationVolume[f].Height,GraphicsUnit.Pixel,tint);independentBossBursts[kind][f]=b;
        }
    }
    private void DrawIndependentBossExplosion(Graphics g,float x,float y,float size,float t)
    {
        Color color=round==2?Color.FromArgb(156,211,239):Color.FromArgb(255,112,29);
        float alpha=IndependentBossAlpha,cx=x+size*.5f,cy=y+size*.54f;
        if(alpha>0&&defeatPortrait!=null)
        {
            float tension=SmoothUnit(t/IndependentBossExplosionFrame),jolt=reducedEffects?0:MathF.Sin(t*2.3f)*tension*2;
            DrawDefeatImage(g,defeatPortrait,new(x+jolt,y,size,size),alpha);
            if(t<IndependentBossExplosionFrame){DrawLaserBloom(g,cx,cy,9+tension*25,13+tension*30,color,tension*.64f);}
        }
        float age=t-IndependentBossExplosionFrame;if(age<0)return;
        float p=Math.Clamp(age/134,0,1),fade=1-SmoothUnit((p-.23f)/.77f),r=28+MathF.Pow(p,.43f)*191;
        int frame=Math.Clamp((int)(p*35),0,35),kind=round==2?0:1;
        if(independentBossBursts[kind][frame]!=null)DrawEchoFrame(g,independentBossBursts[kind][frame],new(cx-r*1.18f,cy-r*.97f,r*2.36f,r*1.94f),fade);
        DrawLaserBloom(g,cx,cy,r*.52f,r*.49f,color,fade*.57f);
        if(age<6&&!reducedEffects)DrawLaserBloom(g,cx,cy,520,200,Color.FromArgb(255,246,225),(1-age/6)*.84f);
        if(!reducedEffects&&age<20){float burst=age/20;DrawLaserBloom(g,cx,cy,20+burst*270,4+burst*16,color,(1-burst)*.62f);}
        int count=reducedEffects?12:48;
        for(int i=0;i<count;i++)
        {
            float phase=Math.Clamp((age-i%5*2)/108,0,1);if(phase<=0||phase>=1)continue;
            float angle=i*2.39996f,speed=74+Scatter01(i*31+9)*197;
            float xx=cx+MathF.Cos(angle)*speed*phase,yy=cy+MathF.Sin(angle)*speed*phase*.7f+phase*phase*79;
            float a=MathF.Sin(MathF.PI*phase)*(1-phase),s=1+i%3*.55f;
            g.FillEllipse(ParticleBrush(color,(int)(230*a)),xx-s,yy-s,s*2,s*2);
            if(i%4==0)g.DrawLine(ParticlePen(color,(int)(120*a),1),xx,yy,xx-MathF.Cos(angle)*9,yy-MathF.Sin(angle)*6);
        }
    }
    private void DisposeIndependentBossExplosions(){foreach(var frames in independentBossBursts)foreach(var b in frames)b?.Dispose();}
}
