using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private static readonly Color CinderOrange=Color.FromArgb(255,112,19);
    private const int CinderAuraWidth=256,CinderAuraHeight=320;
    private const float CinderAuraBreadth=1.45f,CinderAuraRise=1.28f;
    private readonly Dictionary<Image,Bitmap[]> cinderOuterFrames=new();
    private readonly Dictionary<Image,PointF[]> cinderFlameRoots=new();
    private readonly Dictionary<Image,bool[]> cinderBodyMask=new();
    private readonly Bitmap[] cinderFireFrames=new Bitmap[32],cinderKunaiFrames=new Bitmap[16];
    private Bitmap? cinderKunaiArt,cinderBrandArt;
    private void PrepareCinderMaterials()
    {
        string path=AssetPath("CinderImmolation.png");if(!File.Exists(path))return;
        using var atlas=new Bitmap(path);PrepareCinderStone(atlas);
        using var coronaA=CropVfxCell(atlas,new(0,430,768,594),256,320);
        using var coronaB=CropVfxCell(atlas,new(768,430,768,594),256,320);
        for(int f=0;f<32;f++)
        {
            cinderFireFrames[f]=AdvectImmolationMaterial(coronaA,coronaB,f,true);
        }
        using(var fire=new Bitmap(AssetPath("FlameKunaiPure.png")))cinderKunaiArt=CropVfxCell(fire,new(0,0,fire.Width,fire.Height),384,144);
        for(int f=0;f<16;f++)
        {
            var dart=new Bitmap(384,144,PixelFormat.Format32bppPArgb);using var dg=Graphics.FromImage(dart);dg.InterpolationMode=InterpolationMode.HighQualityBicubic;
            // Small advected bands ripple faster behind the point. The bright
            // leading point stays focused while the burning wake unfurls.
            for(int x=0;x<384;x+=4){float along=x/384f;int drift=(int)(MathF.Sin(f*MathF.Tau/16+x*.037f)*5*(1-along*along));dg.DrawImage(cinderKunaiArt,new Rectangle(x,drift,4,144),x,0,4,144,GraphicsUnit.Pixel);}
            cinderKunaiFrames[f]=dart;
        }
        using(var marks=new Bitmap(AssetPath("CinderFlameKunai.png")))cinderBrandArt=CropVfxCell(marks,new(780,710,marks.Width-780,marks.Height-710),192,192);
        foreach(var source in motionFrames.Concat(kickFrames).Concat(slideFrames).Append(furiosoCookieMask!))if(source!=null)BuildCinderOuter(source);
    }
    private static Bitmap AdvectImmolationMaterial(Bitmap a,Bitmap b,int frame,bool corona)
    {
        var output=new Bitmap(a.Width,a.Height,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(output);using var opacity=new ImageAttributes();
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;float phase=frame*MathF.Tau/32,blend=.5f-.5f*MathF.Cos(phase);
        for(int y=0;y<a.Height;y+=4)
        {
            float height=1-y/(float)a.Height;int sway=(int)(MathF.Sin(phase+y*.036f)*(corona?5:2)*height);
            var target=new Rectangle(sway,y,a.Width,4);
            opacity.SetColorMatrix(new ColorMatrix{Matrix33=1-blend});g.DrawImage(a,target,0,y,a.Width,4,GraphicsUnit.Pixel,opacity);
            opacity.SetColorMatrix(new ColorMatrix{Matrix33=blend});g.DrawImage(b,target,0,y,b.Width,4,GraphicsUnit.Pixel,opacity);
        }
        return output;
    }
    private unsafe Bitmap[] BuildCinderOuter(Image actor)
    {
        actor=CinderBodyImage(actor);if(cinderOuterFrames.TryGetValue(actor,out var cached))return cached;
        using var mask=new Bitmap(CinderAuraWidth,CinderAuraHeight,PixelFormat.Format32bppPArgb);
        float w=CinderAuraWidth/CinderAuraBreadth,h=CinderAuraHeight/CinderAuraRise;
        using(var g=Graphics.FromImage(mask)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(actor,new RectangleF((CinderAuraWidth-w)*.5f,CinderAuraHeight-h,w,h));}
        int length=CinderAuraWidth*CinderAuraHeight;var inside=new bool[length];var distance=new float[length];Array.Fill(distance,10000);
        var rootX=new int[length];var rootY=new int[length];var seeds=new List<PointF>();
        var shape=mask.LockBits(new(0,0,CinderAuraWidth,CinderAuraHeight),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<CinderAuraHeight;y++)for(int x=0;x<CinderAuraWidth;x++)
        {
            int n=y*CinderAuraWidth+x;inside[n]=*((byte*)shape.Scan0+y*shape.Stride+x*4+3)>=3;
            if(inside[n]){distance[n]=0;rootX[n]=x;rootY[n]=y;}
        }}finally{mask.UnlockBits(shape);}
        void Relax(int x,int y,int xx,int yy,float weight)
        {
            if(xx<0||xx>=CinderAuraWidth||yy<0||yy>=CinderAuraHeight)return;
            int n=y*CinderAuraWidth+x,m=yy*CinderAuraWidth+xx;
            if(distance[m]+weight>=distance[n])return;distance[n]=distance[m]+weight;rootX[n]=rootX[m];rootY[n]=rootY[m];
        }
        // A distance field keeps each fire tongue attached to an actual limb.
        // This replaces the detached full-height flame-card backdrop.
        for(int y=0;y<CinderAuraHeight;y++)for(int x=0;x<CinderAuraWidth;x++){Relax(x,y,x-1,y,1);Relax(x,y,x,y-1,1);Relax(x,y,x-1,y-1,1.4142f);Relax(x,y,x+1,y-1,1.4142f);}
        for(int y=CinderAuraHeight-1;y>=0;y--)for(int x=CinderAuraWidth-1;x>=0;x--){Relax(x,y,x+1,y,1);Relax(x,y,x,y+1,1);Relax(x,y,x+1,y+1,1.4142f);Relax(x,y,x-1,y+1,1.4142f);}
        for(int y=2;y<CinderAuraHeight-2;y+=4)for(int x=2;x<CinderAuraWidth-2;x+=4)
        {
            int n=y*CinderAuraWidth+x;if(!inside[n]&&distance[n]<2.5f)seeds.Add(new(rootX[n],rootY[n]));
        }
        cinderFlameRoots[actor]=seeds.ToArray();cinderBodyMask[actor]=inside;var frames=new Bitmap[32];
        for(int f=0;f<32;f++)
        {
            var texture=cinderFireFrames[f];var tex=texture.LockBits(new(0,0,CinderAuraWidth,CinderAuraHeight),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            var output=new Bitmap(CinderAuraWidth,CinderAuraHeight,PixelFormat.Format32bppPArgb);var data=output.LockBits(new(0,0,CinderAuraWidth,CinderAuraHeight),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{for(int y=0;y<CinderAuraHeight;y++)for(int x=0;x<CinderAuraWidth;x++)
            {
                int n=y*CinderAuraWidth+x;float d=distance[n];if(inside[n]||d<3.5f||d>35)continue;
                int tx=(x*3+f*8)%256,ty=(y*2+f*10)%320;byte* t=(byte*)tex.Scan0+ty*tex.Stride+tx*4;
                float textureHeat=t[3]>3?Math.Clamp(t[1]/(float)t[3],0,1):0,phase=f*MathF.Tau/32;
                float flow=.5f+.5f*MathF.Sin(phase-x*.091f-y*.038f);
                float curl=MathF.Sin(x*.17f-y*.11f+phase)*MathF.Sin(x*.071f+y*.033f-phase*2);
                float heat=Math.Clamp(.35f+curl*.24f+textureHeat*.35f,0,1);
                // Continuous coordinates avoid Voronoi-shaped wedges and
                // horizontal cuts when the nearest point switches limbs.
                float upward=.79f-.25f*(y-rootY[n])/Math.Max(1,d);
                float reach=(10+heat*heat*24+flow*6)*upward;
                float flame=d<reach?MathF.Pow(1-d/reach,1.65f)*(.4f+heat*.70f)*(.6f+flow*.4f):0;
                float glow=MathF.Exp(-d/3.5f)*.44f,a=Math.Clamp(flame+glow,0,1)*SmoothUnit((35-d)/8);
                byte* p=(byte*)data.Scan0+y*data.Stride+x*4;p[3]=(byte)(a*255);p[2]=p[3];p[1]=(byte)((.30f+heat*.52f)*p[3]);p[0]=(byte)((.025f+heat*.12f)*p[3]);
            }}finally{texture.UnlockBits(tex);output.UnlockBits(data);}frames[f]=output;
        }
        cinderOuterFrames[actor]=frames;return frames;
    }
    private void DrawCinderAttached(Graphics g,Image? actor,RectangleF body,float alpha,bool front)
    {
        if(front||!CinderVisualActive||cinderFireFrames[0]==null)return;
        actor??=furiosoCookieMask;if(actor==null)return;actor=CinderBodyImage(actor);
        float age=360-activeTimer,fade=Math.Min(1,activeTimer/18)*(.30f+.70f*SmoothUnit(age/8))*alpha;
        int f=(int)(animationClock*1.21f)%32;
        var corona=new RectangleF(body.X-body.Width*(CinderAuraBreadth-1)*.5f,body.Bottom-body.Height*CinderAuraRise,body.Width*CinderAuraBreadth,body.Height*CinderAuraRise);
        DrawEchoFrame(g,BuildCinderOuter(actor)[f],corona,fade*(reducedEffects?.64f:.95f));
        if(!reducedEffects&&cinderFlameRoots.TryGetValue(actor,out var roots)&&roots.Length>0)
        {
            for(int i=0;i<16;i++)
            {
                float t=(animationClock*.024f+i*.618034f)%1;PointF root=roots[(i*47)%roots.Length];
                float xx=corona.X+root.X/CinderAuraWidth*corona.Width+MathF.Sin(i*2.4f)*t*9,yy=corona.Y+root.Y/CinderAuraHeight*corona.Height-t*body.Height*.25f;
                int mx=(int)((xx-corona.X)/corona.Width*CinderAuraWidth),my=(int)((yy-corona.Y)/corona.Height*CinderAuraHeight);
                bool covered=false;if(cinderBodyMask.TryGetValue(actor,out var mask))for(int sy=Math.Max(0,my-3);sy<=Math.Min(CinderAuraHeight-1,my+3)&&!covered;sy++)for(int sx=Math.Max(0,mx-3);sx<=Math.Min(CinderAuraWidth-1,mx+3);sx++)if(mask[sy*CinderAuraWidth+sx]){covered=true;break;}
                if(covered)continue;
                float s=.45f+i%3*.17f;int a=(int)(155*MathF.Sin(t*MathF.PI)*fade);
                g.FillEllipse(ParticleBrush(i%4==0?Color.FromArgb(255,222,137):CinderOrange,a),xx-s,yy-s,s*2,s*2);
            }
        }
    }
    private void DisposeCinderMaterials()
    {
        DisposeCinderStone();
        foreach(var frames in cinderOuterFrames.Values.Distinct())foreach(var b in frames)b.Dispose();cinderOuterFrames.Clear();cinderFlameRoots.Clear();cinderBodyMask.Clear();
        foreach(var b in cinderFireFrames)b?.Dispose();foreach(var b in cinderKunaiFrames)b?.Dispose();cinderKunaiArt?.Dispose();cinderBrandArt?.Dispose();
    }
}
