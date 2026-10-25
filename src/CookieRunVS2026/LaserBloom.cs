using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private float laserContactX,laserContactY;
    private float LiveLaserAngle=>MathF.Atan2(laserContactY-(bossY+65),laserContactX-(bossX+65));
    private readonly Dictionary<int,Bitmap> laserBlooms=new();
    private readonly Dictionary<(int Color,int Opacity),Bitmap> laserBloomOpacity=new();
    private void DisposeLaserBloom(){foreach(var b in laserBlooms.Values)b.Dispose();laserBlooms.Clear();foreach(var b in laserBloomOpacity.Values)b.Dispose();laserBloomOpacity.Clear();}
    private void DrawLaserBloom(Graphics g,float x,float y,float rx,float ry,Color color,float alpha)
    {
        if(gpuPrototypeDrawing&&!(lastStandPhase==5&&lastStandClock>=FinaleTiming.Impact&&lastStandClock<FinaleTiming.Explosion))return;
        if(alpha<=0||rx<=0||ry<=0)return;
        if(!laserBlooms.TryGetValue(color.ToArgb(),out var image))
        {
            image=new(128,128,PixelFormat.Format32bppPArgb);using var cg=Graphics.FromImage(image);cg.SmoothingMode=SmoothingMode.AntiAlias;
            using var shape=new GraphicsPath();shape.AddEllipse(0,0,128,128);
            using var glow=new PathGradientBrush(shape){CenterColor=Color.FromArgb(242,255,255,255),SurroundColors=new[]{Color.Transparent}};
            glow.InterpolationColors=new ColorBlend{Colors=new[]{Color.Transparent,Color.FromArgb(32,color),Color.FromArgb(125,color),Color.FromArgb(240,255,255,255)},Positions=new[]{0f,.45f,.79f,1f}};
            cg.FillPath(glow,shape);laserBlooms[color.ToArgb()]=image;
        }
        int opacity=Math.Clamp((int)MathF.Round(alpha*64),1,64);
        var key=(color.ToArgb(),opacity);
        if(!laserBloomOpacity.TryGetValue(key,out var premultiplied))
        {
            premultiplied=BakeOpacity(image,opacity/64f);laserBloomOpacity[key]=premultiplied;
        }
        var saved=g.Save();g.InterpolationMode=InterpolationMode.Bilinear;
        g.DrawImage(premultiplied,new RectangleF(x-rx,y-ry,rx*2,ry*2));g.Restore(saved);
    }
    private void DrawLaserContactFx(Graphics g)
    {
        if(!laserActive||round!=4||DodgeActive)return;
        bool reflected=laserReflected;
        float age=laserTimer,release=reflected?Math.Clamp((21-age)/5,0,1):Math.Clamp(1-age/40,0,1);
        float x=reflected?counterTargetX:laserContactX,y=reflected?counterTargetY:laserContactY;
        Color color=reflected?Color.FromArgb(72,255,223):menheraMode?Color.FromArgb(255,69,115):Color.FromArgb(251,139,225);
        DrawLaserBloom(g,x,y,reflected?65:45,reflected?52:37,color,release*.9f);
        if(release<=0)return;
        int count=reducedEffects?8:34;
        for(int i=0;i<count;i++)
        {
            float t=(age*.055f+i/(float)count)%1,a=i*2.39996f+age*.012f,r=7+t*(reflected?82:52);
            float sx=x+MathF.Cos(a)*r,sy=y+MathF.Sin(a)*r*.65f;
            float intensity=MathF.Sin(t*MathF.PI)*release;
            Color c=i%4==0?Color.White:color;
            g.DrawLine(ParticlePen(c,(int)(225*intensity),i%5==0?2:1),sx,sy,sx+MathF.Cos(a)*(3+t*7),sy+MathF.Sin(a)*(2+t*6));
            if(i%3==0)g.FillEllipse(ParticleBrush(c,(int)(200*intensity)),sx-1,sy-1,2,2);
        }
        using var flare=new Pen(Color.FromArgb((int)(210*release),255,255,255),1.6f){StartCap=LineCap.Round,EndCap=LineCap.Round};
        float s=(reflected?30:20)*release;
        g.DrawLine(flare,x-s*1.7f,y,x+s*1.7f,y);g.DrawLine(flare,x,y-s,x,y+s);
    }
}
