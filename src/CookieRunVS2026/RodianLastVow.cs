using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private const int RodianDespairFrames=150,RodianRageFrames=90,RodianRageRelease=18,RodianMotionSteps=32;
    private Bitmap[] rodianLastVowMotion=Array.Empty<Bitmap>();
    private bool rodianRageReleased;
    private bool RodianSoloShot=>lastStandPhase is 10 or 11 or 1;
    // The generated sheet has generous, irregular padding. Preserve each whole
    // silhouette, then register actual feet, head, elbows and microphone grille.
    private static readonly Rectangle[] RodianAtlasCells={new(0,0,500,516),new(500,0,495,516),new(995,0,541,516),new(0,516,462,508),new(462,516,481,508),new(943,516,593,508)};
    private static readonly Rectangle[] RodianInkBounds={new(39,13,384,487),new(11,57,455,445),new(10,14,505,495),new(12,13,436,455),new(5,12,462,464),new(6,9,576,471)};
    private static readonly PointF[] RodianMicrophones={new(140,393),new(157,414),new(119,254),new(119,224),new(58,193),new(26,165)};
    private static readonly PointF[][] RodianAnatomy=
    {
        new PointF[]{new(130,165),new(180,215),new(151,279),new(203,273),new(211,304),new(105,476),new(313,481)},
        new PointF[]{new(109,211),new(168,246),new(149,309),new(192,306),new(211,321),new(86,480),new(285,481)},
        new PointF[]{new(150,117),new(182,189),new(129,228),new(276,229),new(223,292),new(70,483),new(366,485)},
        new PointF[]{new(118,159),new(172,219),new(115,243),new(199,254),new(205,306),new(70,438),new(331,444)},
        new PointF[]{new(142,139),new(194,196),new(121,205),new(178,211),new(237,289),new(64,445),new(420,453)},
        new PointF[]{new(169,126),new(211,190),new(127,190),new(190,199),new(292,286),new(103,444),new(495,450)}
    };
    private static PointF RodianRegisteredPoint(int pose,PointF p)
    {
        var b=RodianInkBounds[pose];float scale=Math.Min(378f/b.Width,378f/b.Height);
        return new(((384-b.Width*scale)*.5f+(p.X-b.X)*scale)/384,(384-b.Height*scale+(p.Y-b.Y)*scale)/384);
    }
    private static PointF[] RodianRegisteredAnatomy(int pose)
    {
        var p=RodianAnatomy[pose].Select(v=>RodianRegisteredPoint(pose,v)).ToArray();
        // Add the microphone as its own landmark: the shot always leaves the
        // held prop rather than floating beside the hands during extension.
        var all=Pose(p.SelectMany(v=>new[]{v.X,v.Y}).ToArray()).ToList();all.Add(RodianRegisteredPoint(pose,RodianMicrophones[pose]));return all.ToArray();
    }
    private void LoadRodianLastVowMotion()
    {
        string path=AssetPath("RodianLastVow6.png");if(!File.Exists(path))return;
        using var sheet=new Bitmap(path);var keys=new Bitmap[6];
        try
        {
            for(int i=0;i<6;i++)keys[i]=ExtractMainSprite(sheet,RodianAtlasCells[i],384,384);
            ShadeRodianDespairFace(keys[0],0);ShadeRodianDespairFace(keys[1],1);
            var marks=Enumerable.Range(0,6).Select(RodianRegisteredAnatomy).ToArray();
            rodianLastVowMotion=new Bitmap[5*RodianMotionSteps+1];
            for(int f=0;f<rodianLastVowMotion.Length;f++)
            {
                float progress=f/(float)RodianMotionSteps;int a=Math.Min(4,(int)progress);float t=progress-a;
                if(t<.001f){rodianLastVowMotion[f]=(Bitmap)keys[a].Clone();continue;}
                if(t>.999f){rodianLastVowMotion[f]=(Bitmap)keys[a+1].Clone();continue;}
                var target=marks[a].Zip(marks[a+1],(v,w)=>new PointF(Mix(v.X,w.X,t),Mix(v.Y,w.Y,t))).ToArray();
                using var left=WarpCinematicPose(keys[a],marks[a],target);using var right=WarpCinematicPose(keys[a+1],marks[a+1],target);
                // One opaque body per frame. A dissolve between different arm and hair
                // silhouettes creates two translucent characters in a close-up.
                rodianLastVowMotion[f]=(Bitmap)(t<.5f?left:right).Clone();
            }
        }
        finally{foreach(var b in keys)b?.Dispose();}
    }
    private static unsafe void ShadeRodianDespairFace(Bitmap sprite,int pose)
    {
        var center=RodianRegisteredPoint(pose,pose==0?new(132,184):new(113,233));
        var ink=RodianInkBounds[pose];float f=Math.Min(378f/ink.Width,378f/ink.Height);
        var d=sprite.LockBits(new(0,0,384,384),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<384;y++)for(int x=0;x<384;x++)
        {
            float xx=(x-center.X*384)/(44*f),yy=(y-center.Y*384)/(25*f),r=xx*xx+yy*yy;
            if(r>=1)continue;float shadow=.82f*(1-SmoothUnit(r));byte* p=(byte*)d.Scan0+y*d.Stride+x*4;
            p[0]=(byte)(p[0]*(1-shadow*.82f));p[1]=(byte)(p[1]*(1-shadow));p[2]=(byte)(p[2]*(1-shadow));
        }}finally{sprite.UnlockBits(d);}
    }
    private float RodianLastVowPose=>lastStandPhase switch
    {
        10=>SmoothUnit(lastStandClock/68),
        11=>lastStandClock<18?1:lastStandClock<34?1+SmoothUnit((lastStandClock-18)/16):lastStandClock<58?2:2+SmoothUnit((lastStandClock-58)/32),
        1=>lastStandClock<96?3+SmoothUnit(lastStandClock/96):4+SmoothUnit((lastStandClock-96)/54),
        _=>5
    };
    private static PointF RodianLastVowPoint(float pose,PointF[] points)
    {
        int i=Math.Min(4,(int)pose);float t=pose-i;var a=RodianRegisteredPoint(i,points[i]);var b=RodianRegisteredPoint(i+1,points[i+1]);return new(Mix(a.X,b.X,t),Mix(a.Y,b.Y,t));
    }
    private RectangleF RodianLastVowBody
    {
        get
        {
            var r=SmoothRodianEntryBounds(FinaleBossBounds);
            float load=lastStandPhase==1?SmoothUnit(lastStandClock/150):lastStandPhase is 2 or 3 or 8 or 9 or 4 or 5?1:0;
            // The feet stay planted as the shoulders pump against the recoil.
            float heave=MathF.Sin(lastStandRealClock*Mix(.085f,.25f,load))*Mix(.85f,.65f,load);
            r.Y-=heave;r.Height+=heave;
            if(load>0&&!reducedEffects)r.X+=MathF.Sin(lastStandRealClock*1.73f)*load*.38f;
            return r;
        }
    }
    private PointF RodianLastVowSocket
    {
        get{var p=RodianLastVowPoint(RodianLastVowPose,RodianMicrophones);var r=RodianLastVowBody;return new(r.X+r.Width*p.X,r.Y+r.Height*p.Y);}
    }
    private bool DrawRodianLastVowActor(Graphics g)
    {
        if(rodianLastVowMotion.Length==0)return false;
        float alpha=lastStandPhase==5?1-SmoothUnit((lastStandClock-FinaleTiming.Explosion)/28):1;
        if(alpha<=0)return true;
        if(lastStandPhase==10&&rodianEntrySource!=null&&lastStandClock<24)
        {
            float transition=SmootherUnit(lastStandClock/24);
            DrawEchoFrame(g,rodianEntrySource,RodianLastVowBody,1-transition);alpha*=transition;
        }
        int frame=Math.Clamp((int)MathF.Round(RodianLastVowPose*RodianMotionSteps),0,rodianLastVowMotion.Length-1);
        DrawEchoFrame(g,rodianLastVowMotion[frame],RodianLastVowBody,alpha);DrawCinderBrand(g,RodianLastVowBody,alpha);return true;
    }
    private void DrawRodianLastVowStage(Graphics g)
    {
        if(!RodianSoloShot)return;
        float isolation=lastStandPhase==10?SmootherUnit(lastStandClock/32):lastStandPhase==1?1-SmootherUnit((lastStandClock-106)/44):1;
        // Hide the arena, rather than putting a dialogue box over live combat.
        using(var black=new SolidBrush(Color.FromArgb((int)(236*isolation),1,2,5)))g.FillRectangle(black,-W,-H,W*3,H*3);
        if(lastStandPhase==10)return;
        float age=lastStandPhase==11?lastStandClock-RodianRageRelease:lastStandClock+RodianRageFrames-RodianRageRelease;
        if(age<0)return;
        var b=RodianLastVowBody;float power=lastStandPhase==11?SmoothUnit(age/22):1;
        DrawLaserBloom(g,b.X+b.Width*.45f,b.Y+b.Height*.51f,58,86,Color.FromArgb(206,12,64),power*.32f);
        if(lastStandPhase==11&&age<32)
        {
            float burst=SmoothUnit(age/32);DrawLaserBloom(g,b.X+b.Width*.43f,b.Y+b.Height*.52f,28+burst*145,5+burst*30,ClashPink,(1-burst)*.53f);
        }
        int count=reducedEffects?8:24;
        for(int i=0;i<count;i++)
        {
            float p=(age*.016f+Scatter01(i*31+4))%1,a=Scatter01(i*47+3)*MathF.Tau;
            float radius=(22+Scatter01(i*59+9)*75)*(1-p),x=b.X+b.Width*.43f+MathF.Cos(a)*radius,y=b.Y+b.Height*.52f+MathF.Sin(a)*radius*.95f;
            float opacity=MathF.Sin(p*MathF.PI)*power;
            g.FillEllipse(ParticleBrush(Color.FromArgb(255,68,114),(int)(145*opacity)),x,y,1.05f+Scatter01(i+3)*.8f,1.05f+Scatter01(i+3)*.8f);
        }
    }
    private void DrawRodianLastVowDialogue(Graphics g)
    {
        if(!RodianSoloShot)return;
        float labelFade=lastStandPhase==10?SmootherUnit(lastStandClock/32):1;
        Label(g,"강로디언",new(28,12,300,28),15,Color.FromArgb((int)(255*labelFade),194,164,176),false);
        if(lastStandPhase==10)
        {
            float fade=SmoothUnit((lastStandClock-28)/18)*(1-SmoothUnit((lastStandClock-138)/12));
            Label(g,"이젠 다 필요없어...",new(80,549,840,45),25,Color.FromArgb((int)(255*fade),196,187,197));
        }
        else
        {
            float age=lastStandPhase==11?lastStandClock-RodianRageRelease:RodianRageFrames-RodianRageRelease+lastStandClock;
            float fade=SmoothUnit(age/8)*(1-SmoothUnit((age-108)/24));
            if(fade<=0)return;
            // A stable red subtitle stays readable while the world jolts.
            Label(g,"소멸해 버려!!!!!!!!!",new(60,549,880,46),30,Color.FromArgb((int)(255*fade),255,48,69));
        }
    }
    private void DisposeRodianLastVowMotion()
    {rodianEntryFrame?.Dispose();rodianEntryFrame=null;foreach(var b in rodianLastVowMotion)b.Dispose();rodianLastVowMotion=Array.Empty<Bitmap>();}
}
