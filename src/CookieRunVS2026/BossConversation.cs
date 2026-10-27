using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly record struct BossConversationLine(bool Boss, string Text, int Tone=0);
    // 0: arrive/recognize; 1: conversation; 3: mandatory mechanics; 2: dissolve.
    private int bossTalkPhase, bossTalkLine, bossTalkPhaseKind, bossEntranceFoot = -1;
    private float bossTalkClock, bossTalkInputLock;
    private bool bossTalkRevealed, bossTalkHelp, bossTalkIsPhase;
    private Bitmap? bossTalkDepartureFrame;
    private Image? storyHeroPortrait;
    private Image? storyRodianPortrait,storyRodianMenheraPortrait;
    private readonly PrivateFontCollection storyFontCollection = new();
    private readonly Dictionary<float, Font> storyFonts = new();
    private bool storyResourcesLoaded;
    private float BossEntranceDuration => briefingRound == 5 ? 204 : 186;
    private float BossEntrancePlayerX
    {
        get
        {
            float t=Math.Clamp(bossTalkClock/108,0,1);
            // Constant running pace, then a gradual 24-frame deceleration.
            float distance=t<.74f?t*(.85f/.74f):.85f+.15f*(1-MathF.Pow(1-(t-.74f)/.26f,2));
            return -90+(px+90)*distance;
        }
    }
    private Color BossConversationAccent => briefingRound switch
    {
        2 => Color.FromArgb(171,194,206),3=>Color.FromArgb(215,133,92),
        4=>Color.FromArgb(224,153,185),_=>FinalBossAccent
    };
    private BossConversationLine[] BossConversationLines => storyDefeatTalk
        ? round==2?IsangjinDepartureConversation:PurgaeDepartureConversation
        : bossTalkIsPhase
        ? bossTalkPhaseKind==1?PurgaeRageConversation:RodianMenheraConversation
        : briefingRound switch
        {
            2=>IsangjinConversation,3=>PurgaeConversation,4=>RodianConversation,
            _=>FinalBossKind switch{0=>TruePurgaeConversation,1=>GolemConversation,_=>DollConversation}
        };
    private static readonly BossConversationLine[] PurgaeRageConversation =
    {
        new(true,"하.. 정말 귀찮게 구는군..",1),
        new(false,"이젠 변신까지 하네.. 하.. 내 돼지.. 살아는 있는거냐?"),
        new(true,"그래.. 저 깊은 지하에 신선하게 살려놨지.."),
        new(false,"그래?"),new(false,"이제 진짜 끝내줄께")
    };
    private static readonly BossConversationLine[] RodianMenheraConversation =
    {
        new(true,"왜…"),new(true,"왜!!!!",2),new(false,"(속으로) 어우 쏏"),
        new(true,"왜 내 공연을 보려고 하지 않는거야??!? 그깟 돼지가 내 공연보다 가치있다는 거야????",1),
        new(false,"당연하지."),new(true,"...."),new(true,"가질 수 없다면.."),
        new(true,"부서버리겠어!!!",2)
    };
    private static readonly BossConversationLine[] IsangjinConversation =
    {
        new(true,"어제 훔친 돼지를 닮았군.. 아빠인가??"),new(false,"뭣."),
        new(false,"헛소리 하지말고 김스프의 위치나 말해라.. 죽기 싫으면"),
        new(true,"그래.."),new(true,"싫은데ㅋ"),new(false,"그럼 뭐 말할 때까지 패면 되겠네")
    };
    private static readonly BossConversationLine[] PurgaeConversation =
    {
        new(true,"뭐냐 이 인간은.."),new(false,"내 돼지 내놔"),new(true,"내 양분이 될 가축을 네게?"),
        new(false,"뭔.. 개소리야.."),new(true,"그 돼지는 곧 내 힘을 되찾을 양분으로 쓰일 뿐이다.."),
        new(true,"하찮은 인간 주제에 꺼져라.."),new(false,"넌 나를 화나게 했다.")
    };
    private static readonly BossConversationLine[] RodianConversation =
    {
        new(true,"풀어줘서 고마워... 너가 내 남편을 죽였구나??",3),
        new(false,"사모님.. 왜 갇혀있는지는 모르겠는데 그 좀 비켜 주실래요??"),
        new(true,"...안돼.. 넌 내 공연의 영원한 청중이 되는거야…^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^",3),
        new(false,"이건 진짜 위험한데..")
    };
    private static readonly BossConversationLine[] TruePurgaeConversation =
    {
        new(true, "다시 만났군. 이번엔 시간마저 내 편이다."),
        new(false, "살아 있었네. 이번엔 정말 끝내자."),
        new(true, "멈춘 시간 속에서, 네가 할 수 있는 일이 있을까?"),
        new(false, "다시 움직이는 순간을 기다리면 되겠지.")
    };
    private static readonly BossConversationLine[] GolemConversation =
    {
        new(true, "이 불길 너머로는 아무도 못 간다."),
        new(false, "돌아갈 길은 내가 정해."),
        new(true, "그 발밑까지 전부 태워 주마."),
        new(false, "그 전에 네 불부터 꺼 줄게.")
    };
    private static readonly BossConversationLine[] DollConversation =
    {
        new(true, "공연은 계속돼. 관객은 자리를 떠날 수 없어."),
        new(false, "오늘 공연은 여기까지야."),
        new(true, "앙코르. 앙코르. 마지막 음이 끝날 때까지."),
        new(false, "알았어. 내가 끝내 줄게.")
    };

    private void EnsureStoryResources()
    {
        if(storyResourcesLoaded)return;
        storyResourcesLoaded=true;
        string font=AssetPath("DialogueJua-Regular.ttf");
        if(File.Exists(font))storyFontCollection.AddFontFile(font);
        storyHeroPortrait=LoadImage("HeroDialogueCutout.png");
        storyRodianPortrait=LoadImage("RodianDialogueNormal.png");
        storyRodianMenheraPortrait=LoadImage("RodianDialogueMenhera.png");
    }
    private Font StoryFont(float size)
    {
        EnsureStoryResources();
        if(!storyFonts.TryGetValue(size,out var font))
        {
            font=storyFontCollection.Families.Length>0
                ?new Font(storyFontCollection.Families[0],size,FontStyle.Regular,GraphicsUnit.Pixel)
                :new Font("Malgun Gothic",size,FontStyle.Regular,GraphicsUnit.Pixel);
            storyFonts[size]=font;
        }
        return font;
    }
    private void StoryLabel(Graphics g,string text,RectangleF rect,float size,Color color,bool centered=false)
    {
        using var brush=new SolidBrush(color);
        g.DrawString(text,StoryFont(size),brush,rect,centered?centerLabel:leftLabel);
    }
    private void DisposeStoryResources()
    {
        storyHeroPortrait?.Dispose();storyHeroPortrait=null;
        storyRodianPortrait?.Dispose();storyRodianMenheraPortrait?.Dispose();
        storyRodianPortrait=storyRodianMenheraPortrait=null;
        foreach(var font in storyFonts.Values)font.Dispose();storyFonts.Clear();
        storyFontCollection.Dispose();
    }
    private void ResetBossConversation()
    {
        bossTalkDepartureFrame?.Dispose();bossTalkDepartureFrame=null;
        bossTalkPhase=bossTalkLine=bossTalkPhaseKind=0;bossEntranceFoot=-1;
        bossTalkClock=bossTalkInputLock=0;
        bossTalkRevealed=bossTalkHelp=bossTalkIsPhase=false;storyDefeatTalk=false;
    }
    private void BeginBossConversation()=>ResetBossConversation();
    private void BeginPhaseConversation(int kind)
    {
        ResetBossConversation();
        bossBriefingActive=true;briefingRound=round;briefingClock=0;
        bossTalkIsPhase=true;bossTalkPhaseKind=kind;
        bossTalkPhase=1;bossTalkInputLock=18;
        ReleaseInputs();
    }
    private void UpdateBossConversation(float step)
    {
        if(bossTalkHelp)return;
        bossTalkClock+=step;bossTalkInputLock=Math.Max(0,bossTalkInputLock-step);
        if(bossTalkPhase==0)
        {
            int foot=(int)(bossTalkClock/10);
            if(bossTalkClock<92&&foot!=bossEntranceFoot)
            {
                bossEntranceFoot=foot;
                if(audio.ActiveVoices<5)audio.Play($"step{(round==1?0:round==2?1:2)}{foot%2}");
            }
            if(bossTalkClock>=BossEntranceDuration)
            {
                bossTalkPhase=1;bossTalkClock=0;bossTalkInputLock=12;
            }
        }
        else if(bossTalkPhase==2&&bossTalkClock>=36)
        {
            bossBriefingActive=false;sliding=false;audio.StopSliding();slideSoundActive=false;
            sceneFadeTimer=0;parryWindow=0;
            if(!bossTalkIsPhase)warningTimer=0;
            if(storyDefeatTalk){storyDefeatTalk=false;storyDefeatDone=true;BeginBossDefeat();return;}
            bossTalkDepartureFrame?.Dispose();bossTalkDepartureFrame=null;
        }
    }
    private int BossConversationVisible=>bossTalkRevealed?BossConversationLines[bossTalkLine].Text.Length:
        Math.Clamp((int)((bossTalkClock-12)/1.8f),0,BossConversationLines[bossTalkLine].Text.Length);
    private bool HandleBossConversationKey(Keys key)
    {
        if(key==Keys.F1&&bossTalkPhase==1)
        {bossTalkHelp=!bossTalkHelp;return true;}
        if(bossTalkHelp)
        {
            if(key is Keys.Enter or Keys.Space or Keys.F1)bossTalkHelp=false;
            return true;
        }
        if(key is not(Keys.Enter or Keys.Space)||bossTalkInputLock>0)return true;
        if(bossTalkPhase==0)
        {
            // Allow recognition to finish; never skip straight from offscreen.
            if(bossTalkClock>=150){bossTalkPhase=1;bossTalkClock=0;bossTalkInputLock=12;}
            return true;
        }
        if(bossTalkPhase==3)
        {
            bossTalkPhase=2;bossTalkClock=0;audio.Play("select");return true;
        }
        if(bossTalkPhase!=1)return true;
        bossTalkInputLock=10;
        if(BossConversationVisible<BossConversationLines[bossTalkLine].Text.Length)
        {bossTalkRevealed=true;return true;}
        if(bossTalkLine+1<BossConversationLines.Length)
        {bossTalkLine++;bossTalkClock=0;bossTalkRevealed=false;}
        else
        {
            bossTalkPhase=bossTalkIsPhase||storyDefeatTalk?2:3;bossTalkClock=0;audio.Play("select");
        }
        return true;
    }
    // Ending and boss conversations share this panel, including the rounded face.
    private void DrawStoryDialogue(Graphics g,string speaker,string text,int visible,string hint,bool showCue,float clock, int tone=0)
    {
        StoryLabel(g,speaker,new(65,455,820,28),18,Color.FromArgb(221,183,122));
        using var body=new SolidBrush(tone is 1 or 2?Color.FromArgb(255,91,94):tone==3?Color.FromArgb(255,173,215):Color.FromArgb(238,227,207));
        string shown=text[..Math.Clamp(visible,0,text.Length)];
        if(shown.Length>0&&char.IsHighSurrogate(shown[^1]))shown=shown[..^1];
        int crying=shown.IndexOf("😭",StringComparison.Ordinal);
        string bodyText=crying<0?shown:shown[..crying];
        g.DrawString(bodyText,StoryFont(tone==2?27:21),body,
            new RectangleF(65,493,850,65),endingDialogueFormat);
        if(crying>=0)
        {
            // Jua has no emoji glyph. Draw a tiny crying emote here instead of
            // shipping a missing-character box or changing the approved font.
            float x=65+g.MeasureString(bodyText,StoryFont(21),850).Width,y=505;
            using var face=new SolidBrush(Color.FromArgb(242,202,101));g.FillEllipse(face,x,y-10,21,21);
            using var ink=new Pen(Color.FromArgb(107,69,31),1.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawArc(ink,x+3,y-5,5,4,180,150);g.DrawArc(ink,x+13,y-5,5,4,210,150);
            g.DrawEllipse(ink,x+8,y+2,5,6);
            using var tears=new Pen(Color.FromArgb(115,192,227),3){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(tears,x+5,y,x+5,y+9);g.DrawLine(tears,x+16,y,x+16,y+9);
        }
        StoryLabel(g,hint,new(652,563,282,23),12,Color.FromArgb(145,130,110),true);
        if(showCue&&MathF.Sin(clock*.065f)>-.3f)
        {
            using var cue=new SolidBrush(Color.FromArgb(205,173,117));
            g.FillPolygon(cue,new[]{new PointF(938,574),new PointF(945,574),new PointF(941.5f,579)});
        }
    }
    private void DrawBossEntrance(Graphics g)
    {
        DrawArenaGround(g);DrawBoss(g);
        float x=BossEntrancePlayerX;
        float stop=EndingSmooth(Math.Clamp((bossTalkClock-91)/20,0,1));
        float bob=MathF.Abs(MathF.Sin(bossTalkClock*.3f))*1.8f*(1-stop);
        var rect=new RectangleF(x-9,py-bob,PlayerWidth+18,PlayerHeight);
        if(selectedCookie==0&&motionFrames.Length>=4)
        {
            float cycle=bossTalkClock/5;int a=(int)cycle%4,b=(a+1)%4;
            float blend=cycle-MathF.Floor(cycle);
            if(stop<1)
            {
                DrawDefeatImage(g,motionFrames[a],rect,1-stop);
                if(blend>.65f)DrawDefeatImage(g,motionFrames[b],rect,(blend-.65f)/.35f*.65f*(1-stop));
            }
            if(stop>0&&endingIdle!=null)
                DrawDefeatImage(g,endingIdle,new(x+4,py-1,PlayerWidth-2,PlayerHeight),stop);
        }
        else DrawSimpleCookie(g,(int)x,(int)py,PlayerWidth,PlayerHeight);
        if(bossTalkClock is >=118 and <160)
        {
            float k=MathF.Sin(Math.Clamp((bossTalkClock-118)/42,0,1)*MathF.PI);
            StoryLabel(g,"!",new(x+14,py-40-k*5,24,31),25,Color.FromArgb((int)(240*k),241,217,162),true);
        }
        float bars=EndingSmooth(Math.Clamp(bossTalkClock/26,0,1));
        using var black=new SolidBrush(Color.FromArgb((int)(240*bars),8,7,7));
        g.FillRectangle(black,0,0,W,38);g.FillRectangle(black,0,H-38,W,38);
        StoryLabel(g,$"라운드 {briefingRound}  ·  {BriefingBossName}",new(48,7,850,24),16,Gold);
    }
    private void DrawBossConversation(Graphics g)
    {
        if(bossTalkPhase==0){DrawBossEntrance(g);return;}
        if(bossTalkPhase!=2){DrawBossConversationScene(g);return;}
        if(bossTalkDepartureFrame==null)
        {
            bossTalkDepartureFrame=new Bitmap(W,H,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var capture=Graphics.FromImage(bossTalkDepartureFrame);
            DrawBackground(capture);DrawBossConversationScene(capture);
        }
        DrawWorld(g);
        float fade=1-EndingSmooth(Math.Clamp(bossTalkClock/36,0,1));
        DrawDefeatImage(g,bossTalkDepartureFrame,new(0,0,W,H),fade);
    }
    private void DrawBossConversationScene(Graphics g)
    {
        EnsureStoryResources();
        var saved=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        // Dialogue portraits replace the small combat actors, rather than
        // superimposing two different drawings of the same boss.
        DrawArenaGround(g);
        float fade=bossTalkPhase==2?1:EndingSmooth(Math.Clamp(bossTalkClock/22,0,1));
        using(var shade=new SolidBrush(Color.FromArgb((int)(190*fade),5,7,10)))g.FillRectangle(shade,0,0,W,H);
        var actors=g.Save();g.SetClip(new Rectangle(0,57,W,382));
        float breath=reducedEffects?0:MathF.Sin(briefingClock*.035f)*1.2f;
        var spoken=BossConversationLines[bossTalkLine];
        float heroAlpha=(spoken.Boss?.63f:1)*fade,bossAlpha=(spoken.Boss?1:.63f)*fade;
        if(storyHeroPortrait!=null)
        {
            float h=410,w=h*storyHeroPortrait.Width/storyHeroPortrait.Height;
            DrawDefeatImage(g,storyHeroPortrait,new(55-20*(1-fade),59+breath,w,h),heroAlpha);
        }
        else if(endingIdle!=null)DrawDefeatImage(g,endingIdle,new(143,154,150,255),heroAlpha);
        var target=new RectangleF(587+20*(1-fade),76-breath,330,330);
        Image? image=briefingRound switch
        {
            2=>enemy4,3=>PurgaePoseImage((bossTalkIsPhase&&bossTalkPhaseKind==1)||(storyDefeatTalk&&purgaeTransformed)?1:0,0),
            4=>bossTalkIsPhase?(storyRodianMenheraPortrait??melodyMenhera??melodyAngry??enemy5):(storyRodianPortrait??enemy5),_=>null
        };
        if(briefingRound==4&&image!=null&&(image==storyRodianPortrait||image==storyRodianMenheraPortrait))
        {
            // Portrait-only placement: preserve its native tall aspect ratio;
            // combat sprites and hitboxes keep their original dimensions.
            float h=410,w=h*image.Width/image.Height;
            target=new RectangleF(615+20*(1-fade),59-breath,w,h);
        }
        if(briefingRound==5)DrawFinalBossPortrait(g,target);
        else if(image!=null)DrawDefeatImage(g,image,target,bossAlpha);
        using(var bottomFade=new LinearGradientBrush(new Rectangle(0,388,W,51),Color.Transparent,Color.FromArgb(255,8,7,7),90f))
            g.FillRectangle(bottomFade,0,388,W,51);
        g.Restore(actors);
        using(var bar=new SolidBrush(Color.FromArgb(255,8,7,7)))
        {g.FillRectangle(bar,0,0,W,57);g.FillRectangle(bar,0,439,W,161);}
        using(var line=new Pen(Color.FromArgb(80,202,171,119),.8f))
        {g.DrawLine(line,48,57,952,57);g.DrawLine(line,48,439,952,439);}
        StoryLabel(g,storyDefeatTalk?"마지막 대화":bossTalkIsPhase?(bossTalkPhaseKind==1?"광폭화":"멘헤라"):$"라운드 {briefingRound}",new(48,14,152,27),15,Gold);
        StoryLabel(g,BriefingBossName,new(280,11,440,33),23,Color.FromArgb(237,220,187),true);
        if(!bossTalkIsPhase&&!storyDefeatTalk)StoryLabel(g,"대화 후 기믹 안내",new(792,16,160,22),12,Steel,true);
        int visible=bossTalkPhase==2?spoken.Text.Length:BossConversationVisible;
        bool ready=visible>=spoken.Text.Length;
        string hint=!ready?"ENTER / SPACE   문장 보기":bossTalkLine==BossConversationLines.Length-1
            ?storyDefeatTalk?"ENTER / SPACE   다음":bossTalkIsPhase?"ENTER / SPACE   전투 계속":"ENTER / SPACE   보스 설명":"ENTER / SPACE   다음";
        string storyBossName=briefingRound==3&&(storyDefeatTalk&&purgaeTransformed||bossTalkIsPhase&&bossTalkPhaseKind==1)?"광폭화 파괴옥":
            briefingRound==4&&bossTalkIsPhase?"멘헤라 강로디언":BriefingBossName;
        DrawStoryDialogue(g,spoken.Boss?storyBossName:"나",spoken.Text,visible,hint,ready,briefingClock,spoken.Tone);
        g.Restore(saved);
    }
}
