using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private readonly Dictionary<Image,Bitmap[]> cinderStoneFrames=new();
    private readonly Dictionary<Image,PointF> cinderEyePoints=new();
    private readonly Dictionary<Image,Image> cinderGolemActors=new();
    private readonly List<Bitmap> cinderGolemArt=new();
    private readonly Dictionary<Image,Bitmap> cinderGolemRims=new();
    private void PrepareCinderStone(Bitmap unused)
    {
        string core=AssetPath("CinderGolemMotion.png"),actions=AssetPath("CinderGolemActions.png");
        if(!File.Exists(core)||!File.Exists(actions))return;
        using var motion=new Bitmap(core);using var action=new Bitmap(actions);
        int[] left={0,400,750,1180},right={445,805,1218,1536};
        for(int i=0;i<8;i++)
        {
            var sprite=ExtractMainSprite(motion,new(left[i%4],i/4*512,right[i%4]-left[i%4],512),160,200);
            cinderGolemArt.Add(sprite);if(i<motionFrames.Length)cinderGolemActors[motionFrames[i]]=sprite;
        }
        Rectangle[] regions={new(0,0,510,640),new(508,0,607,640),new(1115,0,421,640),new(0,640,525,384),new(525,640,590,384),new(1115,640,421,384)};
        for(int i=0;i<6;i++)
        {
            var sprite=ExtractMainSprite(action,regions[i],i<3?200:240,i<3?200:110);cinderGolemArt.Add(sprite);
            if(i<3&&i<kickFrames.Length)cinderGolemActors[kickFrames[i]]=sprite;
            if(i>=3&&i-3<slideFrames.Length)cinderGolemActors[slideFrames[i-3]]=sprite;
        }
        if(furiosoCookieMask!=null)cinderGolemActors[furiosoCookieMask]=cinderGolemArt[7];
        foreach(var image in cinderGolemArt)BuildCinderStone(image);
        foreach(var pair in cinderGolemActors){cinderStoneFrames[pair.Key]=cinderStoneFrames[pair.Value];cinderEyePoints[pair.Key]=cinderEyePoints[pair.Value];}
    }
    private Image CinderBodyImage(Image actor)=>cinderGolemActors.TryGetValue(actor,out var transformed)?transformed:actor;
    private unsafe Bitmap[] BuildCinderStone(Image actor)
    {
        actor=CinderBodyImage(actor);if(cinderStoneFrames.TryGetValue(actor,out var ready))return ready;
        using var original=new Bitmap(256,256,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(original)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(actor,new Rectangle(0,0,256,256));}
        var source=original.LockBits(new(0,0,256,256),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var frames=new Bitmap[32];
        try
        {
            float eyeScore=0;PointF eye=new(166,55);
            for(int y=0;y<90;y++)for(int x=0;x<256;x++)
            {
                byte* p=(byte*)source.Scan0+y*source.Stride+x*4;if(p[3]<120)continue;
                float score=p[2]-p[1]*1.8f-p[0]*.4f;if(score>eyeScore){eyeScore=score;eye=new(x,y);}
            }
            cinderEyePoints[actor]=eye;
            for(int f=0;f<32;f++)
            {
                var output=new Bitmap(256,256,PixelFormat.Format32bppPArgb);var data=output.LockBits(new(0,0,256,256),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                try{for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    byte* s=(byte*)source.Scan0+y*source.Stride+x*4;if(s[3]==0)continue;
                    float a=s[3],red=s[2]/a,green=s[1]/a,blue=s[0]/a;
                    float hot=Math.Clamp((red-blue-.20f)*2.5f,0,1)*Math.Clamp((red-.27f)*2.4f,0,1);
                    float stream=MathF.Sin(f*MathF.Tau/32-y*.095f+x*.039f),pulse=hot*stream;
                    byte* p=(byte*)data.Scan0+y*data.Stride+x*4;p[3]=s[3];
                    p[2]=(byte)(Math.Clamp(red*(1+pulse*.075f),0,1)*a);
                    p[1]=(byte)(Math.Clamp(green*(1+pulse*.27f)+hot*Math.Max(0,stream)*.025f,0,1)*a);
                    p[0]=(byte)(Math.Clamp(blue*(1+pulse*.15f),0,1)*a);
                }}finally{output.UnlockBits(data);}frames[f]=output;
            }
        }finally{original.UnlockBits(source);}
        cinderStoneFrames[actor]=frames;return frames;
    }
    private Bitmap CinderCloneImage(Image actor)
    {
        if(greenPlayerSprites.TryGetValue(actor,out var ready))return ready;
        var green=new Bitmap(actor.Width,actor.Height,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(green);using var tint=new ImageAttributes();
        tint.SetColorMatrix(new ColorMatrix(new[]{new[]{.10f,.28f,.11f,0,0},new[]{.20f,.56f,.22f,0,0},new[]{.04f,.10f,.04f,0,0},new[]{0f,0,0,1,0},new[]{.015f,.14f,.025f,0,1f}}));
        g.DrawImage(actor,new Rectangle(0,0,green.Width,green.Height),0,0,actor.Width,actor.Height,GraphicsUnit.Pixel,tint);greenPlayerSprites[actor]=green;return green;
    }
    private bool TryDrawCinderGolem(Graphics g,Image original,RectangleF body,Color rim,float alpha)
    {
        if(!CinderVisualActive||!cinderGolemActors.TryGetValue(original,out var actor))return false;
        float amount=SmoothUnit((360-activeTimer)/12)*Math.Min(1,activeTimer/18);
        Image normal=original;if(cloneActive){normal=CinderCloneImage(original);actor=CinderCloneImage(actor);}
        if(amount<.999f)DrawReadableSprite(g,normal,body,rim,alpha*(1-amount));
        DrawCinderAttached(g,actor,body,alpha*amount,false);
        DrawCinderBodyRim(g,actor,body,alpha*amount);
        DrawCinderStone(g,actor,body,alpha);
        return true;
    }
    private void DrawCinderStone(Graphics g,Image? actor,RectangleF body,float alpha)
    {
        if(!CinderVisualActive||cinderGolemArt.Count==0)return;actor??=furiosoCookieMask;if(actor==null)return;
        float amount=SmoothUnit((360-activeTimer)/12)*Math.Min(1,activeTimer/18)*alpha;
        DrawEchoFrame(g,BuildCinderStone(actor)[(int)(animationClock*.82f)%32],body,amount);
    }
    private void DrawCinderBodyRim(Graphics g,Image actor,RectangleF body,float alpha)
    {
        if(!cinderGolemRims.TryGetValue(actor,out var image))
        {
            image=new Bitmap(268,268,PixelFormat.Format32bppPArgb);using var bg=Graphics.FromImage(image);using var attr=new ImageAttributes();bg.InterpolationMode=InterpolationMode.HighQualityBicubic;
            void Edge(Color color,float radius)
            {
                attr.SetColorMatrix(new ColorMatrix(new[]{new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,color.A/255f,0},new[]{color.R/255f,color.G/255f,color.B/255f,0,1}}));
                for(int i=0;i<8;i++){float a=i*MathF.Tau/8;bg.DrawImage(actor,new Rectangle((int)(6+MathF.Cos(a)*radius),(int)(6+MathF.Sin(a)*radius),256,256),0,0,actor.Width,actor.Height,GraphicsUnit.Pixel,attr);}
            }
            Edge(Color.FromArgb(220,4,6,9),4);Edge(cloneActive?Color.FromArgb(140,99,229,123):Color.FromArgb(140,234,143,58),1.5f);
            bg.DrawImage(actor,new Rectangle(6,6,256,256));cinderGolemRims[actor]=image;
        }
        float px=body.Width*6/256,py=body.Height*6/256;DrawEchoFrame(g,image,new(body.X-px,body.Y-py,body.Width+px*2,body.Height+py*2),alpha);
    }
    private void DisposeCinderStone()
    {
        foreach(var a in cinderStoneFrames.Values.Distinct())foreach(var b in a)b.Dispose();cinderStoneFrames.Clear();cinderEyePoints.Clear();cinderGolemActors.Clear();
        foreach(var b in cinderGolemArt)b.Dispose();cinderGolemArt.Clear();
        foreach(var b in cinderGolemRims.Values)b.Dispose();cinderGolemRims.Clear();
    }
}
