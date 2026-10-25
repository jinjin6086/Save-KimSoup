using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private float nativeArtScale=1;

    private void BuildSmoothArena()
    {
        if(backgroundCache!=null&&backgroundCacheRound==round)return;
        DisposeScrollingLayers();
        backgroundCache?.Dispose();groundTile?.Dispose();arenaRuins?.Dispose();arenaRuins=null;
        backgroundCacheRound=round;
        int width=Math.Max(1,(int)Math.Ceiling(W*nativeArtScale)),height=Math.Max(1,(int)Math.Ceiling(H*nativeArtScale));
        if(TryBuildThemedArena(width,height))return;
        backgroundCache=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(backgroundCache))
        {
            g.ScaleTransform(nativeArtScale,nativeArtScale);g.SmoothingMode=SmoothingMode.AntiAlias;
            Color top=round==4?Color.FromArgb(26,22,48):Color.FromArgb(14,25,46);
            Color bottom=round==4?Color.FromArgb(95,66,104):Color.FromArgb(54,79,108);
            using var sky=new LinearGradientBrush(new Rectangle(0,0,W,H),top,bottom,90);g.FillRectangle(sky,0,0,W,H);
            using var haloPath=new GraphicsPath();haloPath.AddEllipse(505,-90,470,470);
            using var halo=new PathGradientBrush(haloPath){CenterColor=Color.FromArgb(45,Accent(round)),SurroundColors=new[]{Color.Transparent}};g.FillPath(halo,haloPath);
            using var moon=new SolidBrush(Color.FromArgb(205,219,228,236));g.FillEllipse(moon,700,83,104,104);
            using var moonShade=new SolidBrush(Color.FromArgb(27,44,63));g.FillEllipse(moonShade,724,71,96,97);
            for(int layer=0;layer<3;layer++)
            {
                using var hills=new GraphicsPath();float y=315+layer*56;
                hills.AddBezier(-80,y+60,110,y-85,205,y+52,366,y-10);
                hills.AddBezier(366,y-10,510,y-90,620,y+48,780,y-30);
                hills.AddBezier(780,y-30,880,y-100,1010,y+14,1090,y+34);
                hills.AddLine(1090,y+34,1090,510);hills.AddLine(1090,510,-80,510);hills.CloseFigure();
                using var fill=new SolidBrush(Color.FromArgb(180,19+layer*2,32+layer*2,52+layer*2));g.FillPath(fill,hills);
                using var rim=new Pen(Color.FromArgb(12,Accent(round)),2);g.DrawPath(rim,hills);
            }
            for(int i=0;i<7;i++)
            {
                float x=90+i*145,y=277+(i*29)%74;
                using var pole=new Pen(Color.FromArgb(60,10,21,38),5);g.DrawLine(pole,x,y,x,500);
                using var light=new SolidBrush(Color.FromArgb(120,Accent(round)));g.FillEllipse(light,x-2,y-2,4,4);
                using var beam=new LinearGradientBrush(new RectangleF(x-38,y,76,190),Color.FromArgb(7,Accent(round)),Color.Transparent,90);
                g.FillPolygon(beam,new[]{new PointF(x,y),new PointF(x+40,490),new PointF(x-40,490)});
            }
            using var mist=new LinearGradientBrush(new Rectangle(0,375,W,125),Color.Transparent,Color.FromArgb(85,Accent(round)),90);g.FillRectangle(mist,0,375,W,125);
            using var vignette=new GraphicsPath();vignette.AddEllipse(-380,-210,1760,1000);
            using var shade=new PathGradientBrush(vignette){CenterColor=Color.Transparent,SurroundColors=new[]{Color.FromArgb(120,4,12,26)}};g.FillRectangle(shade,0,0,W,H);
        }
        groundTile=new Bitmap(Math.Max(1,(int)Math.Ceiling(240*nativeArtScale)),Math.Max(1,(int)Math.Ceiling(100*nativeArtScale)),PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(groundTile))
        {
            g.ScaleTransform(nativeArtScale,nativeArtScale);g.SmoothingMode=SmoothingMode.AntiAlias;
            using var floor=new LinearGradientBrush(new Rectangle(0,0,240,100),Color.FromArgb(39,54,72),Color.FromArgb(10,20,35),90);g.FillRectangle(floor,0,0,240,100);
            using var edge=new Pen(Color.FromArgb(150,151,175,195),1.5f);g.DrawLine(edge,0,1,240,1);
            using var rail=new Pen(Color.FromArgb(35,138,165,191),1);g.DrawLine(rail,0,14,240,14);g.DrawLine(rail,0,53,240,53);
            using var seam=new Pen(Color.FromArgb(34,7,15,29),1);g.DrawLine(seam,30,14,-35,100);g.DrawLine(seam,210,14,145,100);
            using var reflection=new LinearGradientBrush(new Rectangle(0,2,240,42),Color.FromArgb(13,Accent(round)),Color.Transparent,90);g.FillRectangle(reflection,0,2,240,42);
        }
    }

    private void SmoothBackground(Graphics g)
    {
        BuildSmoothArena();g.CompositingMode=CompositingMode.SourceCopy;
        if(themedArena&&scrollingBackdrop!=null)DrawScrollingLayer(g,scrollingBackdrop,BackdropOffset,0,H);
        else DrawCachedLayer(g,backgroundCache!,new RectangleF(0,0,W,H));
        g.CompositingMode=CompositingMode.SourceOver;
        float time=state==GameState.Round?frame:menuClock;
        DrawWorldAtmosphere(g,time);
    }

    private void SmoothGround(Graphics g)
    {
        BuildSmoothArena();float time=state==GameState.Round?frame:menuClock*.08f;
        if(themedArena)
        {
            if(scrollingFloor!=null)DrawScrollingLayer(g,scrollingFloor,FloorOffset,RunningSurfaceTop,H-RunningSurfaceTop);
            else DrawCachedLayer(g,groundTile!,new RectangleF(0,GroundY,W,H-GroundY));
        }
        else
        {
            float offset=(time*GetWorldSpeed())%240;
            for(float x=-offset;x<W;x+=240)DrawCachedLayer(g,groundTile!,new RectangleF(x,GroundY,240,100));
        }
        if(state==GameState.Round)
        {
            float height=Math.Max(0,GroundY-py-PlayerHeight),r=Math.Max(13,30-height*.035f);
            using var shadow=new SolidBrush(Color.FromArgb((int)Math.Clamp(80-height*.15f,12,80),4,10,20));g.FillEllipse(shadow,px+26-r,GroundY-4,r*2,8);
        }
    }

    private static void DrawCachedLayer(Graphics g,Image bitmap,RectangleF target)
    {
        using var transform=g.Transform;var m=transform.Elements;
        if(Math.Abs(m[1])<.001f&&Math.Abs(m[2])<.001f&&Math.Abs(target.Width*m[0]-bitmap.Width)<1.1f&&Math.Abs(target.Height*m[3]-bitmap.Height)<1.1f)
        {
            var saved=g.Save();g.ResetTransform();
            g.DrawImageUnscaled(bitmap,(int)MathF.Round(target.X*m[0]+m[4]),(int)MathF.Round(target.Y*m[3]+m[5]));g.Restore(saved);
        }
        else g.DrawImage(bitmap,target);
    }

    private void DrawResonanceMeter(Graphics g)
    {
        using var track=new SolidBrush(Color.FromArgb(65,8,15,29));g.FillRoundRectangle(track,new Rectangle(38,87,160,7),3,3);
        using var fill=new SolidBrush(resonance>=100?Color.FromArgb(255,215,137):Mint);g.FillRoundRectangle(fill,new RectangleF(38,87,160*resonance/100,7),3,3);
        Label(g,resonance>=100?"Q  역류 준비":"역류  "+$"{resonance:0}%",new(204,78,140,27),12,resonance>=100?Color.FromArgb(255,215,137):Paper,false);
    }
}
