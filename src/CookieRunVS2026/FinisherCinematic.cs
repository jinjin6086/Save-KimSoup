using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? finisherEscortPortrait,finisherShadowPortrait;

    private void DisposeFinisherPortraits()
    {
        finisherEscortPortrait?.Dispose();finisherEscortPortrait=null;
        finisherShadowPortrait?.Dispose();finisherShadowPortrait=null;
        DisposeExitPortalMaterial();
    }

    private void PrepareFinisherPortraits()
    {
        DisposeFinisherPortraits();
        if(round==4&&defeatPortrait!=null)
        {
            finisherShadowPortrait=new Bitmap(288,288,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(finisherShadowPortrait))g.DrawImageUnscaled(defeatPortrait,0,0);
            ShadeFinisherFace(finisherShadowPortrait);
        }
    }

    // This is baked once when the finishing blow lands. Alpha is preserved, so
    // the facial shadow follows the portrait rather than drawing an oval on it.
    private static unsafe void ShadeFinisherFace(Bitmap image)
    {
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try
        {
            for(int y=0;y<data.Height;y++)
            {
                byte* row=(byte*)data.Scan0+y*data.Stride;
                for(int x=0;x<data.Width;x++)
                {
                    float nx=(x-169)/46f,ny=(y-94)/30f;
                    float shade=Math.Clamp((1-MathF.Sqrt(nx*nx+ny*ny))*2.2f,0,1)*.67f;
                    if(shade<=0)continue;
                    byte* pixel=row+x*4;
                    float luma=pixel[2]*.30f+pixel[1]*.59f+pixel[0]*.11f;
                    for(int c=0;c<3;c++)pixel[c]=(byte)Math.Clamp((pixel[c]*(1-shade*.75f)+luma*shade*.75f)*(1-shade),0,255);
                }
            }
        }
        finally{image.UnlockBits(data);}
    }

    private static float FinisherEase(float from,float to,float time)
    {
        float p=Math.Clamp((time-from)/(to-from),0,1);return p*p*(3-2*p);
    }

    private void DrawDistinctBossFinisher(Graphics g)
    {
        if(round==4){DrawRodianExplosionFinisher(g);return;}
        float t=bossDefeatClock,zoom=FinisherEase(18,53,t);
        DrawArenaGround(g);DrawPlayer(g);
        using(var shade=new SolidBrush(Color.FromArgb((int)(102+111*zoom),3,4,7)))g.FillRectangle(shade,0,0,W,H);
        float size=155+66*zoom,x=defeatX+(641-defeatX)*zoom,y=defeatY+(191-defeatY)*zoom;
        if(round is 2 or 3)DrawIndependentBossExplosion(g,x,y,size,t);
        DrawFinisherImpact(g,x+size*.52f,y+size*.50f,t);
        float clear=FinisherEase(198,219,t);
        if(clear>0)
        {
            using var black=new SolidBrush(Color.FromArgb((int)(160*clear),0,0,0));g.FillRectangle(black,0,420,W,97);
            SoulsHeading(g,$"라운드 {round} 클리어",new(180,422,640,47),31,Color.FromArgb((int)(255*clear),Gold));
            string caption=round==2?"이상진 처치":"파괴옥 처치";
            Label(g,caption,new(150,473,700,26),14,Color.FromArgb((int)(237*clear),Paper));
        }
        float bar=43+12*FinisherEase(0,16,t);
        using var bars=new SolidBrush(Color.FromArgb(255,2,3,5));g.FillRectangle(bars,0,0,W,bar);g.FillRectangle(bars,0,H-bar,W,bar);
        if(t>=BossDefeatSkipFrame)Label(g,"ENTER / SPACE  다음",new(738,562,240,27),11,Steel,true,false);
    }

    private void DrawFinisherImpact(Graphics g,float x,float y,float t)
    {
        if(t>=28)return;
        float envelope=1-Math.Clamp(t/28,0,1);
        // A single white contact frame, a black silhouette beat, then crisp
        // ink streaks. These stay short and centred on the final kick contact.
        if(t<3)
        {
            using var white=new SolidBrush(Color.FromArgb((int)(230*(1-t/3)),255,255,255));g.FillRectangle(white,0,0,W,H);
        }
        if(t is >=3 and <8)
        {
            using var ink=new SolidBrush(Color.FromArgb((int)(120*(1-(t-3)/5)),0,0,0));g.FillRectangle(ink,0,0,W,H);
        }
        using var cut=new Pen(Color.FromArgb((int)(245*envelope),255,255,255),Math.Max(.8f,3.8f*envelope)){StartCap=LineCap.Round,EndCap=LineCap.Round};
        float extend=110+Math.Min(t,12)*23;
        g.DrawLine(cut,x-extend,y+extend*.28f,x+extend*.58f,y-extend*.162f);
        using var edge=new Pen(Color.FromArgb((int)(110*envelope),255,255,255),Math.Max(.5f,envelope));
        g.DrawLine(edge,x-extend*.73f,y-extend*.19f,x+extend*.42f,y+extend*.11f);
        int rays=reducedEffects?8:22;
        for(int i=0;i<rays;i++)
        {
            float a=i*2.399963f+.25f,r=32+Math.Min(t,17)*(2.2f+i%5*.7f),length=(16+i%4*8)*envelope;
            float cos=MathF.Cos(a),sin=MathF.Sin(a)*.66f;
            using var ray=new Pen(Color.FromArgb((int)(190*envelope),245,246,250),i%5==0?1.8f:.7f);
            g.DrawLine(ray,x+cos*r,y+sin*r,x+cos*(r+length),y+sin*(r+length));
        }
    }

    private void DrawDarkPassage(Graphics g,float x,float y,float width,float height,float alpha,float time)
    {
        if(alpha<=0||width<=0)return;
        using var outer=new GraphicsPath();outer.AddEllipse(x-width*.84f,y-height*.63f,width*1.68f,height*1.26f);
        using(var haze=new PathGradientBrush(outer){CenterColor=Color.FromArgb((int)(105*alpha),98,110,130),SurroundColors=new[]{Color.Transparent}})g.FillPath(haze,outer);
        for(int layer=0;layer<3;layer++)
        {
            float sway=MathF.Sin(time*.016f+layer*1.7f)*width*.055f;
            using var path=new GraphicsPath();path.AddEllipse(x-width*(.72f-layer*.10f)+sway,y-height*(.56f-layer*.04f),width*(1.44f-layer*.20f),height*(1.12f-layer*.08f));
            using var ink=new PathGradientBrush(path){CenterColor=Color.FromArgb((int)((206+layer*16)*alpha),0,1,4),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(ink,path);
        }
        int count=reducedEffects?7:18;
        for(int i=0;i<count;i++)
        {
            float phase=(time*.004f+i*.137f)%1,side=i%2==0?-1:1;
            float sy=y-height*.5f+phase*height,sx=x+side*width*(.23f+.16f*MathF.Sin(i*1.83f+time*.024f));
            int opacity=(int)(alpha*(22+28*MathF.Sin(phase*MathF.PI)));
            using var wisp=new Pen(Color.FromArgb(opacity,143,155,175),1.1f+i%3*.6f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(wisp,sx,sy+22,sx+side*15,sy+7,sx-side*7,sy-9,sx+side*3,sy-31);
        }
    }

    private void DrawIsangjinDeparture(Graphics g,float x,float y,float size,float t)
    {
        float retreat=FinisherEase(53,181,t),portal=FinisherEase(36,73,t)*(1-FinisherEase(168,200,t));
        DrawDarkPassage(g,796,289,104+16*MathF.Sin(t*.035f),281,portal,t);
        float alpha=1-FinisherEase(113,179,t),s=size*(1-retreat*.43f);
        if(defeatPortrait!=null)
        {
            var target=new RectangleF(x+retreat*139+size*.5f-s*.5f,y+retreat*32,s,s);
            DrawFinisherDriftImage(g,defeatPortrait,target,alpha,retreat*.48f,t);
        }
        if(t>86&&t<192)
        {
            float wave=FinisherEase(86,139,t)*(1-FinisherEase(158,192,t));
            using var shroud=new LinearGradientBrush(new RectangleF(697,126,203,310),Color.FromArgb(0,0,1,4),Color.FromArgb((int)(195*wave),0,1,4),0f);
            g.FillEllipse(shroud,720,126,158,310);
        }
    }

    private void DrawPurgaeEscort(Graphics g,float x,float y,float size,float t)
    {
        float arrival=FinisherEase(47,93,t),carry=FinisherEase(100,191,t),vanish=1-FinisherEase(164,205,t);
        float portal=FinisherEase(51,83,t)*(1-FinisherEase(190,216,t));
        DrawPurgaeExitPortal(g,portal,t,false);
        // Isangjin enters behind Purgae and physically gathers the defeated
        // portrait at his lower arm/tablet before both retreat as one group.
        float depth=FinisherEase(145,191,t);
        float groupX=x-28*arrival+carry*194+depth*31,groupY=y+carry*22,scale=1-carry*.48f;
        float sway=carry is >0 and <1?MathF.Sin((t-100)*.22f)*2:0;
        float escortSize=size*.90f*scale;
        var escort=new RectangleF(groupX+size*.48f*scale+(1-arrival)*49,groupY-11+sway,escortSize,escortSize*1.10f);
        float collapsed=FinisherEase(66,104,t),bodySize=size*(1-collapsed*.29f)*scale;
        var fallen=new RectangleF(groupX+collapsed*7,groupY+collapsed*76-sway*.5f,bodySize,bodySize);
        // Both portraits and the carrying hand share one tiny cached surface,
        // so the moving depth threshold can feather their silhouettes together.
        using(var actors=BeginExitPortalActors())
        {
        if(finisherEscortPortrait!=null)DrawDefeatImage(actors,finisherEscortPortrait,escort,arrival*vanish);
        if(defeatPortrait!=null)
        {
            var saved=actors.Save();actors.TranslateTransform(fallen.X+bodySize*.58f,fallen.Y+bodySize*.55f);actors.RotateTransform(-22*collapsed);
            DrawDefeatImage(actors,defeatPortrait,new(-bodySize*.58f,-bodySize*.55f,bodySize,bodySize),vanish);actors.Restore(saved);
        }
        if(arrival>.1f&&vanish>0)
        {
            float grip=arrival*vanish;
            using var sleeve=new Pen(Color.FromArgb((int)(215*grip),55,58,65),10*scale){StartCap=LineCap.Round,EndCap=LineCap.Round};
            using var skin=new Pen(Color.FromArgb((int)(220*grip),147,142,137),5*scale){StartCap=LineCap.Round,EndCap=LineCap.Round};
            var from=new PointF(escort.X+escort.Width*.25f,escort.Y+escort.Height*.77f);
            var hand=new PointF(fallen.X+bodySize*.69f,fallen.Y+bodySize*.46f);
            actors.DrawLine(sleeve,from,hand);actors.DrawLine(skin,hand.X-5*scale,hand.Y+2*scale,hand.X+3*scale,hand.Y-3*scale);
        }
        }
        DrawExitPortalActors(g,t);
        int motes=reducedEffects?7:19;
        for(int i=0;i<motes;i++)
        {
            float phase=(t*.008f+i*.119f)%1,k=MathF.Sin(phase*MathF.PI)*carry*vanish;
            float dx=groupX-20+i%5*22+phase*72,dy=groupY+size+15-MathF.Sin(phase*MathF.PI)*24;
            using var dust=new Pen(Color.FromArgb((int)(95*k),130,131,139),.9f+i%2);
            g.DrawLine(dust,dx,dy,dx+11,dy-2);
        }
        DrawPurgaeExitPortal(g,portal,t,true);
    }

    private void DrawRodianDissolution(Graphics g,float x,float y,float size,float t)
    {
        float darken=FinisherEase(33,89,t),dissolve=FinisherEase(99,195,t);
        float sag=FinisherEase(45,116,t)*9;
        var target=new RectangleF(x,y+sag,size,size);
        if(defeatPortrait!=null&&dissolve<1)
        {
            DrawFinisherDriftImage(g,defeatPortrait,target,(1-darken)*(1-dissolve),dissolve,t);
            if(finisherShadowPortrait!=null)DrawFinisherDriftImage(g,finisherShadowPortrait,target,darken*(1-dissolve),dissolve,t);
        }
        int count=reducedEffects?15:52;
        for(int i=0;i<count;i++)
        {
            float seed=(i*37%101)/101f,phase=Math.Clamp((t-91-i%7*4)/102,0,1);
            float envelope=MathF.Sin(phase*MathF.PI)*Math.Clamp(dissolve*2.5f,0,1);
            if(envelope<=0)continue;
            float sx=x+size*(.13f+seed*.77f)+MathF.Sin(i*1.13f+phase*7)*phase*22;
            float sy=y+size*(.83f-(i*17%79)/102f)-phase*(48+i%5*10);
            Color tint=i%7==0?Color.FromArgb(225,211,229):Color.FromArgb(153,146,165);
            using var mote=new Pen(Color.FromArgb((int)(170*envelope),tint),i%4==0?2:1){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(mote,sx,sy,sx+MathF.Sin(i)*2,sy-3-phase*6);
        }
    }

    // Thin contiguous rows dissolve into smoke. The original portrait remains
    // aligned; there are no tumbling square fragments or exploding body parts.
    private void DrawFinisherDriftImage(Graphics g,Bitmap portrait,RectangleF target,float alpha,float dissolve,float time)
    {
        if(alpha<=0)return;
        if(dissolve<=.001f){DrawDefeatImage(g,portrait,target,alpha);return;}
        const int rows=36;
        for(int i=0;i<rows;i++)
        {
            float progress=(i+.5f)/rows,edge=Math.Clamp(1-dissolve*1.25f+progress*.25f,0,1);
            float opacity=alpha*edge;
            if(opacity<=.005f)continue;
            float dy=target.Y+i*target.Height/rows;
            float drift=MathF.Sin(i*.63f+time*.09f)*dissolve*5;
            readableMatrix.Matrix33=opacity;readableAlpha.SetColorMatrix(readableMatrix);
            var saved=g.Save();g.SetClip(new RectangleF(target.X-7,dy,target.Width+14,target.Height/rows+.35f),CombineMode.Intersect);
            g.DrawImage(portrait,Rectangle.Round(new RectangleF(target.X+drift,target.Y,target.Width,target.Height)),0,0,portrait.Width,portrait.Height,GraphicsUnit.Pixel,readableAlpha);
            g.Restore(saved);
        }
    }
}
