using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly Dictionary<(Image,int),Bitmap> readableSprites=new();
    private readonly ImageAttributes readableAlpha=new();
    private readonly ColorMatrix readableMatrix=new();
    private bool worldMirroring;

    private void DrawPersistentParryRange(Graphics g)
    {
        if(state!=GameState.Round||PhaseIntroActive||bossBriefingActive||FocusWireframeActive)return;
        RectangleF range=KickHitBox;
        // A quiet one-pixel boundary, using the real upgraded kick geometry.
        using var fill=new SolidBrush(Color.FromArgb(3,Gold));
        using var edge=new Pen(Color.FromArgb(ParryActive?82:40,Gold),1);
        g.FillRectangle(fill,range);
        g.DrawRectangle(edge,range.X,range.Y,range.Width,range.Height);
    }

    private Bitmap ReadableSprite(Image source,Color color)
    {
        var key=(source,color.ToArgb());
        if(readableSprites.TryGetValue(key,out var existing))return existing;
        const int body=256,pad=14;
        var bitmap=new Bitmap(body+pad*2,body+pad*2,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(bitmap);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        using var attributes=new ImageAttributes();
        void Silhouette(Color c,float radius)
        {
            attributes.SetColorMatrix(new ColorMatrix(new[]{new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,c.A/255f,0},new[]{c.R/255f,c.G/255f,c.B/255f,0,1}}));
            for(int i=0;i<8;i++)
            {
                float angle=i*MathF.Tau/8;
                g.DrawImage(source,new Rectangle((int)(pad+MathF.Cos(angle)*radius),(int)(pad+MathF.Sin(angle)*radius),body,body),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        Silhouette(Color.FromArgb(240,4,8,15),12);
        Silhouette(Color.FromArgb(225,color),6);
        attributes.SetColorMatrix(new ColorMatrix(new[]{new[]{1.10f,0,0,0,0},new[]{0f,1.10f,0,0,0},new[]{0f,0,1.10f,0,0},new[]{0f,0,0,1,0},new[]{.035f,.035f,.035f,0,1}}));
        g.DrawImage(source,new Rectangle(pad,pad,body,body),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        readableSprites[key]=bitmap;return bitmap;
    }

    private void DrawReadableSprite(Graphics g,Image source,RectangleF target,Color rim,float alpha=1)
    {
        if(alpha<=0)return;
        var image=ReadableSprite(source,rim);
        float padx=target.Width*14/256,pady=target.Height*14/256;
        var rect=new RectangleF(target.X-padx,target.Y-pady,target.Width+padx*2,target.Height+pady*2);
        if(alpha>=.999f)g.DrawImage(image,rect);
        else
        {
            readableMatrix.Matrix33=Math.Clamp(alpha,0,1);readableAlpha.SetColorMatrix(readableMatrix);
            g.DrawImage(image,Rectangle.Round(rect),0,0,image.Width,image.Height,GraphicsUnit.Pixel,readableAlpha);
        }
    }

    private void DrawReadableObstacle(Graphics g,RectangleF rect,Image? image,bool floating)
    {
        Color rim=floating?Color.FromArgb(255,207,120):Color.FromArgb(255,160,104);
        if(image!=null)DrawReadableSprite(g,image,rect,rim);else DrawStoneObstacle(g,rect,floating);
        if(ReferenceEquals(image,enemy2))return;
        using var dark=new Pen(Color.FromArgb(210,5,10,18),4);
        using var edge=new Pen(rim,1.5f);
        g.DrawRectangle(dark,rect.X,rect.Y,rect.Width,rect.Height);
        g.DrawRectangle(edge,rect.X,rect.Y,rect.Width,rect.Height);
        if(floating)
        {
            using var cue=new Pen(rim,2.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            float x=rect.X+rect.Width*.5f,y=rect.Y-15;
            g.DrawLine(dark,x,y-8,x,y+4);g.DrawLine(cue,x,y-8,x,y+4);
            g.DrawLine(cue,x-5,y-1,x,y+4);g.DrawLine(cue,x,y+4,x+5,y-1);
        }
    }

    private void DrawThreatHalo(Graphics g,Projectile p)
    {
        Color color=p.IsParried?Mint:p.Type==AttackType.BlackBall?Color.FromArgb(239,221,168):Coral;
        using var shadow=new Pen(Color.FromArgb(220,3,8,15),5);
        using var rim=new Pen(Color.FromArgb(205,color),1.7f);
        g.DrawEllipse(shadow,p.X-2,p.Y-2,p.W+4,p.H+4);
        g.DrawEllipse(rim,p.X-2,p.Y-2,p.W+4,p.H+4);
    }

    private void DisposeReadability()
    {
        foreach(var image in readableSprites.Values)image.Dispose();
        readableSprites.Clear();readableAlpha.Dispose();
    }
}
