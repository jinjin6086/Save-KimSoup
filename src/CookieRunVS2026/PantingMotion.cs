using System.Drawing.Imaging;
namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap[] pantingPoses=Array.Empty<Bitmap>();
    private unsafe void PreparePantingMotion()
    {
        if(endingIdle==null||pantingPoses.Length>0)return;
        int w=endingIdle.Width,h=endingIdle.Height;var source=endingIdle.LockBits(new(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        pantingPoses=new Bitmap[16];
        try{for(int f=0;f<pantingPoses.Length;f++)
        {
            var image=new Bitmap(w,h,PixelFormat.Format32bppPArgb);pantingPoses[f]=image;
            var target=image.LockBits(new(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            float breath=.5f-.5f*MathF.Cos(f/(float)pantingPoses.Length*MathF.Tau);
            try{for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float torso=1-SmoothUnit((y/(float)h-.31f)/.41f);
                // Only the chest, shoulders and head heave. The knees and
                // shoes stay planted instead of bouncing the whole picture.
                float sx=x-(2+breath*3)*torso,sy=y-breath*4*torso;
                int ix=(int)MathF.Floor(sx),iy=(int)MathF.Floor(sy);if(ix<0||iy<0||ix>=w-1||iy>=h-1)continue;
                float fx=sx-ix,fy=sy-iy;byte* p=(byte*)source.Scan0+iy*source.Stride+ix*4,q=(byte*)target.Scan0+y*target.Stride+x*4;
                for(int c=0;c<4;c++)q[c]=(byte)Mix(Mix(p[c],p[c+4],fx),Mix(p[c+source.Stride],p[c+source.Stride+4],fx),fy);
            }}finally{image.UnlockBits(target);}
        }}finally{endingIdle.UnlockBits(source);}
    }
    private void DisposePantingMotion(){foreach(var image in pantingPoses)image.Dispose();pantingPoses=Array.Empty<Bitmap>();}
}
