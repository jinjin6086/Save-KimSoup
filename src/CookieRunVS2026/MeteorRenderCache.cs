using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly Bitmap[][] meteorTailFrames={Array.Empty<Bitmap>(),Array.Empty<Bitmap>()};
    private void LoadMeteorTails()
    {
        for(int side=0;side<2;side++)
        {
            var frames=new Bitmap[32];Color flame=side==0?Color.FromArgb(255,143,53):Mint;
            for(int i=0;i<frames.Length;i++)
            {
                var b=new Bitmap(96,128,PixelFormat.Format32bppPArgb);using var cg=Graphics.FromImage(b);
                cg.SmoothingMode=SmoothingMode.AntiAlias;cg.TranslateTransform(48,112);
                DrawMeteorTailShape(cg,44*.44f,86,MathF.Sin(i*MathF.Tau/32)*8,flame);frames[i]=b;
            }
            meteorTailFrames[side]=frames;
        }
    }
    private void DisposeMeteorTails(){foreach(var frames in meteorTailFrames)foreach(var b in frames)b.Dispose();}
    private void DrawCachedMeteorTail(Graphics g,Projectile p,float length)
    {
        var frames=meteorTailFrames[p.IsParried?1:0];
        if(frames.Length==0)return;
        float phase=(frame*.85f+p.FocusX)/MathF.Tau;phase-=MathF.Floor(phase);
        int index=(int)(phase*frames.Length)%frames.Length;float sx=p.W/44,sy=length/86;
        g.DrawImage(frames[index],new RectangleF(-48*sx,-112*sy,96*sx,128*sy));
    }
    private static void DrawMeteorTailShape(Graphics g,float w,float length,float flutter,Color flame)
    {
        using(var path=new GraphicsPath())
        {
            path.AddBezier(-w,0,-w*1.25f,-length*.28f,-w*.12f+flutter,-length*.74f,flutter,-length);
            path.AddBezier(flutter,-length,w*.12f+flutter,-length*.67f,w*1.12f,-length*.29f,w,0);path.CloseFigure();
            using var fire=new LinearGradientBrush(new RectangleF(-w*2,-length,w*4,length+3),Color.FromArgb(0,flame),Color.FromArgb(210,flame),90);g.FillPath(fire,path);
        }
        using(var white=new GraphicsPath())
        {
            white.AddBezier(-w*.35f,0,-w*.5f,-length*.2f,flutter*.4f,-length*.55f,flutter*.6f,-length*.8f);
            white.AddBezier(flutter*.6f,-length*.8f,w*.4f,-length*.3f,w*.4f,-length*.16f,w*.35f,0);white.CloseFigure();
            using var core=new LinearGradientBrush(new RectangleF(-w,-length,w*2,length+2),Color.FromArgb(0,255,218,145),Color.FromArgb(235,255,241,196),90);g.FillPath(core,white);
        }
        using var streak=new Pen(Color.FromArgb(150,flame),1.2f);
        for(int i=0;i<3;i++){float off=(i-1)*w*.7f,tip=flutter+(i-1)*7;g.DrawBezier(streak,off,-15,off*1.4f,-length*.3f,tip,-length*.75f,tip,-length*(.8f+i*.1f));}
    }
}
