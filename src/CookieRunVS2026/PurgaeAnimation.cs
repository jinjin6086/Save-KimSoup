using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Offline registered in-between frames. Gameplay only samples a texture;
    // no morphing, bitmap construction or per-pixel work during combat.
    private const int PurgaeFrameSize=320, PurgaeTweenFrames=12;
    private static readonly (int A,int B)[] PurgaeEdges={
        (0,1),(1,2),(2,3),(3,0),(3,1),(0,4),(4,5),(0,6),(6,0),(0,7),(7,0),(5,2),(2,0),(5,0),(4,0)};
    private readonly Bitmap[][] purgaePoseKeys={Array.Empty<Bitmap>(),Array.Empty<Bitmap>(),Array.Empty<Bitmap>()};
    private float purgaeCastClock=100;
    private int purgaeLoadedRound;
    private static readonly string[] PurgaeFormFiles={"PurgaeNormal","PurgaeRage","PurgaeAscended"};

    private void PreparePurgaeRound(int number)
    {
        purgaeCastClock=100;
        if(number!=3&&(number!=5||FinalBossKind!=0)){DisposePurgaeAnimation();return;}
        if(purgaeLoadedRound!=number)
        {
            DisposePurgaeAnimation();purgaeLoadedRound=number;
        }
        if(number==3){LoadPurgaeForm(0);LoadPurgaeForm(1);}
        else{LoadPurgaeForm(2);PrepareTimeStopStand();}
        LoadPurgaeOrbs();
    }
    private void LoadPurgaeForm(int form)
    {
        if(purgaeMotionFrames[form].Length>0)return;
        string path=AssetPath(PurgaeFormFiles[form]+"Frames.paf");
        if(!File.Exists(path))return;
        purgaeMotionFrames[form]=ReadPurgaeFrames(path,PurgaeFrameSize,8+PurgaeEdges.Length*(PurgaeTweenFrames-2));
        purgaePoseKeys[form]=purgaeMotionFrames[form].Take(8).Select(f=>f.Image).ToArray();
    }
    private Image? PurgaePoseImage(int form,int key)=>purgaePoseKeys[form].Length==8?purgaePoseKeys[form][key]:form==0?enemy3:enemy7??enemy3;
    private void DisposePurgaeAnimation()
    {
        for(int i=0;i<3;i++)
        {foreach(var frame in purgaeMotionFrames[i])frame.Image.Dispose();purgaeMotionFrames[i]=Array.Empty<PurgaePackedFrame>();purgaePoseKeys[i]=Array.Empty<Bitmap>();}
        foreach(var frame in purgaeShinyOrbs)frame.Image.Dispose();purgaeShinyOrbs=Array.Empty<PurgaePackedFrame>();purgaeLoadedRound=0;
        trueStopStandSprite?.Dispose();trueStopStandSprite=null;
    }
    private void PurgaeCastPose()=>purgaeCastClock=0;
    private (int A,int B,float T) PurgaeActionPose(bool gold)
    {
        if(gold&&trueStopPhase!=0)
        {
            if(trueStopPhase==1)return(0,4,SmoothUnit(Math.Clamp(trueStopClock/48,0,1)));
            if(trueStopPhase==2)return(4,5,SmoothUnit(Math.Clamp(trueStopClock/38,0,1)));
            if(trueStopPattern>=2)
                foreach(var lane in trueStopLanes)
                {
                    float age=trueStopClock-lane.At;
                    if(age>=-40&&age<0)return(0,6,SmoothUnit((age+40)/40));
                    if(age>=0&&age<12)return(6,0,SmoothUnit(age/12));
                }
            if(trueStopClock<16)return(5,2,SmoothUnit(trueStopClock/16));
            if(trueStopClock<36)return(2,0,SmoothUnit((trueStopClock-16)/20));
            return(0,1,0);
        }
        if(MeteorSceneActive)
        {
            if(meteorPhase==1)return(0,4,SmoothUnit(Math.Clamp(meteorSceneClock/50,0,1)));
            if(meteorPhase==2)return(4,5,Math.Clamp(meteorSceneClock/22,0,1)*(.96f+.04f*MathF.Sin(meteorSceneClock*.13f)));
            return(5,0,SmoothUnit(Math.Clamp(meteorSceneClock/44,0,1)));
        }
        if(purgaeCastClock<28)
        {
            if(purgaeSkill2Active)
            {
                if(purgaeCastClock<5)return(1,2,SmoothUnit(purgaeCastClock/5));
                if(purgaeCastClock<10)return(2,3,SmoothUnit((purgaeCastClock-5)/5));
                return(3,1,SmoothUnit(Math.Clamp((purgaeCastClock-10)/5,0,1)));
            }
            if(purgaeCastClock<7)return(1,2,SmoothUnit(purgaeCastClock/7));
            if(purgaeCastClock<17)return(2,3,SmoothUnit((purgaeCastClock-7)/10));
            return purgaeSkill2Active?(3,1,SmoothUnit((purgaeCastClock-17)/11)):(3,0,SmoothUnit((purgaeCastClock-17)/11));
        }
        if(purgaeSkill2Active)return(0,1,SmoothUnit(Math.Clamp((32-purgaeSkill2ThrowTimer)/25,0,1)));
        if(bossRecoil>8)return(0,7,Math.Clamp((bossRecoil-8)/12,0,.5f));
        float clock=gold?finalShotClock:attackTimer;
        return(0,1,SmoothUnit(Math.Clamp((clock-(gold?66:80))/18,0,1)));
    }
    private void DrawPurgaePose(Graphics g,int form,RectangleF rect,int from,int to,float progress,float opacity=1)
    {
        var frames=purgaeMotionFrames[form];
        if(frames.Length==0){var fallback=PurgaePoseImage(form,0);if(fallback!=null)DrawCinematicPose(g,fallback,rect,opacity);return;}
        int f=(int)MathF.Round(Math.Clamp(progress,0,1)*(PurgaeTweenFrames-1));
        var frame=frames[PurgaeFrameIndex(from,to,f)];var source=frame.Image;var bounds=frame.Bounds;
        var destination=new RectangleF(rect.X+bounds.X*rect.Width/PurgaeFrameSize,rect.Y+bounds.Y*rect.Height/PurgaeFrameSize,bounds.Width*rect.Width/PurgaeFrameSize,bounds.Height*rect.Height/PurgaeFrameSize);
        var interpolation=g.InterpolationMode;var pixel=g.PixelOffsetMode;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
        if(opacity>=.999f)g.DrawImage(source,destination);
        else
        {
            readableMatrix.Matrix33=Math.Clamp(opacity,0,1);readableAlpha.SetColorMatrix(readableMatrix);
            g.DrawImage(source,Rectangle.Round(destination),0,0,source.Width,source.Height,GraphicsUnit.Pixel,readableAlpha);
        }
        g.InterpolationMode=interpolation;g.PixelOffsetMode=pixel;
    }
    private void DrawPurgaeActor(Graphics g,int form,RectangleF rect)
    {
        var action=PurgaeActionPose(form==2);
        float time=state==GameState.Round?frame:briefingClock;
        float breath=reducedEffects?0:MathF.Sin(time*.045f)*.008f;
        var saved=g.Save();g.TranslateTransform(rect.X+rect.Width*.5f,rect.Bottom);
        g.ScaleTransform(1-breath*.35f,1+breath);
        DrawPurgaePose(g,form,new(-rect.Width*.5f,-rect.Height,rect.Width,rect.Height),action.A,action.B,action.T);
        g.Restore(saved);
    }
    private void DrawPurgaeWorld(Graphics g)
    {
        // Illustrated forms are dialogue portraits; combat keeps the original
        // actor art, anchor and breathing motion without changing its collider.
        float pulse=1f+MathF.Sin(frame*.09f)*.04f;
        int size=(int)(135*pulse);
        int x=(int)(bossX+(bossRecoil>0?MathF.Sin(bossRecoil*2)*bossRecoil*.4f:0));
        int y=(int)bossY;
        var rect=new Rectangle(x,y,size,size);
        DrawCrimsonAura(g,x,y,size);
        Image? image=purgaeTransformed?enemy7??enemy3:enemy3;
        if(image!=null)DrawReadableSprite(g,image,rect,bossRecoil>5?Color.White:purgaeTransformed?Color.FromArgb(215,48,59):Gold);
        DrawCinderBrand(g,new(bossX,bossY,135,135),1);
        if(purgaeSkill2Active)DrawPurgaeSkill2Balls(g,bossX+77.5f,bossY+77.5f);
    }
    private void DrawPurgaeOrb(Graphics g,float cx,float cy,float size,bool reflected=false)
    {
        if(purgaeShinyOrbs.Length==0)
        {
            using var fill=new SolidBrush(reflected?Mint:Color.FromArgb(61,50,28));
            using var rim=new Pen(reflected?Mint:Gold,1.5f);
            g.FillEllipse(fill,cx-size*.5f,cy-size*.5f,size,size);g.DrawEllipse(rim,cx-size*.5f,cy-size*.5f,size,size);return;
        }
        int side=TruePurgaeEncounter?2:purgaeTransformed?1:0;
        int index=((int)(animationClock*.4f+cx*.017f+cy*.013f)%PurgaeOrbFrames+PurgaeOrbFrames)%PurgaeOrbFrames;
        float canvas=size*1.17f;
        g.DrawImage(purgaeShinyOrbs[side*PurgaeOrbFrames+index].Image,new RectangleF(cx-canvas*.5f,cy-canvas*.5f,canvas,canvas));
        if(reflected){using var rim=new Pen(Mint,1.6f);g.DrawEllipse(rim,cx-size*.5f,cy-size*.5f,size,size);}
    }
    private void DrawTruePurgaeBasicProjectile(Graphics g,Projectile p)
    {
        if(p.Type==AttackType.PurgaeSkill2Ball){DrawPurgaeOrb(g,p.X+p.W*.5f,p.Y+p.H*.5f,p.W,p.IsParried);return;}
        Color color=p.IsParried?Mint:Gold;
        using var fill=new SolidBrush(Color.FromArgb(235,p.IsParried?Color.FromArgb(40,112,104):Color.FromArgb(57,48,31)));
        using var edge=new Pen(color,1.5f);
        g.FillEllipse(fill,p.Rect);g.DrawEllipse(edge,p.Rect);
        using var core=new SolidBrush(color);g.FillEllipse(core,p.X+p.W*.38f,p.Y+p.H*.38f,p.W*.24f,p.H*.24f);
    }
}
