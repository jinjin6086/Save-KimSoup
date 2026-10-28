using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap? arenaRuins, groundTile;
    private readonly Dictionary<(int,int),Pen> particlePens = new();
    private readonly Dictionary<int,SolidBrush> particleBrushes = new();
    private readonly PointF[] shardPoints = new PointF[4];
    private readonly Dictionary<(float,bool),Font> labelFonts = new();
    private readonly Dictionary<(string,int,int,float,bool),float> labelSizes = new();
    private readonly StringFormat leftLabel = new() { Alignment=StringAlignment.Near, LineAlignment=StringAlignment.Center, Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap };
    private readonly StringFormat centerLabel = new() { Alignment=StringAlignment.Center, LineAlignment=StringAlignment.Center, Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap };
    private float melodyInterval = 42;
    private readonly int[] melodyGaps = new int[3];
    private float landingDustTimer, landingStrength;
    private float landingOriginFrame, landingOriginX;

    private Font LabelFont(float size,bool bold)
    {
        var key=(size,bold);
        if(!labelFonts.TryGetValue(key,out var font))
            labelFonts[key]=font=new Font("Malgun Gothic",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
        return font;
    }

    private void DisposeArena()
    {
        DisposeScrollingLayers();
        arenaRuins?.Dispose(); groundTile?.Dispose();
        foreach(var pen in particlePens.Values)pen.Dispose();
        foreach(var brush in particleBrushes.Values)brush.Dispose();
        foreach(var font in labelFonts.Values) font.Dispose();
        leftLabel.Dispose();centerLabel.Dispose();
    }

    private SolidBrush ParticleBrush(Color color,int alpha)
    {
        int argb=Color.FromArgb(Math.Clamp(alpha/16,0,15)*16,color).ToArgb();
        if(!particleBrushes.TryGetValue(argb,out var brush))
        {
            if(particleBrushes.Count>=128){foreach(var old in particleBrushes.Values)old.Dispose();particleBrushes.Clear();}
            particleBrushes[argb]=brush=new SolidBrush(Color.FromArgb(argb));
        }
        return brush;
    }

    private Pen ParticlePen(Color color,int alpha,int width)
    {
        int bucket=Math.Clamp(alpha/16,0,15)*16;
        var key=(Color.FromArgb(bucket,color).ToArgb(),width);
        if(!particlePens.TryGetValue(key,out var pen))
        {
            if(particlePens.Count>=256){foreach(var old in particlePens.Values)old.Dispose();particlePens.Clear();}
            particlePens[key]=pen=new Pen(Color.FromArgb(key.Item1),width);
        }
        return pen;
    }

    private void BuildArena() { BuildSmoothArena(); }

    private void DrawArenaBackground(Graphics g) { SmoothBackground(g); }

    private void DrawArenaGround(Graphics g) { SmoothGround(g); }

    private void DrawStoneObstacle(Graphics g,RectangleF r,bool floating,bool tall=false)
    {
        Color rune=floating?Mint:Coral;
        using var stone=new SolidBrush(Color.FromArgb(43,42,53));g.FillRectangle(stone,r);
        using var rim=new Pen(Color.FromArgb(103,98,109),2);g.DrawRectangle(rim,r.X+1,r.Y+1,r.Width-2,r.Height-2);
        using var crack=new Pen(Color.FromArgb(13,15,24),2);g.DrawLine(crack,r.X+r.Width*.2f,r.Y,r.X+r.Width*.55f,r.Y+r.Height*.45f);g.DrawLine(crack,r.X+r.Width*.55f,r.Y+r.Height*.45f,r.X+r.Width*.3f,r.Bottom);
        using var light=new Pen(rune,2);
        float cx=r.X+r.Width/2,cy=r.Y+r.Height/2;
        g.DrawLines(light,new[]{new PointF(cx-8,cy),new PointF(cx,cy-10),new PointF(cx+8,cy),new PointF(cx,cy+10),new PointF(cx-8,cy)});
        if(tall)g.DrawLine(light,cx,r.Y+12,cx,r.Bottom-12);
        if(floating){using var chain=new Pen(Color.FromArgb(101,108,125),2);g.DrawLine(chain,r.X+8,r.Y-45,r.X+8,r.Y);g.DrawLine(chain,r.Right-8,r.Y-45,r.Right-8,r.Y);}
    }

    private void LandingFeedback(float speed)
    {
        landingStrength=Math.Clamp(Math.Abs(speed)/25f,.25f,1f);
        landingDustTimer=24;
        landingOriginFrame=frame;landingOriginX=px+26;
        Burst(px+26,GroundY-3,Color.FromArgb(139,132,143),(int)(8+landingStrength*13),2);
        foreach(var p in impactParticles.Skip(Math.Max(0,impactParticles.Count-(int)(8+landingStrength*13))))
        {p.Vy=-Math.Abs(p.Vy)*.65f;p.Vx=p.Vx*1.5f-4;}
        ApplyLandingPulse();
        audio.Play(landingStrength>.8f?"landheavy":landingStrength>.5f?"landmid":"landsoft");
        playerSquashTimer=8;playerSquashAmount=.10f+landingStrength*.08f;
        TriggerCameraShake(reducedEffects?0:landingStrength*2.2f,5);
    }

    private void DrawLandingDust(Graphics g)
    {
        if(landingDustTimer<=0)return;
        float life=landingDustTimer/24f,r=(1-life)*72*landingStrength+15;
        float x=landingOriginX-(frame-landingOriginFrame)*GetWorldSpeed();
        using var dust=new Pen(Color.FromArgb((int)(110*life),153,144,157),2);
        g.DrawEllipse(dust,x-r,GroundY-3-r*.13f,r*2,r*.26f);
        using var fracture=new Pen(Color.FromArgb((int)(210*life),8,9,16),2);
        float span=20+35*landingStrength;
        g.DrawLines(fracture,new[]{new PointF(x-span,GroundY+1),new PointF(x-span*.45f,GroundY+6),new PointF(x-8,GroundY+2),new PointF(x+11,GroundY+10),new PointF(x+span*.6f,GroundY+4),new PointF(x+span,GroundY+7)});
    }

    private void ArenaMenuBase(Graphics g)
    {
        DrawArenaGround(g);
        using var veil=new SolidBrush(Color.FromArgb(55,6,8,17));g.FillRectangle(veil,0,0,W,H);
        Label(g,$"M 소리 {(audio.Muted?"OFF":"ON")}  ·  F 효과 {(reducedEffects?"약":"기본")}",new(690,563,285,24),11,Paper);
    }

    private void ArenaTitle(Graphics g)
    {
        ArenaMenuBase(g);
        Label(g,"읽고, 피하고, 되돌리는 질주",new(105,120,530,30),16,Accent(1),false,false);
        Label(g,"김스프를",new(100,165,600,84),64,Paper,false);
        Label(g,"구해라!",new(100,244,600,84),64,Paper,false);
        Label(g,"ENTER  ·  시작",new(111,362,390,42),22,Paper,false);
        Label(g,"공격을 읽고, 되돌려라.",new(111,416,440,30),15,Color.FromArgb(165,159,177),false,false);
        if(player1!=null)g.DrawImage(player1,new Rectangle(701,348,104,151));
        Label(g,"↑ 점프   ↓ 슬라이드   SPACE / SHIFT 발차기   Q 역류",new(55,555,680,30),13,Paper,false,false);
    }

    private void ArenaTransition(Graphics g)
    {
        float t=roundTransitionTimer;
        if(t<RoundTransitionDefeatFrames){DrawWorld(g);using var dim=new SolidBrush(Color.FromArgb((int)(180*Math.Clamp(t/RoundTransitionDefeatFrames,0,1)),Ink));g.FillRectangle(dim,0,0,W,H);Label(g,transitionHasBossDefeat?"격 파":"통 과",new(200,215,600,90),44,Paper);return;}
        ArenaMenuBase(g);
        int next=transitionNextRound;
        float reveal=Math.Clamp((t-RoundTransitionDefeatFrames)/24,0,1)*Math.Clamp((RoundTransitionTotalFrames-t)/18,0,1);
        Label(g,next==0?"게임 클리어":"제 "+next+" 막",new(130,140,740,38),19,Accent(next));
        Label(g,next==0?"게임 클리어":RoundName(next),new(130,200,740,80),52,Paper);
        Label(g,next==0?"다음 라운드 준비":RoundHint(next),new(125,314,750,35),18,Paper,true,false);
        if(!string.IsNullOrWhiteSpace(transitionReward))Label(g,"획득  ·  "+transitionReward,new(125,355,750,28),15,Mint);
        using var line=new Pen(Color.FromArgb(110,Accent(next)));g.DrawLine(line,370,295,630,295);
        Image? face=next switch{2=>enemy4,3=>enemy3,4=>enemy5,_=>null};
        if(face!=null)g.DrawImage(face,new Rectangle(448,387,104,112));
        using var fade=new SolidBrush(Color.FromArgb((int)(200*(1-reveal)),Ink));g.FillRectangle(fade,0,0,W,H);
    }

    private void ArenaSelect(Graphics g)
    {
        ArenaMenuBase(g);Label(g,"누가 이 길을 달릴까요",new(100,105,800,52),31,Paper);
        for(int i=0;i<2;i++)
        {
            int x=i==0?270:730;
            using var rune=new Pen(Color.FromArgb(110,i==0?Mint:Accent(4)),2);g.DrawEllipse(rune,x-92,436,184,22);
            if(i==0&&player1!=null)g.DrawImage(player1,new Rectangle(x-48,229,96,139));else DrawSimpleCookie(g,x-34,242,68,121);
            Label(g,i==0?"김스프 쿠키":"늑대 러너",new(x-180,380,360,37),24,Paper);
            Label(g,$"[ {i+1} ]  선택",new(x-180,468,360,28),16,i==0?Mint:Accent(4));
        }
        Label(g,"쿠키를 클릭하거나 1 / 2 키를 누르세요",new(100,549,570,30),13,Paper,false,false);
    }

    private void ArenaAugments(Graphics g)
    {
        ArenaMenuBase(g);Label(g,"길 위에서 발견한 힘",new(100,86,800,52),32,Paper);
        Label(g,"하나를 선택하세요",new(100,143,800,30),14,Paper,true,false);
        for(int i=0;i<3;i++)
        {
            var r=augmentCardRects[i];float cx=r.X+r.Width/2;
            Color color=augmentChoices[i] switch{AugmentType.LifeSteal=>Mint,AugmentType.ParryRange=>Accent(4),_=>Accent(2)};
            using var circle=new Pen(Color.FromArgb(100,color),2);g.DrawEllipse(circle,cx-40,218,80,80);
            string symbol=augmentChoices[i] switch{AugmentType.LifeSteal=>"♥",AugmentType.ParryRange=>"✦",_=>"↻"};
            Label(g,symbol,new(cx-45,215,90,80),43,color);
            Label(g,GetAugmentName(augmentChoices[i]),new(r.X+10,324,r.Width-20,38),22,Paper);
            var lines=GetAugmentDescription(augmentChoices[i]);for(int j=0;j<lines.Length;j++)Label(g,lines[j],new(r.X+12,374+j*24,r.Width-24,24),12,Paper,true,false);
            Label(g,$"[ {i+1} ] 선택",new(r.X+10,452,r.Width-20,27),15,color);
        }
    }

    private void ArenaResult(Graphics g,bool won)
    {
        ArenaMenuBase(g);Label(g,won?"공연의 끝":"다시, 무대 위로",new(100,138,800,38),18,won?Mint:Coral);
        Label(g,won?"김스프를 구했어요!":"게임 오버",new(100,203,800,76),45,Paper);
        Label(g,$"제 {round} 막    ·    {ClearTimeText}",new(100,310,800,35),22,Paper);
        Label(g,won?"ENTER  타이틀로":"R  같은 라운드 재도전    /    ENTER  타이틀로",new(100,411,800,40),17,Paper);
    }

    private void ArenaHud(Graphics g)
    {
        using var shadow=new SolidBrush(Color.FromArgb(125,8,10,20));g.FillRectangle(shadow,22,23,280,60);
        Label(g,$"제 {round} 막   /   {ClearTimeText}",new(38,26,245,23),13,Paper,false);
        using var track=new Pen(Color.FromArgb(80,99,94,109),6);g.DrawLine(track,39,65,248,65);
        using var trail=new Pen(Color.FromArgb(130,Coral),6);g.DrawLine(trail,39,65,39+209*Math.Clamp(hpTrail/MaxHealth,0,1),65);
        using var health=new Pen(Mint,6);g.DrawLine(health,39,65,39+209*Math.Clamp(hp/MaxHealth,0,1),65);
        for(int i=1;i<5;i++){using var seam=new Pen(Ink,2);g.DrawLine(seam,39+i*42,61,39+i*42,69);}
        Label(g,$"{hp:0}",new(254,50,47,28),12,Paper);
        if(round>=2)
        {
            Label(g,RoundName(round)+(round==3&&purgaeTransformed?" · 각성":""),new(594,23,367,29),17,Paper);
            using var baseLine=new Pen(Color.FromArgb(70,Paper),3);g.DrawLine(baseLine,610,61,944,61);
            using var bossHp=new Pen(Accent(round),3);g.DrawLine(bossHp,610,61,610+334*Math.Clamp(1-bossDamage/bossHitRequired,0,1),61);
        }
        Label(g,parryCooldown<=0?"발차기 준비":$"발차기 {parryCooldown/60:0.0}s",new(25,552,160,28),13,parryCooldown<=0?Mint:Paper,false);
        Label(g,"↑ 점프  ↓ 슬라이드  SPACE / SHIFT 발차기  Q 역류",new(200,552,480,28),12,Paper,false,false);
        Label(g,$"M 소리 {(audio.Muted?"OFF":"ON")} · F 효과 {(reducedEffects?"약":"기본")}",new(698,552,280,28),11,Paper);
        if(augmentFlashTimer>0&&!string.IsNullOrWhiteSpace(augmentNotice))Label(g,augmentNotice,new(380,85,590,25),12,Paper);
        DrawResonanceMeter(g);
        DrawCadence(g);
        DrawRoundOneParryTutorial(g);
    }

    private void DrawCadence(Graphics g)
    {
        if(round!=4||!MelodySequenceActive)return;
        float progress=1-Math.Clamp(melodyWordTimer/Math.Max(1,melodyInterval),0,1);
        if(melodyBurstActive)Label(g,$"연속 {melodyBurstTotal-melodyBurstRemaining+1} / {melodyBurstTotal}",new(360,269,280,25),12,Coral);
        bool last=MelodyLaserApproaching;
        Color color=last?Paper:Accent(4);
        Label(g,bossMessage,new(360,107,280,66),last?46:34,color);
        using var track=new Pen(Color.FromArgb(45,color),2);g.DrawEllipse(track,457,179,86,86);
        using var timer=new Pen(color,3);g.DrawArc(timer,457,179,86,86,-90,359*progress);
        Label(g,last?"반사":"듣기",new(462,202,76,35),14,color);
        if(last)
        {
            float radius=30+(1-progress)*64;
            using var focus=new Pen(Color.FromArgb(120,color),2);
            g.DrawEllipse(focus,bossX+77-radius,bossY+65-radius,radius*2,radius*2);
        }
    }
}


