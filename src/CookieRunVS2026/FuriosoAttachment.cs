using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private const int AttachedFlameSize=320,AttachedFlamePad=56,AttachedFlameBody=208;
    private readonly Dictionary<Image,Bitmap[]> attachedFuriosoFire=new();
    private Bitmap? furiosoCookieMask;
    private void PrepareFuriosoAttachments()
    {
        furiosoCookieMask=new Bitmap(80,110,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(furiosoCookieMask)){g.SmoothingMode=SmoothingMode.AntiAlias;g.FillEllipse(Brushes.White,1,1,78,108);}
        foreach(var source in motionFrames.Concat(kickFrames).Concat(slideFrames).Concat(finaleHeroPoses).Append(furiosoCookieMask))BuildAttachedFire(source);
        for(int i=0;i<Math.Min(kickFrames.Length,echoPoseFrames.Length);i++)
        {
            attachedFuriosoFire[echoPoseFrames[i]]=attachedFuriosoFire[kickFrames[i]];
            attachedFuriosoFire[preciseEchoPoseFrames[i]]=attachedFuriosoFire[kickFrames[i]];
        }
        if(motionFrames.Length>5&&echoCrouchPose!=null&&preciseEchoCrouchPose!=null)
        {
            attachedFuriosoFire[echoCrouchPose]=attachedFuriosoFire[motionFrames[5]];
            attachedFuriosoFire[preciseEchoCrouchPose]=attachedFuriosoFire[motionFrames[5]];
        }
        if(phantomKick!=null)BuildAttachedFire(phantomKick);
    }
    private static float FlameNoise(float x,float y)
    {
        static float H(int x,int y){uint n=unchecked((uint)(x*374761393+y*668265263));n=(n^(n>>13))*1274126177u;return (n^(n>>16))/4294967295f;}
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y);float u=SmoothUnit(x-ix),v=SmoothUnit(y-iy);
        return Mix(Mix(H(ix,iy),H(ix+1,iy),u),Mix(H(ix,iy+1),H(ix+1,iy+1),u),v);
    }
    private unsafe Bitmap[] BuildAttachedFire(Image source)
    {
        var cache=attachedFuriosoFire;
        if(cache.TryGetValue(source,out var cached))return cached;
        // Preserve the actor's exact 208-pixel mask while giving the enlarged
        // flame room outside it. Scaling a whole old aura would detach it from
        // the current hands, knees and feet and create a hollow second body.
        const int size=AttachedFlameSize,pad=AttachedFlamePad,body=AttachedFlameBody;int count=size*size;
        using var silhouette=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(silhouette)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(source,new Rectangle(pad,pad,body,body));}
        var distance=new float[count];
        var d=silhouette.LockBits(new(0,0,size,size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<size;y++){byte* row=(byte*)d.Scan0+y*d.Stride;for(int x=0;x<size;x++)distance[y*size+x]=row[x*4+3]>45?0:999;}}
        finally{silhouette.UnlockBits(d);}
        for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++)
        {
            int i=y*size+x;distance[i]=Math.Min(distance[i],Math.Min(Math.Min(distance[i-1]+1,distance[i-size]+1),Math.Min(distance[i-size-1]+1.4142f,distance[i-size+1]+1.4142f)));
        }
        for(int y=size-2;y>0;y--)for(int x=size-2;x>0;x--)
        {
            int i=y*size+x;distance[i]=Math.Min(distance[i],Math.Min(Math.Min(distance[i+1]+1,distance[i+size]+1),Math.Min(distance[i+size-1]+1.4142f,distance[i+size+1]+1.4142f)));
        }
        var frames=new Bitmap[32];
        for(int f=0;f<32;f++)
        {
            var material=furiosoFireBack[f];var output=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
            var input=material.LockBits(new(0,0,material.Width,material.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            var dest=output.LockBits(new(0,0,size,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{for(int y=1;y<size-1;y++){byte* row=(byte*)dest.Scan0+y*dest.Stride;for(int x=1;x<size-1;x++)
            {
                int i=y*size+x;float original=distance[i];if(original<.3f||original>61)continue;
                float turbulence=FlameNoise(x*.035f+f*.025f,y*.031f-f*.08f),flow=FlameNoise(x*.057f-f*.03f,y*.048f+f*.02f);
                int u=26+(int)(turbulence*49),v=((int)(y*.89f+f*2+(flow-.5f)*88)+512)%256;
                byte* tex=(byte*)input.Scan0+v*input.Stride+u*4;
                if(tex[3]<35)tex=(byte*)input.Scan0+v*input.Stride+(191-u)*4;
                float heat=tex[3]/255f;if(heat<.03f)continue;
                float wind=original;
                for(int rise=5;rise<=45&&y+rise<size-1;rise+=5)
                {
                    int shift=(int)(MathF.Sin((y+rise)*.051f+f*MathF.Tau/32)*rise*.17f),xx=Math.Clamp(x+shift,1,size-2);
                    wind=Math.Min(wind,distance[(y+rise)*size+xx]+rise*.38f);
                }
                float reach=8+heat*36,edge=1-SmoothUnit(wind/reach);
                // The irregular outer veil is still connected to the same
                // distance field; no orbiting strands or independent tendrils.
                float veil=(1-SmoothUnit(wind/(reach*1.23f)))*.14f;
                float a=Math.Max(edge,veil)*Math.Min(1,original*.7f);byte* pixel=row+x*4;
                for(int c=0;c<4;c++)pixel[c]=(byte)(tex[c]*a);
            }}}
            finally{material.UnlockBits(input);output.UnlockBits(dest);}
            frames[f]=output;
        }
        cache[source]=frames;return frames;
    }
    private void DrawAttachedFire(Graphics g,Image? actor,RectangleF body,float alpha,float clock)
    {
        actor??=furiosoCookieMask;if(actor==null||alpha<=.002f)return;
        var frames=BuildAttachedFire(actor);float f=((clock%32)+32)%32;int i=(int)f;float mix=f-i;
        float px=body.Width*AttachedFlamePad/AttachedFlameBody,py=body.Height*AttachedFlamePad/AttachedFlameBody;
        var rect=new RectangleF(body.X-px,body.Y-py,body.Width+px*2,body.Height+py*2);
        // Stable fractional coordinates avoid a one-alpha-value shimmer when
        // the same attached texture is translated by whole world pixels.
        rect=new(MathF.Round(rect.X*64)/64,MathF.Round(rect.Y*64)/64,MathF.Round(rect.Width*64)/64,MathF.Round(rect.Height*64)/64);
        // The 32 authored/advected frames already interpolate the source cells.
        // Sampling one at nearly display cadence avoids a second alpha resample.
        DrawEchoFrame(g,frames[i],rect,alpha>=.95f?1:alpha);
    }
    private void DisposeFuriosoAttachments()
    {
        foreach(var frames in attachedFuriosoFire.Values.Distinct())foreach(var b in frames)b.Dispose();attachedFuriosoFire.Clear();furiosoCookieMask?.Dispose();
    }
}
