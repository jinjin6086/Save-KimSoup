using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private readonly Bitmap[] collisionVolume=new Bitmap[40];
    private readonly Bitmap[] detonationVolume=new Bitmap[36];
    private void PrepareCollisionVolume()
    {
        string path=AssetPath("CollisionFlowHD.png");if(!File.Exists(path))return;
        using var flow=new Bitmap(path);var keys=new Bitmap[4];
        try
        {
            for(int i=0;i<4;i++)
            {
                keys[i]=new Bitmap(512,512,PixelFormat.Format32bppPArgb);
                using var kg=Graphics.FromImage(keys[i]);kg.InterpolationMode=InterpolationMode.HighQualityBicubic;
                // Use only the user-selected back-curved flow sheet. Register
                // its throat; no second rocky texture or orb overlays it.
                float[] cx={415,415,420,417},cy={299,302,308,304};
                float scale=.56f*627/(flow.Width/2f),cell=flow.Width/2f;
                kg.DrawImage(flow,new RectangleF(256-cx[i]*.56f,256-cy[i]*.56f,cell*scale,flow.Height/2f*scale),new RectangleF(i%2*cell,i/2*(flow.Height/2f),cell,flow.Height/2f),GraphicsUnit.Pixel);
            }
            for(int f=0;f<40;f++)
            {
                float p=f/10f;int i=(int)p;float t=SmoothUnit(p-i);
                using var mixed=new Bitmap(512,512,PixelFormat.Format32bppPArgb);
                BlendRegisteredPoses(keys[i],keys[(i+1)%4],mixed,t);
                collisionVolume[f]=AdvectCollisionSheet(mixed,f*MathF.Tau/40);
            }
        }
        finally{foreach(var k in keys)k?.Dispose();}
        path=AssetPath("FinalDetonationHD.png");
        if(File.Exists(path))
        {
            using var atlas=new Bitmap(path);var stages=new Bitmap[6];
            try
            {
                for(int i=0;i<6;i++)stages[i]=CropVfxCell(atlas,new(i%3*(atlas.Width/3),i/3*(atlas.Height/2),atlas.Width/3,atlas.Height/2),512,512);
                for(int f=0;f<36;f++)
                {
                    float p=f/35f*5;int i=Math.Min(4,(int)p);float blend=SmoothUnit(p-i);
                    using var mixed=new Bitmap(512,512,PixelFormat.Format32bppPArgb);BlendRegisteredPoses(stages[i],stages[i+1],mixed,blend);
                    detonationVolume[f]=AdvectCollision(mixed,f*.13f);
                }
            }
            finally{foreach(var b in stages)b?.Dispose();}
        }
    }
    private static unsafe Bitmap AdvectCollisionSheet(Bitmap source,float phase)
    {
        int w=source.Width;var output=new Bitmap(w,w,PixelFormat.Format32bppPArgb);
        var input=source.LockBits(new(0,0,w,w),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var dest=output.LockBits(new(0,0,w,w),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<w;y++)for(int x=0;x<w;x++)
        {
            float u=x/(float)(w-1)*2-1,v=y/(float)(w-1)*2-1;
            // Deflected flow opens back toward its own incoming beam. Pull
            // the crest away from the narrow throat as it travels up/down.
            // No radial mask: that would turn the sheet back into an orb.
            float fan=MathF.Pow(MathF.Abs(v),1.45f)*.16f;
            float sx=w*.5f+(u-MathF.Sign(u)*fan)*w*.5f+MathF.Sin(v*10-phase*2)*3.8f*MathF.Abs(v);
            float sy=y+MathF.Sign(v)*(MathF.Sin(MathF.Abs(v)*23-phase*4)*4.3f+MathF.Sin(MathF.Abs(v)*43-phase*5)*1.8f)+MathF.Sin(u*13+phase*2)*1.2f;
            int ix=(int)MathF.Floor(sx),iy=(int)MathF.Floor(sy);if(ix<0||iy<0||ix>=w-1||iy>=w-1)continue;
            float fx=sx-ix,fy=sy-iy;
            float edge=(1-SmoothUnit((MathF.Abs(u)-.90f)/.10f))*(1-SmoothUnit((MathF.Abs(v)-.91f)/.09f));
            byte* p=(byte*)input.Scan0+iy*input.Stride+ix*4,q=(byte*)dest.Scan0+y*dest.Stride+x*4;
            for(int c=0;c<4;c++)q[c]=(byte)(Mix(Mix(p[c],p[c+4],fx),Mix(p[c+input.Stride],p[c+input.Stride+4],fx),fy)*edge);
        }}finally{source.UnlockBits(input);output.UnlockBits(dest);}return output;
    }
    private static unsafe Bitmap AdvectCollision(Bitmap source,float phase)
    {
        int w=source.Width;var output=new Bitmap(w,w,PixelFormat.Format32bppPArgb);
        var input=source.LockBits(new(0,0,w,w),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var dest=output.LockBits(new(0,0,w,w),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<w;y++)for(int x=0;x<w;x++)
        {
            float u=(x-w*.5f)/(w*.5f),v=(y-w*.5f)/(w*.5f);
            float r=MathF.Sqrt(u*u+v*v),a=MathF.Atan2(v,u),turn=MathF.Sin(phase+a*3+r*7)*.035f*(1-Math.Min(1,r));
            float sx=w*.5f+MathF.Cos(a+turn)*r*w*.5f+MathF.Sin(y*.04f+phase)*2.3f,sy=w*.5f+MathF.Sin(a+turn)*r*w*.5f+MathF.Cos(x*.035f-phase)*2.3f;
            int ix=(int)sx,iy=(int)sy;if(ix<0||iy<0||ix>=w-1||iy>=w-1)continue;
            float fx=sx-ix,fy=sy-iy,edge=1-SmoothUnit((r-.85f)/.15f);
            byte* p=(byte*)input.Scan0+iy*input.Stride+ix*4,q=(byte*)dest.Scan0+y*dest.Stride+x*4;
            for(int c=0;c<4;c++)q[c]=(byte)(Mix(Mix(p[c],p[c+4],fx),Mix(p[c+input.Stride],p[c+input.Stride+4],fx),fy)*edge);
        }}finally{source.UnlockBits(input);output.UnlockBits(dest);}return output;
    }
    private void DisposeCollisionVolume(){foreach(var b in collisionVolume)b?.Dispose();Array.Clear(collisionVolume);foreach(var b in detonationVolume)b?.Dispose();Array.Clear(detonationVolume);}
    private void DrawCollisionDeflection(Graphics g,float x,float y,float strength)
    {
        float clock=PlasmaClock,pulse=Math.Min(1,lastStandPulse/9);
        float spread=(1+pulse*.075f+MathF.Sin(clock*.23f)*.018f)*strength;
        float rx=89*spread,ry=128*spread;
        float angle=MathF.Atan2(lastBossY-lastHeroY,lastBossX-lastHeroX);
        var s=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle*180/MathF.PI);g.InterpolationMode=InterpolationMode.Bilinear;
        int frame=((int)(clock*1.27f)%40+40)%40;
        if(collisionVolume[frame]!=null)DrawEchoFrame(g,collisionVolume[frame],new(-rx,-ry,rx*2,ry*2),1);
        DrawLaserBloom(g,0,0,4*spread,22*spread,Color.FromArgb(236,250,255),.80f);
        if(!reducedEffects)
        {
            // A few curved pressure crests follow each sheet's backflow. They
            // travel away from contact, never orbit a circular boundary.
            for(int k=0;k<8;k++)
            {
                float p=(clock*.024f+k*.171f)%1,side=k%2==0?-1:1,up=k%4<2?-1:1;
                float t0=Math.Max(0,p-.20f),fade=MathF.Sin(p*MathF.PI)*.65f;
                if(p-t0<.005f)continue;
                PointF At(float t)=>new(side*rx*(.03f+t*t*.95f),up*ry*(.05f+t*.93f));
                PointF Deriv(float t)=>new(side*rx*t*1.9f,up*ry*.93f);
                var a=At(t0);var b=At(p);var d0=Deriv(t0);var d1=Deriv(p);float dt=(p-t0)/3;
                using var arc=new GraphicsPath();arc.AddBezier(a,new(a.X+d0.X*dt,a.Y+d0.Y*dt),new(b.X-d1.X*dt,b.Y-d1.Y*dt),b);
                g.DrawPath(ParticlePen(side<0?ClashBlue:ClashPink,(int)(140*fade),1),arc);
            }
        }
        g.Restore(s);
    }
    private void DrawApotheosisDetonation(Graphics g,float x,float y,float progress,float power)
    {
        if(gpuPrototypeDrawing)return;
        float p=Math.Clamp(progress,0,1),fade=(1-SmoothUnit((p-.30f)/.70f))*power;
        if(fade<.002f)return;
        float r=32+MathF.Pow(p,.43f)*270;
        DrawLaserBloom(g,x,y,r*1.45f,r*1.18f,ClashBlue,Math.Min(.9f,fade*.8f));
        int frame=Math.Clamp((int)(p*35),0,35);
        if(detonationVolume[frame]!=null)
        {
            DrawEchoFrame(g,detonationVolume[frame],new(x-r*1.23f,y-r*.91f,r*2.46f,r*1.82f),fade);
        }
        DrawLaserBloom(g,x,y,r*.47f,r*.58f,Color.FromArgb(225,248,255),Math.Min(1,fade*.94f));
        if(p<.1f&&!reducedEffects)DrawLaserBloom(g,x,y,650,210,ClashBlue,(1-p/.1f)*.54f);
    }
}
