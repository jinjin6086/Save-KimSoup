using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private int phaseIntroKind;
    private float phaseIntroClock,gravityClock,gravityMirrorTimer,gravityCue;
    private bool phaseCleanupPending;
    private const float PhaseIntroDuration=198;
    private bool PhaseIntroActive=>phaseIntroKind!=0;
    private bool GravityMirrored=>round==2&&gravityMirrorTimer>0;
    private Image? enemy6;
    private float counterFxTimer,counterOriginX,counterOriginY,counterTargetX,counterTargetY;
    private readonly List<BossEcho> bossEchoes=new();
    private float echoClock;
    private sealed class BossEcho { public float X,Y,Life=36;public Image? Image;public Color Color; }

    private void ResetBattleDirector()
    {
        phaseIntroKind=0;phaseIntroClock=0;phaseCleanupPending=false;
        gravityClock=gravityMirrorTimer=gravityCue=counterFxTimer=echoClock=0;
        ResetIsangjinAcceleration();
        bossEchoes.Clear();worldMirroring=false;
    }

    private void BeginPhaseIntro(int kind)
    {
        if((kind==1&&round==3)||(kind==2&&round==4))
        {
            ResetCinderRound();augmentNotice="변신 · 취약 초기화";augmentFlashTimer=70;
        }
        CancelAbilityEchoes();
        phaseIntroKind=kind;phaseIntroClock=0;phaseCleanupPending=true;
        ReleaseInputs();audio.CancelLaserCharge();laserActive=dashActive=false;
        screenFlashTimer=cameraShakeTimer=hitStopTimer=cinematicTimer=0;
        audio.Play("phase");
    }

    private bool UpdatePhaseIntro(float step)
    {
        if(!PhaseIntroActive)return false;
        if(phaseCleanupPending)
        {
            phaseCleanupPending=false;projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
            purgaeSkill2Active=false;Array.Fill(purgaeSkill2BallVisible,false);bossEchoes.Clear();
        }
        float previous=phaseIntroClock;phaseIntroClock+=step;
        if(previous<90&&phaseIntroClock>=90)audio.Play("phasebreak");
        if(phaseIntroClock>=PhaseIntroDuration)
        {
            int completedKind=phaseIntroKind;
            phaseIntroKind=0;menheraIntro=0;phaseIntroClock=0;cameraShakeTimer=screenFlashTimer=parryFxTimer=0;
            warningTimer=75;encounterMessage=round==3?"광폭화 · 방어를 무너뜨리세요":"멘헤라 · 더 거센 공격이 시작됩니다";
            attackTimer=0;invuln=Math.Max(invuln,45);ReleaseInputs();
            if(!practiceActive)BeginPhaseConversation(completedKind);
        }
        return true;
    }

    private void UpdateDirectorGameplay(float step)
    {
        if(round!=2)return;
        gravityClock+=step;gravityMirrorTimer=Math.Max(0,gravityMirrorTimer-step);
        gravityCue=Math.Max(0,gravityCue-step);
        if(gravityClock>=600)
        {
            gravityClock-=600;gravityMirrorTimer=240;gravityCue=50;
            audio.Play("gravity");TriggerCameraShake(2,5);
        }
    }

    private void UpdateDirectorPresentation(float step)
    {
        counterFxTimer=Math.Max(0,counterFxTimer-step*(cinematicTimer>0?.24f:1));
        for(int i=bossEchoes.Count-1;i>=0;i--){bossEchoes[i].Life-=step;if(bossEchoes[i].Life<=0)bossEchoes.RemoveAt(i);}
        if(state!=GameState.Round||round!=4||!menheraMode||dashActive||PhaseIntroActive)return;
        echoClock-=step;
        if(echoClock<=0)
        {
            echoClock+=3;
            if(bossEchoes.Count>=12)bossEchoes.RemoveAt(0);
            Color tint=((int)(menheraWanderClock/24)%4) switch{0=>Color.FromArgb(166,123,235),1=>Color.FromArgb(99,192,214),2=>Color.FromArgb(244,113,181),_=>Color.FromArgb(245,176,124)};
            bossEchoes.Add(new(){X=bossX,Y=bossY,Image=CurrentBossImage,Color=tint});
        }
    }

    private void DrawBossEchoes(Graphics g)
    {
        if(round!=4||!menheraMode||reducedEffects)return;
        foreach(var e in bossEchoes)if(e.Image!=null)
        {
            float k=e.Life/36;
            DrawSpectralBoss(g,e.Image,new(e.X,e.Y,155,155),e.Color,k*k*.28f);
        }
    }

    private void DrawCinematicPose(Graphics g,Image image,RectangleF rect,float alpha)
    {
        readableMatrix.Matrix33=alpha;readableAlpha.SetColorMatrix(readableMatrix);
        g.DrawImage(image,Rectangle.Round(rect),0,0,image.Width,image.Height,GraphicsUnit.Pixel,readableAlpha);
    }

    private void DrawPhaseIntro(Graphics g)
    {
        if(!PhaseIntroActive)return;
        float t=phaseIntroClock,enter=Math.Clamp(t/24,0,1),exit=Math.Clamp((PhaseIntroDuration-t)/24,0,1),weight=Math.Min(enter,exit);
        Color accent=phaseIntroKind==1?Color.FromArgb(255,125,49):Color.FromArgb(255,91,157);
        using var dim=new SolidBrush(Color.FromArgb((int)(210*weight),5,8,16));g.FillRectangle(dim,0,0,W,H);
        using var oval=new GraphicsPath();oval.AddEllipse(240,92,520,430);
        using var halo=new PathGradientBrush(oval){CenterColor=Color.FromArgb((int)(75*weight),accent),SurroundColors=new[]{Color.Transparent}};g.FillPath(halo,oval);
        float focus=1-MathF.Pow(1-enter,3),reveal=Math.Clamp((t-68)/45,0,1),shock=MathF.Exp(-Math.Abs(t-94)*.055f);
        float size=185+80*focus+shock*25,x=bossX+(500-bossX-size*.5f)*focus,y=bossY+(285-bossY-size*.5f)*focus;
        var pose=g.Save();g.TranslateTransform(x+size*.5f,y+size*.5f);
        g.RotateTransform((1-reveal)*-6+MathF.Sin(t*.55f)*shock*2);g.ScaleTransform(1+shock*.035f,1+MathF.Sin(Math.Clamp(t/90,0,1)*MathF.PI)*.08f);
        Image? old=phaseIntroKind==1?enemy3:enemy5,next=phaseIntroKind==1?enemy7??enemy3:PhaseNextImage;
        var rect=new RectangleF(-size*.5f,-size*.5f,size,size);
        if(old!=null&&reveal<1)DrawCinematicPose(g,old,rect,(1-reveal)*weight);
        if(next!=null&&reveal>0)DrawCinematicPose(g,next,rect,reveal*weight);
        g.Restore(pose);
        if(t>=90&&t<125)
        {
            float r=80+(t-90)*11;using var wave=new Pen(Color.FromArgb((int)(200*(1-(t-90)/35)),accent),4);
            g.DrawEllipse(wave,500-r,280-r,r*2,r*2);
        }
        using var bars=new SolidBrush(Color.FromArgb((int)(255*weight),2,4,9));g.FillRectangle(bars,0,0,W,64*weight);g.FillRectangle(bars,0,H-64*weight,W,64*weight);
        Label(g,phaseIntroKind==1?"파괴옥 / 광폭화":"강로디언 / 멘헤라",new(200,73,600,28),16,accent);
        Label(g,t<90?"2페이즈 시작":"공격 강화",new(220,430,560,35),25,Paper);
        Label(g,phaseIntroKind==1?"피해 50% 감소 · 5회 명중 시 5초간 방어 해제":"가질 수 없다면 부숴버리겠어..",new(140,474,720,43),phaseIntroKind==1?20:26,Paper);
    }

    private void DrawGravityOverlay(Graphics g)
    {
        if(round!=2||state!=GameState.Round)return;
        if(gravityClock>=555&&!GravityMirrored)
            Label(g,$"중력반전 예고  {(600-gravityClock)/60:0.0}",new(345,111,310,35),19,Gold);
        if(GravityMirrored)
        {
            using var strip=new SolidBrush(Color.FromArgb(195,8,18,34));g.FillRoundRectangle(strip,new Rectangle(348,110,304,40),10,10);
            Label(g,$"↕  상하 중력반전  {gravityMirrorTimer/60:0.0}초",new(340,116,320,28),17,Color.LightSkyBlue);
            if(gravityCue>25){using var flash=new SolidBrush(Color.FromArgb((int)((gravityCue-25)*2),150,199,255));g.FillRectangle(flash,0,150,W,350);}
        }
    }

    private void BeginLaserCounter()
    {
        counterOriginX=px+PlayerWidth-2;counterOriginY=py+31;
        counterTargetX=bossX+65;counterTargetY=bossY+65;counterFxTimer=43;
        audio.Play("counterbeam");Burst(counterOriginX,counterOriginY,Color.LightCyan,34);
    }

    private void DrawCounterLaser(Graphics g)
    {DrawGrandLaser(g,true);}

    private void DrawCounterFeedback(Graphics g)
    {
        if(counterFxTimer<=0)return;
        float age=43-counterFxTimer;
        Label(g,"레이저 반사",new(px-28,py-45,180,25),14,Color.LightCyan);
        if(age<20)
        {
            float fade=1-age/20;
            for(int i=0;i<12;i++)
            {
                float a=i*2.399f,r=8+age*(1+i%4*.3f);
                g.FillEllipse(ParticleBrush(Color.White,(int)(210*fade)),counterOriginX+MathF.Cos(a)*r,counterOriginY+MathF.Sin(a)*r*.6f,2,2);
            }
        }
        if(age>=19)
        {
            float r=16+(age-19)*3,alpha=Math.Clamp(counterFxTimer/24,0,1);
            for(int i=0;i<14;i++)
            {
                float a=i*2.399f;
                g.FillEllipse(ParticleBrush(Color.LightCyan,(int)(220*alpha)),counterTargetX+MathF.Cos(a)*r,counterTargetY+MathF.Sin(a)*r*.7f,2,2);
            }
        }
    }
}

