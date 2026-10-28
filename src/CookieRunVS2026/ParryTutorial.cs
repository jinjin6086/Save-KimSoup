namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Real input lessons; stage progress, health and clear time remain paused.
    // 0 hands, 1 jump, 2 double jump, 3 slide, 4 reflect, 5 Q, 6 completed.
    private int roundOneLesson,roundOneParryPhase;
    private float roundOneLessonClock,roundOneLearningClock,roundOneParryClock,roundOneSlideHeld;
    private Projectile? roundOneParryTrace;
    private bool roundOneParryMissed,roundOneJumpDone,roundOneDoubleDone,roundOneQUsed,roundOneQDodged,roundOneSkillBorrowed;
    private ActivePower roundOneSavedSkill;
    private float roundOneSavedCooldown,roundOneSavedTimer;
    private bool RoundOneOnboardingActive=>state==GameState.Round&&round==1&&!practiceActive&&roundOneLesson<6;
    private bool RoundOneParryTutorialActive=>RoundOneOnboardingActive&&roundOneLesson==4&&roundOneParryPhase is 1 or 2;
    private void RestoreTutorialSkill()
    {
        if(!roundOneSkillBorrowed)return;
        activeSkill=roundOneSavedSkill;activeCooldown=roundOneSavedCooldown;activeTimer=roundOneSavedTimer;
        skillVisual=0;BeginMoonwalkDodge();roundOneSkillBorrowed=false;
    }
    private void ResetRoundOneParryTutorial()
    {
        RestoreTutorialSkill();roundOneLesson=roundOneParryPhase=0;
        roundOneLessonClock=roundOneLearningClock=roundOneParryClock=roundOneSlideHeld=0;
        roundOneParryTrace=null;roundOneParryMissed=roundOneJumpDone=roundOneDoubleDone=roundOneQUsed=roundOneQDodged=false;
    }
    private void SetRoundOneLesson(int lesson)
    {
        roundOneLesson=lesson;roundOneLessonClock=roundOneSlideHeld=0;
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();roundOneParryTrace=null;
        py=GroundY-PlayerHeight;vy=0;onGround=true;sliding=false;jumps=0;
        parryWindow=parryCooldown=0;ReleaseInputs();audio.StopSliding();slideSoundActive=false;
        if(lesson==4)BeginRoundOneParryTutorial();
        if(lesson==5)
        {
            roundOneSavedSkill=activeSkill;roundOneSavedCooldown=activeCooldown;roundOneSavedTimer=activeTimer;
            roundOneSkillBorrowed=true;activeSkill=ActivePower.Dodge;activeTimer=activeCooldown=0;
        }
    }
    private void BeginRoundOneParryTutorial()
    {
        roundOneParryPhase=1;roundOneParryClock=0;
        bossX=W-202;bossY=GroundY-143;warningTimer=0;
    }
    private void FinishRoundOneParryTutorial()=>SetRoundOneLesson(5);
    private void CompleteRoundOneOnboarding()
    {
        RestoreTutorialSkill();roundOneLesson=6;roundOneParryPhase=3;
        projectiles.Clear();roundOneParryTrace=null;obstacles.Clear();risingObstacles.Clear();
        parryWindow=parryCooldown=0;obstacleSpawnTimer=90;
        py=GroundY-PlayerHeight;vy=0;onGround=true;sliding=false;jumps=0;
        ReleaseInputs();audio.StopSliding();slideSoundActive=false;
        augmentNotice="조작 연습 완료 · 이제 직접 달려보세요";augmentFlashTimer=120;
    }
    private bool HandleRoundOneParryTutorialKey(Keys key)
    {
        if(!RoundOneOnboardingActive||key!=Keys.Enter)return false;
        CompleteRoundOneOnboarding();return true;
    }
    private bool UpdateRoundOneParryTutorial(float step)
    {
        if(!RoundOneOnboardingActive)return false;
        roundOneLearningClock+=step;roundOneLessonClock+=step;
        parryWindow=Math.Max(0,parryWindow-step);parryCooldown=Math.Max(0,parryCooldown-step);
        UpdatePlayer(step);
        if(roundOneLesson==0){if(roundOneLessonClock>=90)SetRoundOneLesson(1);return true;}
        if(roundOneLesson==1)
        {
            if(jumps>=1&&!onGround)roundOneJumpDone=true;
            if(roundOneJumpDone&&onGround&&roundOneLessonClock>=35)SetRoundOneLesson(2);
            return true;
        }
        if(roundOneLesson==2)
        {
            if(jumps>=2&&!onGround)roundOneDoubleDone=true;
            if(roundOneDoubleDone&&onGround&&roundOneLessonClock>=45)SetRoundOneLesson(3);
            return true;
        }
        if(roundOneLesson==3)
        {
            if(sliding&&onGround)roundOneSlideHeld+=step;
            if(roundOneSlideHeld>=24&&roundOneLearningClock>=360)SetRoundOneLesson(4);
            return true;
        }
        if(roundOneLesson==5)
        {
            if(DodgeActive)roundOneQUsed=true;
            activeCooldown=Math.Max(0,activeCooldown-step);activeTimer=Math.Max(0,activeTimer-step);
            UpdateObjects(step);UpdateMoonwalkDodge(step);UpdateParryHits();
            if(roundOneQUsed&&!roundOneQDodged&&roundOneParryTrace==null)
            {
                roundOneParryTrace=new Projectile(px+185,py+24,AttackType.BlackBall,28,-5,0,0);
                projectiles.Add(roundOneParryTrace);
            }
            if(roundOneParryTrace!=null&&PlayerHitBox.IntersectsWith(roundOneParryTrace.Rect))
            {
                if(DodgeActive){roundOneQDodged=true;roundOneLessonClock=0;}
                projectiles.Clear();roundOneParryTrace=null;
            }
            if(roundOneParryTrace is {IsParried:true}){projectiles.Clear();roundOneParryTrace=null;}
            if(!DodgeActive&&!roundOneQDodged){activeCooldown=0;roundOneQUsed=false;projectiles.Clear();roundOneParryTrace=null;}
            if(roundOneQDodged&&roundOneLessonClock>=70)CompleteRoundOneOnboarding();
            return true;
        }
        roundOneParryClock+=step;
        if(roundOneParryPhase==2)
        {
            UpdateObjects(step);
            if(projectiles.RemoveAll(p=>p.IsParried&&BossHitBox.IntersectsWith(p.Rect))>0)Burst(bossX+65,bossY+65,Gold,14);
            if(roundOneParryClock>=100)FinishRoundOneParryTutorial();return true;
        }
        if(roundOneParryTrace==null&&roundOneParryClock>=45)
        {
            roundOneParryTrace=new Projectile(W-150,GroundY-PlayerHeight+24,AttackType.BlackBall,28,-5,0,0);
            projectiles.Add(roundOneParryTrace);
        }
        UpdateObjects(step);UpdateParryHits();
        if(roundOneParryTrace is {IsParried:true})
        {roundOneParryPhase=2;roundOneParryClock=0;roundOneParryTrace=null;audio.Play("reward");}
        else if(roundOneParryTrace!=null&&roundOneParryTrace.X+roundOneParryTrace.W<KickHitBox.Left)
        {
            projectiles.Clear();roundOneParryTrace=null;roundOneParryClock=0;
            roundOneParryMissed=true;parryWindow=parryCooldown=0;
        }
        return true;
    }
    private void DrawRoundOneParryTutorial(Graphics g)
    {
        if(!RoundOneOnboardingActive)return;
        PanelSurface(g,new(160,106,680,140),Gold);
        string[] titles={"손부터 편하게 놓으세요","1. 점프","2. 공중에서 한 번 더","3. 슬라이딩","4. 이상진의 흔적 · 반사 연습","5. Q 스킬 연습"};
        Label(g,titles[roundOneLesson],new(180,115,640,27),17,Gold);
        string help=roundOneLesson switch
        {
            0=>"왼손: Q / W / E · 엄지: SPACE     오른손: ↑ / ↓",
            1=>"↑를 눌러 점프하고 착지해 보세요. 낮은 장애물을 넘을 수 있어요.",
            2=>"↑로 점프한 뒤, 공중에서 ↑를 한 번 더 눌러 높이 뛰세요.",
            3=>"↓를 누르고 있어 보세요. 떠 있는 장애물은 슬라이딩으로 지나가요.",
            4 when roundOneParryPhase==2=>"반사 성공! 검은 탄환이 이상진에게 되돌아갑니다.",
            4 when roundOneParryMissed=>"탄환이 금색 범위에 닿을 때 다시 반사해 보세요. 놓쳐도 괜찮아요.",
            4=>"이상진이 남긴 검은 탄환이 연한 금색 범위에 닿으면 SPACE / SHIFT로 반사하세요.",
            _ when roundOneQDodged=>"회피 성공! Q 도중에도 SPACE / SHIFT로 반사할 수 있어요.",
            _=>"Q로 ‘피한다’를 사용해 탄환을 피하세요. 지금은 연습용 스킬이에요."
        };
        Label(g,help,new(180,151,640,43),12,Paper);
        string detail=roundOneLesson switch
        {
            0=>"W / E는 감옥 보스의 리듬 입력 · SHIFT도 발차기 가능",
            4=>"발차기 판정 약 0.17초 · 기본 재사용 1초 · 반사 성공 시 즉시 재사용",
            5=>"1라운드 클리어 후 Q 선택 · 2라운드 클리어 후 패시브 선택",
            _=>"입력한 동작을 직접 완료하면 다음 연습으로 넘어갑니다."
        };
        Label(g,detail,new(180,197,640,21),11,Gold);
        Label(g,"안전 연습: 피해 0 · 체력 / 시간 정지    ENTER 전체 연습 건너뛰기",new(180,222,640,17),10,Steel);
        if(roundOneLesson==0)
        {
            PanelSurface(g,new(250,278,500,94),Gold);
            Label(g,"[ Q ]   [ W ] [ E ]                     [ ↑ ]",new(275,292,450,25),18,Paper);
            Label(g,"       [ SPACE / SHIFT ]                  [ ↓ ]",new(275,327,450,25),15,Gold);
        }
        if(roundOneLesson==4)
        {
            Label(g,"이상진의 흔적",new(W-221,GroundY-172,194,22),11,Steel);
            if(enemy3!=null)DrawSpectralBoss(g,enemy3,new(W-202,GroundY-143,125,125),Color.FromArgb(170,206,225),.28f);
        }
    }
}
