using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private float focusEntryTimer,focusExitTimer;
    private bool FocusReturning=>focusExitTimer>0&&state==GameState.Round&&!PhaseIntroActive&&!bossBriefingActive&&!TruePurgaeTimeFrozen&&!MelodyLastStandActive;
    private bool FocusWireframeActive=>FocusActive&&(cinematicTimer<=0||reducedEffects);
    private void BeginHackedFocus(){focusEntryTimer=18;focusExitTimer=0;}
    private void EndHackedFocus(){focusEntryTimer=0;focusExitTimer=18;audio.Play("focusenter");}
    private void DrawFocusWorld(Graphics g)
    {
        DrawArenaGround(g);
        using(var dusk=new SolidBrush(Color.FromArgb(222,3,8,21)))g.FillRectangle(dusk,0,103,W,421);
        // The world retains a quiet floor reference while entities become their
        // actual collision bounds. The runner's poses and input remain unchanged.
        using(var floor=new Pen(Color.FromArgb(145,53,156,222),1))g.DrawLine(floor,0,GroundY,W,GroundY);
        float offset=worldTravelDistance%80;
        for(int i=0;i<14;i++)
        {
            float x=i*80-offset;
            g.DrawLine(ParticlePen(Color.FromArgb(83,194,242),55,1),x,GroundY+4,x-9,GroundY+16);
        }
        DrawFocusHazards(g);
        foreach(var o in obstacles)DrawFocusBound(g,o.Rect,false,o.Kind==ObstacleKind.Floating?"↓":"↑");
        foreach(var o in risingObstacles)DrawFocusBound(g,o.Rect,false,"↑↑");
        foreach(var p in projectiles)
        {
            DrawFocusBound(g,ProjectileDamageBounds(p),p.IsParried,"");
            float vx=p.VelocityX,vy=p.VelocityY,len=MathF.Max(1,MathF.Sqrt(vx*vx+vy*vy));
            float x=p.X+p.W*.5f,y=p.Y+p.H*.5f;
            g.DrawLine(ParticlePen(p.IsParried?Color.White:Color.FromArgb(97,214,255),155,1),x,y,x+vx/len*18,y+vy/len*18);
        }
        if(round>=2&&!dashActive)
        {
            DrawFocusBound(g,BossHitBox,false,RoundName(round));
            float x=bossX+65,y=bossY+65;
            g.DrawLine(ParticlePen(Color.FromArgb(90,204,255),100,1),x-7,y,x+7,y);
            g.DrawLine(ParticlePen(Color.FromArgb(90,204,255),100,1),x,y-7,x,y+7);
        }
        DrawPlayer(g);DrawFocusGeometry(g);
        DrawRebuild(g);DrawExpandedAbilityEffects(g);
        if(cinematicTimer<=0||reducedEffects)DrawCounterFeedback(g);
    }
    private void DrawFocusBound(Graphics g,RectangleF rect,bool reflected,string label)
    {
        Color c=reflected?Color.FromArgb(224,255,249):Color.FromArgb(79,185,255);
        using var fill=new SolidBrush(Color.FromArgb(reflected?38:15,c));g.FillRectangle(fill,rect);
        using var outline=new Pen(Color.FromArgb(reflected?245:185,c),reflected?1.6f:1);
        g.DrawRectangle(outline,rect.X,rect.Y,rect.Width,rect.Height);
        if(!reducedEffects)
        {
            using var corner=new Pen(Color.FromArgb(225,c),2);
            float s=Math.Min(7,Math.Min(rect.Width,rect.Height)*.3f);
            g.DrawLine(corner,rect.Left,rect.Top+s,rect.Left,rect.Top);g.DrawLine(corner,rect.Left,rect.Top,rect.Left+s,rect.Top);
            g.DrawLine(corner,rect.Right-s,rect.Bottom,rect.Right,rect.Bottom);g.DrawLine(corner,rect.Right,rect.Bottom,rect.Right,rect.Bottom-s);
        }
        if(label.Length>0)Label(g,label,new(rect.X-12,rect.Y-24,rect.Width+24,20),10,c);
    }
    private void DrawHackedFocusOverlay(Graphics g)
    {
        if(!FocusActive)return;
        for(int i=0;i<(reducedEffects?2:5);i++)
        {
            int y=120+((int)(animationClock*.8f)+i*79)%390;
            g.FillRectangle(ParticleBrush(Color.FromArgb(61,188,255),reducedEffects?8:15),0,y,W,1);
        }
    }
    private void DrawFocusStatus(Graphics g)
    {
        if(FocusActive)
            Label(g,$"느려.  {activeTimer/60:0.0}초   ·   적 속도 48%   ·   실시간 판정",new(24,501,560,20),11,Color.FromArgb(154,226,255));
    }
    private void DrawFocusTears(Graphics g)
    {
        if((!FocusActive&&!FocusReturning)||cinemaFrame==null)return;
        bool returning=FocusReturning,entry=focusEntryTimer>0||returning;
        if(!entry&&((int)animationClock%67)>2)return;
        float power=returning?MathF.Sin((1-focusExitTimer/18)*MathF.PI)*.88f:entry?focusEntryTimer/18:.12f;
        if(reducedEffects)power*=.24f;
        int tick=(int)(animationClock*2),bands=reducedEffects?2:entry?9:2;
        float sourceScale=cinemaFrame.Height/(float)H;
        var saved=g.Save();g.SetClip(new RectangleF(0,104,W,420));
        for(int i=0;i<bands;i++)
        {
            float y=115+(tick*47+i*83)%370,h=entry?4+(i*19+tick)%31:2;
            float displacement=MathF.Sin(tick*1.7f+i*2.1f)*power*(entry?43:8);
            g.DrawImage(cinemaFrame,new RectangleF(displacement,y,W,h),new RectangleF(0,y*sourceScale,cinemaFrame.Width,h*sourceScale),GraphicsUnit.Pixel);
            if(entry)g.FillRectangle(ParticleBrush(Color.FromArgb(116,215,255),(int)(58*power)),0,y,W,i%3==0?2:1);
        }
        if(entry)g.FillRectangle(ParticleBrush(Color.FromArgb(46,145,255),(int)(31*power)),0,104,W,420);
        g.Restore(saved);
    }
    private static readonly byte[] FocusRed=Enumerable.Range(0,256).Select(v=>(byte)Math.Clamp(v*.56+3,0,255)).ToArray();
    private static readonly byte[] FocusGreen=Enumerable.Range(0,256).Select(v=>(byte)Math.Clamp(v*.91+13,0,255)).ToArray();
    private static readonly byte[] FocusBlue=Enumerable.Range(0,256).Select(v=>(byte)Math.Clamp(v*1.08+25,0,255)).ToArray();
    private static unsafe void ApplyHackedFocusGrade(Bitmap bitmap,float strength=1)
    {
        strength=Math.Clamp(strength,0,1);
        var data=bitmap.LockBits(new(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppRgb);
        try{fixed(byte* r=FocusRed,g=FocusGreen,b=FocusBlue)for(int y=0;y<data.Height;y++)
        {
            byte* row=(byte*)data.Scan0+y*data.Stride;
            if(strength>=.999f)
                for(int x=0;x<data.Width;x++){byte* pixel=row+x*4;pixel[0]=b[pixel[0]];pixel[1]=g[pixel[1]];pixel[2]=r[pixel[2]];}
            else
                for(int x=0;x<data.Width;x++){byte* pixel=row+x*4;pixel[0]=(byte)(pixel[0]+(b[pixel[0]]-pixel[0])*strength);pixel[1]=(byte)(pixel[1]+(g[pixel[1]]-pixel[1])*strength);pixel[2]=(byte)(pixel[2]+(r[pixel[2]]-pixel[2])*strength);}
        }}finally{bitmap.UnlockBits(data);}
    }
}

internal sealed partial class GameAudio
{
    private static byte[] MakeFocusEntry()
    {
        const double duration=.32;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var rng=new Random(817);double low=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=rng.NextDouble()*2-1;low+=.21*(n-low);
            double gate=(int)(t*59)%3==0?.35:1,env=Math.Sin(Math.PI*p)*gate;
            double s=(low*.42+Math.Sin(Math.Tau*(330*t+200*t*t))*.2+Math.Sin(t*Math.Tau*74)*.22)*env;
            short v=(short)(Math.Tanh(s*1.8)*.22*short.MaxValue);pcm[i*2]=(byte)v;pcm[i*2+1]=(byte)(v>>8);
        }
        return RoundPlayerClip(pcm,2400);
    }
}
