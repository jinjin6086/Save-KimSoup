using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private float FinaleShake
    {
        get
        {
            if(reducedEffects)return 0;
            if(lastStandPhase==11){float age=lastStandClock-RodianRageRelease;return age>=0?1.25f*MathF.Exp(-age*.065f):0;}
            if(lastStandPhase==4)return .24f+lastStandPulse*.055f;
            if(lastStandPhase!=5)return 0;
            if(lastStandClock>=FinaleTiming.Impact&&lastStandClock<FinaleTiming.Explosion)return 0;
            if(lastStandClock<FinaleTiming.Impact)return .65f+SmoothUnit(lastStandClock/FinaleTiming.Impact)*.65f;
            return 4.8f*MathF.Exp(-(lastStandClock-FinaleTiming.Explosion)*.044f);
        }
    }
    private void DrawFinaleVictory(Graphics g,PointF hero,PointF boss,float cx,float cy,float width)
    {
        float t=lastStandClock;
        if(t<FinaleTiming.Impact)
        {
            // Separate pulse origins create a train of pressure waves. They
            // expand in world space while the camera reveals the full arena.
            for(int k=0;k<(reducedEffects?1:4);k++)
            {
                float p=(t-k*24)/98;
                if(p>0&&p<1)DrawApotheosisSurge(g,Mix(hero.X,cx,.14f+k*.18f),Mix(hero.Y,cy,.14f+k*.18f),p,.65f+k*.07f);
            }
            if(t>70)DrawLaserBloom(g,cx,cy,45+SmoothUnit((t-70)/42)*31,52+SmoothUnit((t-70)/42)*44,ClashBlue,SmoothUnit((t-70)/42)*.8f);
        }
        else if(t>=FinaleTiming.Explosion)
        {
            float age=t-FinaleTiming.Explosion,p=age/(FinaleTiming.Finish-FinaleTiming.Explosion);
            float fade=1-SmoothUnit((age-38)/110);
            float ex=boss.X+(boss.X-hero.X)*.40f,ey=boss.Y+(boss.Y-hero.Y)*.40f;
            DrawClashBeam(g,hero.X,hero.Y,ex,ey,width*(1+.15f*MathF.Sin(p*MathF.PI)),ClashBlue,fade,true);
            var body=FinaleBossBounds;float bx=body.X+body.Width*.50f,by=body.Y+body.Height*.48f;
            // The initial blast expands fast. Its rolling cloud then settles
            // for several seconds instead of slowing the impact itself down.
            float blast=age<46?.60f*SmoothUnit(age/46):.60f+.40f*SmoothUnit((age-46)/194);
            DrawApotheosisDetonation(g,bx,by,blast,1);
            DrawFinaleDebris(g,bx,by,Math.Min(1,age/115),1-SmoothUnit((age-85)/155));
            DrawFinaleFallout(g,bx,by,age);
            for(int k=0;k<(reducedEffects?1:3);k++)
            {
                float wave=(age-k*13)/90;if(wave>0&&wave<1)DrawApotheosisSurge(g,bx,by,wave,1.05f);
            }
        }
    }
    private void DrawFinaleDebris(Graphics g,float x,float y,float progress,float fade)
    {
        if(progress<=0||fade<=0)return;
        int count=reducedEffects?10:54;
        for(int i=0;i<count;i++)
        {
            float z=Scatter01(i*83+8),a=Scatter01(i*37+6)*MathF.Tau;
            float speed=120+z*400,xx=x+MathF.Cos(a)*speed*progress,yy=y+MathF.Sin(a)*speed*progress*.67f+progress*progress*170;
            float size=(1+z*4.5f)*(1-progress*.6f),spin=a+progress*(i%2==0?5:-5);
            var s=g.Save();g.TranslateTransform(xx,yy);g.RotateTransform(spin*180/MathF.PI);
            using var dark=new SolidBrush(Color.FromArgb((int)(225*fade),10,18,36));
            using var lit=new Pen(Color.FromArgb((int)(200*fade),113,207,255),.8f);
            var p=new[]{new PointF(-size,-size*.4f),new PointF(size*.6f,-size),new PointF(size,size*.5f),new PointF(-size*.4f,size*.8f)};
            g.FillPolygon(dark,p);g.DrawLines(lit,p.Take(3).ToArray());g.Restore(s);
        }
    }
    private void DrawFinaleImpactFrames(Graphics g,(float Zoom,float X,float Y) cam)
    {
        float t=lastStandClock-FinaleTiming.Impact;
        if(reducedEffects)
        {
            using var dim=new SolidBrush(Color.FromArgb(175,2,4,9));g.FillRectangle(dim,0,0,W,H);
            DrawLaserBloom(g,500+(lastBossX-cam.X)*cam.Zoom,300+(lastBossY-cam.Y)*cam.Zoom,50,62,ClashBlue,.36f);return;
        }
        bool white=lastStandClock<FinaleTiming.WhiteEnd;
        using var ground=new SolidBrush(white?Color.FromArgb(247,247,247):Color.FromArgb(2,3,5));g.FillRectangle(ground,0,0,W,H);
        var s=g.Save();g.TranslateTransform(500,300);g.ScaleTransform(cam.Zoom,cam.Zoom);g.TranslateTransform(-cam.X,-cam.Y);
        Color ink=white?Color.FromArgb(1,2,4):Color.FromArgb(246,249,253);
        float x=lastBossX,y=lastBossY;
        if(lastStandClock<FinaleTiming.NegativeEnd)
        {
            float r=white?117:143;using var brush=new SolidBrush(ink);
            var edge=new PointF[192];
            for(int i=0;i<edge.Length;i++)
            {
                float a=i/(float)edge.Length*MathF.Tau,scale=.72f+Scatter01(i*73+11)*.28f;
                float reach=r*scale*(i%13==0?1.85f:1);
                edge[i]=new(x+MathF.Cos(a)*reach,y+MathF.Sin(a)*reach*.76f);
            }
            g.FillClosedCurve(brush,edge,FillMode.Winding,.15f);
            for(int i=0;i<180;i++)
            {
                float a=Scatter01(i*17+7)*MathF.Tau,r1=72+Scatter01(i*51+3)*50,r2=r1+30+Scatter01(i*23+1)*230;
                g.DrawLine(ParticlePen(ink,130+(i%6)*20,i%7==0?2:1),x+MathF.Cos(a)*r1,y+MathF.Sin(a)*r1*.76f,x+MathF.Cos(a)*r2,y+MathF.Sin(a)*r2*.76f);
            }
            // A clear negative-space hot axis survives the ink impression.
            using var core=new Pen(white?Color.White:Color.FromArgb(2,3,5),8){EndCap=LineCap.Round};
            g.DrawLine(core,lastHeroX,lastHeroY,x+27,y-9);
            if(finaleHeroMotion.Length>0&&selectedCookie==0)DrawDetailedImpactActor(g,finaleHeroMotion[^1],FinaleHeroBounds,ink,white);
            else if(finaleHeroPoses.Length>2&&selectedCookie==0)DrawDetailedImpactActor(g,finaleHeroPoses[2],FinaleHeroBounds,ink,white);
            else if(selectedCookie==1&&wolfPoses.Length==12)DrawDetailedImpactActor(g,wolfPoses[11],new(FinalePlayerAnchorX-32,py-41,130,120),ink,white);
            if(finaleBossPoses.Length>1)DrawDetailedImpactActor(g,finaleBossPoses[1],FinaleBossBounds,ink,white);
        }
        else
        {
            float hold=SmoothUnit((lastStandClock-FinaleTiming.NegativeEnd)/(FinaleTiming.Silence-FinaleTiming.NegativeEnd));
            float core=lastStandClock>=FinaleTiming.Silence?.32f:.80f-hold*.42f;
            DrawLaserBloom(g,x,y,6-hold*2,6-hold*2,Color.White,core);
            using var line=new Pen(Color.FromArgb((int)(155*(1-hold)),230,242,255),.7f);
            g.DrawLine(line,x-34*(1-hold),y,x+34*(1-hold),y);
        }
        g.Restore(s);
    }
    private void DrawFinaleFallout(Graphics g,float x,float y,float age)
    {
        float fade=SmoothUnit(age/40)*(1-SmoothUnit((age-100)/200));if(fade<=0)return;
        int count=reducedEffects?20:92;
        for(int i=0;i<count;i++)
        {
            float z=Scatter01(i*83+3),side=Scatter01(i*37+6)*2-1;
            float xx=x+side*(35+age*(.22f+z*.55f));
            float yy=y+(Scatter01(i*71+8)-.45f)*95-age*(.10f+z*.22f)+age*age*.0019f;
            float a=fade*(.25f+z*.60f),size=.55f+z*1.1f;
            Color c=i%5==0?Color.FromArgb(234,248,255):i%7==0?ClashPink:ClashBlue;
            g.FillEllipse(ParticleBrush(c,(int)(150*a)),xx,yy,size,size);
        }
    }
    private void DrawDetailedImpactActor(Graphics g,Image source,RectangleF bounds,Color ink,bool white)
    {
        if(!gpuPrototypeDrawing){DrawInkActor(g,source,bounds,ink);return;}
        // Keep the silhouette, but recover photographed cloth/face planes in
        // a narrow charcoal/silver range instead of a solid color cutout.
        float contrast=white?.22f:.34f,offset=white?.012f:.62f;
        using var attrs=new ImageAttributes();
        attrs.SetColorMatrix(new ColorMatrix(new[]{
            new[]{.299f*contrast,.299f*contrast,.299f*contrast,0,0},
            new[]{.587f*contrast,.587f*contrast,.587f*contrast,0,0},
            new[]{.114f*contrast,.114f*contrast,.114f*contrast,0,0},
            new[]{0f,0,0,1f,0},new[]{offset,offset,offset,0,1f}}));
        g.DrawImage(source,Rectangle.Round(bounds),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);
    }
    private static void DrawInkActor(Graphics g,Image source,RectangleF bounds,Color ink)
    {
        using var attrs=new ImageAttributes();float r=ink.R/255f,gg=ink.G/255f,b=ink.B/255f;
        attrs.SetColorMatrix(new ColorMatrix(new[]{new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,0,0},new[]{0f,0,0,1f,0},new[]{r,gg,b,0,1f}}));
        g.DrawImage(source,Rectangle.Round(bounds),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);
    }
}
