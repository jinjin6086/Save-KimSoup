using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private static readonly Color ClashBlue=Color.FromArgb(56,143,255),ClashPink=Color.FromArgb(255,75,173);
    private readonly Bitmap[] blueClashFrames=new Bitmap[32],pinkClashFrames=new Bitmap[32];
    private Bitmap[] finaleHeroPoses=Array.Empty<Bitmap>(),finaleBossPoses=Array.Empty<Bitmap>();
    private Bitmap? finaleVignette,finaleMemoryVignette,finaleBackdrop;
    private Bitmap? finaleZoomBackdrop;
    private float finaleBackdropScale;
    private readonly Bitmap[] blueJunction=new Bitmap[32],pinkJunction=new Bitmap[32],pinkFinaleFire=new Bitmap[32];
    private static float Scatter01(int seed){float f=MathF.Sin(seed*127.1f+311.7f)*43758.5453f;return f-MathF.Floor(f);}
    private void LoadClashMaterials()
    {
        string path=AssetPath("FinalBeamHD.png");
        if(File.Exists(path))
        {
            using var sheet=new Bitmap(path);var cells=new Bitmap[4];
            try
            {
                for(int i=0;i<4;i++)cells[i]=CropVfxCell(sheet,new(0,i*sheet.Height/4,sheet.Width,sheet.Height/4),768,160);
                for(int f=0;f<32;f++)
                {
                    float phase=f/8f;int i=(int)phase,j=(i+1)%4;float blend=SmoothUnit(phase-i);
                    using var white=new Bitmap(768,160,PixelFormat.Format32bppPArgb);
                    using(var g=Graphics.FromImage(white))
                    {
                        g.InterpolationMode=InterpolationMode.HighQualityBilinear;using var a=new ImageAttributes();var m=new ColorMatrix();
                        for(int x=0;x<768;x+=24)
                        {
                            int yy=(int)(MathF.Sin(x*.017f-f*MathF.Tau/32)*2);
                            m.Matrix33=1-blend;a.SetColorMatrix(m);g.DrawImage(cells[i],new Rectangle(x,yy,24,160),x,0,24,160,GraphicsUnit.Pixel,a);
                            m.Matrix33=blend;a.SetColorMatrix(m);g.DrawImage(cells[j],new Rectangle(x,-yy,24,160),x,0,24,160,GraphicsUnit.Pixel,a);
                        }
                    }
                    blueClashFrames[f]=TintClashMaterial(white,true);pinkClashFrames[f]=TintClashMaterial(white,false);
                }
            }
            finally{foreach(var c in cells)c?.Dispose();}
        }
        path=AssetPath("FinalJunctionHD.png");
        if(File.Exists(path))
        {
            using var junction=new Bitmap(path);var cells=new Bitmap[4];
            try
            {
                for(int i=0;i<4;i++)cells[i]=CropVfxCell(junction,new(i%2*(junction.Width/2),i/2*(junction.Height/2),junction.Width/2,junction.Height/2),256,256);
                for(int f=0;f<32;f++)
                {
                    float p=f/8f;int i=(int)p;float t=SmoothUnit(p-i);using var mix=new Bitmap(256,256,PixelFormat.Format32bppPArgb);
                    using(var g=Graphics.FromImage(mix))
                    {
                        g.DrawImageUnscaled(cells[i],0,0);using var attributes=new ImageAttributes();var m=new ColorMatrix{Matrix33=t};attributes.SetColorMatrix(m);
                        g.DrawImage(cells[(i+1)%4],new Rectangle(0,0,256,256),0,0,256,256,GraphicsUnit.Pixel,attributes);
                    }
                    blueJunction[f]=TintClashMaterial(mix,true);pinkJunction[f]=TintClashMaterial(mix,false);
                }
            }
            finally{foreach(var c in cells)c?.Dispose();}
        }
        for(int i=0;i<32;i++)if(furiosoFireBack[i]!=null)
        {
            var b=new Bitmap(192,256,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(b);using var a=new ImageAttributes();
            a.SetColorMatrix(new ColorMatrix(new[]{new[]{0f,.20f,0,0,0},new[]{0f,.30f,0,0,0},new[]{1f,0,.73f,0,0},new[]{0f,0,0,1f,0},new[]{0f,0,0,0,1f}}));
            g.DrawImage(furiosoFireBack[i],new Rectangle(0,0,192,256),0,0,192,256,GraphicsUnit.Pixel,a);pinkFinaleFire[i]=b;
        }
        Bitmap[] LoadPoses(string name)
        {
            string p=AssetPath(name);if(!File.Exists(p))return Array.Empty<Bitmap>();
            using var sheet=new Bitmap(p);return Enumerable.Range(0,3).Select(i=>ExtractMainSprite(sheet,new(i*sheet.Width/3,0,sheet.Width/3,sheet.Height),640,640)).ToArray();
        }
        finaleHeroPoses=LoadPoses("HeroFinaleHD.png");finaleBossPoses=LoadPoses("MelodyFinaleHD.png");
        LoadRodianLastVowMotion();
        finaleVignette=new Bitmap(500,300,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(finaleVignette))
        {
            using var p=new GraphicsPath();p.AddEllipse(-100,-170,700,640);
            using var v=new PathGradientBrush(p){CenterPoint=new(250,165),CenterColor=Color.Transparent,SurroundColors=new[]{Color.FromArgb(245,2,4,12)},FocusScales=new(.35f,.3f)};
            g.FillRectangle(v,0,0,500,300);
        }
        finaleMemoryVignette=BakeOpacity(finaleVignette,.88f);
        var baseVignette=BakeOpacity(finaleVignette,.48f);finaleVignette.Dispose();finaleVignette=baseVignette;
        foreach(var actor in motionFrames.Concat(kickFrames).Concat(slideFrames))
            if(!furiosoBodyLights.ContainsKey(actor))furiosoBodyLights[actor]=CreateAttachedBodyLight(actor);
        // Restore the previous three-pose QTE animation. Keep the registered
        // blend utility for the animated beam materials, without loading the
        // newer eight-pose player atlas.
        PrepareFuriosoAttachments();PrepareCinderMaterials();
        PrepareApotheosisPlasma();
        PrepareCollisionVolume();PrepareIndependentBossExplosions();
    }
    private static Bitmap BakeOpacity(Image source,float opacity)
    {
        var b=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(b);using var a=new ImageAttributes();var m=new ColorMatrix{Matrix33=opacity};a.SetColorMatrix(m);
        g.DrawImage(source,new Rectangle(0,0,b.Width,b.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,a);return b;
    }
    private void DrawFinaleBackdrop(Graphics g)
    {
        if(finaleBackdrop==null||Math.Abs(finaleBackdropScale-nativeArtScale)>.01f)
        {
            finaleBackdrop?.Dispose();finaleBackdropScale=nativeArtScale;
            finaleZoomBackdrop?.Dispose();finaleZoomBackdrop=null;
            finaleBackdrop=new Bitmap((int)MathF.Ceiling(W*nativeArtScale),(int)MathF.Ceiling(H*nativeArtScale),PixelFormat.Format32bppRgb);
            using var bg=Graphics.FromImage(finaleBackdrop);bg.ScaleTransform(nativeArtScale,nativeArtScale);
            bg.SmoothingMode=SmoothingMode.AntiAlias;bg.InterpolationMode=InterpolationMode.HighQualityBilinear;
            DrawBackground(bg);DrawArenaGround(bg);
            using var dim=new SolidBrush(Color.FromArgb(120,3,6,16));bg.FillRectangle(dim,0,0,W,H);
            if(finaleVignette!=null)bg.DrawImage(finaleVignette,new Rectangle(0,0,W,H));
        }
        Image stage=finaleBackdrop;
        if(lastStandPhase==4&&lastStandClock>=72)
        {
            if(finaleZoomBackdrop==null)
            {
                finaleZoomBackdrop=new Bitmap((int)MathF.Round(W*nativeArtScale*LastStandClashZoom),(int)MathF.Round(H*nativeArtScale*LastStandClashZoom),PixelFormat.Format32bppRgb);
                using var bg=Graphics.FromImage(finaleZoomBackdrop);bg.InterpolationMode=InterpolationMode.HighQualityBicubic;bg.DrawImage(finaleBackdrop,new Rectangle(0,0,finaleZoomBackdrop.Width,finaleZoomBackdrop.Height));
            }
            stage=finaleZoomBackdrop;
        }
        var mode=g.CompositingMode;g.CompositingMode=CompositingMode.SourceCopy;DrawCachedLayer(g,stage,new(0,0,W,H));g.CompositingMode=mode;
        if(lastStandPhase==3){using var veil=new SolidBrush(Color.FromArgb(155,3,6,16));g.FillRectangle(veil,0,0,W,H);}
    }
    private static unsafe Bitmap TintClashMaterial(Bitmap source,bool blue)
    {
        var b=(Bitmap)source.Clone();var d=b.LockBits(new(0,0,b.Width,b.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<d.Height;y++){byte* row=(byte*)d.Scan0+y*d.Stride;for(int x=0;x<d.Width;x++)
        {
            byte* p=row+x*4;float alpha=p[3]/255f;if(alpha<=.001f)continue;
            float value=Math.Clamp((p[0]+p[1]+p[2])/(3f*255*alpha),0,1);
            // Dark authored slashes stay dark. Only the radiant material is colored.
            p[0]=(byte)(p[0]*(blue?1:.91f));p[1]=(byte)(p[1]*(blue?.88f:.70f));p[2]=(byte)(p[2]*(blue?.65f:1));
            float edge=Math.Min(1,Math.Min(x,d.Width-1-x)/12f)*Math.Min(1,Math.Min(y,d.Height-1-y)/8f);
            float fade=edge*Math.Clamp((value-.38f)/.35f,0,1);
            for(int c=0;c<4;c++)p[c]=(byte)(p[c]*fade);
        }}}
        finally{b.UnlockBits(d);}return b;
    }
    private void DisposeClashMaterials()
    {
        DisposeIndependentBossExplosions();DisposeCinderMaterials();DisposeRodianLastVowMotion();DisposeCinematicMotion();DisposeCollisionVolume();finaleZoomBackdrop?.Dispose();
        DisposeApotheosisPlasma();
        foreach(var b in blueClashFrames)b?.Dispose();foreach(var b in pinkClashFrames)b?.Dispose();
        foreach(var b in blueJunction)b?.Dispose();foreach(var b in pinkJunction)b?.Dispose();foreach(var b in pinkFinaleFire)b?.Dispose();
        foreach(var b in finaleHeroPoses)b.Dispose();foreach(var b in finaleBossPoses)b.Dispose();finaleVignette?.Dispose();finaleMemoryVignette?.Dispose();finaleBackdrop?.Dispose();
    }
    private void DrawJunction(Graphics g,float x,float y,float width,float height,bool blue,float alpha)
    {
        var frames=blue?blueJunction:pinkJunction;int i=(int)(lastStandRealClock*3.44f)%32;
        var r=new RectangleF(x-width*.5f,y-height*.5f,width,height);
        if(frames[i]!=null)DrawEchoFrame(g,frames[i],r,alpha);
    }
    private void DrawClashEmitter(Graphics g,float x,float y,float angle,float energy,Color color)
    {
        if(energy<=.001f)return;
        var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle*180/MathF.PI);
        DrawJunction(g,0,0,22*energy,58*energy,color==ClashBlue,.92f*energy);
        if(!reducedEffects)DrawJunction(g,5,0,15*energy,76*energy,color==ClashBlue,.28f*energy);
        g.Restore(saved);DrawLaserBloom(g,x,y,20*energy,25*energy,color,.62f*energy);
    }
    private void DrawClashBeam(Graphics g,float x,float y,float tx,float ty,float width,Color color,float energy,bool hero)
    {
        if(gpuPrototypeDrawing)return;
        DrawApotheosisBeam(g,x,y,tx,ty,width,energy,hero);
    }
    private void DrawClashContact(Graphics g,float x,float y,float strength)
    {
        if(gpuPrototypeDrawing)return;
        DrawApotheosisCollision(g,x,y,strength);
    }
    private void DrawFinaleGather(Graphics g,float x,float y,float power,Color color,bool inward)
    {
        if(gpuPrototypeDrawing)return;
        if(power<=.001f)return;
        DrawLaserBloom(g,x,y,15+power*34,15+power*38,color,power*.75f);
        DrawJunction(g,x,y,12+power*29,12+power*33,color==ClashBlue,power*.7f);
        for(int i=0;i<(reducedEffects?19:90);i++)
        {
            float t=(lastStandRealClock*(.012f+power*.015f)+Scatter01(i*17+9))%1;
            float r=(inward?(1-t)*(1-t):t*t)*(26+power*85+i%5*8),angle=Scatter01(i*31+2)*MathF.Tau+lastStandRealClock*.004f;
            float xx=x+MathF.Cos(angle)*r,yy=y+MathF.Sin(angle)*r*.83f;
            float a=MathF.Sin(t*MathF.PI)*power,s=.23f+i%4*.13f;
            g.FillEllipse(ParticleBrush(i%6==0?Color.FromArgb(228,248,255):color,(int)(220*a)),xx-s,yy-s,s*2,s*2);
            if(i%11==0)g.DrawLine(ParticlePen(color,(int)(70*a),1),xx,yy,xx+MathF.Cos(angle)*(inward?2:-2),yy+MathF.Sin(angle)*(inward?2:-2));
        }
    }
}

