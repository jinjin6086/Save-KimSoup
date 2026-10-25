using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Artistic smoke is loaded once. Organic band deformation and layer compositing
    // are baked into a bounded cycle before play. Two persistent small buffers blend
    // neighboring cached poses continuously, leaving one live image blit per aura.
    private Bitmap? guardAuraMaterial,guardAuraMirror,abyssAuraMaterial,abyssAuraMirror;
    private readonly ImageAttributes auraAttributes=new();
    private readonly ColorMatrix auraMatrix=new();
    private readonly PointF[] auraQuad=new PointF[3];
    private readonly Dictionary<Image,Bitmap[]> auraCycles=new();
    private Bitmap[] guardAuraCycle=Array.Empty<Bitmap>(),abyssAuraCycle=Array.Empty<Bitmap>();
    private BitmapData[] guardAuraPixels=Array.Empty<BitmapData>(),abyssAuraPixels=Array.Empty<BitmapData>();
    private Bitmap? guardAuraBlend,abyssAuraBlend;
    private int guardAuraBlendKey=-1,abyssAuraBlendKey=-1;
    private const int AuraCycleLength=20;

    private void LoadHighFidelityAuras()
    {
        string path=AssetPath("AuraMaterials.png");
        if(!File.Exists(path))return;
        using var atlas=new Bitmap(path);
        guardAuraMaterial=CacheAuraMaterial(atlas,0,false);
        guardAuraMirror=CacheAuraMaterial(atlas,0,true);
        abyssAuraMaterial=CacheAuraMaterial(atlas,1,false);
        abyssAuraMirror=CacheAuraMaterial(atlas,1,true);
        // The orange mantle is replaced by small orbital flames; only abyss frames are baked.
        CacheAuraCycle(abyssAuraMaterial,22,.86f);CacheAuraCycle(abyssAuraMirror,18,.67f);
        guardAuraCycle=Array.Empty<Bitmap>();
        abyssAuraCycle=ComposeAuraCycle(abyssAuraMaterial,abyssAuraMirror,true);
        guardAuraPixels=Array.Empty<BitmapData>();abyssAuraPixels=LockAuraFrames(abyssAuraCycle);
        guardAuraBlend=null;
        abyssAuraBlend=new Bitmap(300,400,PixelFormat.Format32bppPArgb);
        foreach(var cycle in auraCycles.Values)foreach(var image in cycle)image.Dispose();auraCycles.Clear();
    }

    private static Bitmap CacheAuraMaterial(Bitmap atlas,int cell,bool mirror)
    {
        var texture=new Bitmap(300,400,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(texture);
        g.CompositingMode=CompositingMode.SourceCopy;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        int width=atlas.Width/2;
        PointF[] points=mirror?new[]{new PointF(300,0),new PointF(0,0),new PointF(300,400)}:
            new[]{new PointF(0,0),new PointF(300,0),new PointF(0,400)};
        g.DrawImage(atlas,points,new RectangleF(cell*width,0,width,atlas.Height),GraphicsUnit.Pixel);
        return texture;
    }

    private void DisposeHighFidelityAuras()
    {
        foreach(var cycle in auraCycles.Values)foreach(var image in cycle)image.Dispose();auraCycles.Clear();
        UnlockAuraFrames(guardAuraCycle,guardAuraPixels);UnlockAuraFrames(abyssAuraCycle,abyssAuraPixels);
        guardAuraPixels=Array.Empty<BitmapData>();abyssAuraPixels=Array.Empty<BitmapData>();
        foreach(var image in guardAuraCycle)image.Dispose();foreach(var image in abyssAuraCycle)image.Dispose();
        guardAuraCycle=Array.Empty<Bitmap>();abyssAuraCycle=Array.Empty<Bitmap>();
        abyssHeatGlow?.Dispose();guardAuraBlend?.Dispose();abyssAuraBlend?.Dispose();
        guardAuraMaterial?.Dispose();guardAuraMirror?.Dispose();
        abyssAuraMaterial?.Dispose();abyssAuraMirror?.Dispose();auraAttributes.Dispose();
    }

    private void CacheAuraCycle(Bitmap texture,float flow,float opacity)
    {
        var frames=new Bitmap[AuraCycleLength];
        for(int frameIndex=0;frameIndex<frames.Length;frameIndex++)
        {
            float phase=frameIndex*MathF.Tau/AuraCycleLength;
            var frameImage=new Bitmap(texture.Width,texture.Height,PixelFormat.Format32bppPArgb);
            using var g=Graphics.FromImage(frameImage);
            g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBilinear;
            auraMatrix.Matrix33=opacity;auraAttributes.SetColorMatrix(auraMatrix);
            const int bands=20;float bandHeight=texture.Height/(float)bands;
            for(int band=0;band<bands;band++)
            {
                float v=band/(float)bands,v2=(band+1)/(float)bands;
                float topShift=(MathF.Sin(v*7+phase)+MathF.Sin(v*13-phase*2)*.38f)*flow*MathF.Sin(v*MathF.PI);
                float bottomShift=(MathF.Sin(v2*7+phase)+MathF.Sin(v2*13-phase*2)*.38f)*flow*MathF.Sin(v2*MathF.PI);
                auraQuad[0]=new(topShift,band*bandHeight);auraQuad[1]=new(texture.Width+topShift,band*bandHeight);auraQuad[2]=new(bottomShift,(band+1)*bandHeight+.55f);
                g.DrawImage(texture,auraQuad,new RectangleF(0,band*bandHeight,texture.Width,Math.Min(bandHeight+.7f,texture.Height-band*bandHeight)),GraphicsUnit.Pixel,auraAttributes);
            }
            frames[frameIndex]=frameImage;
        }
        auraCycles[texture]=frames;
    }

    private Bitmap[] ComposeAuraCycle(Image main,Image outer,bool abyss)
    {
        var result=new Bitmap[AuraCycleLength];
        for(int i=0;i<AuraCycleLength;i++)
        {
            var image=new Bitmap(300,400,PixelFormat.Format32bppPArgb);
            using var g=Graphics.FromImage(image);g.InterpolationMode=InterpolationMode.HighQualityBilinear;
            if(abyss)DrawAbyssHeatCore(g,150,204,295,.89f+MathF.Sin(i*MathF.Tau/AuraCycleLength*2)*.09f);
            g.DrawImage(auraCycles[outer][(AuraCycleLength-i)%AuraCycleLength],new Rectangle(0,0,300,400));
            g.DrawImage(auraCycles[main][i],abyss?new Rectangle(36,64,228,328):new Rectangle(22,27,260,364));
            result[i]=image;
        }
        return result;
    }

    private static BitmapData[] LockAuraFrames(Bitmap[] frames)
    {
        var pixels=new BitmapData[frames.Length];
        for(int i=0;i<frames.Length;i++)pixels[i]=frames[i].LockBits(new Rectangle(0,0,frames[i].Width,frames[i].Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        return pixels;
    }
    private static void UnlockAuraFrames(Bitmap[] frames,BitmapData[] pixels)
    {
        for(int i=0;i<Math.Min(frames.Length,pixels.Length);i++)frames[i].UnlockBits(pixels[i]);
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe void BlendAuraFrames(BitmapData from,BitmapData to,Bitmap target,int weight)
    {
        var destination=target.LockBits(new Rectangle(0,0,target.Width,target.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try
        {
            uint nextWeight=(uint)weight,previousWeight=256-nextWeight;
            const uint mask=0x00ff00ff;
            var maskV=Vector256.Create(mask);var previousV=Vector256.Create(previousWeight);var nextV=Vector256.Create(nextWeight);
            for(int y=0;y<target.Height;y++)
            {
                uint* previous=(uint*)((byte*)from.Scan0+y*from.Stride);
                uint* next=(uint*)((byte*)to.Scan0+y*to.Stride);
                uint* output=(uint*)((byte*)destination.Scan0+y*destination.Stride);
                int x=0;
                if(Vector256.IsHardwareAccelerated)
                    for(;x+8<=target.Width;x+=8)
                    {
                        var a=Vector256.Load(previous+x);var b=Vector256.Load(next+x);
                        var rb=(((a&maskV)*previousV+(b&maskV)*nextV)>>8)&maskV;
                        var ga=((((a>>8)&maskV)*previousV+((b>>8)&maskV)*nextV)>>8)&maskV;
                        (rb|(ga<<8)).Store(output+x);
                    }
                for(;x<target.Width;x++)
                {
                    uint a=previous[x],b=next[x];
                    uint rb=(((a&mask)*previousWeight+(b&mask)*nextWeight)>>8)&mask;
                    uint ga=((((a>>8)&mask)*previousWeight+((b>>8)&mask)*nextWeight)>>8)&mask;
                    output[x]=rb|(ga<<8);
                }
            }
        }
        finally{target.UnlockBits(destination);}
    }

    private void DrawFlowingAuraMaterial(Graphics g,Bitmap[] cycle,RectangleF destination,float alpha,float phase)
    {
        if(alpha<=.001f||cycle.Length==0)return;
        // Pixel interpolation happens only in a fixed 300x400 reusable surface;
        // the large translucent destination still receives one image blit.
        float loop=phase/MathF.Tau;loop-=MathF.Floor(loop);
        float frame=loop*AuraCycleLength;
        int index=(int)frame%cycle.Length,weight=(int)((frame-index)*256);
        bool guard=ReferenceEquals(cycle,guardAuraCycle);
        var pixels=guard?guardAuraPixels:abyssAuraPixels;
        Bitmap? texture=guard?guardAuraBlend:abyssAuraBlend;
        if(texture==null||pixels.Length!=cycle.Length)return;
        int key=index*257+weight;
        ref int previousKey=ref (guard?ref guardAuraBlendKey:ref abyssAuraBlendKey);
        if(previousKey!=key)
        {
            BlendAuraFrames(pixels[index],pixels[(index+1)%cycle.Length],texture,weight);
            previousKey=key;
        }
        if(alpha<.99f)
        {
            auraMatrix.Matrix33=Math.Clamp(alpha,0,1);auraAttributes.SetColorMatrix(auraMatrix);
            g.DrawImage(texture,Rectangle.Round(destination),0,0,texture.Width,texture.Height,GraphicsUnit.Pixel,auraAttributes);
        }
        else g.DrawImage(texture,destination);
    }

    private void DrawGuardAuraV2(Graphics g,bool ward)
    {DrawOrbitingGuard(g,ward,true);}

    private void DrawAbyssAura(Graphics g,float x,float y,float size)
    {
        if(abyssAuraMaterial==null||abyssAuraMirror==null)return;
        float cx=x+size*.5f,cy=y+size*.53f,time=animationClock*.066f;
        float pulse=1+MathF.Sin(time*1.7f)*.055f;
        float scale=reducedEffects?.86f:1,w=size*2.45f*pulse*scale,h=size*2.57f*pulse*scale;
        // A fast organic field burns behind the portrait, with several opposed
        // currents around its silhouette instead of a slow vertical smoke banner.
        DrawFlowingAuraMaterial(g,abyssAuraCycle,new(cx-w*.5f+MathF.Sin(time)*5,cy-h*.51f,w,h),1,time);
        int count=reducedEffects?10:40;
        for(int i=0;i<count;i++)
        {
            float t=(animationClock*(.016f+i%4*.002f)+i*.618034f)%1;
            float angle=i*2.39996f+time*.14f,radius=size*(.53f+MathF.Sin(t*MathF.PI)*.48f);
            float xx=cx+MathF.Cos(angle)*radius+MathF.Sin(t*10+i)*10;
            float yy=cy+size*.72f-t*size*1.65f+MathF.Sin(angle)*size*.31f;
            float alpha=MathF.Sin(t*MathF.PI),r=.6f+(i%4)*.35f;
            Color ember=i%7==0?Color.FromArgb(255,120,111):Color.FromArgb(220,22,51);
            g.FillEllipse(ParticleBrush(ember,(int)(235*alpha)),xx-r,yy-r,r*2,r*2);
            if(!reducedEffects&&i%3==0)g.DrawLine(ParticlePen(ember,(int)(135*alpha),1),xx,yy+5,xx+MathF.Sin(time+i)*2,yy-4);
        }
    }
}
