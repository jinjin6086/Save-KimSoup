using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap[] kickFrames=Array.Empty<Bitmap>();
    private Bitmap? kickLight;
    private float kickPoseTimer;
    private int lastFootstrike=-1;
    private float menheraWanderClock;
    private readonly List<KickSpark> kickSparks=new(72);
    private readonly Dictionary<(Image,int),Bitmap> spectralBossSprites=new();
    private sealed class KickSpark { public float X,Y,Vx,Vy,Life,Total,Size;public Color Color; }
    private static readonly Color KickGold=Color.FromArgb(255,212,151);

    private void LoadKickCombat()
    {
        string file=AssetPath("PlayerKick.png");
        if(!File.Exists(file))return;
        using var sheet=new Bitmap(file);int cell=sheet.Width/3;
        kickFrames=new Bitmap[3];
        // Equal canvases keep the planted foot stable while the other leg extends.
        for(int i=0;i<3;i++)kickFrames[i]=ExtractMainSprite(sheet,new(i*cell,0,cell,sheet.Height),200,200);
    }
    private void DisposeKickCombat(){foreach(var f in kickFrames)f.Dispose();foreach(var f in spectralBossSprites.Values)f.Dispose();kickLight?.Dispose();}
    private void ResetKickCombat(){kickPoseTimer=menheraWanderClock=0;lastFootstrike=-1;kickSparks.Clear();}
    private void BeginKickPose(){kickPoseTimer=18;audio.Play("kickswing");}
    private void UpdateKickCombat(float step)
    {
        kickPoseTimer=Math.Max(0,kickPoseTimer-step);
        for(int i=kickSparks.Count-1;i>=0;i--)
        {
            var p=kickSparks[i];p.Life-=step;
            if(p.Life<=0){kickSparks.RemoveAt(i);continue;}
            p.X+=p.Vx*step;p.Y+=p.Vy*step;p.Vx*=MathF.Pow(.94f,step);p.Vy+=.10f*step;
        }
        if(state!=GameState.Round||PhaseIntroActive||sceneFadeTimer>0||hitStopTimer>0||gamePaused)return;
        int contact=(int)(animationClock/10);
        if(contact==lastFootstrike)return;
        lastFootstrike=contact;
        if(!onGround||sliding||DodgeActive||kickPoseTimer>0)return;
        // Footsteps have lower priority than telegraphs, impacts and voices.
        if(audio.ActiveVoices<5)audio.Play($"step{(round==1?0:round==2?1:2)}{contact%2}");
    }
    private void KickImpact(bool perfect)
    {
        kickPoseTimer=15;
        float x=px+55,y=py+35;
        int count=reducedEffects?12:perfect?65:44;
        for(int i=0;i<count&&kickSparks.Count<128;i++)
        {
            // Most flecks follow the reflected attack; a few short grains fall
            // below the boot so the contact reads as weight rather than magic.
            float angle=-1.45f+rng.NextSingle()*2.9f,speed=2+rng.NextSingle()*(perfect?10:8);
            float life=11+rng.Next(18);
            kickSparks.Add(new(){X=x,Y=y,Vx=MathF.Cos(angle)*speed,Vy=MathF.Sin(angle)*speed-1.4f,
                Life=life,Total=life,Size=.45f+rng.NextSingle()*1.45f,Color=i%7==0?Color.FromArgb(255,249,226):i%4==0?Color.FromArgb(163,116,60):KickGold});
        }
    }
    private bool DrawKickActor(Graphics g)
    {
        if(kickPoseTimer<=0||DodgeActive||sliding)return false;
        float elapsed=18-kickPoseTimer;
        float poseProgress=elapsed<4?elapsed/4:elapsed<10?1:1+(elapsed-10)/8;
        poseProgress=Math.Clamp(poseProgress,0,2);
        int pose=(int)poseProgress,next=Math.Min(2,pose+1);
        float blend=poseProgress-pose;
        blend=blend*blend*(3-2*blend);
        if(selectedCookie==0&&kickFrames.Length==3)
        {
            // The three original photographs overlap during transitions. The
            // support-foot offset also interpolates instead of jumping 12 px.
            float width=89,a=pose==1?12:0,b=next==1?12:0,x=px-14+a+(b-a)*blend;
            var rect=new RectangleF(x,py-11,width,width);
            DrawPlayerSprite(g,kickFrames[pose],rect,Gold,1-blend);
            if(next!=pose&&blend>.002f)DrawPlayerSprite(g,kickFrames[next],rect,Gold,blend);
        }
        else if(wolfPoses.Length==12)DrawWolfKick(g,poseProgress);
        else
        {
            DrawSimpleCookie(g,(int)px-4,(int)py,PlayerWidth,PlayerHeight);
            using var boot=new Pen(Color.FromArgb(241,178,201),12){StartCap=LineCap.Round,EndCap=LineCap.Round};
            float extension=poseProgress<=1?poseProgress:2-poseProgress,reach=17+21*extension;
            g.DrawLine(boot,px+31,py+52,px+31+reach,py+36);
        }
        return true;
    }
    private void DrawKickFeedback(Graphics g)
    {
        foreach(var p in kickSparks)
        {
            float k=Math.Clamp(p.Life/p.Total,0,1),size=p.Size*(.5f+k*.5f);
            int alpha=(int)(235*MathF.Min(1,k*2));
            if(!reducedEffects&&size>1.1f)
                g.FillEllipse(ParticleBrush(p.Color,(int)(alpha*.12f)),p.X-size*2.7f,p.Y-size*2.7f,size*5.4f,size*5.4f);
            g.FillEllipse(ParticleBrush(p.Color,alpha),p.X-size,p.Y-size,size*2,size*2);
            if(k>.25f)
            {
                var streak=ParticlePen(p.Color,(int)(alpha*.55f),1);
                g.DrawLine(streak,p.X-p.Vx*.85f,p.Y-p.Vy*.85f,p.X,p.Y);
            }
        }
        foreach(var hit in parrySlices)
        {
            float k=hit.Life/(hit.Perfect?25:20),p=1-k;
            if(p>.46f)continue;
            float fade=1-p/.46f;
            float x=hit.X+23,y=hit.Y-3,w=21+p*44,h=8+p*12;
            DrawKickLight(g,new(x-w*1.6f,y-h*2,w*3.2f,h*4),fade*(hit.Perfect?1:.85f));
            using(var heel=new Pen(Color.FromArgb((int)(240*fade),255,252,224),2.4f){StartCap=LineCap.Round,EndCap=LineCap.Round})
                g.DrawBezier(heel,x-38-p*24,y+23+p*20,x-17,y-27-p*8,x+20,y-22,x+48+p*30,y-10);
            for(int s=0;s<(reducedEffects?3:9);s++)
            {
                float a=s*2.399f,r=9+p*(70+s%3*19),gx=x+MathF.Cos(a)*r,gy=y+MathF.Sin(a)*r*.63f;
                float size=(s%3==0?4:2.3f)*fade;
                var gleam=ParticlePen(s%2==0?Color.White:KickGold,(int)(230*fade),1);
                g.DrawLine(gleam,gx-size,gy,gx+size,gy);g.DrawLine(gleam,gx,gy-size*1.6f,gx,gy+size*1.6f);
            }
            // A single curved leather-contact highlight, with a soft golden
            // wake and a fine hot edge. No geometric seal or rotating burst.
            var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(hit.Angle*180/MathF.PI);
            using var wake=new Pen(Color.FromArgb((int)(38*fade),201,157,88),5){StartCap=LineCap.Round,EndCap=LineCap.Round};
            using var glint=new Pen(Color.FromArgb((int)(225*fade),255,245,215),1.05f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(wake,-13,6,5,-7,25,-8,44,-1);g.DrawBezier(glint,-11,5,6,-6,25,-7,42,-1);
            g.Restore(saved);
        }
    }
    private void DrawKickLight(Graphics g,RectangleF rect,float alpha)
    {
        if(kickLight==null)
        {
            kickLight=new Bitmap(192,96,PixelFormat.Format32bppPArgb);
            using var cg=Graphics.FromImage(kickLight);cg.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=new GraphicsPath();path.AddEllipse(0,0,192,96);
            using var light=new PathGradientBrush(path){CenterColor=Color.FromArgb(210,255,238,193),SurroundColors=new[]{Color.Transparent}};
            cg.FillPath(light,path);
        }
        readableMatrix.Matrix33=Math.Clamp(alpha,0,1);readableAlpha.SetColorMatrix(readableMatrix);
        g.DrawImage(kickLight,Rectangle.Round(rect),0,0,kickLight.Width,kickLight.Height,GraphicsUnit.Pixel,readableAlpha);
    }
    private void DrawImpactDust(Graphics g)
    {
        if(parryFxTimer<=0||parryFxX<500)return;
        float k=Math.Clamp(parryFxTimer/18,0,1),p=1-k;
        for(int i=0;i<(reducedEffects?4:12);i++)
        {
            float a=i*2.39996f,r=(12+i%3*6)+p*(35+i%4*9);
            float x=parryFxX+MathF.Cos(a)*r,y=parryFxY+MathF.Sin(a)*r*.7f;
            g.FillEllipse(ParticleBrush(KickGold,(int)(210*k)),x,y,1.5f+i%2,1.5f+i%2);
        }
    }
    private void DrawEatStraw(Graphics g)
    {
        float x=px+40,y=py+19;
        // A reusable brushed-metal straw. Its mouthpiece stays on the lips,
        // and the rounded bend aims the open end toward incoming obstacles.
        using var tube=new GraphicsPath();tube.AddBezier(x,y,x+5,y+1,x+9,y+2,x+13,y+7);
        tube.AddBezier(x+13,y+7,x+21,y+18,x+21,y+21,x+29,y+21);tube.AddLine(x+29,y+21,x+40,y+21);
        using var shadow=new Pen(Color.FromArgb(205,14,17,20),4.4f){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var body=new Pen(Color.FromArgb(232,133,148,150),3.05f){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var highlight=new Pen(Color.FromArgb(235,222,230,220),.75f){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round};
        g.DrawPath(shadow,tube);g.DrawPath(body,tube);
        var shifted=g.Save();g.TranslateTransform(0,-.65f);g.DrawPath(highlight,tube);g.Restore(shifted);
        using var aperture=new SolidBrush(Color.FromArgb(240,26,36,35));g.FillEllipse(aperture,x+38.5f,y+19.6f,2.8f,3.0f);
        using var lip=new Pen(Color.FromArgb(228,199,210,205),.65f);g.DrawArc(lip,x+38.5f,y+19.6f,2.8f,3,250,220);
        for(int i=0;i<(reducedEffects?3:7);i++)
        {
            float t=(animationClock*.028f+i/7f)%1,ease=1-(1-t)*(1-t);
            float sx=x+41+(1-ease)*48,sy=y+21+MathF.Sin(i*2.7f)*(1-ease)*15;
            float a=145*MathF.Sin(t*MathF.PI),r=.65f+(1-ease)*.5f;
            g.FillEllipse(ParticleBrush(i%3==0?KickGold:Mint,(int)a),sx-r,sy-r,r*2,r*2);
            if(!reducedEffects&&t>.15f&&t<.7f)
                g.DrawLine(ParticlePen(Mint,(int)(a*.32f),1),sx+3,sy,sx,sy);
        }
    }
    private void UpdateMenheraWander(float step)
    {
        menheraWanderClock+=step;
        float t=menheraWanderClock;
        float x=725+MathF.Sin(t*.012f)*94+MathF.Sin(t*.027f)*24;
        float y=213+MathF.Sin(t*.018f+.4f)*58+MathF.Sin(t*.008f)*21;
        float ease=1-MathF.Pow(.966f,step);
        bossX+=(x-bossX)*ease;bossY+=(y-bossY)*ease;
    }
    private void DrawSpectralBoss(Graphics g,Image source,RectangleF rect,Color color,float alpha)
    {
        var key=(source,color.ToArgb());
        if(!spectralBossSprites.TryGetValue(key,out var ghost))
        {
            if(spectralBossSprites.Count>=16){foreach(var old in spectralBossSprites.Values)old.Dispose();spectralBossSprites.Clear();}
            ghost=new Bitmap(256,256,PixelFormat.Format32bppPArgb);
            using var cg=Graphics.FromImage(ghost);using var tint=new ImageAttributes();
            tint.SetColorMatrix(new ColorMatrix(new[]{new[]{.08f,0,0,0,0},new[]{0f,.08f,0,0,0},new[]{0f,0,.08f,0,0},new[]{0f,0,0,1f,0},new[]{color.R/255f*.92f,color.G/255f*.92f,color.B/255f*.92f,0,1f}}));
            cg.InterpolationMode=InterpolationMode.HighQualityBicubic;
            cg.DrawImage(source,new Rectangle(0,0,256,256),0,0,source.Width,source.Height,GraphicsUnit.Pixel,tint);
            spectralBossSprites[key]=ghost;
        }
        readableMatrix.Matrix33=alpha;readableAlpha.SetColorMatrix(readableMatrix);
        g.DrawImage(ghost,Rectangle.Round(rect),0,0,ghost.Width,ghost.Height,GraphicsUnit.Pixel,readableAlpha);
    }
    private void DrawMelodyHaze(Graphics g,int x,int y,int size)
    {
        using var path=new GraphicsPath();path.AddEllipse(x-36,y-28,size+72,size+56);
        using var haze=new PathGradientBrush(path){CenterColor=Color.FromArgb(menheraMode?34:20,191,106,176),SurroundColors=new[]{Color.Transparent}};
        g.FillPath(haze,path);
        if(!menheraMode||reducedEffects)return;
        for(int i=0;i<7;i++)
        {
            float t=(animationClock*.007f+i/7f)%1;
            float sx=x+size*.5f+MathF.Sin(i*2.4f+t*5)*size*.6f,sy=y+size*(1-t);
            g.FillEllipse(ParticleBrush(Color.FromArgb(233,160,221),(int)(100*MathF.Sin(t*MathF.PI))),sx,sy,1.5f,2.5f);
        }
    }
}

internal sealed partial class GameAudio
{
    private static byte[] MakeKick(bool perfect)
    {
        double duration=perfect?.29:.23;int count=(int)(duration*SampleRate);
        var pcm=new byte[count*2];var random=new Random(perfect?231:229);double low=0,mid=0,phase=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,n=random.NextDouble()*2-1;
            low+=.026*(n-low);mid+=.18*(n-mid);phase+=Math.Tau*(58+99*Math.Exp(-t*42))/SampleRate;
            double body=Math.Sin(phase)*Math.Exp(-t*24)*.83;
            double leather=(mid-low)*Math.Exp(-t*35)*1.55;
            double contactAge=t-.017,contact=contactAge<0?0:(mid-low)*Math.Min(1,contactAge/.003)*Math.Exp(-contactAge*56)*.70;
            double cloth=low*Math.Exp(-t*16)*1.6;
            double weight=perfect?Math.Sin(t*Math.Tau*78)*Math.Exp(-t*19)*.24:0;
            double envelope=Math.Min(1,t/.003)*Math.Min(1,(duration-t)*36);
            short value=(short)(Math.Tanh((body+leather+contact+cloth+weight)*1.6)*envelope*(perfect?.43:.37)*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return RoundPlayerClip(pcm,perfect?1850:1600);
    }
    private static byte[] MakeFootstep(int terrain,int foot)
    {
        const double duration=.16;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var rng=new Random(122+terrain*7+foot);
        double low=0,mid=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,n=rng.NextDouble()*2-1;low+=.035*(n-low);mid+=.17*(n-mid);
            double thump=Math.Sin(Math.Tau*(terrain==1?91:70)*(1+foot*.055)*t)*Math.Exp(-t*43)*.66;
            double scuff=(mid-low)*Math.Exp(-t*(terrain==0?31:44))*.82;
            double grit=low*Math.Exp(-t*24)*(terrain==0?1.7:1.1);
            double env=Math.Min(1,t/.0035)*Math.Min(1,(duration-t)*45);
            short value=(short)(Math.Tanh((thump+scuff+grit)*1.4)*env*(terrain==1?.115:.105)*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return RoundPlayerClip(pcm,terrain==1?1450:1200);
    }
}
