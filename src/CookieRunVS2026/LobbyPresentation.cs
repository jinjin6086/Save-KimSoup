using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private static readonly Rectangle LobbyStartBounds=new(78,373,294,58);
    private static readonly Rectangle LobbyPracticeBounds=new(78,443,294,44);
    private Bitmap? lobbyBackdrop;
    private Bitmap[] lobbyResolveFrames=Array.Empty<Bitmap>();
    private Bitmap[] lobbyIdleFrames=Array.Empty<Bitmap>();
    private bool lobbyArtLoaded;
    private float lobbyClock,lobbyPointerX,lobbyPointerY,lobbyHover;
    private int lobbyHovered=-1;
    private GameState lobbyObservedState=GameState.Title;

    private void EnsureLobbyArt()
    {
        if(lobbyArtLoaded)return;lobbyArtLoaded=true;
        lobbyBackdrop=LoadEndingBackdrop("World1Meadow.png");
        if(lobbyBackdrop!=null)
        {
            using var cg=Graphics.FromImage(lobbyBackdrop);
            using var wash=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.Black,Color.Transparent,0f);
            wash.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(250,8,13,21),Color.FromArgb(211,9,15,24),Color.FromArgb(37,10,17,25)},Positions=new[]{0f,.47f,1f}};
            cg.FillRectangle(wash,0,0,W,H);
            using var lower=new LinearGradientBrush(new Rectangle(0,350,W,250),Color.Transparent,Color.FromArgb(245,5,9,14),90f);
            cg.FillRectangle(lower,0,350,W,250);
            using var upper=new LinearGradientBrush(new Rectangle(0,0,W,125),Color.FromArgb(140,5,9,15),Color.Transparent,90f);
            cg.FillRectangle(upper,0,0,W,125);
        }
        string path=AssetPath("LobbyResolveMotion3.png");
        if(!File.Exists(path))return;
        using var atlas=new Bitmap(path);int cell=atlas.Width/3;
        using var hdOpen=File.Exists(AssetPath("LobbyResolveOpenHD.png"))?new Bitmap(AssetPath("LobbyResolveOpenHD.png")):null;
        using var hdClosed=File.Exists(AssetPath("LobbyResolveClosedHD.png"))?new Bitmap(AssetPath("LobbyResolveClosedHD.png")):null;
        const int poseWidth=810,poseHeight=1080;
        var keys=new Bitmap[3];
        try
        {
            for(int i=0;i<3;i++)
            {
                keys[i]=new Bitmap(poseWidth,poseHeight,PixelFormat.Format32bppPArgb);
                using var cg=Graphics.FromImage(keys[i]);
                cg.InterpolationMode=InterpolationMode.HighQualityBicubic;
                // The middle cell is the partly closed hand. Using the closed
                // HD pose for it skipped that keyframe and made the squeeze snap.
                Image? hd=i==0?hdOpen:i==2?hdClosed:null;
                int sourceWidth=hd?.Width??cell,sourceHeight=hd?.Height??atlas.Height;
                float fit=Math.Min(poseWidth/(float)sourceWidth,poseHeight/(float)sourceHeight);
                var bounds=new RectangleF((poseWidth-sourceWidth*fit)/2,poseHeight-sourceHeight*fit,sourceWidth*fit,sourceHeight*fit);
                if(hd!=null)cg.DrawImage(hd,bounds);
                else cg.DrawImage(atlas,bounds,new Rectangle(i*cell,0,cell,atlas.Height),GraphicsUnit.Pixel);
            }
            // Bake the open-hand -> resolve motion once, rather than running
            // pixel blending during the game loop. Original key art is retained.
            lobbyResolveFrames=new Bitmap[25];
            for(int f=0;f<25;f++)
            {
                float p=f/24f*2;int a=Math.Min(1,(int)p);
                var frame=new Bitmap(poseWidth,poseHeight,PixelFormat.Format32bppPArgb);
                BlendRegisteredPoses(keys[a],keys[a+1],frame,p-a);
                lobbyResolveFrames[f]=frame;
            }
            lobbyIdleFrames=new Bitmap[32];
            for(int i=0;i<lobbyIdleFrames.Length;i++)lobbyIdleFrames[i]=BakeLobbyIdle(keys[2],i*MathF.Tau/lobbyIdleFrames.Length);
        }
        finally{foreach(var key in keys)key?.Dispose();}
    }
    private void DisposeLobbyArt()
    {
        lobbyBackdrop?.Dispose();foreach(var frame in lobbyResolveFrames)frame.Dispose();foreach(var frame in lobbyIdleFrames)frame.Dispose();
        lobbyResolveFrames=lobbyIdleFrames=Array.Empty<Bitmap>();
    }
    private static unsafe Bitmap BakeLobbyIdle(Bitmap image,float phase)
    {
        int w=image.Width,h=image.Height;var result=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
        var input=image.LockBits(new(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
        var output=result.LockBits(new(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try
        {
            for(int y=0;y<h;y++)
            {
                float v=y/(float)h,chest=MathF.Sin(v*MathF.PI),head=1-EndingSmooth(Math.Clamp((v-.14f)/.3f,0,1));
                float breath=MathF.Sin(phase)*1.25f*chest;
                float sway=MathF.Sin(phase*.999f-v*1.8f)*.75f*head;
                for(int x=0;x<w;x++)
                {
                    float sx=x-sway-(x/(float)w-.5f)*breath*.6f,sy=y+breath;
                    int ix=(int)MathF.Floor(sx),iy=(int)MathF.Floor(sy);
                    if(ix<0||iy<0||ix>=w-1||iy>=h-1)continue;
                    float fx=sx-ix,fy=sy-iy;
                    byte* src=(byte*)input.Scan0+iy*input.Stride+ix*4,dst=(byte*)output.Scan0+y*output.Stride+x*4;
                    for(int c=0;c<4;c++)dst[c]=(byte)Mix(Mix(src[c],src[c+4],fx),Mix(src[c+input.Stride],src[c+input.Stride+4],fx),fy);
                }
            }
        }
        finally{image.UnlockBits(input);result.UnlockBits(output);}
        return result;
    }
    private void UpdateLobbyPresentation(float step)
    {
        if(state!=lobbyObservedState)
        {
            lobbyObservedState=state;lobbyClock=0;lobbyHovered=-1;
        }
        if(state is not(GameState.Title or GameState.CookieSelect))return;
        lobbyClock+=step;
        lobbyHover+=(lobbyHovered>=0?1-lobbyHover:-lobbyHover)*(1-MathF.Pow(.82f,step));
    }
    private void UpdateLobbyPointer(MouseEventArgs e)
    {
        if(state is not(GameState.Title or GameState.CookieSelect))return;
        float s=Math.Max(.001f,Math.Min(Width/(float)W,Height/(float)H));
        float x=(e.X-(Width-W*s)/2)/s,y=(e.Y-(Height-H*s)/2)/s;
        lobbyPointerX=Math.Clamp((x-500)/500,-1,1);lobbyPointerY=Math.Clamp((y-300)/300,-1,1);
        lobbyHovered=state==GameState.Title?(LobbyStartBounds.Contains((int)x,(int)y)?0:LobbyPracticeBounds.Contains((int)x,(int)y)?1:-1):-1;
        Cursor=lobbyHovered>=0?Cursors.Hand:Cursors.Default;
    }
    private void OpenLobbySelection()
    {
        practiceActive=false;state=GameState.CookieSelect;lobbyClock=0;
        lobbyObservedState=state;lobbyHovered=-1;Cursor=Cursors.Default;audio.Play("select");
    }
    private void DrawLobbyBackdrop(Graphics g,bool select=false)
    {
        EnsureLobbyArt();
        g.Clear(Color.FromArgb(9,14,20));
        if(lobbyBackdrop!=null)
        {
            float drift=MathF.Sin(menuClock*.0018f)*5;
            g.DrawImage(lobbyBackdrop,new RectangleF(-28+drift-lobbyPointerX*3,-13-lobbyPointerY*2,1056,634));
        }
        if(select){using var dim=new SolidBrush(Color.FromArgb(115,7,12,19));g.FillRectangle(dim,0,0,W,H);}
        for(int i=0;i<(reducedEffects?6:22);i++)
        {
            float x=(i*127.73f+menuClock*.055f)%1040-20;
            float y=100+(i*71)%425-MathF.Sin(menuClock*.006f+i)*11;
            float a=(.5f+.5f*MathF.Sin(menuClock*.012f+i*2))*(i%4==0?65:30);
            g.FillEllipse(ParticleBrush(Gold,(int)a),x,y,i%4==0?1.8f:1,1.5f);
        }
    }
    private void DrawLobbyResolveActor(Graphics g)
    {
        if(lobbyResolveFrames.Length==0)
        {
            EnsureStoryResources();if(storyHeroPortrait!=null)DrawDefeatImage(g,storyHeroPortrait,new(510,56,470,655),1);return;
        }
        // A short held anticipation, a firm squeeze, then a settled idle. The
        // hand never opens and closes mechanically on every idle cycle.
        float resolve=EndingSmooth(Math.Clamp((lobbyClock-28)/66,0,1));
        int frame=Math.Clamp((int)(resolve*24),0,24);
        Bitmap image=lobbyClock>=94&&!reducedEffects&&lobbyIdleFrames.Length>0
            ?lobbyIdleFrames[(int)((lobbyClock-94)*.028f/MathF.Tau*lobbyIdleFrames.Length)%lobbyIdleFrames.Length]:lobbyResolveFrames[frame];
        const float x=475,y=48,w=524,h=699;
        var old=g.Save();g.SetClip(new RectangleF(445,35,555,565),CombineMode.Intersect);
        g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        // Continuous inverse deformation is baked with bilinear sampling. A
        // single draw has no strip joins and keeps the idle animation cheap.
        g.DrawImage(image,new RectangleF(x+lobbyPointerX*2,y+lobbyPointerY,w,h));
        g.Restore(old);
        using var hem=new LinearGradientBrush(new Rectangle(460,471,540,129),Color.Transparent,Color.FromArgb(246,5,9,14),90);
        g.FillRectangle(hem,460,471,540,129);
    }
    private void DrawLobbyButton(Graphics g,Rectangle r,string key,string text,bool primary,int index)
    {
        bool hover=lobbyHovered==index;
        using var fill=new LinearGradientBrush(r,primary?Color.FromArgb(237,220,182):Color.FromArgb(125,21,29,39),primary?Color.FromArgb(190,161,109):Color.FromArgb(145,12,19,29),0f);
        g.FillRectangle(fill,r);
        using var rim=new Pen(Color.FromArgb(primary||hover?190:65,Gold),hover?1.5f:1);
        g.DrawRectangle(rim,r.X,r.Y,r.Width,r.Height);
        Color ink=primary?Color.FromArgb(28,29,31):Paper;
        Label(g,key,new(r.X+17,r.Y,r.Width*.23f,r.Height),11,primary?Color.FromArgb(90,75,53):Gold,false,false);
        Label(g,text,new(r.X+86,r.Y,r.Width-110,r.Height),primary?19:15,ink,false,true);
        using var arrow=new Pen(primary?Color.FromArgb(80,65,43):Gold,1.2f);
        float ax=r.Right-26+(hover?2*lobbyHover:0),ay=r.Y+r.Height/2f;
        g.DrawLine(arrow,ax-5,ay,ax+5,ay);g.DrawLines(arrow,new[]{new PointF(ax+1,ay-4),new PointF(ax+5,ay),new PointF(ax+1,ay+4)});
    }
    private void DrawResolveLobby(Graphics g)
    {
        DrawLobbyBackdrop(g);DrawLobbyResolveActor(g);
        Label(g,"S A V E   K I M S O U P",new(80,65,400,22),10,Gold,false,false);
        using(var stroke=new Pen(Color.FromArgb(165,Gold),1))g.DrawLine(stroke,80,103,117,103);
        // Fixed text bounds keep the Korean title clear at 900p and ultrawide.
        Label(g,"김스프를",new(73,122,425,84),58,Color.FromArgb(242,234,217),false);
        Label(g,"구해라",new(71,201,429,101),78,Color.FromArgb(242,234,217),false);
        Label(g,"보스들을 물리치고 김스프를 구하세요.",new(80,318,405,31),14,Color.FromArgb(182,189,194),false,false);
        DrawLobbyButton(g,LobbyStartBounds,"ENTER","게임 시작",true,0);
        DrawLobbyButton(g,LobbyPracticeBounds,"T","연습 모드",false,1);
        using(var rule=new Pen(Color.FromArgb(48,Gold),.8f))g.DrawLine(rule,80,519,920,519);
        Label(g,"↑ 점프     ↓ 슬라이드     SPACE / SHIFT 발차기     Q 스킬",new(80,533,650,25),10.5f,Steel,false,false);
        Label(g,$"M 소리 {(audio.Muted?"OFF":"ON")}   ·   F 효과 {(reducedEffects?"약":"기본")}",new(720,533,200,25),10,Steel,true,false);
        Label(g,"4개 라운드  ·  능력 직접 선택  ·  선택형 종결 보스",new(80,566,700,19),9,Color.FromArgb(109,121,130),false,false);
        float reveal=1-EndingSmooth(Math.Clamp(lobbyClock/32,0,1));
        if(reveal>0){using var fade=new SolidBrush(Color.FromArgb((int)(255*reveal),6,10,16));g.FillRectangle(fade,0,0,W,H);}
    }
}
