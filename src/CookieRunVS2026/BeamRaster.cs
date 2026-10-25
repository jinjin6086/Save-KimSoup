using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private Bitmap? blueBeamSurface,pinkBeamSurface;
    private readonly ParallelOptions beamWorkers=new(){MaxDegreeOfParallelism=Math.Max(1,Math.Min(8,Environment.ProcessorCount))};
    // Resample the premultiplied plasma once, at the final device resolution.
    // GDI+ otherwise repeats a costly rotated alpha reconstruction for every
    // layer. Disjoint scanlines can be shaded independently on CPU cores.
    private unsafe void DrawRasterizedPlasma(Graphics g,Bitmap source,float length,float radius,float energy,bool blue)
    {
        using var matrix=g.Transform;var axis=new[]{new PointF(0,0),new PointF(length,0),new PointF(0,radius*3)};matrix.TransformPoints(axis);
        float dx=axis[1].X-axis[0].X,dy=axis[1].Y-axis[0].Y,len=MathF.Sqrt(dx*dx+dy*dy);
        float high=MathF.Sqrt(MathF.Pow(axis[2].X-axis[0].X,2)+MathF.Pow(axis[2].Y-axis[0].Y,2));
        if(len<.3f||high<.3f)return;
        float cosine=dx/len,sine=dy/len;
        float marginX=MathF.Abs(sine)*high*.5f+3,marginY=MathF.Abs(cosine)*high*.5f+3;
        var visible=g.VisibleClipBounds;var cp=new[]{new PointF(visible.Left,visible.Top),new PointF(visible.Right,visible.Top),new PointF(visible.Left,visible.Bottom),new PointF(visible.Right,visible.Bottom)};matrix.TransformPoints(cp);
        int left=(int)MathF.Floor(Math.Max(cp.Min(p=>p.X),Math.Min(axis[0].X,axis[1].X)-marginX));
        int top=(int)MathF.Floor(Math.Max(cp.Min(p=>p.Y),Math.Min(axis[0].Y,axis[1].Y)-marginY));
        int right=(int)MathF.Ceiling(Math.Min(cp.Max(p=>p.X),Math.Max(axis[0].X,axis[1].X)+marginX));
        int bottom=(int)MathF.Ceiling(Math.Min(cp.Max(p=>p.Y),Math.Max(axis[0].Y,axis[1].Y)+marginY));
        int w=right-left,h=bottom-top;if(w<=0||h<=0||w>16000||h>16000)return;
        Bitmap? buffer=blue?blueBeamSurface:pinkBeamSurface;
        if(buffer==null||buffer.Width<w||buffer.Height<h)
        {
            int bw=Math.Max(buffer?.Width??0,(w+127)/128*128),bh=Math.Max(buffer?.Height??0,(h+127)/128*128);
            buffer?.Dispose();buffer=new Bitmap(bw,bh,PixelFormat.Format32bppPArgb);
            if(blue)blueBeamSurface=buffer;else pinkBeamSurface=buffer;
        }
        var input=source.LockBits(new(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var dest=buffer.LockBits(new(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        nint src=input.Scan0,dst=dest.Scan0;int ss=input.Stride,ds=dest.Stride,sw=source.Width,sh=source.Height;
        float du=cosine/len*(sw-1),dv=-sine/high*(sh-1),originX=axis[0].X,originY=axis[0].Y;
        try
        {
            Parallel.For(0,h,beamWorkers,y=>
            {
                float px=left+.5f-originX,py=top+y+.5f-originY;
                float u=(px*cosine+py*sine)/len*(sw-1),v=((-px*sine+py*cosine)/high+.5f)*(sh-1);
                byte* row=(byte*)dst+y*ds;
                for(int x=0;x<w;x++,u+=du,v+=dv)
                {
                    byte* outp=row+x*4;*(uint*)outp=0;int ix=(int)MathF.Floor(u),iy=(int)MathF.Floor(v);
                    if(ix<0||iy<0||ix>=sw-1||iy>=sh-1)continue;
                    byte* p=(byte*)src+iy*ss+ix*4;
                    if((p[3]|p[7]|p[ss+3]|p[ss+7])==0)continue;
                    float fx=u-ix,fy=v-iy,a=(1-fx)*(1-fy)*energy,b=fx*(1-fy)*energy,c=(1-fx)*fy*energy,d=fx*fy*energy;
                    for(int k=0;k<4;k++)outp[k]=(byte)Math.Clamp(p[k]*a+p[k+4]*b+p[k+ss]*c+p[k+ss+4]*d+.5f,0,255);
                }
            });
        }
        finally{source.UnlockBits(input);buffer.UnlockBits(dest);}
        var state=g.Save();g.ResetTransform();g.DrawImageUnscaledAndClipped(buffer,new Rectangle(left,top,w,h));g.Restore(state);
    }
    private void DisposeBeamSurfaces(){blueBeamSurface?.Dispose();pinkBeamSurface?.Dispose();blueBeamSurface=pinkBeamSurface=null;}
}
