using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private Bitmap? furiosoGlow;
    private readonly Bitmap[] furiosoFireBack=new Bitmap[32],furiosoFireFront=new Bitmap[32];
    private readonly Dictionary<Image,Bitmap> furiosoBodyLights=new();
    private static Bitmap CreateFuriosoGlow()
    {
        var b=new Bitmap(192,256,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(b);using var p=new GraphicsPath();p.AddEllipse(4,4,184,248);
        using var light=new PathGradientBrush(p){CenterColor=Color.FromArgb(105,13,88,255),SurroundColors=new[]{Color.Transparent}};
        g.FillPath(light,p);return b;
    }
    private bool FuriosoBurning=>state==GameState.Round&&activeSkill==ActivePower.Echo&&activeTimer>0&&!PhaseIntroActive&&!bossBriefingActive&&!MelodyLastStandActive;
    private float FuriosoFade=>Math.Min(1,activeTimer/18)*Math.Min(1,(360-activeTimer)/10+.15f)*(reducedEffects?.65f:1);
    // The actual pose supplies its own current rectangle. No previous-frame anchor.
    private void DrawFuriosoAttached(Graphics g,Image? actor,RectangleF body,float alpha,bool front)
    {
        if(!FuriosoBurning)return;
        float fade=FuriosoFade*alpha,foot=body.Bottom-2,cx=body.X+body.Width*.51f;
        bool low=body.Width>body.Height*1.35f;
        var target=new RectangleF(cx-body.Width*.78f,foot-body.Height*1.43f,body.Width*1.56f,body.Height*1.47f);
        if(!front&&actor!=null)
        {
            if(!furiosoBodyLights.TryGetValue(actor,out var light)){light=CreateAttachedBodyLight(actor);furiosoBodyLights.Add(actor,light);}
            DrawEchoFrame(g,light,new(body.X-body.Width*.09f,body.Y-body.Height*.08f,body.Width*1.18f,body.Height*1.16f),fade*.84f);
        }
        if(!front&&furiosoGlow!=null)DrawEchoFrame(g,furiosoGlow,target,fade*.32f);
        DrawAttachedFire(g,actor,body,fade*(front?.18f:1),animationClock*.95f);
        if(front&&!reducedEffects)for(int i=0;i<29;i++)
        {
            float t=(animationClock*.024f+i*.618034f)%1,side=MathF.Sin(i*2.39996f);
            float x=cx+side*(body.Width*.30f+t*body.Width*.40f),y=foot-t*body.Height*(low?1.19f:1.55f);
            float s=.48f+i%4*.27f,a=MathF.Sin(t*MathF.PI)*fade*(i%3==0?.7f:1);
            g.FillEllipse(ParticleBrush(i%4==0?Color.FromArgb(205,250,255):ClashBlue,(int)(225*a)),x-s,y-s,s*2,s*2);
            if(i%5==0)g.DrawLine(ParticlePen(ClashBlue,(int)(115*a),1),x,y+3+t*4,x+side*1.3f,y-2);
        }
    }
    private static Bitmap CreateAttachedBodyLight(Image actor)
    {
        var b=new Bitmap(actor.Width+24,actor.Height+24,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(b);using var a=new ImageAttributes();
        a.SetColorMatrix(new ColorMatrix(new[]{new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,.16f,0},new[]{.04f,.39f,1f,0,1}}));
        for(int i=0;i<12;i++){float p=i*MathF.Tau/12;g.DrawImage(actor,new Rectangle(12+(int)(MathF.Cos(p)*5),12+(int)(MathF.Sin(p)*5),actor.Width,actor.Height),0,0,actor.Width,actor.Height,GraphicsUnit.Pixel,a);}
        return b;
    }
    private void DrawFuriosoMaterial(Graphics g,RectangleF target,float alpha,bool front,float clock)
    {
        float phase=((clock%32)+32)%32;int i=(int)phase,j=(i+1)%32;float mix=phase-i;
        var frames=front?furiosoFireFront:furiosoFireBack;
        if(frames[i]!=null)DrawEchoFrame(g,frames[i],target,alpha*(1-mix));
        if(frames[j]!=null&&mix>.002f)DrawEchoFrame(g,frames[j],target,alpha*mix);
    }
    private void PrepareFuriosoFire()
    {
        string file=AssetPath("FuriosoFlameHD.png");if(!File.Exists(file))return;
        using var sheet=new Bitmap(file);var cells=new Bitmap[4];
        try
        {
            for(int i=0;i<4;i++)cells[i]=CropVfxCell(sheet,new(i%2*(sheet.Width/2),i/2*(sheet.Height/2),sheet.Width/2,sheet.Height/2),192,256);
            for(int f=0;f<32;f++)
            {
                float phase=f/8f;int i=(int)phase,j=(i+1)%4;float blend=SmoothUnit(phase-i);
                var back=new Bitmap(192,256,PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(back))
                {
                    g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                    using var a=new ImageAttributes();var m=new ColorMatrix();
                    // Advected bands retain authored fine flame texture, no wires.
                    for(int y=0;y<256;y+=8)
                    {
                        float wave=MathF.Sin(f*MathF.Tau/32+y*.053f)*1.8f;
                        var dst=new Rectangle((int)wave,y,192,8);
                        m.Matrix33=1-blend;a.SetColorMatrix(m);g.DrawImage(cells[i],dst,0,y,192,8,GraphicsUnit.Pixel,a);
                        m.Matrix33=blend;a.SetColorMatrix(m);g.DrawImage(cells[j],dst,0,y,192,8,GraphicsUnit.Pixel,a);
                    }
                }
                furiosoFireBack[f]=back;var front=(Bitmap)back.Clone();FeatherFuriosoFront(front);furiosoFireFront[f]=front;
            }
        }
        finally{foreach(var cell in cells)cell?.Dispose();}
        LoadClashMaterials();
    }
    private static Bitmap CropVfxCell(Bitmap sheet,Rectangle cell,int width,int height)
    {
        int l=cell.Right,t=cell.Bottom,r=cell.Left,b=cell.Top;
        for(int y=cell.Top;y<cell.Bottom;y+=2)for(int x=cell.Left;x<cell.Right;x+=2)
            if(sheet.GetPixel(x,y).A>22){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
        if(r<=l||b<=t){l=cell.Left;t=cell.Top;r=cell.Right-1;b=cell.Bottom-1;}
        var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(image);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.DrawImage(sheet,new Rectangle(3,3,width-6,height-6),new Rectangle(l,t,r-l+1,b-t+1),GraphicsUnit.Pixel);return image;
    }
    private static unsafe void FeatherFuriosoFront(Bitmap b)
    {
        var d=b.LockBits(new(0,0,b.Width,b.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<d.Height;y++){byte* row=(byte*)d.Scan0+y*d.Stride;for(int x=0;x<d.Width;x++)
        {
            float edge=Math.Clamp((Math.Abs(x-d.Width*.5f)-d.Width*.27f)/(d.Width*.14f),0,1);
            float feet=Math.Clamp((y/(float)d.Height-.78f)/.15f,0,1),a=Math.Max(edge*.8f,feet*.9f);
            for(int c=0;c<4;c++)row[x*4+c]=(byte)(row[x*4+c]*a);
        }}}
        finally{b.UnlockBits(d);}
    }
    private void DisposeFuriosoFire()
    {
        foreach(var b in furiosoFireBack)b?.Dispose();foreach(var b in furiosoFireFront)b?.Dispose();
        foreach(var b in furiosoBodyLights.Values.Distinct())b.Dispose();furiosoBodyLights.Clear();
        Array.Clear(furiosoFireBack);Array.Clear(furiosoFireFront);DisposeClashMaterials();DisposeFuriosoAttachments();
    }
}
