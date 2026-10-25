using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool themedArena;
    private static readonly string[] WorldFiles={"World1Meadow.png","World2Prison.png","World3Hell.png","World4Concert.png"};
    // Each painting's running surface is aligned to the same collision plane.
    private static readonly float[] WorldContact={.77f,.75f,.70f,.74f};
    private int WorldIndex=>round==5?FinalBossKind+1:Math.Clamp(round-1,0,3);
    private string WorldName=>round==5?"종결전":WorldIndex switch{0=>"초원",1=>"감옥",2=>"지옥",_=>"황폐한 공연장"};
    private Image? CurrentBossImage=>round switch
    {
        2=>enemy4,
        3=>purgaeTransformed?enemy7??enemy3:enemy3,
        4=>(MelodySequenceActive||laserActive)?enemy6??melodyAngry??enemy5:menheraMode?melodyMenhera??melodyAngry??enemy5:enemy5,
        5=>null,
        _=>enemy1
    };

    private bool TryBuildThemedArena(int width,int height)
    {
        themedArena=false;
        string path=AssetPath(WorldFiles[WorldIndex]);
        if(!File.Exists(path))return false;
        using var source=new Bitmap(path);
        float contact=source.Height*WorldContact[WorldIndex];
        using var palette=new ImageAttributes();
        palette.SetColorMatrix(new ColorMatrix(new[]{new[]{.65f,.06f,.06f,0,0},new[]{.06f,.65f,.06f,0,0},new[]{.06f,.06f,.65f,0,0},new[]{0f,0,0,1,0},new[]{0f,0,0,0,1}}));
        void PaintSource(Graphics g,RectangleF dest,RectangleF src)=>g.DrawImage(source,Rectangle.Round(dest),src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,palette);
        backgroundCache=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        groundTile=new Bitmap(width,Math.Max(1,(int)Math.Ceiling((H-GroundY)*nativeArtScale)),PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(backgroundCache))
        {
            g.ScaleTransform(nativeArtScale,nativeArtScale);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            PaintSource(g,new RectangleF(0,0,W,GroundY),new RectangleF(0,0,source.Width,contact));
            PaintSource(g,new RectangleF(0,GroundY,W,H-GroundY),new RectangleF(0,contact,source.Width,source.Height-contact));
            using var quiet=new SolidBrush(Color.FromArgb(52,4,10,18));g.FillRectangle(quiet,0,0,W,H);
            // Calm the playable strip without losing the identity of each environment.
            using var veil=new LinearGradientBrush(new Rectangle(0,330,W,170),Color.Transparent,Color.FromArgb(64,5,9,17),90);
            g.FillRectangle(veil,0,330,W,170);
        }
        using(var g=Graphics.FromImage(groundTile))
        {
            g.ScaleTransform(nativeArtScale,nativeArtScale);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            PaintSource(g,new RectangleF(0,0,W,H-GroundY),new RectangleF(0,contact,source.Width,source.Height-contact));
            using var quiet=new SolidBrush(Color.FromArgb(52,4,10,18));g.FillRectangle(quiet,0,0,W,H-GroundY);
        }
        themedArena=true;
        BuildScrollingLayers();
        return true;
    }

    private void DrawWorldAtmosphere(Graphics g,float time)
    {
        int theme=WorldIndex;
        Color color=theme switch{0=>Color.FromArgb(125,255,235,171),1=>Color.FromArgb(65,172,198,214),2=>Color.FromArgb(175,255,142,58),_=>Color.FromArgb(85,205,179,213)};
        using var dust=new SolidBrush(color);
        int count=reducedEffects?5:theme==2?18:10;
        for(int i=0;i<count;i++)
        {
            float x=((i*137-worldTravelDistance*.28f)%W+W)%W;
            float y=theme==2?490-(i*43+time*.8f)%360:160+(i*53)%310+MathF.Sin(time*.008f+i)*9;
            float r=theme==2?1.3f+i%2:theme==0?1.2f:1;
            g.FillEllipse(dust,x,y,r*2,r*2);
        }
    }

    private void DrawThemedGroundMotion(Graphics g,float time)
    {
        // Sparse texture cues move beneath the actor; the painted floor remains continuous.
        Color c=WorldIndex switch{0=>Color.FromArgb(65,74,65,30),1=>Color.FromArgb(45,8,17,24),2=>Color.FromArgb(95,180,55,24),_=>Color.FromArgb(55,9,11,16)};
        using var mark=new Pen(c,1);
        for(int i=0;i<18;i++)
        {
            float x=((i*79-time*GetWorldSpeed())%W+W)%W,y=GroundY+3+i%4*3;
            g.DrawLine(mark,x,y,x+5+i%3*4,y+1);
        }
    }
}
