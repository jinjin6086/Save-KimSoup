using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // A narrow, ragged opening with depth, rather than a luminous outline oval.
    // Its drifting material is baked once; the animation only moves this
    // small texture and a bounded number of curves and embers.
    private const float PurgaePortalThreshold=831;
    private Bitmap? exitPortalMaterial,exitPortalActors;

    private void DisposeExitPortalMaterial()
    {
        exitPortalMaterial?.Dispose();exitPortalMaterial=null;
        exitPortalActors?.Dispose();exitPortalActors=null;
    }

    private Graphics BeginExitPortalActors()
    {
        exitPortalActors??=new Bitmap(430,420,PixelFormat.Format32bppPArgb);
        var g=Graphics.FromImage(exitPortalActors);g.Clear(Color.Transparent);
        g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.TranslateTransform(-560,-100);return g;
    }

    private unsafe void DrawExitPortalActors(Graphics g,float time)
    {
        if(exitPortalActors==null)return;
        if(time>113)
        {
            var data=exitPortalActors.LockBits(new Rectangle(0,0,430,420),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
            try
            {
                for(int y=0;y<420;y++)
                {
                    float edge=PurgaePortalThreshold-560+MathF.Sin((y+100)*.052f+time*.031f)*3+MathF.Sin(y*.139f-time*.038f)*1.5f;
                    byte* row=(byte*)data.Scan0+y*data.Stride;
                    for(int x=Math.Max(0,(int)edge-19);x<430;x++)
                    {
                        float p=Math.Clamp((x-edge+19)/32,0,1),mask=1-p*p*(3-2*p);
                        byte* pixel=row+x*4;
                        pixel[0]=(byte)(pixel[0]*mask);pixel[1]=(byte)(pixel[1]*mask);pixel[2]=(byte)(pixel[2]*mask);pixel[3]=(byte)(pixel[3]*mask);
                    }
                }
            }
            finally{exitPortalActors.UnlockBits(data);}
        }
        g.DrawImage(exitPortalActors,new Rectangle(560,100,430,420),0,0,430,420,GraphicsUnit.Pixel);
    }

    private unsafe void PrepareExitPortalMaterial()
    {
        if(exitPortalMaterial!=null)return;
        const int width=128,height=256;
        exitPortalMaterial=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        var data=exitPortalMaterial.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try
        {
            for(int y=0;y<height;y++)
            {
                byte* row=(byte*)data.Scan0+y*data.Stride;
                for(int x=0;x<width;x++)
                {
                    float u=x/(float)width,v=y/(float)height;
                    float ripple=MathF.Sin(u*19+MathF.Sin(v*MathF.Tau*2)*2.6f)
                        +.49f*MathF.Sin(u*37-v*MathF.Tau*3+MathF.Sin(u*11))
                        +.21f*MathF.Sin(u*71+v*MathF.Tau*7);
                    float vein=MathF.Pow(Math.Clamp((ripple+.58f)/2.25f,0,1),3.2f);
                    byte alpha=(byte)(21+vein*167);
                    byte* pixel=row+x*4;
                    pixel[3]=alpha;pixel[2]=(byte)((58+vein*92)*alpha/255);
                    pixel[1]=(byte)((6+vein*12)*alpha/255);pixel[0]=(byte)((31+vein*43)*alpha/255);
                }
            }
        }
        finally{exitPortalMaterial.UnlockBits(data);}
    }

    private static GraphicsPath ExitPortalShape(float x,float y,float width,float height,float time,float expansion=1)
    {
        const int steps=18;
        var points=new PointF[steps*2];
        for(int i=0;i<steps;i++)
        {
            float p=i/(float)(steps-1),waist=MathF.Pow(Math.Max(0,MathF.Sin(p*MathF.PI)),.65f);
            float bend=MathF.Sin(p*5.7f+time*.013f)*3+MathF.Sin(p*17.3f-time*.022f)*2;
            float left=.5f*width*waist*(.94f+.08f*MathF.Sin(p*21-time*.024f));
            float right=.5f*width*waist*(1+.085f*MathF.Sin(p*18+time*.019f));
            points[i]=new(x+(bend-left)*expansion,y+(p-.5f)*height*expansion);
            points[steps*2-1-i]=new(x+(bend+right)*expansion,y+(p-.5f)*height*expansion);
        }
        var path=new GraphicsPath();path.AddClosedCurve(points,.30f);return path;
    }

    private void DrawPurgaeExitPortal(Graphics g,float alpha,float time,bool foreground)
    {
        if(alpha<=.002f)return;
        alpha=Math.Clamp(alpha,0,1);
        PrepareExitPortalMaterial();
        const float cx=849,cy=315;
        float aperture=MathF.Sqrt(alpha),width=150*aperture,height=360*(.65f+.35f*aperture);
        if(foreground){DrawExitPortalThreshold(g,cx,cy,width,height,alpha,time);return;}

        // The floor receives the rift's light and contact shadow, anchoring it
        // to the arena even while its top and sides remain torn and weightless.
        using(var ground=new GraphicsPath())
        {
            ground.AddEllipse(cx-112,GroundY-24,224,42);
            using var shadow=new PathGradientBrush(ground){CenterColor=Color.FromArgb((int)(205*alpha),1,0,3),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(shadow,ground);
        }
        using(var spill=new GraphicsPath())
        {
            spill.AddEllipse(cx-124,GroundY-15,248,27);
            using var glow=new PathGradientBrush(spill){CenterColor=Color.FromArgb((int)(91*alpha),164,24,57),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(glow,spill);
        }
        using var hazePath=ExitPortalShape(cx,cy,width,height,time,1.33f);
        using(var haze=new PathGradientBrush(hazePath){CenterColor=Color.FromArgb((int)(101*alpha),95,11,41),SurroundColors=new[]{Color.Transparent}})g.FillPath(haze,hazePath);
        using var mouth=ExitPortalShape(cx,cy,width,height,time);
        using(var depth=new PathGradientBrush(mouth){CenterPoint=new(cx+14,cy-7),CenterColor=Color.FromArgb((int)(253*alpha),1,0,5),SurroundColors=new[]{Color.FromArgb((int)(235*alpha),38,3,21)}})g.FillPath(depth,mouth);

        var clipped=g.Save();g.SetClip(mouth,CombineMode.Intersect);
        if(exitPortalMaterial!=null)
        {
            using var material=new TextureBrush(exitPortalMaterial,WrapMode.Tile);
            material.TranslateTransform(cx-width*.54f,cy-height*.5f-time*.57f);
            material.ScaleTransform(width/128,height/256);
            g.FillRectangle(material,cx-width,cy-height,width*2,height*2);
        }
        for(int layer=0;layer<3;layer++)
        {
            float drift=MathF.Sin(time*.013f+layer*1.7f)*3;
            using var fold=ExitPortalShape(cx+width*(.08f+layer*.055f)+drift,cy-layer*3,width*(.74f-layer*.13f),height*(.88f-layer*.12f),time+layer*83);
            using var foldShade=new PathGradientBrush(fold){CenterColor=Color.FromArgb((int)((97+layer*21)*alpha),0,0,5),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(foldShade,fold);
            using var crease=new Pen(Color.FromArgb((int)((21-layer*4)*alpha),156,31,80),.7f);g.DrawPath(crease,fold);
        }
        // Receding filaments converge past the visible mouth. Their varied
        // speed, width and occlusion sell a passage, with no flat centre icon.
        int filaments=reducedEffects?6:15;
        for(int i=0;i<filaments;i++)
        {
            float p=(i*.137f+time*.0017f)%1,side=i%2==0?-1:1;
            float sy=cy-height*.43f+p*height*.86f;
            float sx=cx+side*width*.46f*MathF.Pow(MathF.Sin(p*MathF.PI),.55f);
            float inward=.26f+.11f*MathF.Sin(time*.014f+i);
            using var strand=new Pen(Color.FromArgb((int)((17+i%4*5)*alpha),136,25,70),.7f+i%3*.55f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(strand,sx,sy,sx-side*width*.25f,sy-18,cx+width*inward,cy+(sy-cy)*.37f,cx+width*.16f,cy+(sy-cy)*.20f);
        }
        // A deep black shelf remains darker than the drifting front surface.
        using(var core=new GraphicsPath())
        {
            core.AddEllipse(cx-width*.08f,cy-height*.35f,width*.45f,height*.68f);
            using var voidBrush=new PathGradientBrush(core){CenterColor=Color.FromArgb((int)(238*alpha),0,0,3),SurroundColors=new[]{Color.Transparent}};g.FillPath(voidBrush,core);
        }
        g.Restore(clipped);

        using(var bloom=new Pen(Color.FromArgb((int)(15*alpha),211,42,91),12))g.DrawPath(bloom,mouth);
        using(var outer=new Pen(Color.FromArgb((int)(39*alpha),181,35,77),5))g.DrawPath(outer,mouth);
        using(var lip=new Pen(Color.FromArgb((int)(86*alpha),100,15,53),1.7f))g.DrawPath(lip,mouth);
        DrawExitPortalEdge(g,cx,cy,width,height,alpha,time,false);
        DrawExitPortalEmbers(g,cx,cy,width,height,alpha,time,false);
    }

    private void DrawExitPortalEdge(Graphics g,float x,float y,float width,float height,float alpha,float time,bool front)
    {
        int count=reducedEffects?7:17;
        for(int i=0;i<count;i++)
        {
            float p=(i+.35f)/count,side=i%2==0?-1:1;
            if(front&&side>0)continue;
            float pulse=.65f+.35f*MathF.Sin(time*.075f+i*1.7f);
            float span=.025f+i%4*.007f;
            var a=ExitPortalEdgePoint(x,y,width,height,p,side,time);
            var b=ExitPortalEdgePoint(x,y,width,height,p+span*.33f,side,time);
            var c=ExitPortalEdgePoint(x,y,width,height,p+span*.66f,side,time);
            var d=ExitPortalEdgePoint(x,y,width,height,p+span,side,time);
            using var soft=new Pen(Color.FromArgb((int)(38*alpha*pulse),239,59,105),3.8f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            using var hot=new Pen(Color.FromArgb((int)((139+i%3*31)*alpha*pulse),i%4==0?Color.FromArgb(246,155,152):Color.FromArgb(212,53,91)),.6f+i%3*.23f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(soft,a,b,c,d);g.DrawBezier(hot,a,b,c,d);
        }
    }

    private static PointF ExitPortalEdgePoint(float x,float y,float width,float height,float progress,float side,float time)
    {
        float p=Math.Clamp(progress,0,1),waist=MathF.Pow(Math.Max(0,MathF.Sin(p*MathF.PI)),.65f);
        float bend=MathF.Sin(p*5.7f+time*.013f)*3+MathF.Sin(p*17.3f-time*.022f)*2;
        float warp=side<0?.94f+.08f*MathF.Sin(p*21-time*.024f):1+.085f*MathF.Sin(p*18+time*.019f);
        return new(x+bend+side*width*.5f*waist*warp,y+(p-.5f)*height);
    }

    private void DrawExitPortalThreshold(Graphics g,float cx,float cy,float width,float height,float alpha,float time)
    {
        // This soft vertical shelf hides the world-space clipping threshold;
        // its edge moves, but the crossing position stays stable and readable.
        float visible=alpha*FinisherEase(105,132,time);
        if(visible>0)
        {
            using var clipShape=ExitPortalShape(cx,cy,width,height,time);
            var saved=g.Save();g.SetClip(clipShape,CombineMode.Intersect);
            const float blendWidth=42;
            using var shelf=new LinearGradientBrush(new RectangleF(PurgaePortalThreshold-19,cy-height*.5f,blendWidth,height),Color.FromArgb(0,2,0,7),Color.FromArgb((int)(227*visible),2,0,7),0f);
            g.FillRectangle(shelf,PurgaePortalThreshold-19,cy-height*.5f,blendWidth,height);
            using var deep=new SolidBrush(Color.FromArgb((int)(227*visible),2,0,7));
            g.FillRectangle(deep,PurgaePortalThreshold-19+blendWidth-.5f,cy-height*.5f,width,height);
            for(int i=0;i<(reducedEffects?3:8);i++)
            {
                float sy=cy-height*.43f+i*height*.12f,sx=PurgaePortalThreshold+MathF.Sin(time*.044f+i*1.8f)*4;
                using var veil=new Pen(Color.FromArgb((int)(105*visible),7,1,13),13+i%3*3){StartCap=LineCap.Round,EndCap=LineCap.Round};
                g.DrawBezier(veil,sx-8,sy+31,sx+12,sy+9,sx-10,sy-11,sx+9,sy-34);
                using var glint=new Pen(Color.FromArgb((int)(44*visible),154,25,69),.9f);
                g.DrawBezier(glint,sx-10,sy+23,sx+4,sy+5,sx-6,sy-9,sx+3,sy-25);
            }
            g.Restore(saved);
        }
        DrawExitPortalEdge(g,cx,cy,width,height,alpha,time,true);
        DrawExitPortalEmbers(g,cx,cy,width,height,alpha,time,true);
    }

    private void DrawExitPortalEmbers(Graphics g,float x,float y,float width,float height,float alpha,float time,bool front)
    {
        int count=reducedEffects?9:29;
        for(int i=0;i<count;i++)
        {
            if((i%3==0)!=front)continue;
            float phase=(time*(.0042f+i%3*.0009f)+i*.173f)%1;
            float seed=(i*37%101)/101f,angle=i*2.399963f+phase*.63f;
            float outer=1-phase,span=.32f+outer*.53f;
            float sx=x+MathF.Cos(angle)*width*span,sy=y+(seed-.5f)*height*(.35f+outer*.90f)-phase*11;
            float opacity=MathF.Sin(phase*MathF.PI)*alpha;
            Color color=i%5==0?Color.FromArgb(245,166,152):Color.FromArgb(183,43,83);
            using var glow=new SolidBrush(Color.FromArgb((int)(37*opacity),color));g.FillEllipse(glow,sx-3,sy-3,6,6);
            using var mote=new Pen(Color.FromArgb((int)(196*opacity),color),.7f+i%3*.45f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            float toward=(x+12-sx)*.045f;
            g.DrawLine(mote,sx,sy,sx+toward,sy-1.5f-i%3);
        }
    }
}
