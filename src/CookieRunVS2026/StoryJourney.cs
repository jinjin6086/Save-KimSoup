using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Story scenes own their clock and inputs. Combat, the clear timer and QTE
    // never advance behind them; existing rewards and checkpoints remain intact.
    private int journeyKind,journeyScene,journeyLine,journeyNext,journeyFoot=-1;
    private float journeyClock,journeyTextClock,journeyLock;
    private bool journeyRevealed,storyDefeatTalk,storyDefeatDone,journeyCue;
    private Bitmap? journeyBedroom,journeySleepingBedroom,journeyRoutes;
    private Image? journeyAngryHero;
    private readonly Bitmap?[] journeyWorlds=new Bitmap?[4];
    private bool journeyLoaded;
    private const float JourneyTravelFrames=330;
    private readonly record struct JourneyLine(string Speaker,string Text,int Tone=0);
    private static readonly JourneyLine[][] OpeningLines=
    {
        Array.Empty<JourneyLine>(),
        new[]{new JourneyLine("나","김스프? …김스프!"),new JourneyLine("나","어디 간 거야?")},
        new[]{new JourneyLine("나","쪽지…?"),new JourneyLine("이상진의 쪽지","잘 쓸게요~")},
        new[]{new JourneyLine("나","마왕성… 뒤에 주소까지 적어 놨네."),new JourneyLine("나","기다려. 지금 간다.",1)},
        Array.Empty<JourneyLine>(),
        new[]{new JourneyLine("나","왜 김스프를 긴빠이 해갔을까…"),new JourneyLine("나","무사해야 할텐데.."),new JourneyLine("나","그 자식.. 잡히면 가만 안둬..",1)}
    };
    private static readonly JourneyLine[] FirstGateLines=
    {
        new("나","여긴가.."),new("나","후.."),new("나","가자.")
    };
    private static readonly BossConversationLine[] IsangjinDepartureConversation=
    {
        new(false,"야 죽기전에 위치는 말하고 죽어라?"),
        new(true,"내 뒤쪽에 있는 문을 열면 김스프에게 향할 수 있지.."),
        new(false,"그래 이제 죽으렴"),new(true,"흐흐.. 어짜피 넌 군주님을 꺾을 수 없을거다..")
    };
    private static readonly BossConversationLine[] PurgaeDepartureConversation=
    {
        new(true,"정말 강한녀석이군 너는.."),new(false,"아으 따가워.. 진짜 기분나쁘네"),
        new(true,"남겨진 내 아내보다 강할지는 모르겠지만 넌 강하다"),
        new(false,"아.. 또 있나 보내..ㅉ"),
        new(true,"삶을 더 연명하고자 하는 욕심에 더 비참한 최후를 맞이하게 되어버렸구나..")
    };
    private JourneyLine[] JourneyLines=>journeyKind==1?OpeningLines[journeyScene]:FirstGateLines;
    private bool JourneyHasDialogue=>journeyKind==3||(journeyKind==1&&journeyScene is 1 or 2 or 3 or 5);
    private float JourneyDialogueWait=>journeyKind==3?90:journeyScene==1?100:journeyScene is 2 or 3?62:40;
    private int JourneyVisible=>journeyRevealed?JourneyLines[journeyLine].Text.Length:
        Math.Clamp((int)((journeyTextClock-JourneyDialogueWait)/1.8f),0,JourneyLines[journeyLine].Text.Length);
    private int JourneyMusicKey=>journeyKind==1?(journeyScene>=4?1:0):journeyKind==3?1:
        journeyClock<188?(round==4?12:round==3?13:round==5?13:round):journeyNext switch{2=>2,3=>13,4=>12,5=>FinalBossKind==0?13:FinalBossKind==1?3:4,_=>2};

    private void EnsureJourneyResources()
    {
        if(journeyLoaded)return;journeyLoaded=true;EnsureStoryResources();
        journeyBedroom=LoadEndingBackdrop("StoryBedroom.png");
        journeySleepingBedroom=LoadEndingBackdrop("StoryBedroomSleeping.png");
        journeyAngryHero=LoadImage("StoryHeroAngry.png");
        string routes=AssetPath("StoryRoutes.png");
        if(File.Exists(routes))journeyRoutes=new Bitmap(routes);
        for(int i=0;i<4;i++)journeyWorlds[i]=LoadEndingBackdrop(WorldFiles[i]);
    }
    private void DisposeJourneyResources()
    {
        journeyBedroom?.Dispose();journeySleepingBedroom?.Dispose();journeyRoutes?.Dispose();journeyAngryHero?.Dispose();
        foreach(var b in journeyWorlds)b?.Dispose();
    }
    private void ResetStoryJourney()
    {
        journeyKind=journeyScene=journeyLine=journeyNext=0;
        journeyClock=journeyTextClock=journeyLock=0;journeyFoot=-1;journeyRevealed=journeyCue=false;
    }
    private void OpenJourney(int kind,int next)
    {
        EnsureJourneyResources();ResetStoryJourney();journeyKind=kind;journeyNext=next;
        state=GameState.StoryScene;journeyLock=20;sceneFadeTimer=0;
        ReleaseInputs();audio.StopSliding();slideSoundActive=false;
        audio.CancelLaserCharge();audio.StopDepartureSounds();
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        laserActive=dashActive=sparkActive=false;burnTimer=hitStopTimer=cameraShakeTimer=screenFlashTimer=0;
    }
    private void BeginOpeningStory(){round=1;ResetEncounterFlow();OpenJourney(1,1);}
    private void BeginFirstGateStory()
    {
        if(practiceActive){BeginRoundTransition(2,false);return;}
        OpenJourney(3,2);
    }
    private void BeginStageTravel(int next)
    {
        if(practiceActive){StartRound(next);return;}
        ResetEncounterFlow();OpenJourney(2,next);
    }
    private void SetOpeningScene(int scene)
    {
        journeyScene=scene;journeyLine=0;journeyClock=journeyTextClock=0;
        journeyLock=16;journeyRevealed=journeyCue=false;journeyFoot=-1;ReleaseInputs();
        if(scene==2)audio.Play("select");
    }
    private void UpdateStoryJourney(float step)
    {
        journeyClock+=step;journeyTextClock+=step;journeyLock=Math.Max(0,journeyLock-step);
        if(journeyKind==1)
        {
            if(journeyScene==0&&journeyClock>=390){SetOpeningScene(1);return;}
            if(journeyScene==4&&journeyClock>=180){SetOpeningScene(5);return;}
            if(journeyScene==1&&!journeyCue&&journeyClock>=72){journeyCue=true;audio.Play("select");}
            if(journeyScene is 4 or 5)JourneyFootsteps(step,0);
        }
        else if(journeyKind==2)
        {
            if(journeyClock<156||journeyClock>222)JourneyFootsteps(step,journeyNext is 3 or 4?2:1);
            if(!journeyCue&&journeyClock>=168){journeyCue=true;audio.Play("select");}
            if(journeyClock>=JourneyTravelFrames)FinishStageTravel();
        }
    }
    private void JourneyFootsteps(float step,int surface)
    {
        int foot=(int)(journeyClock/15);
        if(foot==journeyFoot)return;journeyFoot=foot;
        if(audio.ActiveVoices<6)audio.Play($"step{surface}{foot%2}");
    }
    private void FinishStageTravel()
    {
        int next=journeyNext;ResetStoryJourney();
        if(next==0){OpenFinalBossChoice();return;}
        if(next<0){BeginRescueEnding();return;}
        StartRound(next);sceneFadeTimer=18;
    }
    private bool HandleStoryJourneyKey(Keys key)
    {
        if(state!=GameState.StoryScene)return false;
        if(key is not(Keys.Space or Keys.Enter)||journeyLock>0)return true;
        if(journeyKind==2)
        {
            // Do not jump out of an entrance while the runner is still outside.
            if(journeyClock>=292)FinishStageTravel();return true;
        }
        if(!JourneyHasDialogue)return true;
        if(journeyClock<JourneyDialogueWait)return true;
        journeyLock=12;
        if(JourneyVisible<JourneyLines[journeyLine].Text.Length){journeyRevealed=true;return true;}
        if(journeyLine+1<JourneyLines.Length)
        {journeyLine++;journeyRevealed=false;journeyTextClock=JourneyDialogueWait;return true;}
        if(journeyKind==3){ResetStoryJourney();BeginRoundTransition(2,false);return true;}
        if(journeyScene==5){StartRound(1);sceneFadeTimer=18;return true;}
        SetOpeningScene(journeyScene+1);return true;
    }
    private void BeginStoryDefeatTalk()
    {
        CancelCinderFlight();ResetTruePurgaeEncounter();ResetIsangjinClock();
        ResetBossConversation();bossBriefingActive=true;briefingRound=round;briefingClock=0;
        storyDefeatTalk=true;bossTalkPhase=1;bossTalkInputLock=24;
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();
        laserActive=purgaeSkill2Active=false;Array.Fill(purgaeSkill2BallVisible,false);
        burnTimer=parryWindow=counterFxTimer=damageFxTimer=hitStopTimer=0;
        audio.CancelLaserCharge();audio.StopSliding();ReleaseInputs();
    }

    private void DrawStoryJourney(Graphics g)
    {
        EnsureJourneyResources();var saved=g.Save();
        g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        if(journeyKind==1)DrawOpeningJourney(g);
        else if(journeyKind==3)DrawFirstGateJourney(g);
        else DrawStageTravel(g);
        if(JourneyHasDialogue)DrawJourneyDialogue(g);
        else
        {
            using var bar=new SolidBrush(Color.FromArgb(244,8,7,7));
            g.FillRectangle(bar,0,0,W,38);g.FillRectangle(bar,0,H-38,W,38);
            if(journeyKind==1&&journeyScene==0)
                StoryLabel(g,"그날 새벽",new(48,9,300,24),16,Color.FromArgb(219,203,174));
            if(journeyKind==2&&journeyClock>245)
                StoryLabel(g,journeyNext is 0 or -1?"지하감옥으로":$"라운드 {journeyNext}  ·  {JourneyDestination}",new(48,9,860,24),16,Gold);
        }
        g.Restore(saved);
    }
    private string JourneyDestination=>journeyNext switch{2=>"감옥",3=>"화산",4=>"지하감옥 앞 황폐한 공연장",5=>"봉인된 심부",_=>"지하감옥"};
    private void JourneyBackdrop(Graphics g,Bitmap? image,float drift=0,float scale=1.04f)
    {
        g.Clear(Color.FromArgb(10,12,16));
        if(image!=null)g.DrawImage(image,new RectangleF((W-W*scale)/2+drift,(H-H*scale)/2,W*scale,H*scale));
        using var gradient=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(12,4,7,14),Color.FromArgb(65,3,4,8),90);
        g.FillRectangle(gradient,0,0,W,H);
    }
    private void DrawJourneyActor(Graphics g,float x,float floor,float height,bool running,float alpha=1,float time=float.NaN)
    {
        float clock=float.IsNaN(time)?journeyClock:time;
        float width=height*.59f;
        DrawEndingShadow(g,x+width*.48f,floor-1,width*.45f,height*.035f);
        if(selectedCookie==1&&wolfPoses.Length==12)
        {DrawWolfPose(g,running?(int)(clock/8)%4:8,new(x-width*.2f,floor-height,width*1.4f,height));return;}
        Image? pose=running&&motionFrames.Length>=4?motionFrames[(int)(clock/7)%4]:endingIdle??player1;
        if(pose==null)return;
        float bob=running?MathF.Abs(MathF.Sin(clock*.28f))*1.6f:MathF.Sin(clock*.036f)*.8f;
        DrawDefeatImage(g,pose,new(x,floor-height-bob,width,height),alpha);
    }
    private void DrawOpeningJourney(Graphics g)
    {
        if(journeyScene==5)
        {
            JourneyBackdrop(g,journeyWorlds[0],-journeyClock*.018f,1.08f);
            DrawJourneyActor(g,265,426,145,true);DrawJourneyAtmosphere(g,0);return;
        }
        JourneyBackdrop(g,journeyScene==0?journeySleepingBedroom??journeyBedroom:journeyBedroom,MathF.Sin(journeyClock*.0025f)*2,1.015f);
        if(journeyScene==0)
        {
            float enter=EndingSmooth(Math.Clamp((journeyClock-48)/100,0,1));
            float leave=EndingSmooth(Math.Clamp((journeyClock-242)/115,0,1));
            float x=925-(925-636)*enter+leave*400;
            float lift=EndingSmooth(Math.Clamp((journeyClock-182)/44,0,1));
            float bend=MathF.Sin(Math.Clamp((journeyClock-150)/95,0,1)*MathF.PI)*8;
            var thief=g.Save();g.TranslateTransform(x,328);g.RotateTransform(-bend);
            if(enemy4!=null)DrawDefeatImage(g,enemy4,new(-45,-115,91,133),1);
            g.Restore(thief);
            float friendX=640+(x-602)*lift,friendY=329-46*lift+MathF.Sin(journeyClock*.045f)*1.1f;
            var friend=g.Save();g.TranslateTransform(friendX,friendY);g.RotateTransform(-10-8*lift);
            DrawEndingFriend(g,new(-35,-27,70,66),false);g.Restore(friend);
            // Eyelids are small curved strokes: KimSoup is asleep, not a prop.
            if(lift<.2f)
            {
                using var eye=new Pen(Color.FromArgb(90,35,25),1.8f);
                g.DrawArc(eye,631,322,8,4,0,180);g.DrawArc(eye,648,322,8,4,0,180);
            }
            if(journeyClock<120)
                StoryLabel(g,"…",new(681,287,40,24),18,Color.FromArgb(190,192,202,219));
            float black=journeyClock<25?1-journeyClock/25:journeyClock>362?(journeyClock-362)/28:0;
            JourneyFade(g,black);return;
        }
        if(journeyScene==1)
        {
            // A short dark cut separates the sleeping shot from the awakened
            // player. Never morph a lying body into a standing cutout.
            DrawJourneyActor(g,308,416,150,false);
            if(journeyClock>72)
            {
                float pulse=.7f+.3f*MathF.Sin(journeyClock*.07f);
                using var pen=new Pen(Color.FromArgb((int)(150*pulse),237,201,136),1.4f);
                g.DrawArc(pen,595,310,99,41,10,160);
                StoryLabel(g,"?",new(360,239,35,40),28,Gold,true);
            }
            JourneyFade(g,1-EndingSmooth(journeyClock/24));return;
        }
        if(journeyScene is 2 or 3)
        {
            using(var dim=new SolidBrush(Color.FromArgb(135,5,8,13)))g.FillRectangle(dim,0,0,W,H);
            Image? portrait=journeyScene==3?journeyAngryHero??storyHeroPortrait:storyHeroPortrait;
            if(portrait!=null)
            {
                float h=390,w=h*portrait.Width/portrait.Height;
                DrawDefeatImage(g,portrait,new(42,73,w,h),.85f);
            }
            DrawJourneyNote(g);return;
        }
        float run=EndingSmooth(Math.Clamp((journeyClock-18)/133,0,1));
        DrawJourneyActor(g,308+run*780,416,150,true);
        JourneyFade(g,EndingSmooth(Math.Clamp((journeyClock-142)/38,0,1)));
    }
    private void DrawJourneyNote(Graphics g)
    {
        float arrive=EndingSmooth(Math.Clamp(journeyClock/42,0,1));
        float flip=journeyScene==3?MathF.Cos(MathF.PI*EndingSmooth(Math.Clamp(journeyClock/54,0,1))):1;
        var saved=g.Save();g.TranslateTransform(652,244+10*(1-arrive));g.RotateTransform(-2+2*arrive);
        // Collapse only while turning; render the reverse at its natural orientation.
        g.ScaleTransform(Math.Max(.015f,Math.Abs(flip))*(.92f+.08f*arrive),1);
        using var shadow=new SolidBrush(Color.FromArgb(90,0,0,0));g.FillRectangle(shadow,-171+9,-139+12,350,280);
        var paper=new RectangleF(-175,-142,350,284);
        using(var parchment=new LinearGradientBrush(paper,Color.FromArgb(246,234,203),Color.FromArgb(197,176,135),66))g.FillRectangle(parchment,paper);
        using(var edge=new Pen(Color.FromArgb(185,143,102),1.3f))g.DrawRectangle(edge,-175,-142,350,284);
        using(var grain=new Pen(Color.FromArgb(17,97,69,41),.7f))
            for(int i=0;i<31;i++)g.DrawLine(grain,-169,-132+i*9,168,-133+i*9);
        bool reverse=journeyScene==3&&flip<0;
        if(reverse)
        {
            StoryLabel(g,"목적지",new(-143,-107,280,27),17,Color.FromArgb(110,77,54));
            StoryLabel(g,"마왕성",new(-145,-56,290,52),39,Color.FromArgb(64,41,33),true);
            StoryLabel(g,"지하 감옥",new(-142,8,284,38),24,Color.FromArgb(88,57,41),true);
            using var rule=new Pen(Color.FromArgb(125,120,78,46),1);g.DrawLine(rule,-121,68,121,68);
            StoryLabel(g,"쪽지 뒷면에 남겨진 주소",new(-141,92,282,23),13,Color.FromArgb(118,89,64),true);
        }
        else
        {
            StoryLabel(g,"잘 쓸게요~",new(-148,-48,296,65),39,Color.FromArgb(65,40,32),true);
            StoryLabel(g,"— 이상진",new(17,72,125,31),19,Color.FromArgb(115,77,47),true);
        }
        g.Restore(saved);
    }
    private void DrawFirstGateJourney(Graphics g)
    {
        DrawJourneyRoute(g,0,Math.Min(journeyClock/160,1)*12);
        float approach=EndingSmooth(Math.Clamp(journeyClock/82,0,1));
        DrawJourneyActor(g,208+approach*118,439,148,journeyClock<80);
        DrawJourneyAtmosphere(g,0);
    }
    private void DrawJourneyRoute(Graphics g,int route,float pan)
    {
        if(journeyRoutes==null){JourneyBackdrop(g,journeyWorlds[Math.Clamp(route,0,3)],-pan,1.06f);return;}
        int cw=journeyRoutes.Width/2,ch=journeyRoutes.Height/2;
        g.DrawImage(journeyRoutes,new RectangleF(-pan,-12,W+40,H+24),new RectangleF(route%2*cw,route/2*ch,cw,ch),GraphicsUnit.Pixel);
        using var shade=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(13,0,0,0),Color.FromArgb(70,3,6,10),90);
        g.FillRectangle(shade,0,0,W,H);
    }
    private void DrawStageTravel(Graphics g)
    {
        float t=journeyClock;
        int route=journeyNext switch{2=>0,3=>1,4=>2,_=>3};
        if(t<202)
        {
            float walk=EndingSmooth(Math.Clamp(t/187,0,1));
            DrawJourneyRoute(g,route,walk*38);DrawJourneyAtmosphere(g,route);
            DrawJourneyActor(g,216+walk*515,445,148,true,1-EndingSmooth(Math.Clamp((t-172)/25,0,1)));
            // An entering runner is occluded by the entrance, then the camera
            // passes the jamb. This is a spatial cut, rather than a title slide.
            float cover=EndingSmooth(Math.Clamp((t-174)/28,0,1));
            using var jamb=new SolidBrush(Color.FromArgb(255,6,8,11));
            g.FillRectangle(jamb,W*(1-cover),0,W*cover,H);
            if(t<85)StoryLabel(g,"저벅저벅",new(260,388,170,27),14,Color.FromArgb((int)(120*MathF.Sin(Math.Clamp(t/85,0,1)*MathF.PI)),224,213,194),true);
        }
        else
        {
            int idx=journeyNext switch{2=>1,3=>2,4=>3,5=>Math.Clamp(FinalBossKind+1,0,3),_=>1};
            float arrive=EndingSmooth(Math.Clamp((t-211)/102,0,1));
            JourneyBackdrop(g,journeyWorlds[idx],16*(1-arrive),1.055f);
            DrawJourneyAtmosphere(g,idx);
            DrawJourneyActor(g,-66+arrive*276,idx==2?429:440,148,true);
            JourneyFade(g,1-EndingSmooth(Math.Clamp((t-202)/28,0,1)));
        }
    }
    private void DrawJourneyAtmosphere(Graphics g,int theme)
    {
        Color c=theme==0?Color.FromArgb(237,225,175):theme==2?Color.FromArgb(233,127,72):Color.FromArgb(161,189,207);
        for(int i=0;i<18;i++)
        {
            float x=((i*83.6f-journeyClock*.2f)%1040+1040)%1040-20;
            float y=82+i*19%330+MathF.Sin(journeyClock*.014f+i)*7;
            g.FillEllipse(ParticleBrush(c,35+i%5*7),x,y,1.5f,1.5f);
        }
    }
    private void DrawJourneyDialogue(Graphics g)
    {
        var line=JourneyLines[journeyLine];int visible=JourneyVisible;bool ready=visible>=line.Text.Length;
        using var bar=new SolidBrush(Color.FromArgb(255,8,7,7));
        g.FillRectangle(bar,0,0,W,45);g.FillRectangle(bar,0,439,W,161);
        using var rule=new Pen(Color.FromArgb(80,202,171,119),.8f);g.DrawLine(rule,48,439,952,439);
        string chapter=journeyKind==3?"마왕성 앞":journeyScene switch{1=>"빈자리",2=>"남겨진 쪽지",3=>"쪽지의 뒷면",_=>"라운드 1  ·  초원"};
        StoryLabel(g,chapter,new(48,10,850,26),16,Gold);
        DrawStoryDialogue(g,line.Speaker,line.Text,visible,ready?"ENTER / SPACE   다음":"ENTER / SPACE   문장 보기",ready,journeyClock,line.Tone);
    }
    private static void JourneyFade(Graphics g,float opacity)
    {
        if(opacity<=0)return;using var black=new SolidBrush(Color.FromArgb((int)(255*Math.Clamp(opacity,0,1)),5,7,11));g.FillRectangle(black,0,0,W,H);
    }
}
