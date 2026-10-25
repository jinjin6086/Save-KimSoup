using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private void DrawGuardAuraBehind(Graphics g)
    {
        if(state!=GameState.Round||PhaseIntroActive||bossBriefingActive)return;
        bool ward=false;
        if(ward||wardBreakFx>0)DrawOrbitingGuard(g,ward,false);
    }
    private void DrawOrbitingGuard(Graphics g,bool ward,bool front)
    {
        bool ready=ward;
        float life=ready?1:Math.Clamp(wardBreakFx/28,0,1);
        if(life<=0)return;
        float time=animationClock*.047f,cx=px+26,cy=py+(sliding?53:38);
        float scatter=ready?0:(1-life)*31,rx=(sliding?51:42)+scatter,ry=(sliding?14:33)+scatter*.45f;
        int count=ward?4:3;
        for(int i=0;i<count;i++)
        {
            float angle=time+i*MathF.Tau/count,depth=MathF.Sin(angle);
            if((depth>=0)!=front)continue;
            float x=cx+MathF.Cos(angle)*rx,y=cy+depth*ry;
            float tangent=MathF.Sin(angle*.7f)*13;
            float scale=(.81f+(depth+1)*.115f)*(reducedEffects?.88f:1);
            var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(tangent);g.ScaleTransform(scale,scale);
            DrawOrbitShield(g,time+i*2.4f,life*(front?1:.72f));g.Restore(saved);
            if(!reducedEffects)
                for(int j=1;j<5;j++)
                {
                    float a=angle-j*.10f,s=(2.1f-j*.35f)*life;
                    float xx=cx+MathF.Cos(a)*rx,yy=cy+MathF.Sin(a)*ry;
                    g.FillEllipse(ParticleBrush(Color.FromArgb(255,151,48),(int)(80*life*(1-j*.17f))),xx-s,yy-s,s*2,s*2);
                }
        }
    }
    private void DrawOrbitShield(Graphics g,float phase,float alpha)
    {
        using var shield=new GraphicsPath();
        shield.AddLines(new[]{new PointF(-16,-18),new PointF(0,-22),new PointF(16,-18),new PointF(15,0)});
        shield.AddBezier(15,0,14,12,6,19,0,24);shield.AddBezier(0,24,-6,19,-14,12,-15,0);shield.CloseFigure();
        if(!reducedEffects){using var glow=new Pen(Color.FromArgb((int)(38*alpha),255,104,18),7){LineJoin=LineJoin.Round};g.DrawPath(glow,shield);}
        using var face=new LinearGradientBrush(new Rectangle(-18,-23,36,49),Color.FromArgb((int)(245*alpha),255,184,62),Color.FromArgb((int)(235*alpha),123,37,11),35);
        g.FillPath(face,shield);
        using var rim=new Pen(Color.FromArgb((int)(245*alpha),255,214,131),1.8f){LineJoin=LineJoin.Round};g.DrawPath(rim,shield);
        using var inset=new GraphicsPath();inset.AddLines(new[]{new PointF(-11,-13),new PointF(0,-16),new PointF(11,-13),new PointF(10,1)});
        inset.AddBezier(10,1,9,8,4,13,0,17);inset.AddBezier(0,17,-4,13,-9,8,-10,1);inset.CloseFigure();
        using var inner=new SolidBrush(Color.FromArgb((int)(210*alpha),143,58,17));g.FillPath(inner,inset);
        using var trim=new Pen(Color.FromArgb((int)(170*alpha),255,154,52),1);g.DrawPath(trim,inset);
        using var spike=new LinearGradientBrush(new Rectangle(-25,-30,50,50),Color.FromArgb((int)(245*alpha),255,230,167),Color.FromArgb((int)(240*alpha),194,91,23),90);
        g.FillPolygon(spike,new[]{new PointF(-6,-21),new PointF(0,-32),new PointF(6,-21)});
        g.FillPolygon(spike,new[]{new PointF(-15,-10),new PointF(-25,-6),new PointF(-15,-1)});
        g.FillPolygon(spike,new[]{new PointF(15,-10),new PointF(25,-6),new PointF(15,-1)});
        using var ridge=new Pen(Color.FromArgb((int)(230*alpha),255,218,137),1.7f);g.DrawLine(ridge,0,-13,0,15);
        g.FillEllipse(ParticleBrush(Color.FromArgb(255,231,162),(int)(220*alpha)),-2,-5,4,4);
        if(!reducedEffects)
        {
            using var ember=new Pen(Color.FromArgb((int)(130*alpha),255,119,28),1){StartCap=LineCap.Round,EndCap=LineCap.Round};
            for(int i=0;i<2;i++){float x=(i==0?-17:17),rise=MathF.Sin(phase*2+i)*3;g.DrawBezier(ember,x,7,x+3,0,x-4,-10,x,-17+rise);}
        }
    }
    private Bitmap? abyssHeatGlow;
    private void DrawAbyssHeatCore(Graphics g,float x,float y,float size,float alpha)
    {
        if(abyssHeatGlow==null)
        {
            abyssHeatGlow=new Bitmap(192,192,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var cg=Graphics.FromImage(abyssHeatGlow);using var p=new GraphicsPath();p.AddEllipse(0,0,192,192);
            using var heat=new PathGradientBrush(p){CenterColor=Color.FromArgb(230,74,0,16),SurroundColors=new[]{Color.Transparent}};cg.FillPath(heat,p);
        }
        auraMatrix.Matrix33=alpha;auraAttributes.SetColorMatrix(auraMatrix);
        g.DrawImage(abyssHeatGlow,Rectangle.Round(new RectangleF(x-size*.5f,y-size*.5f,size,size)),0,0,192,192,GraphicsUnit.Pixel,auraAttributes);
    }
}
