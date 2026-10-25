using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private int finalBossKind=-1,finalVolley,finalSpecialHits;
    private float finalBossClock,finalShotClock,finalSpecialCooldown,finalSpecialClock;
    private float finalSpecialX,finalSweepY;
    private bool finalSpecialActive;
    private int FinalBossKind=>Math.Clamp(finalBossKind,0,2);
    private string FinalBossName=>FinalBossKind switch{0=>"(진) 파괴옥",1=>"잿불 골렘",_=>"앵콜 인형"};
    private string FinalBossSetting=>FinalBossKind switch
    {
        0=>"황금 균열을 두르고 승천한 파괴옥의 최종 모습.\n시간을 멈추고 탄환과 천륜창을 배치한다.",
        1=>"감옥의 용광로에서 나온 돌 골렘.\n불덩이를 던지고 발밑에서 불기둥을 터뜨립니다.",
        _=>"공연이 끝나도 멈추지 않는 자동 연주 인형.\n음표를 쏘다가 화면 위아래로 줄을 휘두릅니다."
    };
    private string[] FinalBossRules=>FinalBossKind switch
    {
        0=>new[]{"시간 정지|이상진 소환 뒤 정지. 금빛 승천형이 사방에 돌탄환 24~36개 배치.","연속 발차기 반사|해제 직전 SPACE 예약. 해제 후 빠르게 다시 눌러 반사.","시간 속의 창|정지된 창·종말의 초침은 점프 / 슬라이드. 창 반사 불가.","클리어 조건|체력 72. 정지 중 Q 지속·재사용 시간도 멈춥니다."},
        1=>new[]{"발차기 반사|날아오는 화염구는 반사할 수 있습니다.","2단 점프|발밑 표시 후 불기둥이 두 번 올라옵니다. ↑를 두 번 사용하세요.","반사 불가|바닥에서 올라오는 불기둥은 발차기로 반사할 수 없습니다.","클리어 조건|보스 체력은 20. Q 회피와 피해 감소도 사용할 수 있습니다."},
        _=>new[]{"발차기 반사|플레이어를 향해 오는 음표를 반사하세요.","점프 / 슬라이드|아래쪽 붉은 줄은 점프, 위쪽 붉은 줄은 ↓ 슬라이드로 회피.","반사 불가|붉은 줄은 약 0.8초 후 지나갑니다. 발차기로 반사할 수 없습니다.","클리어 조건|보스 체력은 20. 줄 경로는 예고된 뒤 바뀌지 않습니다."}
    };
    private Color FinalBossAccent=>FinalBossKind switch{0=>Color.FromArgb(239,189,82),1=>Color.FromArgb(255,135,63),_=>Color.FromArgb(224,156,193)};

    private void ChooseFinalBoss(){if(finalBossKind<0)finalBossKind=rng.Next(3);}
    private void ResetFinalBossRun(){ResetTruePurgaeEncounter();finalBossKind=-1;finalBossChoice=0;finalBossChoiceClock=0;}
    private void InitializeFinalBossRound()
    {
        ChooseFinalBoss();finalVolley=finalSpecialHits=0;
        finalBossClock=finalShotClock=finalSpecialClock=0;
        finalSpecialCooldown=210;finalSpecialActive=false;
        backgroundCacheRound=-1;
        bossHitRequired=FinalBossKind==0?72:20;
        InitializeTruePurgaeEncounter();
    }
    private void UpdateFinalBoss(float step)
    {
        if(TruePurgaeEncounter){UpdateTruePurgaeBoss(step);return;}
        finalBossClock+=step;
        float x=739+MathF.Sin(finalBossClock*.018f)*30,y=228+MathF.Sin(finalBossClock*.027f)*23;
        bossX+=(x-bossX)*(1-MathF.Pow(.95f,step));bossY+=(y-bossY)*(1-MathF.Pow(.95f,step));
        finalShotClock+=step;
        if(finalShotClock>=88&&!finalSpecialActive)
        {
            finalShotClock=0;finalVolley++;
            int n=FinalBossKind==0?(finalVolley%3==0?3:1):FinalBossKind==1?2:finalVolley%3==0?3:1;
            float sx=bossX-4,sy=bossY+59,aim=MathF.Atan2(py+31-sy,px+26-sx);
            for(int i=0;i<n;i++)
            {
                float a=aim+(i-(n-1)*.5f)*.13f,speed=FinalBossKind==2?10.8f:9.3f;
                var type=FinalBossKind switch{0=>AttackType.StoneChunk,1=>AttackType.Fireball,_=>AttackType.MelodyNote};
                projectiles.Add(new(sx,sy,type,31,MathF.Cos(a)*speed,MathF.Sin(a)*speed,FinalBossKind==0?400:450));
            }
            audio.Play("volley");
        }
        if(!finalSpecialActive)
        {
            finalSpecialCooldown-=step;
            if(finalSpecialCooldown<=0)
            {
                finalSpecialActive=true;finalSpecialClock=0;finalSpecialHits=0;finalSpecialX=px+26;
                finalSweepY=finalVolley%2==0?477:432;audio.Play("dashwarn");
            }
            return;
        }
        finalSpecialClock+=step;
        if(FinalBossKind==2)
        {
            if(finalSpecialClock>=56&&finalSpecialHits==0)
            {
                finalSpecialHits=1;float top=sliding?py+36:py+8,bottom=py+PlayerHeight-4;
                if(finalSweepY+14>=top&&finalSweepY-14<=bottom)ReceiveHit(800,"붉은 줄",Coral,true);
                audio.Play("dash");
            }
        }
        else
        {
            int wanted=FinalBossKind==1?2:1;
            if(finalSpecialHits<wanted&&finalSpecialClock>=50+finalSpecialHits*22)
            {
                finalSpecialHits++;
                if(Math.Abs(px+26-finalSpecialX)<87&&py+PlayerHeight>GroundY-56)ReceiveHit(FinalBossKind==0?700:650,FinalBossKind==0?"내려찍기":"불기둥",Coral,true);
                audio.Play("meteorhit");TriggerCameraShake(reducedEffects?0:3,8);
            }
        }
        if(finalSpecialClock>100)
        {
            finalSpecialActive=false;finalSpecialCooldown=FinalBossKind==1?300:270;finalShotClock=35;
        }
    }
    private void DrawFinalBossMechanics(Graphics g)
    {
        if(TruePurgaeEncounter){DrawTruePurgaeTimeOverlay(g);return;}
        if(round!=5||!finalSpecialActive)return;
        float t=finalSpecialClock;
        if(FinalBossKind==2)
        {
            bool warn=t<50;float alpha=warn?135:Math.Max(0,220-(t-56)*10);
            using var shade=new Pen(Color.FromArgb(warn?22:40,202,61,59),28);
            using var edge=new Pen(Color.FromArgb((int)Math.Clamp(alpha,0,220),241,104,89),warn?1.3f:3);
            g.DrawLine(shade,0,finalSweepY,W,finalSweepY);g.DrawLine(edge,0,finalSweepY-14,W,finalSweepY-14);g.DrawLine(edge,0,finalSweepY+14,W,finalSweepY+14);
            if(warn)Label(g,finalSweepY>450?"↑ 점프  ·  붉은 줄 반사 불가":"↓ 슬라이드  ·  붉은 줄 반사 불가",new(305,138,390,28),15,Paper);
            if(t>=50&&t<66){using var cable=new Pen(Color.FromArgb(235,255,222,182),3);g.DrawLine(cable,0,finalSweepY,W,finalSweepY);}
            return;
        }
        float pulse=.5f+.5f*MathF.Sin(t*.25f);
        if(t<50)
        {
            using var area=new SolidBrush(Color.FromArgb((int)(30+pulse*25),224,77,53));g.FillEllipse(area,finalSpecialX-87,GroundY-9,174,18);
            using var brim=new Pen(Color.FromArgb(200,224,116,71),1.4f);g.DrawArc(brim,finalSpecialX-87,GroundY-9,174,18,5,170);
            Label(g,FinalBossKind==0?"↑ 점프  ·  내려찍기":"↑↑ 2단 점프  ·  불기둥 2회",new(finalSpecialX-145,GroundY-123,290,28),14,Paper);
        }
        int pulses=FinalBossKind==1?2:1;
        for(int i=0;i<pulses;i++)
        {
            float age=t-50-i*22;if(age<0||age>24)continue;
            float k=1-age/24,height=MathF.Sin(Math.Clamp(age/8,0,1)*MathF.PI*.5f)*115;
            if(FinalBossKind==1)
            {
                using var fire=new LinearGradientBrush(new RectangleF(finalSpecialX-65,GroundY-height,130,height+1),Color.FromArgb(0,255,76,35),Color.FromArgb((int)(210*k),255,174,76),90);
                using var plume=new GraphicsPath();plume.AddBezier(finalSpecialX-65,GroundY,finalSpecialX-32,GroundY-height*.5f,finalSpecialX-24,GroundY-height*.8f,finalSpecialX,GroundY-height);
                plume.AddBezier(finalSpecialX,GroundY-height,finalSpecialX+18,GroundY-height*.6f,finalSpecialX+36,GroundY-height*.2f,finalSpecialX+65,GroundY);plume.CloseFigure();g.FillPath(fire,plume);
            }
            using var dust=new Pen(Color.FromArgb((int)(140*k),FinalBossAccent),2);
            for(int j=0;j<12;j++)
            {
                float a=j*2.399f,r=12+age*(2+j%3);
                g.DrawLine(dust,finalSpecialX+MathF.Cos(a)*r,GroundY-MathF.Abs(MathF.Sin(a))*r*.5f,finalSpecialX+MathF.Cos(a)*(r+3),GroundY-MathF.Abs(MathF.Sin(a))*(r+3)*.5f);
            }
        }
    }

    private void DrawFinalBossPortrait(Graphics g,RectangleF rect)
    {
        if(FinalBossKind==0){DrawTruePurgaePortrait(g,rect);return;}
        var saved=g.Save();g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/160,rect.Height/180);
        float t=finalBossClock+briefingClock;
        Color accent=FinalBossAccent;
        using var shadow=new Pen(Color.FromArgb(230,7,8,9),6){LineJoin=LineJoin.Round};
        using var edge=new Pen(Color.FromArgb(205,accent),1.3f){LineJoin=LineJoin.Round};
        using var metal=new LinearGradientBrush(new Rectangle(20,15,120,150),Color.FromArgb(87,84,70),Color.FromArgb(24,27,30),65);
        using var dark=new SolidBrush(Color.FromArgb(16,20,23));
        using var gold=new SolidBrush(accent);
        float breathe=MathF.Sin(t*.045f)*2;
        g.TranslateTransform(0,breathe);
        if(FinalBossKind==0)
        {
            using var body=new GraphicsPath();body.AddLines(new[]{new PointF(35,59),new PointF(125,59),new PointF(143,142),new PointF(113,161),new PointF(47,161),new PointF(17,142)});body.CloseFigure();g.DrawPath(shadow,body);g.FillPath(metal,body);g.DrawPath(edge,body);
            g.FillRectangle(dark,48,100,65,40);
            for(int i=0;i<5;i++)g.DrawLine(edge,53+i*13,99,53+i*13,139);
            using var head=new GraphicsPath();head.AddLines(new[]{new PointF(49,21),new PointF(112,21),new PointF(122,42),new PointF(112,75),new PointF(50,75),new PointF(39,42)});head.CloseFigure();g.DrawPath(shadow,head);g.FillPath(metal,head);g.DrawPath(edge,head);
            g.FillRectangle(dark,47,40,67,16);g.FillRectangle(gold,56,47,48,3);
            float raised=finalSpecialActive?finalSpecialClock<50?Math.Clamp(finalSpecialClock/25,0,1):Math.Max(0,1-(finalSpecialClock-50)/7):0;
            using var shoulder=new Pen(Color.FromArgb(74,75,69),21){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(shoulder,28,80,13,124-raised*80);g.DrawLine(shoulder,133,80,148,124-raised*80);
            for(int i=0;i<4;i++){g.FillEllipse(gold,34+i*30,77,3,3);}
            g.FillRectangle(dark,44,157,26,19);g.FillRectangle(dark,91,157,26,19);
        }
        else if(FinalBossKind==1)
        {
            using var rock=new GraphicsPath();rock.AddLines(new[]{new PointF(42,29),new PointF(67,12),new PointF(111,22),new PointF(122,57),new PointF(150,86),new PointF(137,137),new PointF(104,164),new PointF(48,164),new PointF(20,134),new PointF(10,92),new PointF(36,57)});rock.CloseFigure();g.DrawPath(shadow,rock);g.FillPath(metal,rock);g.DrawPath(edge,rock);
            using var cracks=new Pen(Color.FromArgb(222,239,96,45),2);
            g.DrawLines(cracks,new[]{new PointF(80,38),new PointF(72,70),new PointF(94,103),new PointF(76,135),new PointF(85,158)});
            g.DrawLines(cracks,new[]{new PointF(72,70),new PointF(37,84),new PointF(29,113)});g.DrawLines(cracks,new[]{new PointF(94,103),new PointF(129,91),new PointF(139,103)});
            using var corePath=new GraphicsPath();corePath.AddEllipse(54,84,54,54);
            using var core=new PathGradientBrush(corePath){CenterColor=Color.FromArgb(255,252,221,146),SurroundColors=new[]{Color.FromArgb(236,138,35,21)}};g.FillPath(core,corePath);
            g.FillEllipse(gold,51,51,15,5);g.FillEllipse(gold,91,51,15,5);
            g.FillRectangle(dark,36,162,32,16);g.FillRectangle(dark,97,162,32,16);
        }
        else
        {
            using var thread=new Pen(Color.FromArgb(165,166,157,133),.8f);for(int i=0;i<4;i++)g.DrawLine(thread,26+i*36,-9,30+i*33,74+(i%2)*40);
            using var wood=new LinearGradientBrush(new Rectangle(42,18,78,144),Color.FromArgb(116,71,72),Color.FromArgb(32,23,35),90);
            using var torso=new GraphicsPath();torso.AddBezier(48,67,61,54,105,54,116,72);torso.AddLine(116,72,128,150);torso.AddBezier(128,150,93,166,61,166,33,150);torso.CloseFigure();g.DrawPath(shadow,torso);g.FillPath(wood,torso);g.DrawPath(edge,torso);
            g.FillEllipse(dark,47,18,68,58);g.DrawEllipse(edge,47,18,68,58);
            using var mask=new SolidBrush(Color.FromArgb(225,205,168));g.FillEllipse(mask,53,24,56,43);
            g.FillEllipse(dark,62,38,11,8);g.FillEllipse(dark,88,38,11,8);g.DrawArc(edge,70,43,25,15,0,180);
            using var arms=new Pen(Color.FromArgb(175,152,121),9){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(arms,48,79,14,110+MathF.Sin(t*.045f)*7);g.DrawLine(arms,114,79,149,95+MathF.Cos(t*.045f)*8);
            using var baton=new Pen(accent,2);g.DrawLine(baton,149,95,157,62);
            for(int i=0;i<5;i++)g.DrawLine(edge,48+i*15,95,40+i*19,144);
            g.FillEllipse(gold,72,94,17,17);g.FillRectangle(dark,47,158,24,19);g.FillRectangle(dark,91,158,24,19);
        }
        g.Restore(saved);
    }
}
