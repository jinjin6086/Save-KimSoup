using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? trueStopStaticScene,trueStopDynamicScene;
    private int trueStopStaticCycle=-1;
    private bool trueStopCapturingBase;
    private bool trueStopDrawingDynamic;
    private void DisposeTrueStopSurfaces()
    {
        trueStopStaticScene?.Dispose();trueStopDynamicScene?.Dispose();
        trueStopStaticScene=trueStopDynamicScene=null;trueStopStaticCycle=-1;
    }
    private void PaintTruePurgaeFrozenFrame(Graphics g,float scale)
    {
        int width=Math.Max(1,(int)(W*scale)),height=Math.Max(1,(int)(H*scale));
        if(trueStopStaticScene==null||trueStopStaticScene.Width!=width||trueStopStaticScene.Height!=height||trueStopStaticCycle!=trueStopCycle)
        {
            DisposeTrueStopSurfaces();trueStopStaticCycle=trueStopCycle;
            trueStopStaticScene=new Bitmap(width,height,PixelFormat.Format32bppRgb);
            trueStopDynamicScene=new Bitmap(width,Math.Max(1,(int)(515*scale)),PixelFormat.Format32bppPArgb);
            trueStopCapturingBase=renderingScene=true;
            try{using var cg=Graphics.FromImage(trueStopStaticScene);cg.ScaleTransform(scale,scale);RenderArenaFrame(cg);}
            finally{trueStopCapturingBase=renderingScene=false;}
            DesaturateScene(trueStopStaticScene);
        }
        // Cache the stationary arena. Draw all actors through the normal world
        // path so the runner, attacks and skill effects retain their layer order.
        using(var cg=Graphics.FromImage(trueStopDynamicScene!))
        {
            cg.Clear(Color.Transparent);cg.ScaleTransform(scale,scale);
            cg.SmoothingMode=SmoothingMode.AntiAlias;cg.InterpolationMode=InterpolationMode.HighQualityBilinear;
            cg.CompositingQuality=CompositingQuality.HighSpeed;
            cg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            trueStopDrawingDynamic=true;
            try{DrawWorld(cg);}finally{trueStopDrawingDynamic=false;}
        }
        DesaturateTransparentScene(trueStopDynamicScene!);
        var compositing=g.CompositingMode;g.CompositingMode=CompositingMode.SourceCopy;
        DrawCachedLayer(g,trueStopStaticScene,new RectangleF(0,0,W,H));g.CompositingMode=compositing;
        DrawCachedLayer(g,trueStopDynamicScene!,new RectangleF(0,0,W,515));
        g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        DrawPracticeHud(g);PremiumHud(g);DrawPause(g);
    }
    private static unsafe void DesaturateTransparentScene(Bitmap bitmap)
    {
        var data=bitmap.LockBits(new(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try
        {
            for(int y=0;y<data.Height;y++)
            {
                uint* row=(uint*)((byte*)data.Scan0+y*data.Stride);
                for(int x=0;x<data.Width;x++)
                {uint c=row[x],v=(((c>>16)&255)*77+((c>>8)&255)*150+(c&255)*29)>>8;row[x]=(c&0xff000000)|v*0x010101;}
            }
        }
        finally{bitmap.UnlockBits(data);}
    }
}
