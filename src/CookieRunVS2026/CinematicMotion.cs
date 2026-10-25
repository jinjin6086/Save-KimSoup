using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap[] finaleHeroMotion=Array.Empty<Bitmap>(),finaleHeroKeys=Array.Empty<Bitmap>();
    private const int FinaleMotionFrames=64;
    // Landmark registration aligns the head, planted feet, elbows and palms
    // before interpolation. A pose never dissolves into a second, offset head.
    private static readonly PointF[][] FinaleLandmarks =
    {
        Pose(.54f,.17f,.45f,.29f,.40f,.43f,.55f,.49f,.51f,.60f,.10f,.96f,.79f,.95f),
        Pose(.56f,.17f,.46f,.29f,.49f,.43f,.60f,.48f,.52f,.60f,.10f,.96f,.80f,.95f),
        Pose(.55f,.17f,.46f,.29f,.52f,.36f,.65f,.39f,.52f,.60f,.10f,.96f,.80f,.95f),
        Pose(.55f,.17f,.46f,.29f,.55f,.35f,.66f,.38f,.52f,.60f,.10f,.96f,.80f,.95f),
        Pose(.53f,.17f,.46f,.31f,.64f,.35f,.81f,.29f,.54f,.61f,.10f,.96f,.81f,.95f),
        Pose(.54f,.17f,.46f,.31f,.68f,.31f,.88f,.25f,.54f,.61f,.10f,.96f,.81f,.95f),
        Pose(.54f,.17f,.46f,.31f,.70f,.30f,.89f,.23f,.54f,.61f,.10f,.96f,.81f,.95f),
        Pose(.56f,.17f,.47f,.31f,.71f,.31f,.92f,.25f,.54f,.61f,.10f,.96f,.81f,.95f)
    };
    private static PointF[] Pose(params float[] p)
    {
        var points=new List<PointF>{new(0,0),new(1,0),new(0,1),new(1,1),new(.5f,1),new(.08f,.68f),new(.90f,.68f)};
        for(int i=0;i<p.Length;i+=2)points.Add(new(p[i],p[i+1]));
        return points.ToArray();
    }
    private void LoadCinematicMotion()
    {
        string path=AssetPath("HeroFinaleMotion8.png");if(!File.Exists(path))return;
        using var sheet=new Bitmap(path);finaleHeroKeys=new Bitmap[8];
        for(int i=0;i<8;i++)
        {
            int x=i%4*sheet.Width/4,y=i/4*sheet.Height/2;
            int right=(i%4+1)*sheet.Width/4,bottom=(i/4+1)*sheet.Height/2;
            var key=new Bitmap(384,384,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(key)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(sheet,new Rectangle(0,0,384,384),new Rectangle(x,y,right-x,bottom-y),GraphicsUnit.Pixel);}
            finaleHeroKeys[i]=key;
        }
        finaleHeroMotion=new Bitmap[FinaleMotionFrames];
        // This is performed once at load. Runtime animation only samples a
        // prepared, full-opacity frame; no image morphing or allocation in play.
        for(int f=0;f<FinaleMotionFrames;f++)
        {
            float p=f/(float)(FinaleMotionFrames-1)*7;int a=Math.Min(6,(int)p);float t=p-a;
            var target=FinaleLandmarks[a].Zip(FinaleLandmarks[a+1],(v,w)=>new PointF(Mix(v.X,w.X,t),Mix(v.Y,w.Y,t))).ToArray();
            using var left=WarpCinematicPose(finaleHeroKeys[a],FinaleLandmarks[a],target);
            using var right=WarpCinematicPose(finaleHeroKeys[a+1],FinaleLandmarks[a+1],target);
            var b=new Bitmap(384,384,PixelFormat.Format32bppPArgb);
            BlendRegisteredPoses(left,right,b,t);finaleHeroMotion[f]=b;
        }
        foreach(var key in finaleHeroKeys)BuildAttachedFire(key);
    }
    private static unsafe Bitmap WarpCinematicPose(Bitmap source,PointF[] from,PointF[] to)
    {
        int size=source.Width;var output=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        var input=source.LockBits(new(0,0,size,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var dest=output.LockBits(new(0,0,size,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size,dx=0,dy=0,sum=0;
            for(int j=0;j<to.Length;j++)
            {
                float ax=u-to[j].X,ay=v-to[j].Y,w=1/(.00035f+MathF.Pow(ax*ax+ay*ay,1.7f));
                dx+=(from[j].X-to[j].X)*w;dy+=(from[j].Y-to[j].Y)*w;sum+=w;
            }
            float sx=x+dx/sum*size,sy=y+dy/sum*size;int ix=(int)MathF.Floor(sx),iy=(int)MathF.Floor(sy);
            if(ix<0||iy<0||ix>=size-1||iy>=size-1)continue;
            float fx=sx-ix,fy=sy-iy;byte* p=(byte*)input.Scan0+iy*input.Stride+ix*4,op=(byte*)dest.Scan0+y*dest.Stride+x*4;
            for(int c=0;c<4;c++)op[c]=(byte)Mix(Mix(p[c],p[c+4],fx),Mix(p[c+input.Stride],p[c+input.Stride+4],fx),fy);
        }}finally{source.UnlockBits(input);output.UnlockBits(dest);}return output;
    }
    private static unsafe void BlendRegisteredPoses(Bitmap left,Bitmap right,Bitmap output,float t)
    {
        var rect=new Rectangle(0,0,left.Width,left.Height);
        var a=left.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var b=right.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var d=output.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<rect.Height;y++)for(int x=0;x<rect.Width*4;x++)
        {byte* p=(byte*)a.Scan0+y*a.Stride+x,q=(byte*)b.Scan0+y*b.Stride+x,r=(byte*)d.Scan0+y*d.Stride+x;*r=(byte)Mix(*p,*q,t);}}
        finally{left.UnlockBits(a);right.UnlockBits(b);output.UnlockBits(d);}
    }
    private float FinaleMotionProgress=>lastStandPhase switch
    {
        8=>SmoothUnit(Math.Clamp((lastStandClock-20)/185,0,1))*7,
        9 or 4 or 5=>7,_=>0
    };
    private PointF FinaleMotionPalm
    {
        get{float p=FinaleMotionProgress;int i=Math.Min(6,(int)p);float t=p-i;return new(Mix(FinaleLandmarks[i][10].X,FinaleLandmarks[i+1][10].X,t),Mix(FinaleLandmarks[i][10].Y,FinaleLandmarks[i+1][10].Y,t));}
    }
    private void DisposeCinematicMotion()
    {
        foreach(var b in finaleHeroMotion)b.Dispose();foreach(var b in finaleHeroKeys)b.Dispose();
        finaleHeroMotion=finaleHeroKeys=Array.Empty<Bitmap>();
    }
}
