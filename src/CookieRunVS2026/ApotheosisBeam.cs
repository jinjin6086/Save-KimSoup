using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // A prefiltered, premultiplied volume slice. Density, hot inner flow and
    // the cooler cylindrical skin are shaded separately before tone mapping.
    // This is 2D volumetric shading, not hardware ray tracing.
    private const int PlasmaFrames=40,PlasmaWidth=1536,PlasmaHeight=384;
    private readonly Bitmap[] apotheosisBlue=new Bitmap[PlasmaFrames],apotheosisPink=new Bitmap[PlasmaFrames];
    private bool plasmaReady;
    private float PlasmaClock=>MelodyLastStandActive?lastStandRealClock:animationClock;

    private void PrepareApotheosisPlasma()
    {
        if(plasmaReady)return;
        if(!PrepareRibbonPlasma())
        {
            // Preserve the previous authored beam as an asset fallback.
            for(int i=0;i<PlasmaFrames;i++)
            {
                apotheosisBlue[i]=blueClashFrames[i%32]!=null?(Bitmap)blueClashFrames[i%32].Clone():new Bitmap(768,160,PixelFormat.Format32bppPArgb);
                apotheosisPink[i]=pinkClashFrames[i%32]!=null?(Bitmap)pinkClashFrames[i%32].Clone():new Bitmap(768,160,PixelFormat.Format32bppPArgb);
            }
        }
        plasmaReady=true;
    }
    private void DisposeApotheosisPlasma()
    {
        DisposeBeamSurfaces();
        foreach(var b in apotheosisBlue)b?.Dispose();foreach(var b in apotheosisPink)b?.Dispose();
        plasmaReady=false;
    }
    private void DrawApotheosisBeam(Graphics g,float x,float y,float tx,float ty,float width,float energy,bool blue)
    {
        if(energy<=.002f||width<.1f)return;
        if(!plasmaReady)PrepareApotheosisPlasma();
        energy=Math.Clamp(energy,0,1);float dx=tx-x,dy=ty-y,len=MathF.Sqrt(dx*dx+dy*dy);if(len<.5f)return;
        // Keep the proportions of the first ribbon preview. A short half-beam
        // must not squeeze the entire long atlas into a fat accordion.
        float clock=PlasmaClock,angle=MathF.Atan2(dy,dx);
        const float ribbonThicknessScale=1.5f;
        float displayedWidth=Math.Min(width,len*.118f)*ribbonThicknessScale;
        float r=displayedWidth*.5f*(.72f+.28f*energy);
        Color color=blue?ClashBlue:ClashPink;
        if(!reducedEffects)
        {
            // Light reaches the floor below the actual moving beam axis.
            float reach=Math.Clamp(1-MathF.Abs((y+ty)*.5f-GroundY)/430,0,1);
            DrawLaserBloom(g,(x+tx)*.5f,GroundY+3,Math.Min(400,len*.60f),22+reach*14,color,energy*reach*.36f);
            // The authored ribbon already contains its multi-scale halo.
            // Avoid a second screen-sized haze over the same bright volume.
        }
        var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle*180/MathF.PI);
        float throat=Math.Min(len*.14f,26),tip=Math.Min(len*.04f,8);
        int frame=(int)(clock*(blue?1.31f:1.43f))%PlasmaFrames;
        var material=(blue?apotheosisBlue:apotheosisPink)[frame];
        // The tapered contour is baked into high-resolution fractional alpha.
        // A GDI Region clip has a binary edge and stair-steps under cinematic
        // zoom. No hard clip, tiled boundary or stroked polygon remains here.
        // These high-resolution, prefiltered alpha textures are reduced at
        // ordinary display sizes. Bilinear sampling retains the soft contour
        // without the costly bicubic reconstruction of every zoomed pixel.
        g.InterpolationMode=InterpolationMode.Bilinear;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        DrawRasterizedPlasma(g,material,len,r,energy,blue);
        // Receding pressure collars are elliptical cross sections, lit on one
        // side; their fast translation makes the cylindrical depth readable.
        int collars=reducedEffects?0:2;
        for(int i=0;i<collars;i++)
        {
            float p=(clock*.017f+i/(float)collars)%1,xx=throat+p*Math.Max(1,len-throat);
            float rr=r*(.15f+.85f*p),a=MathF.Sin(p*MathF.PI)*energy*.65f;
            using var back=new Pen(Color.FromArgb((int)(28*a),color),1.2f);g.DrawArc(back,xx-rr*.16f,-rr,rr*.32f,rr*2,95,170);
            using var front=new Pen(Color.FromArgb((int)(150*a),blue?Color.FromArgb(160,230,255):Color.FromArgb(255,170,220)),1.6f);
            g.DrawArc(front,xx-rr*.16f,-rr,rr*.32f,rr*2,-85,170);
        }
        DrawPlasmaParticles(g,len,r,clock,energy,blue);
        DrawPlasmaEndArcs(g,len,r,clock,energy,blue);
        g.Restore(saved);
        DrawApotheosisEmitter(g,x,y,angle,r,energy,blue);
    }
    private void DrawPlasmaEndArcs(Graphics g,float length,float radius,float clock,float energy,bool blue)
    {
        Color c=blue?ClashBlue:ClashPink;
        for(int k=0;k<(reducedEffects?4:10);k++)
        {
            float p=(clock*(.012f+k%3*.003f)+Scatter01(k*41+17))%1,side=k%2==0?-1:1,z=Scatter01(k*73+2);
            float fade=MathF.Sin(p*MathF.PI)*energy,tail=Math.Max(0,p-.34f),dt=p-tail;
            if(dt*radius<.2f)continue;
            float a=.35f+z*.30f,b=.80f+z*.25f,curl=1.2f+z*.8f;
            PointF At(float t)=>new(length+radius*(a*t-b*t*t),side*radius*(.30f+curl*t));
            PointF Tangent(float t)=>new(radius*(a-2*b*t),side*radius*curl);
            var start=At(tail);var end=At(p);var d0=Tangent(tail);var d1=Tangent(p);
            using var path=new GraphicsPath();
            path.AddBezier(start,new(start.X+d0.X*dt/3,start.Y+d0.Y*dt/3),new(end.X-d1.X*dt/3,end.Y-d1.Y*dt/3),end);
            g.DrawPath(ParticlePen(c,(int)(40*fade),3),path);
            g.DrawPath(ParticlePen(k%3==0?Color.FromArgb(229,249,255):c,(int)(150*fade),1),path);
            g.FillEllipse(ParticleBrush(Color.FromArgb(234,249,255),(int)(210*fade)),end.X-.65f,end.Y-.65f,.9f,.9f);
        }
    }
    private static void BakeInnerPlasmaGrains(Bitmap bitmap,int frame,bool blue)
    {
        using var g=Graphics.FromImage(bitmap);g.SmoothingMode=SmoothingMode.AntiAlias;
        for(int i=0;i<220;i++)
        {
            float depth=Scatter01(i*97+11),t=(frame*(.007f+depth*.034f)+Scatter01(i*31+5))%1;
            float cap=Math.Clamp((t-.94f)/.06f,0,1);
            float throat=(.065f+.935f*SmoothUnit(t/.20f))*MathF.Sqrt(Math.Max(.0001f,1-cap*cap)),x=t*bitmap.Width;
            float lane=(Scatter01(i*61+3)*2-1)*.70f;
            float y=bitmap.Height*.5f+(lane+MathF.Sin(t*11+i*3.7f+frame*.035f)*.065f)*bitmap.Height*.33f*throat;
            float fade=MathF.Sin(t*MathF.PI)*(.25f+.75f*depth),size=.65f+depth*1.45f;
            Color color=i%5==0?Color.FromArgb(238,251,255):blue?Color.FromArgb(95,211,255):Color.FromArgb(255,132,216);
            using var ink=new SolidBrush(Color.FromArgb((int)(205*fade),color));
            if(i%4==0)g.FillPolygon(ink,new[]{new PointF(x-4-depth*9,y),new PointF(x,y-size),new PointF(x+size*2,y),new PointF(x,y+size)});
            else g.FillEllipse(ink,x-size,y-size,size*2,size*2);
        }
    }
    private void DrawPlasmaParticles(Graphics g,float length,float r,float clock,float energy,bool blue)
    {
        Color c=blue?ClashBlue:ClashPink;int count=reducedEffects?16:96;
        for(int i=0;i<count;i++)
        {
            float z=Scatter01(i*29+17),speed=.012f+z*.016f;
            float t=(clock*speed+Scatter01(i*19+3))%1,xx=t*length;
            float yy=(i%2==0?-1:1)*(r*(.12f+t*.76f+z*.36f)+MathF.Sin(i*6.8f+t*3.8f)*r*.10f);
            float a=MathF.Sin(t*MathF.PI)*energy*(.24f+z*.76f),size=.35f+z*1.35f;
            Color spark=i%7==0?Color.FromArgb(237,249,255):c;
            size*=.64f;
            if(i%6==0)g.DrawLine(ParticlePen(spark,(int)(135*a),1),xx-(1+z*5),yy,xx,yy);
            else g.FillEllipse(ParticleBrush(spark,(int)(220*a)),xx-size,yy-size,size*2,size*2);
            if(z>.94f)DrawLaserBloom(g,xx,yy,1.9f,1.05f,c,a*.32f);
        }
    }
    private void DrawApotheosisEmitter(Graphics g,float x,float y,float angle,float r,float energy,bool blue)
    {
        Color c=blue?ClashBlue:ClashPink;var s=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle*180/MathF.PI);
        DrawJunction(g,5,0,Math.Max(17,r*.69f),r*2.06f,blue,energy*.87f);
        DrawLaserBloom(g,0,0,r*.56f,r*.72f,c,energy*.85f);
        DrawLaserBloom(g,2,0,Math.Max(6,r*.19f),Math.Max(5,r*.14f),Color.FromArgb(232,250,255),energy*.82f);
        g.Restore(s);
    }
    private void DrawApotheosisCollision(Graphics g,float x,float y,float strength)
    {
        strength=Math.Clamp(strength,.1f,2);float t=PlasmaClock;
        DrawCollisionDeflection(g,x,y,strength);
        if(!reducedEffects)DrawLaserBloom(g,x,y,56*strength,1.8f,Color.FromArgb(222,245,255),.40f);
        int count=reducedEffects?20:120;
        for(int i=0;i<count;i++)
        {
            float p=(t*(.023f+Scatter01(i*43)*.026f)+Scatter01(i*13))%1;
            float side=i%2==0?-1:1,angle=side*MathF.PI*.5f+(Scatter01(i*71)-.5f)*1.16f;
            float z=Scatter01(i*59),dist=(9+p*(85+z*65))*strength;
            float xx=x+MathF.Cos(angle)*dist*.78f,yy=y+MathF.Sin(angle)*dist;
            Color c=i%4==0?Color.FromArgb(238,251,255):i%2==0?ClashBlue:ClashPink;
            float a=(1-p)*strength*(.35f+z*.55f),size=.45f+z*1.0f;
            if(i%5==0)g.DrawLine(ParticlePen(c,(int)Math.Min(180,155*a),1),xx,yy,xx+MathF.Cos(angle)*(1+z*4),yy+MathF.Sin(angle)*(1+z*5));
            else g.FillEllipse(ParticleBrush(c,(int)Math.Min(230,210*a)),xx-size*.65f,yy-size*.65f,size*1.3f,size*1.3f);
        }
    }
    // Progress is the success shot's normalized 0..1 clock. Main timeline
    // owns the cut, zoom and monochrome impact frames.
    private void DrawApotheosisSurge(Graphics g,float x,float y,float progress,float power)
    {
        float t=Math.Clamp(progress,0,1),fade=(1-SmoothUnit((t-.56f)/.44f))*Math.Clamp(power,0,2);
        if(fade<=.002f)return;
        DrawLaserBloom(g,x,y,90+t*270,45+t*100,ClashBlue,Math.Min(.72f,fade*.64f));
        for(int k=0;k<(reducedEffects?2:5);k++)
        {
            float p=Math.Clamp(t*1.2f-k*.085f,0,1);if(p<=0)continue;
            float radius=18+MathF.Pow(p,.64f)*540,alpha=fade*(1-p)*(.8f-k*.095f);
            using var far=new Pen(Color.FromArgb((int)(55*alpha),ClashBlue),Math.Max(1,9*(1-p)));
            g.DrawArc(far,x-radius,y-radius*.31f,radius*2,radius*.62f,185,170);
            using var near=new Pen(Color.FromArgb((int)(160*alpha),Color.FromArgb(134,218,255)),Math.Max(1,4*(1-p)));
            g.DrawArc(near,x-radius,y-radius*.31f,radius*2,radius*.62f,5,170);
        }
        int count=reducedEffects?24:180;
        for(int i=0;i<count;i++)
        {
            float a=Scatter01(i*41+9)*MathF.Tau,z=Scatter01(i*73+1),r=(15+t*(180+z*440));
            float xx=x+MathF.Cos(a)*r,yy=y+MathF.Sin(a)*r*.65f,l=(6+z*30)*t;
            g.DrawLine(ParticlePen(i%5==0?Color.White:ClashBlue,(int)Math.Clamp(220*fade*(1-t*.6f),0,255),z>.8f?2:1),xx,yy,xx-MathF.Cos(a)*l,yy-MathF.Sin(a)*l*.65f);
        }
    }
}
