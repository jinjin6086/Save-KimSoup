using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private static readonly Color Gold=Color.FromArgb(201,177,123),Steel=Color.FromArgb(170,165,153);
    private bool renderingScene;
    private void PaintPremiumFrame(Graphics g,float scale)
    {
        bool finisher=FinisherMonochromeActive;
        bool counter=state==GameState.Round&&!MelodyLastStandActive&&!bossBriefingActive&&sceneFadeTimer<=0&&cinematicTimer>0&&!reducedEffects;
        bool timeStop=TruePurgaeTimeFrozen&&!finisher&&!counter;
        bool focus=!IsangjinClockActive&&(FocusActive||FocusReturning)&&!finisher&&!counter&&!timeStop&&sceneFadeTimer<=0;
        if(timeStop){PaintTruePurgaeFrozenFrame(g,scale);return;}
        if(!finisher&&!counter&&!focus&&!timeStop){RenderArenaFrame(g);return;}
        float sceneScale=scale;
        int w=Math.Max(1,(int)(W*sceneScale)),h=Math.Max(1,(int)(H*sceneScale));
        if(cinemaFrame==null||cinemaFrame.Width!=w||cinemaFrame.Height!=h){cinemaFrame?.Dispose();cinemaFrame=new(w,h,PixelFormat.Format32bppRgb);}
        renderingScene=focus||timeStop;
        try{using var cg=Graphics.FromImage(cinemaFrame);cg.ScaleTransform(sceneScale,sceneScale);RenderArenaFrame(cg);}finally{renderingScene=false;}
        g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.HighSpeed;
        if(finisher||counter||timeStop)DesaturateScene(cinemaFrame);else ApplyHackedFocusGrade(cinemaFrame,FocusActive?1:focusExitTimer/18);
        DrawOpaqueScene(g,cinemaFrame);
        g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if(focus){DrawFocusTears(g);PremiumHud(g);}
        else if(timeStop)PremiumHud(g);
        else if(counter)
        {
            float k=Math.Min(1,cinematicTimer/8);
            using var bars=new SolidBrush(Color.FromArgb((int)(220*k),4,8,15));g.FillRectangle(bars,0,0,W,19*k);g.FillRectangle(bars,0,H-19*k,W,19*k);
            Label(g,"정확한 반사",new(300,76,400,33),21,Color.White);
            if(laserActive&&laserReflected){DrawCounterLaser(g);DrawLaserContactFx(g);DrawCounterFeedback(g);}
        }
        DrawPause(g);
    }
    private static unsafe void DesaturateScene(Bitmap bitmap)
    {
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppRgb);
        try
        {
            for(int y=0;y<data.Height;y++)
            {
                uint* row=(uint*)((byte*)data.Scan0+y*data.Stride);
                for(int x=0;x<data.Width;x++){uint c=row[x],v=(((c>>16)&255)*77+((c>>8)&255)*150+(c&255)*29)>>8;row[x]=0xff000000|v*0x010101;}
            }
        }
        finally{bitmap.UnlockBits(data);}
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct DibInfo
    {
        public uint Size;public int Width,Height;public ushort Planes,BitCount;
        public uint Compression,SizeImage;public int XPels,YPels;public uint Used,Important;
    }
    [DllImport("gdi32.dll")]private static extern int StretchDIBits(IntPtr dc,int x,int y,int w,int h,int sx,int sy,int sw,int sh,IntPtr bits,ref DibInfo info,uint usage,uint rop);
    [DllImport("gdi32.dll")]private static extern int SetStretchBltMode(IntPtr dc,int mode);
    private static void DrawOpaqueScene(Graphics g,Bitmap bitmap)
    {
        using var transform=g.Transform;var m=transform.Elements;
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
        IntPtr dc=IntPtr.Zero;
        try
        {
            dc=g.GetHdc();int old=SetStretchBltMode(dc,4); // HALFTONE: smooth native GDI stretch, not nearest-neighbor.
            var info=new DibInfo{Size=40,Width=data.Width,Height=-data.Height,Planes=1,BitCount=32};
            StretchDIBits(dc,(int)m[4],(int)m[5],(int)(W*m[0]),(int)(H*m[3]),0,0,data.Width,data.Height,data.Scan0,ref info,0,0x00CC0020);
            SetStretchBltMode(dc,old);
        }
        finally{if(dc!=IntPtr.Zero)g.ReleaseHdc(dc);bitmap.UnlockBits(data);}
    }
    private void DrawAnimatedPlayer(Graphics g,Rectangle target)
    {
        if(sliding&&onGround&&!DodgeActive&&slideFrames.Length==3){DrawSlidingActor(g);return;}
        if(slideExitTimer>0&&onGround&&slideFrames.Length==3)DrawSlidePose(g,2,slideExitTimer/6);
        int index;
        float phase=animationClock/5f;
        if(activeTimer>0&&activeSkill==ActivePower.Dodge)index=5;
        else if((activeTimer>0&&activeSkill==ActivePower.Eat)||(passiveSkill==PassivePower.Feast&&skillVisual>0))index=6+(int)(animationClock/8)%2;
        else if(sliding)index=5;
        else if(!onGround)index=4;
        else index=(int)phase%4;
        float alpha=(cloneActive?.72f:1)*(slideExitTimer>0&&onGround?1-slideExitTimer/6:1);
        if(DodgeActive){alpha=.34f;target.Offset(-8,0);}
        spriteMatrix.Matrix33=alpha;spriteAlpha.SetColorMatrix(spriteMatrix);
        DrawPlayerSprite(g,motionFrames[index],target,Mint,alpha);
        // Short blend through adjacent actual poses softens the contact changes.
        if(onGround&&!sliding&&activeTimer<=0)
        {
            float blend=Math.Clamp((phase-MathF.Floor(phase)-.65f)/.35f,0,1);
            if(blend>0){spriteMatrix.Matrix33=blend*.65f*alpha;spriteAlpha.SetColorMatrix(spriteMatrix);DrawPlayerSprite(g,motionFrames[(index+1)%4],target,Mint,blend*.65f*alpha);}
        }
    }
    private void DrawPremiumProjectile(Graphics g,Projectile p)
    {
        if(TruePurgaeEncounter){DrawTruePurgaeBasicProjectile(g,p);return;}
        if(round==3&&p.Type==AttackType.PurgaeSkill2Ball){DrawPurgaeOrb(g,p.X+p.W*.5f,p.Y+p.H*.5f,p.W,p.IsParried);return;}
        if(PurgaeBasicProjectile(p)){DrawPurgaeBasicProjectile(g,p);return;}
        DrawThreatHalo(g,p);
        if(p.Type==AttackType.Meteor&&meteorArt!=null)
        {
            DrawFallingMeteor(g,p);
            return;
        }
        if(p.Type==AttackType.MelodyNote)
        {
            Color c=p.IsParried?Gold:menheraMode?Color.FromArgb(255,90,127):Color.FromArgb(237,168,223);
            using var glow=new SolidBrush(Color.FromArgb(45,c));g.FillEllipse(glow,p.X-7,p.Y-7,p.W+14,p.H+14);
            using var stem=new Pen(c,3.5f);using var head=new SolidBrush(c);
            g.FillEllipse(head,p.X+4,p.Y+19,15,11);g.DrawLine(stem,p.X+18,p.Y+23,p.X+18,p.Y+1);g.DrawBezier(stem,p.X+18,p.Y+1,p.X+24,p.Y+8,p.X+31,p.Y+6,p.X+28,p.Y+15);
            return;
        }
        p.Draw(g,frame);
    }
    private void DrawPremiumEffects(Graphics g)
    {
        if(skillVisual>0&&passiveSkill==PassivePower.Feast)
        {
            using var siphon=new Pen(Color.FromArgb((int)Math.Min(150,skillVisual*3),Mint),3);
            g.DrawBezier(siphon,bossX+65,bossY+65,520,120,350,420,px+28,py+25);
            for(int i=0;i<6;i++){float t=(animationClock*.025f+i/6f)%1;float x=bossX+65+(px-bossX-35)*t,y=bossY+65+(py-bossY-35)*t-MathF.Sin(t*MathF.PI)*65;g.FillEllipse(ParticleBrush(Mint,180),x,y,7,7);}
        }
        if(skillVisual>0&&passiveSkill==PassivePower.Truce&&truceUsed)
        {
            float radius=20+(65-skillVisual)*2;using var restore=new Pen(Color.FromArgb((int)Math.Min(160,skillVisual*3),Gold),3);
            g.DrawEllipse(restore,px+26-radius,py+35-radius,radius*2,radius*2);
        }
        DrawEncoreEffects(g);
        DrawAbsorbFlow(g);
        if(activeTimer>0&&activeSkill==ActivePower.Eat)
        {
            DrawEatStraw(g);
        }
        if(sparkActive)
        {
            float radius=40+Math.Min(absorbedCount,12)*2.5f;
            using var glow=new SolidBrush(Color.FromArgb(35,Mint));
            g.FillEllipse(glow,sparkX-radius-18,sparkY-radius-18,(radius+18)*2,(radius+18)*2);
            using var orbPath=new GraphicsPath();orbPath.AddEllipse(sparkX-radius,sparkY-radius,radius*2,radius*2);
            using var body=new PathGradientBrush(orbPath){CenterColor=Color.FromArgb(255,255,255,245),SurroundColors=new[]{Color.FromArgb(130,58,207,191)}};
            g.FillEllipse(body,sparkX-radius,sparkY-radius,radius*2,radius*2);
            using var arc=new Pen(Color.White,2);using var rim=new Pen(Color.FromArgb(210,194,255,237),3);
            g.DrawArc(rim,sparkX-radius-6,sparkY-radius-6,(radius+6)*2,(radius+6)*2,frame*9,140);
            g.DrawArc(rim,sparkX-radius*.76f,sparkY-radius*.76f,radius*1.52f,radius*1.52f,-frame*12,190);
            for(int i=0;i<12;i++){float a=i*MathF.Tau/12+frame*.2f;float r=radius+12+MathF.Sin(frame*.6f+i)*14;g.DrawLines(arc,new[]{new PointF(sparkX+MathF.Cos(a)*radius*.6f,sparkY+MathF.Sin(a)*radius*.6f),new PointF(sparkX+MathF.Cos(a+.1f)*r*.85f,sparkY+MathF.Sin(a+.1f)*r*.85f),new PointF(sparkX+MathF.Cos(a-.06f)*r*.92f,sparkY+MathF.Sin(a-.06f)*r*.92f),new PointF(sparkX+MathF.Cos(a+.1f)*r,sparkY+MathF.Sin(a+.1f)*r)});}
        }
        if(skillVisual>0&&cloneActive){using var ring=new Pen(Color.FromArgb((int)Math.Min(190,skillVisual*3),Color.FromArgb(107,255,138)),2);g.DrawEllipse(ring,px-15,py-12,82,96);}
    }
    private readonly Dictionary<(int,int,int,bool),Bitmap> panelSurfaces=new();
    private float panelScale=1;
    private void PanelSurface(Graphics g,RectangleF rect,Color accent,bool selected=false)
    {
        var key=((int)rect.Width,(int)rect.Height,accent.ToArgb(),selected);
        if(!panelSurfaces.TryGetValue(key,out var bitmap))
        {
            if(panelSurfaces.Count>=48){foreach(var old in panelSurfaces.Values)old.Dispose();panelSurfaces.Clear();}
            bitmap=new Bitmap(Math.Max(1,(int)Math.Ceiling(rect.Width*panelScale)),Math.Max(1,(int)Math.Ceiling(rect.Height*panelScale)),PixelFormat.Format32bppPArgb);
            using var cg=Graphics.FromImage(bitmap);cg.ScaleTransform(panelScale,panelScale);cg.SmoothingMode=SmoothingMode.AntiAlias;
            PaintPanelSurface(cg,new RectangleF(0,0,rect.Width-.5f,rect.Height-.5f),accent,selected);panelSurfaces[key]=bitmap;
        }
        DrawCachedLayer(g,bitmap,rect);
    }
    private void PaintPanelSurface(Graphics g,RectangleF rect,Color accent,bool selected)
    {
        // Cached charcoal and brushed brass retain the landscape between compact UI elements.
        using var body=SoulsPanelPath(rect,4);
        using var fill=new LinearGradientBrush(rect,Color.FromArgb(selected?241:224,32,30,27),Color.FromArgb(selected?244:228,10,11,12),90f);
        g.FillPath(fill,body);
        using var rim=new Pen(Color.FromArgb(selected?188:88,Gold),selected?1.3f:.8f);g.DrawPath(rim,body);
        using var inner=new Pen(Color.FromArgb(selected?63:24,Gold),.7f);
        if(rect.Width>18&&rect.Height>18)g.DrawRectangle(inner,rect.Left+4,rect.Top+4,rect.Width-8,rect.Height-8);
        using var patina=new Pen(Color.FromArgb(selected?80:36,Gold),.8f);
        g.DrawLine(patina,rect.Left+9,rect.Top+1,rect.Right-9,rect.Top+1);
        if(selected)
        {
            using var warmth=new LinearGradientBrush(new RectangleF(rect.Left+1,rect.Top+1,rect.Width-2,rect.Height-2),Color.FromArgb(27,Gold),Color.Transparent,90f);
            g.FillPath(warmth,body);
        }
    }
    private void Diamond(Graphics g,float x,float y,float radius,Color color)
    {
        using var p=new Pen(color,1.4f);g.DrawPolygon(p,new[]{new PointF(x,y-radius),new PointF(x+radius,y),new PointF(x,y+radius),new PointF(x-radius,y)});
    }
    private void PremiumHud(Graphics g)
    {
        PanelSurface(g,new(24,20,305,79),Gold);
        Label(g,cloneActive?"플레이어  /  클론":selectedCookie==1?"늑대 러너":"플레이어",new(39,27,176,21),10.5f,Gold,false,false);
        Label(g,$"{hp:0} / {PlayerMaxHealth:0}",new(213,27,99,21),11,Paper);
        float health=Math.Clamp(hp/PlayerMaxHealth,0,1),trail=Math.Clamp(hpTrail/PlayerMaxHealth,0,1);
        DrawSoulsGauge(g,new(40,53,271,11),health,trail,Color.FromArgb(124,28,34),Color.FromArgb(193,61,52));
        Label(g,$"{WorldName}  ·  라운드 {round}  ·  {ClearTimeText}",new(39,71,275,18),9.5f,Steel,false,false);
        if(round>=2)
        {
            PanelSurface(g,new(584,20,392,76),Gold);
            string phase=menheraMode&&round==4?"  ·  멘헤라":purgaeTransformed&&round==3?"  ·  광폭화":"";
            Label(g,RoundName(round)+phase,new(604,28,258,24),15,Paper,false);
            Label(g,$"{Math.Max(0,bossHitRequired-bossDamage):0.#} / {bossHitRequired}",new(871,31,84,18),10,Gold);
            DrawSoulsGauge(g,new(604,61,351,7),Math.Clamp(1-bossDamage/bossHitRequired,0,1),0,Color.FromArgb(106,79,37),Gold);
            DrawArmorStatus(g);
        }
        PanelSurface(g,new(24,525,234,55),Gold);
        DrawSoulsKey(g,new(37,536,51,30),"SPACE",parryCooldown<=0);
        Label(g,parryCooldown<=0?"발차기 준비":$"발차기  {parryCooldown/60:0.0}초",new(101,529,144,25),13.5f,Paper,false);
        Label(g,"공격 반사  ·  SHIFT 겸용",new(101,553,144,17),9,Steel,false,false);
        PanelSurface(g,new(270,525,333,55),Gold,activeTimer>0);
        DrawSoulsKey(g,new(283,536,30,30),"Q",activeCooldown<=0||activeTimer>0);
        Label(g,ActiveName,new(326,529,262,25),14,Paper,false);
        Label(g,ActiveHudStatus,new(326,553,262,18),10,activeTimer>0?Gold:Steel,false,false);
        PanelSurface(g,new(615,525,361,55),Gold,skillVisual>0&&passiveSkill!=PassivePower.None);
        Label(g,"특성",new(628,537,35,24),10,Gold);
        using(var divider=new Pen(Color.FromArgb(68,Gold),.8f))g.DrawLine(divider,669,537,669,568);
        Label(g,PassiveName,new(681,529,278,25),13,Paper,false);
        Label(g,PassiveHudStatus,new(681,553,278,18),9.5f,Steel,false,false);
        if(augmentFlashTimer>0&&!string.IsNullOrWhiteSpace(augmentNotice))Label(g,augmentNotice,new(345,96,620,22),12,Paper);
        DrawCadence(g);
        // Keep skill status in the free row above the controls. Mechanic
        // banners and time-stop instructions retain their full upper area.
        DrawCinderStatus(g);DrawFocusStatus(g);
    }
    private void PremiumTitle(Graphics g)
    {
        DrawSoulsMenuBase(g,.76f);
        using(var shade=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(168,9,9,10),Color.FromArgb(12,9,9,10),0f))g.FillRectangle(shade,0,0,W,H);
        Label(g,"달리기 · 발차기 · 보스전",new(83,81,480,26),12,Gold,false,false);
        using(var edge=new Pen(Color.FromArgb(80,Gold),1))g.DrawLine(edge,82,119,82,329);
        SoulsHeading(g,"김스프를",new(100,134,520,83),58,Paper,false);
        SoulsHeading(g,"구해라",new(98,217,520,92),72,Paper,false);
        DrawSoulsRule(g,104,326,385);
        Label(g,"보스들을 물리치고 김스프를 구하세요.",new(104,346,486,32),14,Steel,false,false);
        PanelSurface(g,new(104,406,300,57),Gold,true);
        Label(g,"ENTER    게임 시작",new(119,415,270,38),18,Paper);
        if(motionFrames.Length>0)
        {
            using var floor=new SolidBrush(Color.FromArgb(104,0,0,0));g.FillEllipse(floor,642,477,172,17);
            DrawReadableSprite(g,motionFrames[0],new Rectangle(626,208,200,278),Gold,.94f);
        }
        PanelSurface(g,new(423,406,160,57),Gold);Label(g,"T  연습 모드",new(423,406,160,57),15,Paper);
        Label(g,"4개 라운드  /  능력 직접 선택  /  선택 도전: 종결 보스",new(104,479,570,26),12,Gold,false,false);
        Label(g,"↑ 점프    ↓ 슬라이드    SPACE / SHIFT 발차기    Q 스킬    P 일시정지",new(45,549,720,26),11,Steel,false,false);
    }
    private void DrawSkillCards(Graphics g)
    {
        if(pendingDraft==3){DrawSoulsForge(g);return;}
        DrawSoulsMenuBase(g,.90f);
        Label(g,pendingDraft==1?"라운드 1 클리어":"라운드 2 클리어",new(150,26,700,23),12,Gold);
        SoulsHeading(g,selectedCard<0?(pendingDraft==1?"Q 스킬 선택":"패시브 스킬 선택"):"능력 획득",new(100,52,800,43),30,Paper);
        DrawSoulsRule(g,350,103,300);
        Label(g,selectedCard<0?(pendingDraft==1?"6개 Q 스킬 중 하나를 고르세요.":"패시브 5개 중 선택 · 공백 칸은 선택할 수 없습니다."):CardName(hiddenCards[selectedCard])+" 선택 완료",new(100,111,800,24),12,selectedCard<0?Steel:Gold,true,false);
        for(int i=0;i<DraftCardCount;i++)
        {
            Rectangle r=DraftCardRect(i);bool chosen=selectedCard==i,focused=selectedCard<0&&skillCardFocus==i;
            var saved=g.Save();g.TranslateTransform(r.X,r.Y);
            PanelSurface(g,new(0,0,r.Width,r.Height),Gold,chosen||focused);
            if(!DraftCardAvailable(i)){
                SoulsHeading(g,"—",new(14,65,r.Width-28,40),26,Steel);
                Label(g,"공백",new(14,108,r.Width-28,25),12,Steel);g.Restore(saved);continue;
            }
            Label(g,$"[ {i+1} ]",new(14,10,45,20),11,Gold,false,false);
            Label(g,chosen?"선택 완료":pendingDraft==1?"Q 스킬":"패시브",new(r.Width-120,10,106,20),10,chosen?Gold:Steel);
            SoulsHeading(g,CardName(hiddenCards[i]),new(14,32,r.Width-28,27),19,Paper);
            DrawSkillPreview(g,new(14,62,r.Width-28,65),pendingDraft==1,hiddenCards[i]);
            var lines=CardLines(hiddenCards[i]);
            for(int j=0;j<lines.Length;j++)Label(g,lines[j],new(13,132+j*13,r.Width-26,14),10.2f,Steel,true,false);
            if(selectedCard>=0&&!chosen){using var dim=new SolidBrush(Color.FromArgb(133,6,7,8));g.FillRectangle(dim,0,0,r.Width,r.Height);}
            g.Restore(saved);
        }
        Label(g,selectedCard<0?(pendingDraft==1?"1~6 / 카드 클릭  ·  방향키 + ENTER 선택":"1~4, 6 / 카드 클릭  ·  방향키 + ENTER 선택"):"ENTER  다음 라운드  ·  잠시 후 자동 진행",new(110,533,780,27),12,selectedCard<0?Steel:Gold);
    }
    private void PremiumSelect(Graphics g)
    {
        DrawSoulsMenuBase(g,.88f);
        Label(g,"게임 준비",new(100,44,800,25),12,Gold,true,false);
        SoulsHeading(g,"캐릭터 선택",new(100,83,800,53),31,Paper);
        DrawSoulsRule(g,355,147,290);
        for(int i=0;i<2;i++)
        {
            int x=i==0?70:530;
            PanelSurface(g,new(x,165,400,350),Gold);
            Label(g,i==0?"I":"II",new(x+30,179,340,25),12,Gold);
            using(var shade=new SolidBrush(Color.FromArgb(102,0,0,0)))g.FillEllipse(shade,x+123,369,155,13);
            if(i==0&&motionFrames.Length>0)DrawReadableSprite(g,motionFrames[0],new Rectangle(x+130,206,140,175),Gold,1);
            else DrawWolfPose(g,8,new(x+120,208,165,175));
            SoulsHeading(g,i==0?"러너":"늑대 러너",new(x+30,386,340,37),25,Paper);
            Label(g,i==0?"체력 5,000  ·  기본 공격력":"체력 4,000  ·  공격력 ×1.5",new(x+30,429,340,26),12,Steel,true,false);
            DrawSoulsRule(g,x+116,468,168);
            Label(g,$"[ {i+1} ]   선택",new(x+30,476,340,27),14,Gold);
        }
        Label(g,"1 / 2  또는 인물 카드 클릭",new(100,541,800,25),12,Steel,true,false);
    }
    private void PremiumResult(Graphics g,bool won)
    {
        DrawSoulsMenuBase(g,.94f);
        using(var band=new LinearGradientBrush(new Rectangle(0,148,W,144),Color.FromArgb(15,0,0,0),Color.FromArgb(220,0,0,0),90f))g.FillRectangle(band,0,148,W,144);
        Label(g,won?"게임 클리어":"게임 오버",new(200,119,600,26),12,won?Gold:Steel,true,false);
        SoulsHeading(g,won?"김스프 구출 성공":"쓰러졌다",new(100,177,800,76),won?43:52,won?Gold:Color.FromArgb(176,60,49));
        DrawSoulsRule(g,300,286,400);
        Label(g,$"라운드 {round}    /    플레이 시간 {ClearTimeText}",new(250,307,500,34),19,Paper);
        Label(g,"Q  "+ActiveName,new(180,359,640,25),12,Steel,true,false);
        Label(g,"패시브  "+PassiveName,new(180,389,640,25),12,Steel,true,false);
        PanelSurface(g,new(324,441,352,49),Gold,true);
        Label(g,won?"ENTER    타이틀로":"R    같은 라운드 재도전",new(336,448,328,33),16,Paper);
        if(!won)Label(g,"ENTER    타이틀로",new(300,510,400,25),12,Steel,true,false);
    }
    private void PremiumTransition(Graphics g)
    {
        float t=roundTransitionTimer;
        if(t<45)
        {
            DrawWorld(g);
            using var dim=new SolidBrush(Color.FromArgb((int)(192*t/45),7,7,8));g.FillRectangle(dim,0,0,W,H);
            SoulsHeading(g,$"라운드 {round} 클리어",new(160,233,680,59),36,Gold);
            Label(g,$"플레이 시간  {ClearTimeText}",new(250,331,500,29),16,Paper);
            DrawSoulsRule(g,340,307,320);return;
        }
        DrawSoulsMenuBase(g,.86f);
        float enter=Math.Clamp((t-45)/30,0,1);float x=83+(1-enter)*(1-enter)*40;
        int next=transitionNextRound;
        Label(g,$"라운드 {round} 클리어",new(x,122,650,28),14,Gold,false,false);
        string title=pendingDraft switch{1=>"Q 스킬 선택",2=>"패시브 스킬 선택",3=>"발차기 강화",_=>next==0?"게임 클리어":next==5?"마지막 선택":$"라운드 {next}"};
        SoulsHeading(g,title,new(x,181,650,77),49,Paper,false);
        DrawSoulsRule(g,x+2,278,433);
        string description=pendingDraft switch{1=>"6개 Q 스킬 중 원하는 하나를 고릅니다.",2=>"6개 패시브 중 원하는 하나를 고릅니다.",3=>"재사용 시간, 범위, 회복량을 함께 강화합니다.",_=>next==0?"김스프를 구하러 갑니다.":next==5?"종결 보스에 도전하거나 바로 김스프를 구할 수 있습니다.":$"다음 보스: {RoundName(next)}"};
        Label(g,description,new(x+2,300,641,40),15,Steel,false,false);
        Label(g,$"플레이 시간  {ClearTimeText}",new(x+2,354,580,27),14,Gold,false,false);
        Image? face=next switch{2=>enemy4,3=>enemy3,4=>enemy5,_=>null};
        if(face!=null)
        {
            using var floor=new SolidBrush(Color.FromArgb(80,0,0,0));g.FillEllipse(floor,738,451,145,14);
            DrawReadableSprite(g,face,new Rectangle(728,271,164,184),Gold,.9f);
        }
        using(var track=new Pen(Color.FromArgb(46,Gold),1))g.DrawLine(track,86,431,586,431);
        using(var line=new Pen(Gold,1))g.DrawLine(line,86,431,86+500*Math.Clamp((t-45)/110,0,1),431);
        Label(g,next==0?"김스프 구출":next==5?"종결 보스 도전 여부 선택":$"다음: 라운드 {next}",new(86,448,450,24),11,Gold,false,false);
        using var veil=new SolidBrush(Color.FromArgb((int)(200*(1-enter)),Ink));g.FillRectangle(veil,0,0,W,H);
    }
}
