using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? phantomKick;
    private readonly Dictionary<Image,Bitmap> greenPlayerSprites=new();
    private float breathFxTimer;
    private bool FocusActive=>activeSkill==ActivePower.Slow&&activeTimer>0&&state==GameState.Round&&!PhaseIntroActive&&!bossBriefingActive&&!TruePurgaeTimeFrozen&&!MelodyLastStandActive;
    private RectangleF PlayerHitBox=>new(px+7,sliding?py+30:py+8,PlayerWidth-14,sliding?38:PlayerHeight-12);
    private RectangleF KickHitBox=>new(px-16-parryRangeBonus,py-12-parryRangeBonus*.5f,PlayerWidth+32+parryRangeBonus*2,PlayerHeight+24+parryRangeBonus);
    private RectangleF BossHitBox
    {
        get{float pulse=1+MathF.Sin(frame*.09f)*(round==4?.075f:.04f);int size=(int)((round==4?155:135)*pulse);return new(bossX-8,bossY-8,size+16,size+16);}
    }
    private void LoadSkillSpectacle()
    {
        string path=AssetPath("PhantomSideKick.png");
        if(File.Exists(path)){using var image=new Bitmap(path);phantomKick=ExtractMainSprite(image,new(0,0,image.Width,image.Height),320,215);}
        LoadEchoMotion();
    }
    private void DisposeSkillSpectacle(){DisposeEchoMotion();phantomKick?.Dispose();foreach(var image in greenPlayerSprites.Values)image.Dispose();greenPlayerSprites.Clear();}
    private void DrawPlayerSprite(Graphics g,Image source,RectangleF target,Color rim,float alpha=1)
    {
        if(TryDrawCinderGolem(g,source,target,rim,alpha))return;
        if(cloneActive)
        {
            if(!greenPlayerSprites.TryGetValue(source,out var green))
            {
                green=new(source.Width,source.Height,PixelFormat.Format32bppPArgb);
                using var cg=Graphics.FromImage(green);using var tint=new ImageAttributes();
                tint.SetColorMatrix(new ColorMatrix(new[]{new[]{.10f,.28f,.11f,0,0},new[]{.20f,.56f,.22f,0,0},new[]{.04f,.10f,.04f,0,0},new[]{0f,0,0,1f,0},new[]{.015f,.14f,.025f,0,1f}}));
                cg.DrawImage(source,new Rectangle(0,0,green.Width,green.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,tint);greenPlayerSprites[source]=green;
            }
            if(cinderOuterFrames.TryGetValue(source,out var cinder))cinderOuterFrames[green]=cinder;
            if(cinderEyePoints.TryGetValue(source,out var eyes))cinderEyePoints[green]=eyes;
            if(attachedFuriosoFire.TryGetValue(source,out var fire))attachedFuriosoFire[green]=fire;
            if(furiosoBodyLights.TryGetValue(source,out var bodyLight))furiosoBodyLights[green]=bodyLight;
            source=green;rim=Color.FromArgb(105,255,138);
        }
        if(CinderVisualActive&&!cloneActive)rim=Color.FromArgb(171,121,88);
        if(breathFxTimer>0&&!sliding)
        {
            float inhale=MathF.Sin((1-breathFxTimer/42)*MathF.PI);
            target=new(target.X-inhale*1.5f,target.Y-inhale*2,target.Width+inhale*3,target.Height+inhale*2);
        }
        DrawFuriosoAttached(g,source,target,alpha,false);DrawCinderAttached(g,source,target,alpha,false);
        if(MelodyLastStandActive)DrawEchoFrame(g,source,target,alpha);else DrawReadableSprite(g,source,target,rim,alpha);
        DrawCinderStone(g,source,target,alpha);
        DrawFuriosoAttached(g,source,target,alpha,true);DrawCinderAttached(g,source,target,alpha,true);
    }
    private void DrawSpectralEcho(Graphics g,AbilityEcho echo)
    {
        float t=EchoProgress(echo);PointF position=EchoPosition(echo);
        float x=position.X,y=position.Y,dx=echo.TargetX-echo.X,dy=echo.TargetY-echo.Y;
        float length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy)),ux=dx/length,uy=dy/length;
        Color color=echo.Precision?Color.FromArgb(255,212,142):Color.FromArgb(108,255,228);
        float alpha=Math.Clamp(t*9+.18f,0,1)*Math.Clamp((1-t)*7+.45f,0,1);
        float extension=Math.Clamp((t*42-18)/16,0,1);
        var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(MathF.Atan2(dy,dx)*180/MathF.PI);
        if(!reducedEffects)
        {
            // The actor and its afterimages share the exact same straight axis.
            // Only the attached aura ripples; the lead heel never follows an arc.
            for(int i=3;i>0;i--)
            {
                var trail=g.Save();g.TranslateTransform(-i*(8+extension*7),0);
                DrawEchoPoses(g,echo,Math.Max(0,t-i/EchoFlightFrames),alpha*(.14f-i*.025f));g.Restore(trail);
            }
            if(inhaleMist!=null)for(int i=0;i<5;i++)
            {
                float wave=MathF.Sin(animationClock*.24f+i*1.7f),s=42+i%3*8;
                DrawEchoFrame(g,inhaleMist,new(-58-i*19,-s*.5f+wave*8,s*1.4f,s),alpha*.22f);
            }
            // The ghost carries the same authored flame material as the runner.
            // It is attached to the pose envelope on the straight flight axis.


        }
        DrawEchoPoses(g,echo,t,alpha*.94f);g.Restore(saved);
        for(int i=0;i<(reducedEffects?4:15);i++)
        {
            float age=(animationClock*.043f+i/15f)%1,side=MathF.Sin(i*2.4f+age*4)*15;
            float distance=24+age*126,sx=x-ux*distance-uy*side,sy=y-uy*distance+ux*side;
            g.FillEllipse(ParticleBrush(i%4==0?Color.White:color,(int)(195*alpha*(1-age))),sx,sy,1.2f+i%2,1.2f+i%2);
        }
        if(t>.78f)DrawKickLight(g,new(x-22,y-10,44,20),alpha*.65f);
    }
    private void DrawGuardAura(Graphics g,bool ward)
    {
        Color color=ward?Color.FromArgb(255,215,144):Color.FromArgb(255,144,49);
        float phase=animationClock*.018f;
        DrawKickLight(g,new(px-40,py-23,135,115),ward?.22f:.36f);
        int count=reducedEffects?8:28;
        for(int i=0;i<count;i++)
        {
            float t=(phase*.22f+i/(float)count)%1,angle=i*2.39996f+phase*.25f;
            float radius=30+MathF.Sin(t*MathF.PI)*13,depth=MathF.Sin(angle);
            float x=px+26+MathF.Cos(angle)*radius,y=py+87-t*112;
            float a=MathF.Sin(t*MathF.PI)*(depth*.24f+.7f);
            var pen=ParticlePen(color,(int)(150*a),i%4==0?2:1);
            g.DrawBezier(pen,x,y+7,x-5,y+2,x+4,y-3,x,y-11);
            g.FillEllipse(ParticleBrush(i%5==0?Color.FromArgb(255,237,192):color,(int)(210*a)),x-1,y-1,2,3);
        }
        // Rising ribbons follow the silhouette instead of hiding it inside a solid shield.
        for(int i=0;i<(reducedEffects?1:3);i++)
        {
            float offset=MathF.Sin(phase+i*2.1f)*5;
            using var path=new GraphicsPath();path.AddBezier(px-5-i*6,py+79,px-27-offset,py+39,px+13+offset,py+3,px+30,py-13);
            using var haze=new Pen(Color.FromArgb(26,color),7);using var edge=new Pen(Color.FromArgb(115-i*22,color),1.1f);
            g.DrawPath(haze,path);g.DrawPath(edge,path);
        }
    }
    private void DrawInhale(Graphics g)
    {
        if(breathFxTimer<=0)return;
        float progress=Math.Clamp(1-breathFxTimer/42,0,1);
        float mouthX=px+35,mouthY=py+18;
        if(inhaleMist!=null)for(int i=0;i<(reducedEffects?7:18);i++)
        {
            float delay=i%4*.035f,local=Math.Clamp((progress-delay)/(1-delay),0,1);
            float form=Math.Clamp(local/.24f,0,1),pull=Math.Clamp((local-.22f)/.73f,0,1);
            float ease=pull*pull*(3-2*pull),angle=i*2.39996f+MathF.Sin(local*4+i)*.12f*(1-pull);
            float radius=(67+i%4*12)*(1-ease),size=(41+i%3*10)*(1+form*.2f)*(1-ease*.78f);
            float x=mouthX+MathF.Cos(angle)*radius,y=mouthY+MathF.Sin(angle)*radius*.73f+12*(1-ease);
            float opacity=form*(1-Math.Clamp((local-.84f)/.16f,0,1))*.63f;
            var puff=g.Save();g.TranslateTransform(x,y);g.RotateTransform(i*41+MathF.Sin(local*7+i)*19);
            DrawEchoFrame(g,inhaleMist,new(-size*.62f,-size*.44f,size*1.24f,size*.88f),opacity);g.Restore(puff);
        }
        Label(g,"호흡  ·  Q −1초",new(px-53,py-67,160,23),11,Mint);
    }
    private void DrawFocusGeometry(Graphics g)
    {
        if(!FocusActive)return;
        using var playerPen=new Pen(Color.FromArgb(230,100,222,255),1.1f);
        using var kickPen=new Pen(Color.FromArgb(ParryActive?250:160,255,214,126),ParryActive?1.8f:1){DashStyle=ParryActive?DashStyle.Solid:DashStyle.Dash};
        void Box(RectangleF r,Pen pen){g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}
        Box(PlayerHitBox,playerPen);Box(KickHitBox,kickPen);
        using var threat=new Pen(Color.FromArgb(210,255,101,117),.9f);
        foreach(var p in projectiles)Box(p.Rect,p.IsParried?playerPen:threat);
        foreach(var o in obstacles)Box(o.Rect,threat);foreach(var o in risingObstacles)Box(o.Rect,threat);
        if(round>=2&&!dashActive)Box(BossHitBox,threat);
        Label(g,ParryActive?"반사 판정 ON":"발차기 범위",new(px-35,py-28,140,19),10,Gold);
    }
    private void DrawFocusOverlay(Graphics g)
    {
        if(!FocusActive)return;
        int tick=(int)(animationClock/3);
        for(int i=0;i<(reducedEffects?2:7);i++)
        {
            int y=142+(tick*31+i*67)%350;
            g.FillRectangle(ParticleBrush(i%2==0?Color.LightCyan:Color.FromArgb(255,139,150),reducedEffects?12:21),80+(i*137+tick*19)%310,y,180+(i*83)%370,i%3==0?2:1);
        }
        Label(g,$"느려.  {activeTimer/60:0.0}초   ·   청록: 몸 / 금색: 발차기 / 붉은색: 위험",new(180,115,640,23),11,Color.FromArgb(211,250,255));
    }
    private static readonly byte[] FocusTone=Enumerable.Range(0,256).Select(v=>(byte)Math.Clamp((v-106)*1.13+119,0,255)).ToArray();
    private static unsafe void ApplyFocusGrade(Bitmap bitmap)
    {
        var data=bitmap.LockBits(new(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppRgb);
        try{fixed(byte* tone=FocusTone)for(int y=0;y<data.Height;y++){byte* row=(byte*)data.Scan0+y*data.Stride;for(int x=0;x<data.Width;x++)
        {
            byte* pixel=row+x*4;pixel[0]=tone[pixel[0]];pixel[1]=tone[pixel[1]];pixel[2]=tone[pixel[2]];
        }}}finally{bitmap.UnlockBits(data);}
    }
}
