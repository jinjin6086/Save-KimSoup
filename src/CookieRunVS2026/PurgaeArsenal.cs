using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? purgaeRockArt;
    private static bool PurgaeBasicProjectile(Projectile p)=>p.Type is AttackType.StoneChunk or AttackType.Fireball;
    private void SpawnPurgaeBasicAttack()
    {
        PurgaeCastPose();
        float x=bossX-8,y=bossY+55,dx=px+26-x,dy=py+30-y,length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy));
        projectiles.Add(new(x,y,purgaeTransformed?AttackType.Fireball:AttackType.StoneChunk,purgaeTransformed?34:32,dx/length*9.2f,dy/length*9.2f));
    }
    private Bitmap RockProjectileArt()
    {
        if(purgaeRockArt!=null)return purgaeRockArt;
        purgaeRockArt=new Bitmap(144,144,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(purgaeRockArt);g.SmoothingMode=SmoothingMode.AntiAlias;
        PointF[] outline={new(28,28),new(71,14),new(112,34),new(130,74),new(111,117),new(63,131),new(22,107),new(13,63)};
        using var rock=new GraphicsPath();rock.AddPolygon(outline);
        using var fill=new LinearGradientBrush(new Rectangle(12,12,120,120),Color.FromArgb(162,151,134),Color.FromArgb(45,43,43),65);g.FillPath(fill,rock);
        using var rim=new Pen(Color.FromArgb(224,210,181),4);g.DrawPath(rim,rock);
        using var face=new SolidBrush(Color.FromArgb(110,202,190,168));g.FillPolygon(face,new[]{new PointF(28,28),new PointF(71,14),new PointF(83,61),new PointF(46,80)});
        using var shadow=new SolidBrush(Color.FromArgb(135,24,26,30));g.FillPolygon(shadow,new[]{new PointF(83,61),new PointF(112,34),new PointF(130,74),new PointF(111,117),new PointF(69,102)});
        using var crack=new Pen(Color.FromArgb(220,28,29,32),3);g.DrawLines(crack,new[]{new PointF(45,27),new PointF(52,52),new PointF(40,72),new PointF(63,94),new PointF(59,127)});
        g.DrawLines(crack,new[]{new PointF(52,52),new PointF(82,61),new PointF(105,92),new PointF(122,90)});
        for(int i=0;i<20;i++){float x=30+i*37%80,y=33+i*29%75;using var grain=new SolidBrush(Color.FromArgb(90,i%2==0?Color.White:Color.Black));g.FillEllipse(grain,x,y,2+i%3,2+i%2);}
        return purgaeRockArt;
    }
    private void DrawPurgaeBasicProjectile(Graphics g,Projectile p)
    {
        float cx=p.X+p.W*.5f,cy=p.Y+p.H*.5f,vx=p.VelocityX,vy=p.VelocityY;
        if(p.IsParried){vx=bossX+65-cx;vy=bossY+65-cy;}
        float speed=Math.Max(1,MathF.Sqrt(vx*vx+vy*vy)),dx=vx/speed,dy=vy/speed;
        if(p.Type==AttackType.StoneChunk)
        {
            for(int i=0;i<3;i++)
            {
                float t=(animationClock*.12f+i*.33f)%1,r=2+(1-t)*2;
                g.FillEllipse(ParticleBrush(Color.FromArgb(180,164,139),(int)(100*(1-t))),cx-dx*(18+t*24)-r,cy-dy*(18+t*24)-r,r*2,r*2);
            }
            var saved=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(animationClock*6+p.X*.3f);
            DrawReadableSprite(g,RockProjectileArt(),new(-p.W*.65f,-p.H*.65f,p.W*1.3f,p.H*1.3f),p.IsParried?Mint:Gold);g.Restore(saved);
            return;
        }
        Color flame=p.IsParried?Mint:Color.FromArgb(255,62,48);
        var state=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(MathF.Atan2(dy,dx)*180/MathF.PI);
        float length=63+MathF.Sin(animationClock*.6f)*8,radius=p.W*.48f;
        if(!reducedEffects)
            for(int i=0;i<3;i++)
            {
                float t=(animationClock*.035f+i/3f)%1,s=5+t*7;
                g.FillEllipse(ParticleBrush(Color.FromArgb(29,11,15),(int)(65*(1-t))),-20-t*66-s,-s+MathF.Sin(t*7+i)*8,s*2.5f,s*1.6f);
            }
        using var tail=new GraphicsPath();
        tail.AddBezier(7,-radius,-18,-radius*1.35f,-length*.7f,-radius*.4f,-length,-MathF.Sin(animationClock*.7f)*7);
        tail.AddBezier(-length,-MathF.Sin(animationClock*.7f)*7,-length*.6f,radius*.5f,-14,radius*1.2f,7,radius);tail.CloseFigure();
        using var fire=new LinearGradientBrush(new RectangleF(-length-1,-radius*1.5f,length+12,radius*3),Color.FromArgb(0,flame),Color.FromArgb(225,flame),0f);g.FillPath(fire,tail);
        if(!reducedEffects)
            for(int i=0;i<3;i++)
            {
                float offset=(i-1)*radius*.42f,flutter=MathF.Sin(animationClock*.26f+i*2.1f)*6;
                using var tongue=new Pen(Color.FromArgb(120-i*23,p.IsParried?Color.FromArgb(213,255,236):Color.FromArgb(255,175,83)),1.1f+i*.4f){StartCap=LineCap.Round,EndCap=LineCap.Round};
                g.DrawBezier(tongue,-4,offset,-19,offset+flutter,-length*.65f,offset-flutter,-length+14+i*5,flutter*.5f);
            }
        int emberCount=reducedEffects?3:9;
        for(int i=0;i<emberCount;i++)
        {
            float t=(animationClock*.07f+i/(float)emberCount)%1,ey=MathF.Sin(i*2+animationClock*.14f)*(7+t*8),ex=-20-t*67,s=1.8f*(1-t)+.6f;
            Color color=i%3==0?(p.IsParried?Color.FromArgb(231,255,235):Color.FromArgb(255,185,103)):flame;
            g.FillEllipse(ParticleBrush(color,(int)(180*(1-t))),ex,ey,s,s);
            g.DrawLine(ParticlePen(color,(int)(85*(1-t)),1),ex,ey,ex+3+t*4,ey);
        }
        using var ball=new GraphicsPath();ball.AddEllipse(-radius,-radius,radius*2,radius*2);
        using var core=new PathGradientBrush(ball){CenterPoint=new(-radius*.2f,-radius*.25f),CenterColor=p.IsParried?Color.White:Color.FromArgb(255,255,248,191),SurroundColors=new[]{p.IsParried?Color.FromArgb(230,47,232,207):Color.FromArgb(240,192,22,30)}};
        g.FillPath(core,ball);using var edge=new Pen(p.IsParried?Color.White:Color.FromArgb(255,252,163,75),2);g.DrawPath(edge,ball);g.Restore(state);
    }

    private void DrawCrimsonAura(Graphics g,float x,float y,float size)
    {
        if(round!=3||!purgaeTransformed)return;
        DrawAbyssAura(g,x,y,size);
    }
}
