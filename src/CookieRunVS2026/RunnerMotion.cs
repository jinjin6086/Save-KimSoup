using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? scrollingBackdrop,scrollingFloor;
    private float worldTravelDistance,slideMotionClock,slideExitTimer;
    private bool previousSlide;
    private Bitmap[] slideFrames=Array.Empty<Bitmap>();
    private Image? melodyMenhera;
    private const int RunningSurfaceTop=GroundY-34;
    private float BackdropOffset=>worldTravelDistance*.13f;
    private float FloorOffset=>worldTravelDistance;
    private Image? PhaseNextImage=>phaseIntroKind switch{1=>enemy7??enemy3,2=>melodyMenhera??melodyAngry??enemy5,_=>null};

    private void LoadSurgeMotion()
    {
        string file=AssetPath("PlayerSlide.png");
        if(File.Exists(file))
        {
            using var sheet=new Bitmap(file);int cell=sheet.Width/3;slideFrames=new Bitmap[3];
            for(int i=0;i<3;i++)slideFrames[i]=ExtractMainSprite(sheet,new(i*cell,0,cell,sheet.Height),240,110);
        }
        file=AssetPath("MelodyMenhera.png");
        if(File.Exists(file)){using var sheet=new Bitmap(file);melodyMenhera=ExtractMainSprite(sheet,new(0,0,sheet.Width,sheet.Height),256,290);}
    }
    private void ResetRunnerMotion(){worldTravelDistance=slideMotionClock=slideExitTimer=0;previousSlide=false;}
    private void UpdateRunnerMotion(float step)
    {
        if(sliding){slideMotionClock+=step;slideExitTimer=0;}
        else
        {
            if(previousSlide)slideExitTimer=6;
            else slideExitTimer=Math.Max(0,slideExitTimer-step);
            slideMotionClock=0;
        }
        previousSlide=sliding;
    }
    private void DisposeScrollingLayers(){scrollingBackdrop?.Dispose();scrollingFloor?.Dispose();scrollingBackdrop=scrollingFloor=null;}
    private void DisposeSurgeMotion(){foreach(var image in slideFrames)image.Dispose();melodyMenhera?.Dispose();}

    private void BuildScrollingLayers()
    {
        DisposeScrollingLayers();
        if(backgroundCache==null)return;
        int w=backgroundCache.Width,h=backgroundCache.Height;
        scrollingBackdrop=new Bitmap(w*2,h,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(scrollingBackdrop))
        {
            g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(backgroundCache,0,0);
            var saved=g.Save();g.TranslateTransform(w*2,0);g.ScaleTransform(-1,1);g.DrawImageUnscaled(backgroundCache,0,0);g.Restore(saved);
        }
        int top=(int)MathF.Round(RunningSurfaceTop*nativeArtScale),height=h-top;
        scrollingFloor=new Bitmap(w*2,height,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(scrollingFloor))
        {
            g.CompositingMode=CompositingMode.SourceCopy;
            g.DrawImage(scrollingBackdrop,new Rectangle(0,0,w*2,height),new Rectangle(0,top,w*2,height),GraphicsUnit.Pixel);
        }
        FeatherFloor(scrollingFloor,Math.Max(1,(int)(19*nativeArtScale)));
    }
    private static unsafe void FeatherFloor(Bitmap bitmap,int rows)
    {
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,Math.Min(rows,bitmap.Height)),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try
        {
            for(int y=0;y<data.Height;y++)
            {
                float t=y/(float)Math.Max(1,data.Height-1),weight=t*t*(3-2*t);byte* row=(byte*)data.Scan0+y*data.Stride;
                for(int x=0;x<data.Width*4;x++)row[x]=(byte)(row[x]*weight);
            }
        }
        finally{bitmap.UnlockBits(data);}
    }
    private static float WrapWorldDistance(float value)=>((value%(W*2))+W*2)%(W*2);
    private void DrawScrollingLayer(Graphics g,Bitmap image,float distance,int top,int height)
    {
        float offset=WrapWorldDistance(distance);
        DrawCachedLayer(g,image,new RectangleF(-offset,top,W*2,height));
        if(offset>W)DrawCachedLayer(g,image,new RectangleF(W*2-offset,top,W*2,height));
    }

    private void DrawSlidePose(Graphics g,int pose,float alpha)
    {
        if(slideFrames.Length!=3)return;
        var rect=new RectangleF(px-25,GroundY-46,110,46);
        DrawPlayerSprite(g,slideFrames[pose],rect,Mint,alpha);
    }
    private void DrawSlidingActor(Graphics g)
    {
        float blend=Math.Clamp(slideMotionClock/6,0,1);
        if(blend<1)DrawSlidePose(g,0,1-blend);
        if(blend>0)DrawSlidePose(g,1,blend);
        if(!reducedEffects)
            for(int i=0;i<8;i++)
            {
                float t=(animationClock*.065f+i*.125f)%1,x=px-20-t*65,y=GroundY-2-MathF.Sin(t*MathF.PI)*6;
                g.FillEllipse(ParticleBrush(Color.FromArgb(148,118,89),(int)(110*(1-t))),x,y,3+t*4,2+t*2);
            }
    }
    private void DrawCookieSlide(Graphics g)
    {
        if(wolfPoses.Length==12){DrawWolfPose(g,5,new(px-31,GroundY-47,125,52));return;}
        if(slideFrames.Length==3&&TryDrawCinderGolem(g,slideFrames[1],new(px-25,GroundY-46,110,46),Paper,1))return;
        using var body=new SolidBrush(cloneActive?Color.FromArgb(64,216,106):Color.HotPink);using var light=new SolidBrush(cloneActive?Color.FromArgb(140,255,167):Color.FromArgb(255,162,207));
        using var edge=new Pen(Color.FromArgb(222,255,255,255),2.3f);
        g.FillEllipse(body,px-9,GroundY-31,50,25);g.DrawEllipse(edge,px-9,GroundY-31,50,25);
        g.FillEllipse(light,px-6,GroundY-43,29,27);g.DrawEllipse(edge,px-6,GroundY-43,29,27);
        using var leg=new Pen(cloneActive?Color.FromArgb(64,216,106):Color.HotPink,10){StartCap=LineCap.Round,EndCap=LineCap.Round};
        g.DrawLine(leg,px+26,GroundY-12,px+67,GroundY-7);g.DrawLine(leg,px+14,GroundY-10,px+49,GroundY-2);
        g.FillEllipse(Brushes.White,px+60,GroundY-13,17,10);g.FillEllipse(Brushes.Black,px+7,GroundY-34,3,4);g.FillEllipse(Brushes.Black,px+16,GroundY-34,3,4);
    }
}
