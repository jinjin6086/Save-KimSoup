using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap[] echoPoseFrames=Array.Empty<Bitmap>(),preciseEchoPoseFrames=Array.Empty<Bitmap>();
    private Bitmap? echoCrouchPose,preciseEchoCrouchPose;
    private Bitmap? inhaleMist;
    private readonly PointF[] echoArcPoints=new PointF[8];
    private readonly List<EchoContact> echoContacts=new(12);
    private sealed class EchoContact { public float X,Y,Life=15;public bool Precision; }

    private void LoadEchoMotion()
    {
        if(kickFrames.Length>0)
        {
            echoPoseFrames=kickFrames.Select(f=>MakeEchoFrame(f,false)).ToArray();
            preciseEchoPoseFrames=kickFrames.Select(f=>MakeEchoFrame(f,true)).ToArray();
        }
        // Reuse the actual low, two-knee crouch from the player's motion sheet.
        // It is tinted once at load time, never captured or recolored per frame.
        if(motionFrames.Length>5)
        {
            echoCrouchPose=MakeEchoFrame(motionFrames[5],false);
            preciseEchoCrouchPose=MakeEchoFrame(motionFrames[5],true);
        }
        inhaleMist=CreateInhaleMist();furiosoGlow=CreateFuriosoGlow();PrepareFuriosoFire();
    }
    private void DisposeEchoMotion()
    {
        foreach(var image in echoPoseFrames)image.Dispose();
        foreach(var image in preciseEchoPoseFrames)image.Dispose();
        echoCrouchPose?.Dispose();preciseEchoCrouchPose?.Dispose();
        inhaleMist?.Dispose();furiosoGlow?.Dispose();DisposeFuriosoFire();
    }
    private void CancelAbilityEchoes()
    {
        abilityEchoes.Clear();echoContacts.Clear();audio.StopEchoFlight();CancelCinderFlight();
    }
    private static float EchoProgress(AbilityEcho echo)=>Math.Clamp(1-echo.Delay/EchoFlightFrames,0,1);
    private static PointF EchoPosition(AbilityEcho echo)
    {
        float t=EchoProgress(echo);
        return new(echo.X+(echo.TargetX-echo.X)*t,echo.Y+(echo.TargetY-echo.Y)*t);
    }
    private static Bitmap MakeEchoFrame(Bitmap source,bool precise)
    {
        var frame=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(frame);using var attributes=new ImageAttributes();
        float r=precise?1.15f:.35f,b=precise?.48f:1.05f;
        attributes.SetColorMatrix(new ColorMatrix(new[]{
            new[]{.25f*r,.25f,.25f*b,0,0},new[]{.58f*r,.58f,.58f*b,0,0},
            new[]{.17f*r,.17f,.17f*b,0,0},new[]{0f,0,0,.94f,0},
            new[]{precise?.21f:.06f,precise?.18f:.23f,precise?.05f:.25f,0,1f}}));
        g.DrawImage(source,new Rectangle(0,0,frame.Width,frame.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        return frame;
    }
    private void DrawEchoFrame(Graphics g,Image source,RectangleF rect,float alpha)
    {
        if(alpha<=.002f)return;
        if(alpha>=.999f){g.DrawImage(source,rect);return;}
        readableMatrix.Matrix33=Math.Clamp(alpha,0,1);readableAlpha.SetColorMatrix(readableMatrix);
        g.DrawImage(source,Rectangle.Round(rect),0,0,source.Width,source.Height,GraphicsUnit.Pixel,readableAlpha);
    }
    private void DrawEchoPoses(Graphics g,AbilityEcho echo,float t,float alpha)
    {
        float age=t*42; // Preserve every pose beat while the full flight runs at 1.5x.
        Bitmap[] poses=echo.Precision?preciseEchoPoseFrames:echoPoseFrames;
        Bitmap? crouch=echo.Precision?preciseEchoCrouchPose:echoCrouchPose;
        if(poses.Length!=3)
        {
            if(phantomKick!=null)DrawEchoFrame(g,phantomKick,new(-142,-49,146,98),alpha);
            return;
        }
        // Eight opening frames hold the compressed silhouette. Then the guard,
        // chambered knee and extended foot each have a readable beat. Smooth
        // crossfades connect those photographs before the horizontal ghost pose.
        // Translation remains linear for the entire 28-frame flight.
        int stage=EchoPoseStage(age);float mix=EchoPoseBlend(age);
        Image Pose(int i)=>i switch{0=>crouch??poses[0],1=>poses[2],2=>poses[0],3=>poses[1],_=>phantomKick??poses[1]};
        RectangleF Bounds(int i)=>i switch
        {
            0=>new(-104,-30,108,83),
            1=>new(-99,-51,104,104),
            2=>new(-99,-53,104,104),
            3=>new(-102,-53,108,104),
            _=>new(-142,-49,146,98)
        };
        float chamber=age<8?MathF.Sin(age/8*MathF.PI)*2:0;
        RectangleF a=Bounds(stage);a.Offset(-chamber,chamber);
        DrawEchoPoseFlame(g,Pose(stage),a,stage,alpha*(1-mix));
        DrawEchoFrame(g,Pose(stage),a,alpha*(1-mix));
        if(mix>0&&stage<4)
        {
            var b=Bounds(stage+1);
            // The center slides continuously into the airborne silhouette; this
            // avoids a visible sideways pop when its longer canvas takes over.
            float dx=(b.X-a.X)*mix*.25f,dy=(b.Y-a.Y)*mix*.25f;
            b.Offset(-dx*(1-mix),-dy*(1-mix));
            DrawEchoPoseFlame(g,Pose(stage+1),b,stage+1,alpha*mix);
            DrawEchoFrame(g,Pose(stage+1),b,alpha*mix);
        }
    }
    private void DrawEchoPoseFlame(Graphics g,Image actor,RectangleF pose,int stage,float alpha)
    {
        // The envelope is derived from this actual pose's alpha silhouette.
        DrawAttachedFire(g,actor,pose,alpha*.88f,animationClock*.8f);
    }
    private static int EchoPoseStage(float age)=>age<14?0:age<23?1:age<29?2:age<34?3:4;
    private static float EchoPoseBlend(float age)
    {
        float blend=age<14?(age-8)/6:age<23?(age-18)/5:age<29?(age-23)/6:age<34?(age-29)/5:0;
        blend=Math.Clamp(blend,0,1);return blend*blend*(3-2*blend);
    }
    private void AddEchoContact(AbilityEcho echo)
    {
        if(echoContacts.Count>=12)echoContacts.RemoveAt(0);
        echoContacts.Add(new(){X=echo.TargetX,Y=echo.TargetY,Precision=echo.Precision});
    }
    private void UpdateEchoContacts(float step)
    {
        for(int i=echoContacts.Count-1;i>=0;i--)
        {
            echoContacts[i].Life-=step;
            if(echoContacts[i].Life<=0)echoContacts.RemoveAt(i);
        }
    }
    private void DrawEchoContacts(Graphics g)
    {
        foreach(var impact in echoContacts)
        {
            float k=impact.Life/15,p=1-k;
            Color c=impact.Precision?KickGold:Color.FromArgb(125,255,234);
            DrawKickLight(g,new(impact.X-40-p*22,impact.Y-15-p*11,80+p*44,30+p*22),k*.95f);
            for(int i=0;i<(reducedEffects?5:19);i++)
            {
                float angle=i*2.39996f,r=8+p*(24+i%4*14),s=(1+i%3*.5f)*k;
                float x=impact.X+MathF.Cos(angle)*r,y=impact.Y+MathF.Sin(angle)*r*.7f;
                g.FillEllipse(ParticleBrush(i%3==0?Color.White:c,(int)(230*k)),x-s,y-s,s*2,s*2);
                if(i%3==0)g.DrawLine(ParticlePen(c,(int)(200*k),1),x,y,x-MathF.Cos(angle)*9*k,y-MathF.Sin(angle)*7*k);
            }
        }
    }
    private static Bitmap CreateInhaleMist()
    {
        // Soft overlapping lobes are rasterized once. Drawing the inhaled smoke
        // only scales this cached stamp; it never allocates per-particle textures.
        var stamp=new Bitmap(128,128,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(stamp);g.SmoothingMode=SmoothingMode.AntiAlias;
        for(int i=0;i<9;i++)
        {
            float angle=i*2.39996f,r=i==0?0:12+i%3*7;
            float x=64+MathF.Cos(angle)*r,y=64+MathF.Sin(angle)*r*.8f,size=56+i%4*9;
            using var path=new GraphicsPath();path.AddEllipse(x-size*.5f,y-size*.5f,size,size);
            using var mist=new PathGradientBrush(path){CenterColor=Color.FromArgb(i==0?105:51,192,225,228),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(mist,path);
        }
        return stamp;
    }
}
