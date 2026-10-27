using System.Drawing.Drawing2D;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private const int LastStandTarget=70;
    private const float LastStandClashZoom=1.65f;
    private const float LastStandLimit=600,LastStandMinimum=600;
    // Six monologue lines need twelve seconds to read. The ten-second input
    // window and every cue relative to the hopeful music remain unchanged.
    private const float LastStandMemoryFrames=720,LastStandGatherFrames=210,LastStandIgniteFrames=36,LastStandFinishFrames=FinaleTiming.Finish;
    // Despair -> rage -> charge -> rush -> remembered promise -> gather -> ignition -> ten-second
    // clash -> overpower. Clocks are real simulation frames, independent of slow.
    private int lastStandPhase,lastStandPresses,lastStandAttempts,lastStandBeat=-1,lastStandSuccessBeat;
    private float lastStandClock,lastStandRealClock,lastStandLastPress=-100,lastStandPressure,lastStandPulse,lastStandSoundClock;
    private float lastVictoryPressure,lastVictoryZoom,lastVictoryX,lastVictoryY;
    private bool lastStandResolved;
    private float lastHeroX,lastHeroY,lastBossX,lastBossY;
    private bool MelodyLastStandActive=>state==GameState.Round&&round==4&&lastStandPhase>0;
    private void ResetMelodyLastStand()
    {
        audio.StopClashSounds();audio.SetFinaleMix(1);
        lastStandPhase=lastStandPresses=lastStandAttempts=lastStandSuccessBeat=0;lastStandBeat=-1;
        lastStandClock=lastStandRealClock=lastStandPressure=lastStandPulse=lastStandSoundClock=0;
        lastStandLastPress=-100;lastStandResolved=false;rodianRageReleased=false;rodianEntrySource=null;rodianEntryFrame?.Dispose();rodianEntryFrame=null;finaleBackdrop?.Dispose();finaleBackdrop=null;
    }
    private bool TryBeginMelodyLastStand()
    {
        if(round!=4||PracticeLocksPhase||lastStandResolved||lastStandPhase>0||bossHitRequired-bossDamage>=1)return false;
        bossDamage=Math.Min(bossDamage,bossHitRequired-1);augmentFlashTimer=0;
        CaptureRodianQteEntry();
        lastStandPhase=12;lastStandClock=lastStandRealClock=0;lastStandBeat=-1;rodianRageReleased=false;
        lastStandPresses=lastStandAttempts=0;lastStandPressure=.12f;lastStandLastPress=-100;
        CancelAbilityEchoes();CancelMeteorStorm(true);projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        audio.CancelLaserCharge();audio.StopSliding();audio.StopClashSounds();
        laserActive=dashActive=fullRush=sparkActive=false;melodyStep=-1;melodyBurstActive=false;
        phaseIntroKind=0;phaseCleanupPending=false;cinematicTimer=hitStopTimer=cameraShakeTimer=screenFlashTimer=0;
        focusEntryTimer=focusExitTimer=0;parryWindow=parryCooldown=0;sliding=false;menheraMode=true;
        lastHeroX=selectedCookie==0?FinalePlayerAnchorX+95:FinalePlayerAnchorX+91;lastHeroY=selectedCookie==0?py-39:py-8;lastBossX=FinaleBossAnchorX-10;lastBossY=FinaleBossAnchorY+55;
        if(rodianLastVowMotion.Length>0){var muzzle=RodianLastVowPoint(5,RodianMicrophones);var body=FinaleBossBounds;lastBossX=body.X+body.Width*muzzle.X;lastBossY=body.Y+body.Height*muzzle.Y;}
        lastStandPulse=0;lastStandSoundClock=0;bossMessage="";bossShoutTimer=0;
        audio.SetFinaleMix(.12f);ReleaseInputs();return true;
    }
    private void SetLastStandPhase(int phase,string cue)
    {
        lastStandPhase=phase;lastStandClock=0;lastStandSoundClock=0;lastStandBeat=-1;lastStandSuccessBeat=0;
        if(phase!=4)audio.StopClashSounds();if(cue.Length>0)audio.Play(cue);
        audio.SetFinaleMix(phase==10?.015f:phase==11?.025f:phase==3?.025f:phase==8?.10f:.20f);
    }
    private bool UpdateMelodyLastStand(float step)
    {
        if(!MelodyLastStandActive)return false;
        if(lastStandPhase==12)
        {
            lastStandClock+=step;
            if(lastStandClock>=RodianEntryHoldFrames)SetLastStandPhase(10,"clashdespair");
            audio.Collect();return true;
        }
        bool impactHold=lastStandPhase==5&&lastStandClock>=FinaleTiming.Impact&&lastStandClock<FinaleTiming.Explosion;
        if(!impactHold){lastStandRealClock+=step;animationClock+=step*(lastStandPhase==3?.025f:.48f);}
        lastStandClock+=step;lastStandPulse=Math.Max(0,lastStandPulse-step);
        switch(lastStandPhase)
        {
            case 10:
                if(lastStandClock>=RodianDespairFrames)SetLastStandPhase(11,"");
                break;
            case 11:
                if(lastStandClock>=RodianRageRelease&&!rodianRageReleased){rodianRageReleased=true;audio.Play("clashrage");lastStandPulse=16;}
                if(lastStandClock>=RodianRageFrames)SetLastStandPhase(1,"clashcharge");
                break;
            case 1:
                if(lastStandClock>=150)SetLastStandPhase(2,"clashapproach");
                break;
            case 2:
                if(lastStandClock>=18){SetLastStandPhase(3,"clashmemory");lastStandPulse=12;}
                break;
            case 3:
                int heart=(int)(lastStandClock/53);
                if(heart!=lastStandBeat){lastStandBeat=heart;audio.Play("clashheart");}
                if(lastStandClock>=LastStandMemoryFrames)SetLastStandPhase(8,"clashhero");
                break;
            case 8:
                if(lastStandClock>=LastStandGatherFrames){SetLastStandPhase(9,"clashignite");lastStandPulse=18;}
                break;
            case 9:
                if(lastStandClock>=LastStandIgniteFrames)SetLastStandPhase(4,"");
                break;
            case 4:
                float fraction=Math.Clamp(lastStandPresses/(float)LastStandTarget,0,1);
                float target=.12f+fraction*.76f;
                if(lastStandRealClock-lastStandLastPress>27)target=Math.Max(.05f,target-.09f);
                lastStandPressure+=(target-lastStandPressure)*(1-MathF.Pow(.92f,step));
                lastStandSoundClock-=step;
                if(lastStandSoundClock<=0){audio.PlayClashSustain();lastStandSoundClock=4;}
                if(lastStandClock>=LastStandLimit)
                {
                    if(lastStandPresses>=LastStandTarget)
                    {
                        var cam=LastStandCamera;lastVictoryZoom=cam.Zoom;lastVictoryX=cam.X;lastVictoryY=cam.Y;lastVictoryPressure=lastStandPressure;
                        SetLastStandPhase(5,"");audio.PlayClashSurge();lastStandPulse=25;
                    }
                    else
                    {
                        SetLastStandPhase(6,"clashfail");lastStandAttempts++;
                        invuln=0;ReceiveHit(1800,"최후의 탈덕안돼",ClashPink,true);
                    }
                }
                break;
            case 5:
                lastStandPressure=Mix(lastVictoryPressure,.984f,SmoothUnit(lastStandClock/108));
                if(lastStandClock>=FinaleTiming.Impact&&lastStandSuccessBeat<1){lastStandSuccessBeat=1;audio.PlayClashBreak();}
                if(lastStandClock>=FinaleTiming.Silence&&lastStandClock<FinaleTiming.Explosion)audio.SetFinaleMix(0);
                if(lastStandClock>=FinaleTiming.Explosion&&lastStandSuccessBeat<2){lastStandSuccessBeat=2;audio.PlayClashExplosion();lastStandPulse=30;}
                if(lastStandClock>=LastStandFinishFrames)
                {
                    lastStandResolved=true;bossDamage=bossHitRequired;
                    // Let the long explosion tail cross the cut into recovery.
                    if(practiceActive)practiceClears++;else score+=5000;
                    lastStandPhase=0;BeginBossDefeat();bossDefeatClock=0;
                }
                break;
        }
        audio.Collect();return true;
    }
    private bool HandleMelodyLastStandKey(Keys key)
    {
        if(!MelodyLastStandActive)return false;
        if(key==Keys.Space)
        {
            if(lastStandPhase==6&&lastStandClock>=30)
            {
                lastStandPresses=0;lastStandPressure=.12f;lastStandLastPress=-100;
                SetLastStandPhase(8,"clashhero");return true;
            }
            if(lastStandPhase==4&&lastStandRealClock-lastStandLastPress>=3)
            {
                lastStandPresses=Math.Min(LastStandTarget,lastStandPresses+1);
                lastStandLastPress=lastStandRealClock;lastStandPulse=9;
                if(lastStandPresses%3==1)audio.Play("clashtap");
            }
            return true;
        }
        return key is Keys.Q or Keys.Up or Keys.Down or Keys.ShiftKey or Keys.Enter;
    }
    private static float SmoothUnit(float t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    private static float Mix(float a,float b,float t)=>a+(b-a)*t;
    private (float Zoom,float X,float Y) LastStandCamera
    {
        get
        {
            float bx=FinaleBossAnchorX+65,by=FinaleBossAnchorY+70,hx=FinalePlayerAnchorX+64,hy=py;
            if(lastStandPhase==10){float t=SmootherUnit(lastStandClock/72);return(Mix(1,2.65f,t),Mix(500,FinaleBossAnchorX+39,t),Mix(300,FinaleBossAnchorY+78,t));}
            if(lastStandPhase==11){float t=SmootherUnit((lastStandClock-RodianRageRelease)/58);return(Mix(2.65f,2.38f,t),Mix(FinaleBossAnchorX+39,FinaleBossAnchorX+53,t),Mix(FinaleBossAnchorY+78,FinaleBossAnchorY+70,t));}
            if(lastStandPhase==1){float t=SmootherUnit(lastStandClock/150);return(Mix(2.38f,1.95f,t),Mix(FinaleBossAnchorX+53,bx,t),by);}
            if(lastStandPhase==2){float t=SmootherUnit((lastStandClock-3)/15);return(Mix(1.95f,3.05f,t),Mix(bx,hx,t),Mix(by,hy-3,t));}
            if(lastStandPhase==3)return(3.05f+.08f*SmoothUnit(lastStandClock/LastStandMemoryFrames),hx,hy-3);
            if(lastStandPhase is 8 or 9)return(3.13f,hx,hy-3);
            float cx=Mix(lastHeroX,lastBossX,lastStandPressure),cy=Mix(lastHeroY,lastBossY,lastStandPressure);
            float focusX=Mix((lastHeroX+lastBossX)*.5f,cx,.65f),focusY=Mix(300,cy,.80f);
            if(lastStandPhase==4)
            {
                float t=SmoothUnit(lastStandClock/72);
                return(Mix(3.13f,LastStandClashZoom,t),Mix(hx,focusX,t),Mix(hy-3,focusY,t));
            }
            if(lastStandPhase==5)
            {
                float t=SmoothUnit(lastStandClock/60),overshoot=MathF.Sin(t*MathF.PI)*.13f;
                float z=Mix(lastVictoryZoom>0?lastVictoryZoom:LastStandClashZoom,1,t)-overshoot;
                if(lastStandClock>=FinaleTiming.Explosion)z+=MathF.Exp(-(lastStandClock-FinaleTiming.Explosion)*.065f)*.045f;
                return(z,Mix(lastVictoryZoom>0?lastVictoryX:focusX,500,t),Mix(lastVictoryZoom>0?lastVictoryY:focusY,300,t));
            }
            return(1,500,300);
        }
    }
    private RectangleF FinaleHeroBounds=>new(FinalePlayerAnchorX-40,py-67,143,143);
    private RectangleF FinaleBossBounds
    {
        get{float push=lastStandPhase==5?SmoothUnit((lastStandClock-40)/145)*28:0;return new(FinaleBossAnchorX-17+push,FinaleBossAnchorY-3,185,185);}
    }
    private float FinaleHeroPose=>lastStandPhase switch{8=>SmoothUnit((lastStandClock-42)/66),9=>1+SmoothUnit(lastStandClock/18),4 or 5=>2,_=>0};
    private PointF FinaleHeroSocket
    {
        get{if(selectedCookie!=0)return new(lastHeroX,lastHeroY);var r=FinaleHeroBounds;float p=SmoothUnit(Math.Min(1,FinaleHeroPose));return new(Mix(r.X+r.Width*.60f,lastHeroX,p),Mix(r.Y+r.Height*.44f,lastHeroY,p));}
    }
    private PointF FinaleBossSocket
    {
        get{if(rodianLastVowMotion.Length>0)return RodianLastVowSocket;var r=FinaleBossBounds;float t=lastStandPhase==1?SmoothUnit((lastStandClock-110)/40):1;return new(Mix(r.X+r.Width*.33f,lastBossX,t),Mix(r.Y+r.Height*.49f,lastBossY,t));}
    }
    private void DrawFinaleActors(Graphics g)
    {
        if(!DrawRodianLastVowActor(g))
        {
            var br=FinaleBossBounds;float ba=lastStandPhase==5?1-SmoothUnit((lastStandClock-FinaleTiming.Explosion)/28):1;
            int pose=lastStandPhase==1?0:1;
            if(finaleBossPoses.Length==3)DrawEchoFrame(g,finaleBossPoses[pose],br,ba);
            else if(melodyMenhera!=null)DrawReadableSprite(g,melodyMenhera,br,ClashPink,ba);
        }
        // During her final words the shot belongs to the boss alone.
        if(RodianSoloShot)return;
        var hr=FinaleHeroBounds;DrawCloneAura(g,hr);float power=lastStandPhase==8?SmoothUnit(lastStandClock/LastStandGatherFrames):lastStandPhase is 9 or 4 or 5?1:0;
        if(power>0)
        {
            if(selectedCookie==0&&finaleHeroPoses.Length==3)
            {
                float pp=FinaleHeroPose;int ii=Math.Min(2,(int)pp);float aa=pp-ii;
                DrawAttachedFire(g,finaleHeroPoses[ii],hr,power*(1-aa),lastStandRealClock*.95f);
                if(aa>.002f&&ii<2)DrawAttachedFire(g,finaleHeroPoses[ii+1],hr,power*aa,lastStandRealClock*.95f);
            }
            else if(selectedCookie==1&&wolfPoses.Length==12)DrawAttachedFire(g,wolfPoses[lastStandPhase is 9 or 4 or 5?11:9],new(FinalePlayerAnchorX-32,py-41,130,120),power,lastStandRealClock*.95f);
            else DrawAttachedFire(g,null,new(FinalePlayerAnchorX,py,PlayerWidth,PlayerHeight),power,lastStandRealClock*.95f);

        }
        if(selectedCookie==0&&finaleHeroPoses.Length==3)
        {
            float p=FinaleHeroPose;int i=Math.Min(2,(int)p);float a=p-i;
            float reveal=lastStandPhase==2?SmootherUnit(lastStandClock/8):1;
            DrawPlayerSprite(g,finaleHeroPoses[i],hr,ClashBlue,(1-a)*reveal);
            if(a>.002f&&i<2)DrawPlayerSprite(g,finaleHeroPoses[i+1],hr,ClashBlue,a*reveal);
        }
        else if(selectedCookie==1&&wolfPoses.Length==12)DrawWolfFinale(g);
        else DrawSimpleCookie(g,(int)FinalePlayerAnchorX,(int)py,PlayerWidth,PlayerHeight);
        if(lastStandPhase==3&&LastStandMemoryIndex==1)DrawFinaleSweat(g,hr);
    }
    private void DrawMelodyLastStand(Graphics g)
    {
        if(lastStandPhase==12){DrawRodianEntryFrame(g,1);return;}
        if(lastStandPhase==7)
        {
            DrawRodianExplosionFinisher(g);
            Label(g,"연습 성공 · TAB 재시작 / ESC 설정",new(100,552,800,42),20,Paper);
            return;
        }
        var cam=LastStandCamera;var scene=g.Save();
        g.InterpolationMode=InterpolationMode.Bilinear;
        float shake=FinaleShake;
        using(var outside=new SolidBrush(Color.FromArgb(3,5,12)))g.FillRectangle(outside,0,0,W,H);
        g.TranslateTransform(500+MathF.Sin(lastStandRealClock*2.7f)*shake,300+MathF.Cos(lastStandRealClock*2.1f)*shake*.5f);
        g.ScaleTransform(cam.Zoom,cam.Zoom);g.TranslateTransform(-cam.X,-cam.Y);
        DrawFinaleBackdrop(g);
        DrawRodianLastVowStage(g);
        if(lastStandPhase==1)
        {
            var b=FinaleBossSocket;float p=SmoothUnit(lastStandClock/150);
            DrawFinaleGather(g,b.X,b.Y,p,ClashPink,true);
            DrawLaserBloom(g,b.X,b.Y,35*(1-p)+13,35*(1-p)+13,ClashPink,p);
        }
        if(lastStandPhase==1)
        {
            float p=SmoothUnit(lastStandClock/150),f=(lastStandRealClock*.65f)%32;int i=(int)f;
            var br=FinaleBossBounds;float breadth=1.17f-p*.15f;
            var r=new RectangleF(br.X-br.Width*(breadth-1)*.5f,br.Y-25,br.Width*breadth,br.Height+27);
            if(pinkFinaleFire[i]!=null)DrawEchoFrame(g,pinkFinaleFire[i],r,p*p*.73f);
        }
        DrawFinaleActors(g);
        if(lastStandPhase==1)
        {
            var socket=FinaleBossSocket;float p=SmoothUnit(lastStandClock/150);
            DrawFinaleGather(g,socket.X,socket.Y,p,ClashPink,true);
            DrawLaserBloom(g,socket.X,socket.Y,8+p*13,8+p*14,ClashPink,p);
            if(lastStandClock>126&&!reducedEffects)DrawLaserBloom(g,socket.X,socket.Y,62,3,Color.FromArgb(255,203,231),SmoothUnit((lastStandClock-126)/24)*.8f);
        }
        PointF h=FinaleHeroSocket,boss=FinaleBossSocket;
        float dx=boss.X-lastHeroX,dy=boss.Y-lastHeroY;
        if(lastStandPhase is 2 or 3 or 8)
        {
            float p=lastStandPhase==2?SmoothUnit(lastStandClock/12):1;
            float proximity=lastStandPhase==3?Mix(37,28,SmoothUnit(lastStandClock/LastStandMemoryFrames)):lastStandPhase==8?28:37;
            float len=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy)),t=p*(1-proximity/len);
            float xx=Mix(boss.X,lastHeroX,t),yy=Mix(boss.Y,lastHeroY,t);
            DrawClashBeam(g,boss.X,boss.Y,xx,yy,78,ClashPink,1,false);
            DrawLaserBloom(g,xx,yy,29,31,ClashPink,.85f);
            if(lastStandPhase==8)DrawFinaleGather(g,h.X,h.Y,SmoothUnit(lastStandClock/LastStandGatherFrames),ClashBlue,true);
        }
        else if(lastStandPhase is 9 or 4 or 5)
        {
            float pressure=lastStandPhase==9?.045f:lastStandPressure;
            float cx=lastHeroX+dx*pressure,cy=lastHeroY+dy*pressure;
            float force=lastStandPhase==5?1-SmoothUnit((lastStandClock-55)/63):1;
            DrawClashBeam(g,boss.X,boss.Y,cx,cy,84,ClashPink,force,false);
            float ignition=lastStandPhase==9?SmoothUnit(lastStandClock/9):1;
            float thickness=72+Math.Min(1,lastStandPulse/9)*6;
            if(lastStandPhase!=5||lastStandClock<FinaleTiming.Explosion)
            {
                DrawClashBeam(g,h.X,h.Y,cx,cy,thickness,ClashBlue,ignition,true);
                DrawClashContact(g,cx,cy,(1.08f+Math.Min(1,lastStandPulse/9)*.13f)*ignition);
            }
            if(lastStandPhase==5)
            {
                DrawFinaleVictory(g,h,boss,cx,cy,thickness);
            }
        }
        else if(lastStandPhase==6)DrawClashBeam(g,boss.X,boss.Y,lastHeroX-90,lastHeroY+dy*.1f,92,ClashPink,1-SmoothUnit(lastStandClock/44),false);
        g.Restore(scene);
        if(lastStandPhase==10&&lastStandClock<24)DrawRodianEntryFrame(g,1-SmootherUnit(lastStandClock/24));
        if(lastStandPhase==5&&lastStandClock>=FinaleTiming.Impact&&lastStandClock<FinaleTiming.Explosion)DrawFinaleImpactFrames(g,cam);
        // The vignette is already composited into the opaque stage cache.

        if(!reducedEffects&&lastStandPhase==9&&lastStandClock<9)
        {
            // A localized optical exposure, not the removed ordinary kick flash.
            float a=(1-lastStandClock/9)*.42f;
            DrawLaserBloom(g,480,310,590,180,ClashBlue,a);
        }
        DrawLastStandHud(g);
        DrawRodianLastVowDialogue(g);
        if(lastStandPhase==3)DrawLastStandMemory(g);
    }
    private void DrawLastStandMemory(Graphics g)
    {
        float t=lastStandClock/LastStandMemoryFrames;
        // The remembered prisoner appears behind the close-up as a dim image,
        // with a restrained photographic wipe instead of a dialogue panel.
        if(endingFriend!=null&&t<.72f)
        {
            float a=MathF.Sin(Math.Clamp(t/.72f,0,1)*MathF.PI)*.42f;
            DrawEchoFrame(g,endingFriend,new(700,150,145,137),a);
            for(int i=0;i<5;i++)g.DrawLine(ParticlePen(Color.FromArgb(126,150,176),(int)(210*a),3),704+i*32,141,704+i*32,296);
        }
        string line=LastStandMemoryLines[LastStandMemoryIndex];
        float local=(t*LastStandMemoryLines.Length)%1;
        float fade=Math.Min(1,local*10)*Math.Min(1,(1-local)*10);
        Label(g,line,new(100,547,800,45),t<.72f?23:27,Color.FromArgb((int)(255*fade),229,239,250));
    }
    private void DrawLastStandHud(Graphics g)
    {
        float bar=lastStandPhase==4?44:52;
        if(lastStandPhase==10)bar*=SmootherUnit(lastStandClock/32);
        using var black=new SolidBrush(Color.FromArgb(248,2,3,7));g.FillRectangle(black,0,0,W,bar);g.FillRectangle(black,0,H-bar,W,bar);
        if(lastStandPhase==4)
        {
            Label(g,"강로디언 · 최후발악",new(28,9,420,27),13,Color.FromArgb(225,161,194),false);
            Label(g,$"{hp:0} / {PlayerMaxHealth:0}",new(748,9,225,27),13,Paper);
            float left=Math.Max(0,(LastStandLimit-lastStandClock)/60),amount=lastStandPresses/(float)LastStandTarget;
            using var border=new Pen(Color.FromArgb(131,187,220),1);g.DrawRectangle(border,239,565,85,26);
            Label(g,"SPACE",new(240,565,83,25),13,Color.FromArgb(203,233,255));
            Label(g,$"연타   {lastStandPresses} / {LastStandTarget}",new(337,562,262,31),18,Paper,false);
            Label(g,$"{left:0.0}초",new(648,562,116,31),17,Paper);
            using var track=new SolidBrush(Color.FromArgb(40,58,82));g.FillRectangle(track,30,548,940,3);
            using var fill=new SolidBrush(ClashBlue);g.FillRectangle(fill,30,548,940*amount,3);
        }
        else
        {
            string line=lastStandPhase switch{1=>"",2=>"",8=>lastStandClock>=24?"내 가족이라고….!":"",9=>"",5=>lastStandClock<FinaleTiming.Impact?"밀어붙여!":"",6=>"SPACE  다시 도전",7=>"연습 성공 · TAB 재시작 / ESC 설정",_=>""};
            if(line.Length>0)Label(g,line,new(100,552,800,42),lastStandPhase==8?26:21,lastStandPhase==1?ClashPink:Paper);
            if(lastStandPhase==8||lastStandPhase==9)Label(g,"곧 SPACE 연타",new(695,15,275,27),13,Color.FromArgb(146,188,223));
        }
    }
}
