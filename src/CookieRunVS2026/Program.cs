using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace CookieRunVS2026;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GameForm());
    }
}

public sealed class GameForm : Form
{
    private const int BaseWidth = 1000;
    private const int BaseHeight = 600;
    private const int Fps = 60;

    private readonly Timer timer = new() { Interval = 16 };
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private double lastTickSeconds;
    private readonly GamePanel game = new();
    private readonly Button cookieButton1 = new();
    private readonly Button cookieButton2 = new();

    public GameForm()
    {
        Text = "승쿠런 1.17 버전";
        ClientSize = new Size(BaseWidth, BaseHeight);
        MinimumSize = new Size(900, 540);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        game.Dock = DockStyle.Fill;
        Controls.Add(game);

        // 쿠키 선택을 마우스로 확실하게 받을 수 있도록 실제 WinForms 버튼을 오버레이한다.
        SetupCookieButton(cookieButton1, "🍪 1번 쿠키 선택", 0);
        SetupCookieButton(cookieButton2, "🐺 2번 늑대 러너 선택", 1);
        Controls.Add(cookieButton1);
        Controls.Add(cookieButton2);
        cookieButton1.Visible = false;
        cookieButton2.Visible = false;
        cookieButton1.BringToFront();
        cookieButton2.BringToFront();

        // 키보드는 GamePanel 포커스에 의존하지 않고 폼에서 직접 전달한다.
        KeyDown += (_, e) =>
        {
            game.HandleKeyDown(e);
            e.Handled = true;
            e.SuppressKeyPress = true;
        };
        KeyUp += (_, e) =>
        {
            game.HandleKeyUp(e);
            e.Handled = true;
        };

        Shown += (_, _) => game.Focus();
        Deactivate += (_, _) => game.PauseForFocusLoss();
        FormClosed += (_, _) => timer.Dispose();
        Resize += (_, _) => LayoutCookieButtons();

        WindowState = FormWindowState.Maximized;
        lastTickSeconds = stopwatch.Elapsed.TotalSeconds;
        timer.Tick += (_, _) =>
        {
            double now = stopwatch.Elapsed.TotalSeconds;
            float elapsedSeconds = (float)Math.Max(0, now - lastTickSeconds);
            float deltaSeconds = Math.Clamp(elapsedSeconds, 1f / 240f, 0.05f);
            lastTickSeconds = now;

            game.AdvanceClearTimerFromHost(elapsedSeconds);
            game.UpdateGame(deltaSeconds);
            game.Invalidate();
        };
        timer.Start();
    }

    private void SetupCookieButton(Button button, string text, int cookieIndex)
    {
        button.Text = text;
        button.Font = new Font("Malgun Gothic", 15, FontStyle.Bold);
        button.BackColor = Color.HotPink;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 2;
        button.Cursor = Cursors.Hand;
        button.TabStop = false;

        // Click/MouseDown 둘 다 연결해서 어떤 입력 방식에서도 선택되게 한다.
        button.Click += (_, _) => ChooseCookieFromForm(cookieIndex);
        button.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ChooseCookieFromForm(cookieIndex);
        };
    }

    private void ChooseCookieFromForm(int cookieIndex)
    {
        game.SelectCookie(cookieIndex);
        cookieButton1.Visible = false;
        cookieButton2.Visible = false;
        game.Focus();
        game.Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (game.State == GameState.CookieSelect)
        {
            if (key == Keys.D1 || key == Keys.NumPad1)
            {
                ChooseCookieFromForm(0);
                return true;
            }
            if (key == Keys.D2 || key == Keys.NumPad2)
            {
                ChooseCookieFromForm(1);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void LayoutCookieButtons()
    {
        bool show = false; // Cards own selection; native overlays would break letterboxing.
        cookieButton1.Visible = show;
        cookieButton2.Visible = show;

        if (!show)
            return;

        float sx = ClientSize.Width / 1000f;
        float sy = ClientSize.Height / 600f;
        int buttonW = Math.Max(180, (int)(260 * sx));
        int buttonH = Math.Max(46, (int)(54 * sy));
        int y = Math.Min(ClientSize.Height - buttonH - 18, (int)(515 * sy));

        cookieButton1.Bounds = new Rectangle(
            (int)(140 * sx), y, buttonW, buttonH);

        cookieButton2.Bounds = new Rectangle(
            (int)(600 * sx), y, buttonW, buttonH);

        cookieButton1.BringToFront();
        cookieButton2.BringToFront();
    }
}

public enum GameState
{
    Title,
    CookieSelect,
    Round,
    RoundTransition,
    AugmentSelect,
    Ending,
    GameOver,
    PracticeSelect,
    FinalBossSelect,
    StoryScene
}

public enum AugmentType
{
    ParryCooldownReduction,
    LifeSteal,
    ParryRange,
    Resonance,
    LandingPulse,
    Recovery
}

public enum AttackType
{
    BlackBall,
    PurgaeSkill2Ball,
    YellowSquare,
    MelodyNote,
    MelodyCluster,
    Meteor,
    StoneChunk,
    Fireball
}

public sealed partial class GamePanel : Panel
{
    private const int W = 1000;
    private const int H = 600;
    private const int GroundY = 500;
    private const int MaxHealth = 5000;
    private const float GameFps = 60f;

    private readonly Random rng = new();
    private readonly List<Projectile> projectiles = new();
    private readonly List<RectObj> obstacles = new();
    private readonly List<RisingObstacle> risingObstacles = new();

    // 원본 게임 에셋
    private Image? player1;
    private Image? player2;
    private Image? player3;
    private Image? enemy1;
    private Image? enemy2;
    private Image? enemy3;
    private Image? enemy7;
    private Image? enemy4;
    private Image? enemy5;

    private GameState state = GameState.Title;
    private int round = 1;
    private float frame;
    private float hp = MaxHealth;
    private int selectedCookie;
    private int score;

    private float px = 210;
    private float py = GroundY - PlayerHeight;
    private float vy;
    private bool onGround = true;
    private bool sliding;
    private int jumps;

    private int bossParryHits;
    private float bossDamage;
    private int bossHitRequired;
    private float attackTimer;
    private float parryWindow;
    private float parryCooldown;
    private float invuln;
    private float flashTimer;
    private float player3ShowTimer;
    private float playerSquashTimer;
    private float playerSquashAmount;

    // 전투 피드백 / 연출
    private float cameraShakeTimer;
    private float cameraShakePower;
    private float screenFlashTimer;
    private float parryFxTimer;
    private float parryFxX;
    private float parryFxY;
    private float parryFxRadius;
    private float hitStopTimer;
    private float laserTimer;
    private bool laserActive;
    private bool laserReflected;
    private float laserAngle;
    private float laserEnergyPulse;
    private Bitmap? backgroundCache;
    private int backgroundCacheRound = -1;

    private readonly SolidBrush textShadowBrush = new(Color.FromArgb(155, 0, 0, 0));
    private readonly Font bossName18Font = new("Malgun Gothic", 18, FontStyle.Bold);
    private readonly Font bossName21Font = new("Malgun Gothic", 21, FontStyle.Bold);
    private readonly Font bossTimerFont = new("Malgun Gothic", 14, FontStyle.Bold);
    private readonly Font bossUltimateFont = new("Malgun Gothic", 17, FontStyle.Bold);
    private readonly Font bossChargeFont = new("Malgun Gothic", 16, FontStyle.Bold);
    private readonly Font melodyNoteFont = new("Segoe UI Symbol", 38, FontStyle.Bold);
    private readonly Font melodyFinalNoteFont = new("Segoe UI Symbol", 46, FontStyle.Bold);
    private readonly Font melodyBeatFont = new("Malgun Gothic", 18, FontStyle.Bold);
    private readonly Font melodyFinalBeatFont = new("Malgun Gothic", 24, FontStyle.Bold);
    private readonly Font uiLabelFont = new("Malgun Gothic", 13, FontStyle.Bold);
    private readonly Font uiHpFont = new("Malgun Gothic", 12, FontStyle.Bold);
    private readonly Font uiParryFont = new("Malgun Gothic", 14, FontStyle.Bold);
    private readonly Font uiSmallFont = new("Malgun Gothic", 11, FontStyle.Regular);
    private readonly Font uiBeatFont = new("Malgun Gothic", 15, FontStyle.Bold);
    private readonly Font uiShout30Font = new("Malgun Gothic", 30, FontStyle.Bold);
    private readonly Font uiShout40Font = new("Malgun Gothic", 40, FontStyle.Bold);
    private readonly Font uiAugFont = new("Malgun Gothic", 16, FontStyle.Bold);
    private readonly Font uiStatusFont = new("Malgun Gothic", 12, FontStyle.Bold);
    private readonly Font playerParryFont = new("Malgun Gothic", 15, FontStyle.Bold);
    private readonly Font laserNoteSmallFont = new("Segoe UI Symbol", 18, FontStyle.Bold);
    private readonly Font laserNoteBigFont = new("Segoe UI Symbol", 22, FontStyle.Bold);


    // 증강 시스템: 3장 중 1장을 선택하고 누적한다.
    private float augmentCountdown;
    private const float FirstAugmentDelay = 480f; // 약 8초
    private const float AugmentInterval = 720f;   // 이후 약 12초마다
    private const float BaseParryCooldown = 60f;          // 1초 @ 60 FPS
    private const float ParryCooldownReductionPerAugment = 12f; // 증강 1회당 0.2초 감소
    private float parryCooldownReduction;                  // 발차기 쿨타임 감소 누적량
    private float lifeStealPercent = 0.05f;      // 시작부터 최대 HP의 5% 체력 흡수
    private float parryRangeBonus;                // 발차기 범위 각 방향 추가량
    private readonly AugmentType[] augmentChoices = new AugmentType[3];
    private readonly Rectangle[] augmentCardRects =
    {
        new Rectangle(44, 140, 292, 190),
        new Rectangle(354, 140, 292, 190),
        new Rectangle(664, 140, 292, 190),
        new Rectangle(44, 338, 292, 190),
        new Rectangle(354, 338, 292, 190),
        new Rectangle(664, 338, 292, 190)
    };
    private float augmentFlashTimer;
    private string augmentNotice = string.Empty;

    // 보스 위치/움직임
    private float bossX = 740;
    private float bossY = 205;
    private float bossModeTimer;
    private float obstacleSpawnTimer;
    private bool nextObstacleIsRising;
    private const float ObstacleInterval = 72f; // 모든 일반 장애물은 동일한 간격으로 생성

    // 라운드 전환 연출: 보스 패배 → 검은 화면 → 다음 라운드 표시 → 시작
    private int transitionNextRound;
    private float roundTransitionTimer;
    private float sceneFadeTimer;
    private const float SceneFadeFrames = 24f;
    private bool transitionHasBossDefeat;
    private const float RoundTransitionDefeatFrames = 45f;
    private const float RoundTransitionBlackFrames = 35f;
    private const float RoundTransitionTitleFrames = 75f;
    private const float RoundTransitionTotalFrames =
        RoundTransitionDefeatFrames + RoundTransitionBlackFrames + RoundTransitionTitleFrames;

    // 강로디언 리듬 기믹
    // 0=탈, 1=덕, 2=안, 3=돼 / -1=기믹 대기
    private int melodyStep = -1;
    private float melodyWordTimer;
    private float melodySequenceCooldown;
    private float bossShoutTimer;
    private string bossMessage = string.Empty;

    // 파괴옥 스킬 / 광폭화
    private bool purgaeTransformed;
    private float purgaeSkill1Cooldown;
    private float purgaeSkill2Cooldown;
    private bool purgaeSkill2Active;
    private readonly bool[] purgaeSkill2BallVisible = new bool[6];
    private int purgaeSkill2ThrowsRemaining;
    private int purgaeSkill2NextBall;
    private float purgaeSkill2ThrowTimer;
    private float skill2DamageDebuffTimer;
    private float burnTimer;
    private float burnTickTimer;
    private const float PurgaeSkill1Interval = 600f;  // 10초
    private const float PurgaeSkill2Interval = 600f; // 10초
    private const float PurgaeSkill2NormalDamage = 650f;
    private const float PurgaeSkill2ReducedDamage = 325f;
    private const float PurgaeSkill1MeteorDamage = 700f;
    private const float PurgaeBurnDamage = 45f;
    private const float PurgaeBurnDuration = 180f;     // 3초
    private const float PurgaeBurnTickInterval = 30f;  // 0.5초

    private static readonly string[] MelodyWords = { "탈", "덕", "안", "돼!" };
    private const int MelodyMinGap = 26; // 0.43–1.03 seconds; each phrase has a different cadence.
    private const int MelodyMaxGap = 62;
    private const float MelodySequenceCooldown = 540f; // 약 9초
    private const float MelodyStartDelay = 300f; // 라운드 시작 후 5초
    private const int MelodyHeavyDamage = 2000;

    private bool MelodySequenceActive => melodyStep >= 0;

    private bool AssetsReady => player1 != null && player2 != null && player3 != null && enemy1 != null && enemy2 != null && enemy3 != null && enemy4 != null && enemy5 != null;
    private bool ParryActive => parryWindow > 0;
    public GameState State => state;

    public GamePanel()
    {
        Dock = DockStyle.Fill;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
        TabStop = true;
        BackColor = Color.Black;
        LoadAssets();
        LoadPremiumAssets();LoadIsangjinClockAssets();LoadWolfPlayer();
        LoadKickCombat();LoadRescueEnding();LoadSkillSpectacle();LoadHighFidelityAuras();LoadMoonwalkDodge();

        // 입력 이벤트는 실제 게임 패널에서 받아야 합니다.
        // Form의 MouseDown/KeyDown만 연결하면 자식 컨트롤인 GamePanel이
        // 이벤트를 먼저 소비하기 때문에 쿠키 선택 클릭이 먹지 않을 수 있습니다.
        MouseDown += (_, e) => HandleMouseDown(e);
        MouseMove += (_, e) => UpdateLobbyPointer(e);
        MouseLeave += (_, _) => { lobbyHovered=-1;Cursor=Cursors.Default; };
        Focus();
    }

    private void LoadAssets()
    {
        player1 = LoadImage("Player1.png");
        player2 = LoadImage("Player2.png");
        player3 = LoadImage("Player3.png");
        enemy1 = LoadImage("enemy1.png");
        enemy2 = LoadImage("enemy2.png");
        enemy3 = LoadImage("enemy3.png");
        enemy7 = LoadImage("Enemy7Cutout.png") ?? LoadImage("enemy7.png");
        enemy4 = LoadImage("enemy4.png");
        enemy5 = LoadImage("enemy5.png");
    }

    private static Image? LoadImage(string fileName)
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", fileName),
            Path.Combine(Environment.CurrentDirectory, "Assets", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Environment.CurrentDirectory, fileName)
        };

        foreach (string path in candidates)
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                using FileStream stream = File.OpenRead(path);
                using Image source = Image.FromStream(stream);
                // Decode and downsample once, instead of resampling multi-megapixel PNGs every frame.
                float scale = Math.Min(1f, 640f / Math.Max(source.Width,source.Height));
                var bitmap = new Bitmap(Math.Max(1,(int)(source.Width*scale)),Math.Max(1,(int)(source.Height*scale)),PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                { graphics.InterpolationMode=InterpolationMode.HighQualityBicubic; graphics.DrawImage(source,new Rectangle(0,0,bitmap.Width,bitmap.Height)); }
                return bitmap;
            }
            catch
            {
                // 다음 후보 경로 시도
            }
        }

        return null;
    }

    public void UpdateGame(float deltaSeconds)
    {
        try
        {
            float remaining=Math.Clamp(deltaSeconds,0,0.05f);
            while(remaining>0.00001f){float dt=Math.Min(remaining,1f/60f);TickGame(dt);remaining-=dt;}
        }
        finally { RefreshGpuVfxLifecycle(); }
    }
    private void TickGame(float deltaSeconds)
    {
        // 라운드 사이 전환 연출은 게임플레이 업데이트와 분리한다.
        float step = Math.Clamp(deltaSeconds * GameFps, 0.01f, 3f);
        if(!CinderBurning)audio.StopCinderFire();
        UpdateRodianBackgroundMusic();
        if (gamePaused) { audio.Collect(); return; }
        UpdateDialogueVoice(step);
        UpdateLobbyPresentation(step);
        if(state==GameState.StoryScene){UpdateStoryJourney(step);audio.Collect();return;}
        if(state==GameState.Ending){sceneFadeTimer=Math.Max(0,sceneFadeTimer-step);UpdateRescueEnding(step);UpdatePolish(step);return;}
        if(state==GameState.FinalBossSelect){UpdateFinalBossChoice(step);UpdatePolish(step);audio.Collect();return;}
        if(UpdateEncounterFlow(step))return;
        if(UpdateMelodyLastStand(step))return;
        if(UpdatePhaseIntro(step)){UpdatePolish(step);return;}
        if(UpdateTruePurgaeFrozenTime(step)){audio.Collect();return;}
        if(UpdateIsangjinClock(step)){audio.Collect();return;}
        UpdatePremiumPresentation(step);
        if(state==GameState.AugmentSelect){UpdateCards(step);UpdatePolish(step);return;}
        if(cinematicTimer>0)step*=.24f;
        UpdatePolish(step);
        if(pendingMenheraCleanup){projectiles.RemoveAll(p=>!p.IsParried);pendingMenheraCleanup=false;}
        if (hitStopTimer > 0) { hitStopTimer = Math.Max(0, hitStopTimer - step); if (!MelodySequenceActive&&!MeteorSceneActive) return; }
        if (sceneFadeTimer > 0)
        {
            sceneFadeTimer = Math.Max(0, sceneFadeTimer - step);
            return;
        }

        if (state == GameState.RoundTransition)
        {
            UpdateRoundTransition(step);
            return;
        }

        if (state != GameState.Round)
            return;
        if(round==4&&menheraIntro>0){frame+=step;UpdatePlayer(step);return;}

        UpdateIsangjinAcceleration(step);
        float directorStep=step;
        float enemyStep=step*AbilityEnemyTimeScale*IsangjinTimeScale;
        // Convert elapsed real time to legacy 60-FPS units.
        // This keeps timers/movement consistent even when WinForms timer ticks jitter.
        frame += step;
        purgaeCastClock=Math.Min(100,purgaeCastClock+step*AbilityEnemyTimeScale);
        UpdateDirectorGameplay(directorStep*AbilityEnemyTimeScale);
        UpdatePremiumCombat(step);
        UpdateRebuild(step);
        if (state != GameState.Round || PhaseIntroActive) return;

        if (invuln > 0) invuln = Math.Max(0, invuln - step);
        if (flashTimer > 0) flashTimer = Math.Max(0, flashTimer - step);
        if (parryWindow > 0) parryWindow = Math.Max(0, parryWindow - step);
        if (melodySequenceCooldown > 0) melodySequenceCooldown = Math.Max(0, melodySequenceCooldown - step*AbilityEnemyTimeScale);
        if (bossShoutTimer > 0) bossShoutTimer = Math.Max(0, bossShoutTimer - step*AbilityEnemyTimeScale);
        if (parryCooldown > 0) parryCooldown = Math.Max(0, parryCooldown - step);
        if (player3ShowTimer > 0) player3ShowTimer = Math.Max(0, player3ShowTimer - step);
        if (cameraShakeTimer > 0)
            cameraShakeTimer = Math.Max(0, cameraShakeTimer - step);
        if (screenFlashTimer > 0)
            screenFlashTimer = Math.Max(0, screenFlashTimer - step);
        if (parryFxTimer > 0)
            parryFxTimer = Math.Max(0, parryFxTimer - step);
        if (playerSquashTimer > 0)
            playerSquashTimer = Math.Max(0, playerSquashTimer - step);
        laserEnergyPulse += 0.18f * step;

        UpdateLaser(enemyStep);
        if (state != GameState.Round || PhaseIntroActive) return;

        if (skill2DamageDebuffTimer > 0)
            skill2DamageDebuffTimer = Math.Max(0, skill2DamageDebuffTimer - step);

        if (burnTimer > 0)
        {
            burnTimer = Math.Max(0, burnTimer - step);
            burnTickTimer -= step;
            if (burnTickTimer <= 0 && burnTimer > 0 && hp > 0)
            {
                ReceiveHit(PurgaeBurnDamage,"화상",Coral,true,true);
                burnTickTimer = PurgaeBurnTickInterval;
                flashTimer = 4;
            }
        }

        if (augmentFlashTimer > 0)
            augmentFlashTimer = Math.Max(0, augmentFlashTimer - step);

        if (hp <= 0 && !TrySurvive()) { state = GameState.GameOver; return; }

        // Tutorial pressure: 42 HP/sec. Bosses: 12 HP/sec so non-healing drafts remain viable.
        if(!practiceActive)hp = Math.Max(0, hp - step * (round==1 ? .70f : .20f));
        if (!TrySurvive()) { state = GameState.GameOver; audio.CancelLaserCharge(); audio.Play("down"); return; }

        if(practiceActive)UpdatePracticeEncounter(enemyStep);
        else if (round == 1)
            UpdateRound1(enemyStep);
        else if (round == 2)
            UpdateRound2(enemyStep);
        else if (round == 3)
            UpdateRound3(enemyStep);
        else if(round==4)
            UpdateRound4(enemyStep);
        else
            UpdateFinalBoss(enemyStep);

        if (state != GameState.Round || PhaseIntroActive || TruePurgaeTimeFrozen) return;

        UpdatePlayer(step);
        if(laserActive&&laserReflected){counterOriginX=px+50;counterOriginY=py+31;counterTargetX=bossX+65;counterTargetY=bossY+65;}
        UpdateObjects(enemyStep);
        UpdateRisingObstacles(enemyStep);
        UpdateParryHits();
        if (state != GameState.Round || PhaseIntroActive) return;
        ConsumeNearbyObjects();
        CheckCollisions();
        CleanupGroundMeteors();
    }

    private void StartRound(int newRound)
    {
        ResetStoryJourney();storyDefeatDone=false;
        PrepareCheckpoint();
        state = GameState.Round;
        audio.CancelLaserCharge();
        round = newRound;
        ResetRebuild(false);
        ResetPremiumRound();ResetEncounterFlow();
        frame = 0;
        hp = PlayerMaxHealth;
        hpTrail = PlayerMaxHealth;
        damageFxTimer = 0;
        jumpBuffer = 0;
        parryCombo = 0; comboTimer = 0;
        impactParticles.Clear();
        sceneFadeTimer = 0;
        ReleaseInputs();
        px = 210;
        py = GroundY - PlayerHeight;
        vy = 0;
        onGround = true;
        sliding = false;
        jumps = 0;

        projectiles.Clear();
        obstacles.Clear();
        risingObstacles.Clear();
        attackTimer = 0;
        parryWindow = 0;
        parryCooldown = 0;
        roundTransitionTimer = 0;
        transitionNextRound = 0;
        transitionHasBossDefeat = false;
        invuln = 0;
        flashTimer = 0;
        player3ShowTimer = 0;
        cameraShakeTimer = 0;
        cameraShakePower = 0;
        screenFlashTimer = 0;focusEntryTimer=focusExitTimer=0;
        parryFxTimer = 0;
        parryFxX = parryFxY = 0;
        parryFxRadius = 0;
        hitStopTimer = 0;
        landingDustTimer = 0;
        laserTimer = 0;
        laserActive = false;
        laserReflected = false;
        laserAngle = 0;
        playerSquashTimer = 0;
        playerSquashAmount = 0;

        augmentCountdown = newRound == 1 ? FirstAugmentDelay : AugmentInterval;
        augmentFlashTimer = 0;
        augmentNotice = string.Empty;

        bossParryHits = 0;
        bossDamage = 0f;
        bossHitRequired = newRound switch
        {
            2 => 25, // 이상진
            3 => 30, // 파괴옥
            4 => 40, // 강로디언
            5 => 20, // 종결 보스
            _ => 0
        };

        bossX = 740;
        bossY = 205;
        bossModeTimer = 0;
        obstacleSpawnTimer = 35;
        nextObstacleIsRising = false; // 첫 장애물은 일반 네모, 이후 enemy2와 번갈아 생성
        melodyStep = -1;
        melodyWordTimer = 0;
        melodySequenceCooldown = newRound == 4 ? MelodyStartDelay : 0;
        bossShoutTimer = 0;
        bossMessage = string.Empty;

        purgaeTransformed = false;
        purgaeSkill1Cooldown = newRound == 3 ? 240f : 0f;
        purgaeSkill2Cooldown = newRound == 3 ? 0f : 0f; // 3라운드 시작 즉시 스킬2 발동
        purgaeSkill2Active = false;
        purgaeSkill2ThrowsRemaining = 0;
        purgaeSkill2NextBall = 0;
        purgaeSkill2ThrowTimer = 0;
        skill2DamageDebuffTimer = 0;
        burnTimer = 0;
        burnTickTimer = 0;
        Array.Fill(purgaeSkill2BallVisible, false);
        if(newRound==5)InitializeFinalBossRound();
        PreparePurgaeRound(newRound);
        BeginBossBriefing(newRound);

        Focus();
    }

    private void UpdateRound1(float step)
    {
        if (frame >= 1080)
        {
            BeginFirstGateStory();
            return;
        }

        UpdateObstacleStream(step);
    }

    private void UpdateRound2(float step)
    {
        float targetY=Math.Clamp(py-160,170,270);
        bossY+=(targetY-bossY)*(1-MathF.Pow(.955f,step));
        bossX=746+MathF.Sin(frame*.022f)*28;
        if(hasGate||EncounterBreathing)return;
        attackTimer+=step;
        int pattern=(int)(encounterTime/510)%3;
        float interval=pattern==1?124:88;
        if(attackTimer>=interval-24 && attackTimer<interval)incomingWarning=24-(attackTimer-(interval-24));
        if(attackTimer>=interval)
        {
            SpawnBossFan(pattern==1?3:pattern==2?2:1,pattern==1?.18f:.11f,pattern==1?8.2f:9.1f);
            attackTimer=0;
        }
        UpdateBossObstacles(step);
    }

    private void UpdateRound3(float step)
    {
        if(MeteorSceneActive)return;
        if (!hasGate && !EncounterBreathing) attackTimer += step;
        bossModeTimer += step;

        float targetX = purgaeTransformed
            ? 728 + MathF.Sin(frame * 0.032f) * 50f
            : 735 + MathF.Sin(frame * 0.026f) * 36f;
        float targetY = purgaeTransformed
            ? 205 + MathF.Sin(frame * 0.045f) * 42f
            : 205 + MathF.Sin(frame * 0.045f) * 28f;

        float ease = 1f - MathF.Pow(1f - (purgaeTransformed ? 0.055f : 0.035f), step);
        bossX += (targetX - bossX) * ease;
        bossY += (targetY - bossY) * ease;

        // 기본 검은 공 공격은 변신 전/후 모두 계속 사용한다.
        if (attackTimer >= 98f && !purgaeSkill2Active && !hasGate && !EncounterBreathing)
        {
            SpawnPurgaeBasicAttack();
            attackTimer = 0;
        }

        // 스킬2는 변신 전에도 발동한다.
        // 변신 전에는 초록 공(도트 데미지 없음), 변신 후에는 빨간 공(화상)으로 생성된다.
        if (!purgaeSkill2Active)
        {
            purgaeSkill2Cooldown -= step;
            if (purgaeSkill2Cooldown <= 0 && !hasGate)
                StartPurgaeSkill2();
        }
        else
        {
            UpdatePurgaeSkill2(step);
        }

        // 스킬1(메테오)만 변신 이후에 활성화한다.
        if (purgaeTransformed)
        {
            purgaeSkill1Cooldown -= step;
            if (purgaeSkill1Cooldown <= 0 && !purgaeSkill2Active)
            {
                StartPurgaeSkill1();
                purgaeSkill1Cooldown = PurgaeSkill1Interval;
            }
        }

        // 파괴옥 보스전에서는 변신 전/후 모두 기존 네모 장애물(enemy1)을 유지한다.
        UpdateBossObstacles(step);
    }

    private void StartPurgaeSkill1()
    {
        StartMeteorStorm();
    }

    private void StartPurgaeSkill2()
    {
        warningTimer = 95; encounterMessage = purgaeTransformed ? "각성 구슬  ·  화상을 조심하세요" : "초록 구슬  ·  순서대로 날아옵니다";
        purgaeSkill2Active = true;
        audio.Play("charge");
        purgaeSkill2ThrowsRemaining = purgaeSkill2BallVisible.Length; // 6개 전부 사용
        purgaeSkill2NextBall = 0;
        purgaeSkill2ThrowTimer = purgaeTransformed ? rng.Next(27, 35) : rng.Next(24, 33);

        for (int i = 0; i < purgaeSkill2BallVisible.Length; i++)
            purgaeSkill2BallVisible[i] = true;

        bossMessage = "...";
        bossShoutTimer = 28f;
        TriggerCameraShake(2f, 5f);
    }

    private void UpdatePurgaeSkill2(float step)
    {
        purgaeSkill2ThrowTimer -= step;
        if (purgaeSkill2ThrowTimer > 0)
            return;

        if (purgaeSkill2ThrowsRemaining <= 0 || purgaeSkill2NextBall >= purgaeSkill2BallVisible.Length)
        {
            purgaeSkill2Active = false;
            purgaeSkill2Cooldown = PurgaeSkill2Interval;
            Array.Fill(purgaeSkill2BallVisible, false);
            return;
        }

        // 위쪽 공부터 시작해서 시계방향으로 0 → 1 → 2 → ... 순서로 사용한다.
        PurgaeCastPose();
        int selected = purgaeSkill2NextBall;
        purgaeSkill2NextBall++;
        purgaeSkill2BallVisible[selected] = false;

        const float radius = 108f;
        float bossCenterX = bossX + 77.5f;
        float bossCenterY = bossY + 77.5f;
        float angle = -MathF.PI / 2f + selected * (MathF.PI * 2f / 6f);
        float startX = bossCenterX + MathF.Cos(angle) * radius;
        float startY = bossCenterY + MathF.Sin(angle) * radius;

        float dx = (px + PlayerWidth * 0.5f) - startX;
        float dy = (py + PlayerHeight * 0.5f) - startY;
        float len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        float speed = purgaeTransformed ? 23f : 17.5f;
        float damage = skill2DamageDebuffTimer > 0
            ? PurgaeSkill2ReducedDamage
            : PurgaeSkill2NormalDamage;

        projectiles.Add(new Projectile(
            startX, startY, AttackType.PurgaeSkill2Ball, 34,
            dx / len * speed, dy / len * speed, damage,
            burnOnHit: purgaeTransformed));

        purgaeSkill2ThrowsRemaining--;
        if (purgaeSkill2ThrowsRemaining > 0)
        {
            // 발사 간격은 살짝 불규칙하게 유지하지만 순서는 고정된다.
            purgaeSkill2ThrowTimer = purgaeTransformed ? rng.Next(15, 23) : rng.Next(16, 25);
        }
        else
        {
            purgaeSkill2Active = false;
            purgaeSkill2Cooldown = PurgaeSkill2Interval;
            Array.Fill(purgaeSkill2BallVisible, false);
        }
    }

    private void UpdateRound4(float step)
    {
        if(menheraIntro>0)return;
        if(dashActive){UpdateMelodyDash(step);return;}
        if(menheraMode)UpdateMenheraWander(step);
        else
        {
            bossX+=(742+MathF.Sin(frame*.022f)*48-bossX)*(1-MathF.Pow(.94f,step));
            bossY+=(215+MathF.Sin(frame*.035f)*32-bossY)*(1-MathF.Pow(.94f,step));
        }
        // Normal notes continue through the random rhythm and the laser aftermath.
        attackTimer+=step;
        float interval=menheraMode?42:66;
        if(attackTimer>=interval&&!LaserPriorityWindow){SpawnMelodyNote();attackTimer-=interval;}
        attackTimer=Math.Min(attackTimer,interval);
        UpdateBossObstacles(step);
        if(laserActive)return;
        if(!MelodySequenceActive&&melodySequenceCooldown<=0)StartMelodySequence();
        if(MelodySequenceActive)
        {
            melodyWordTimer-=step;
            if(melodyWordTimer<=0)
            {
                AnnounceMelodyWord(melodyStep);
                melodyInterval=melodyStep<3?melodyGaps[melodyStep]:42;
                melodyWordTimer=melodyInterval;
                if(melodyStep==2)audio.PlayLaserCharge((int)melodyInterval);
                if(melodyStep==3){TriggerMelodyFinale();FinishMelodyBurstBeat();}
                else melodyStep++;
            }
            return;
        }
        if(melodyBurstActive)return;
        dashCooldown-=step;
        if(dashCooldown<=0)StartMelodyDash();
    }

    private void StartMelodySequence()
    {
        bool followup=menheraMode&&melodyBurstActive&&melodyBurstRemaining<melodyBurstTotal;
        BeginMelodyBurst();
        hasGate=false;
        for (int i=0;i<melodyGaps.Length;i++) melodyGaps[i] = menheraMode?rng.Next(26,47):rng.Next(MelodyMinGap, MelodyMaxGap+1);
        melodyStep = followup?3:0;
        melodyInterval = followup?rng.Next(28,43):melodyGaps[0];
        melodyWordTimer = followup?melodyInterval:0;
        if(followup){bossMessage="돼!";audio.PlayLaserCharge((int)melodyInterval);}
        bossShoutTimer = 45;
        if(!followup)bossMessage = MelodyWords[0];
        flashTimer = 6;
    }

    private void AnnounceMelodyWord(int stepIndex)
    {
        bossMessage = MelodyWords[Math.Clamp(stepIndex, 0, MelodyWords.Length - 1)];
        bossShoutTimer = 40;
        if (stepIndex < 3) audio.Play("beat"+stepIndex);
        flashTimer = stepIndex == 3 ? 14 : 5;
    }

    private void TriggerMelodyFinale()
    {
        bossMessage = "돼!";
        bossShoutTimer = 70;
        TriggerScreenFlash(18);

        StartUltimateLaser();

        // "돼!"의 판정 순간 발차기 중이어야 한다.
        if (activeTimer>0 && activeSkill==ActivePower.Absorb && !ParryActive)
        { absorbedCount=Math.Min(12,absorbedCount+3);laserActive=false;audio.Play("absorb");return; }
        if (ParryActive)
        {
            // A beam clash keeps its laser counter even during Cinder.
            laserReflected=true;BeginLaserCounter();
            ParryFeedback();
            parryWindow = 0;
            parryCooldown = 0; // 성공 즉시 쿨타임 초기화
            if(state!=GameState.Round||PhaseIntroActive)return;
            cinematicTimer=reducedEffects?12:42;

            TriggerParryEffect(px + PlayerWidth * 0.5f, py + PlayerHeight * 0.5f, 42f);
            TriggerCameraShake(3.5f, 7f);
            score += 1500;
        }
        else
        {
            // 두 페이즈 모두 기본 피해 2,000. 회피와 가호가 정상 적용됩니다.
            ReceiveHit(MelodyHeavyDamage, "리듬", Accent(4), true);
            TriggerCameraShake(11f, 18f);
            TriggerScreenFlash(28);
        }
    }

    private void StartUltimateLaser()
    {
        laserActive = true;
        audio.Play("beam");
        laserReflected = false;
        laserTimer = 0;
        float targetX = px + PlayerWidth * 0.5f;
        float targetY = py + 31f;
        laserAngle = MathF.Atan2(targetY - (bossY + 65f), targetX - (bossX + 65f));
        laserEnergyPulse = 0;laserContactX=px+26;laserContactY=py+31;
    }

    private void UpdateLaser(float step)
    {
        if (!laserActive)
            return;

        laserTimer += step;

        if (laserReflected)
        {
            // 반사 레이저가 보스에게 되돌아가는 짧은 딜레이
            if (laserTimer >= 19f)
            {
                laserActive = false;

                RegisterBossParryHit(1200);
            }

            return;
        }

        // 비반사 레이저는 약 1초 동안 유지되며 끝에서 사라진다.
        if (laserTimer >= 58f)
            laserActive = false;
    }

    private void TriggerParryEffect(float x, float y, float radius)
    {
        parryFxX = x;
        parryFxY = y;
        parryFxRadius = radius;
        parryFxTimer = 18f;
    }

    private void TriggerCameraShake(float power, float duration)
    {
        if (reducedEffects) return;
        if (cameraShakeTimer <= 0) cameraShakePower = 0;
        cameraShakePower = Math.Max(cameraShakePower, power);
        cameraShakeTimer = Math.Max(cameraShakeTimer, duration);
    }

    private void TriggerScreenFlash(float duration)
    {
        screenFlashTimer = Math.Max(screenFlashTimer, reducedEffects ? Math.Min(3,duration) : duration);
    }

    private void SpawnEnemy1Obstacle()
    {
        int w = rng.Next(48, 62);

        if (rng.Next(2) == 0)
        {
            // 1종: 땅에 붙은 장애물 → 점프로 피한다.
            int h = rng.Next(55, 72);
            obstacles.Add(new RectObj(W + 40, GroundY - h, w, h, ObstacleKind.Ground));
        }
        else
        {
            // 2종: 머리 높이에 떠 있는 장애물 → 숙여서 피한다.
            const int h = 55;
            float y = GroundY - PlayerHeight - 30;
            obstacles.Add(new RectObj(W + 40, y, w, h, ObstacleKind.Floating));
        }
    }

    private void UpdateBossObstacles(float step)
    {
        UpdateObstacleStream(step);
    }

    private void UpdateObstacleStream(float step)
    {
        if(MeteorSceneActive||(round==4&&menheraMode)){obstacles.Clear();risingObstacles.Clear();return;}
        if (dashActive || hasGate || (round!=4 && (MelodySequenceActive || EncounterBreathing)) || (round==3 && purgaeSkill2Active)) return;
        obstacleSpawnTimer -= step;
        if (obstacleSpawnTimer > 0)
            return;

        // enemy1과 enemy2를 하나의 스트림으로 관리한다.
        // 둘 다 정확히 같은 X에서, 정확히 같은 시간 간격으로 생성되므로
        // 화면에서 장애물 사이의 거리가 일정하게 유지된다.
        if (nextObstacleIsRising)
            SpawnRisingObstacle();
        else
            SpawnEnemy1Obstacle();

        nextObstacleIsRising = !nextObstacleIsRising;
        obstacleSpawnTimer += round==4 ? (menheraMode?40:54) : ObstacleInterval;
    }

    private void SpawnRisingObstacle()
    {
        // enemy2: 땅에서 올라온 뒤 지면에 고정된 2칸짜리 장애물.
        // 높이 방향 이동은 전혀 없고, 일반 네모 장애물과 동일하게 왼쪽으로 이동한다.
        const int width = 62;
        const int height = 118;
        risingObstacles.Add(new RisingObstacle(W + 40, width, height));
    }

    private void UpdateRisingObstacles(float step)
    {
        // enemy2는 Y축으로 절대 움직이지 않는다. 일반 네모 장애물과
        // 완전히 같은 월드 스크롤 속도로 X만 왼쪽으로 이동한다.
        float worldSpeed = GetWorldSpeed() * step;

        foreach (RisingObstacle obstacle in risingObstacles)
            obstacle.Update(step, worldSpeed);

        risingObstacles.RemoveAll(o => o.IsFinished || o.X + o.W < -100);
    }

    private float GetWorldSpeed()
    {
        float speed = 9f;
        // 4라운드의 리듬 기믹 중에는 화면 흐름을 살짝 끌어올려 박자를 살린다.
        if (round == 4 && MelodySequenceActive)
            speed *= 1.12f;
        return speed;
    }

    private void SpawnAttackFromBoss(AttackType type, int size)
    {
        float startX = bossX - 8;
        float startY = bossY + 55;

        if (type == AttackType.BlackBall || type == AttackType.MelodyNote || type == AttackType.MelodyCluster)
        {
            float dx = px - startX;
            float dy = (py + 30) - startY;
            float len = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            float speed = type == AttackType.MelodyCluster ? 10.5f : 9.2f;
            projectiles.Add(new Projectile(startX, startY, type, size, dx / len * speed, dy / len * speed));
        }
        else
        {
            projectiles.Add(new Projectile(startX, startY, type, size, -8.0f, 5.5f));
        }
    }

    private void UpdatePlayer(float step)
    {
        bool wasGrounded = onGround;

        if (!onGround)
        {
            vy += 1.55f * step;
            if (vy > 25) vy = 25;
            py += vy * step;
        }

        if (py + PlayerHeight >= GroundY)
        {
            py = GroundY - PlayerHeight;
            float landingVelocity = vy;
            vy = 0;

            if (!wasGrounded)
            {
                LandingFeedback(landingVelocity);
            }

            onGround = true;
            jumps = 0;
            if (jumpBuffer > 0) Jump();
        }
        else
        {
            onGround = false;
        }

        if (playerSquashTimer <= 0)
            playerSquashAmount = 0;

        py = Math.Clamp(py, 0, H - PlayerHeight);
    }

    private const int PlayerHeight = 75;
    private const int PlayerWidth = 52;

    private void UpdateObjects(float step)
    {
        float worldSpeed = GetWorldSpeed();
        worldTravelDistance+=worldSpeed*step;

        // Legacy speeds are expressed in pixels/frame at 60 FPS.
        worldSpeed *= step;

        foreach (RectObj o in obstacles)
            o.X -= worldSpeed;

        obstacles.RemoveAll(o => o.X + o.W < -20);

        foreach (Projectile p in projectiles)
            p.Update(worldSpeed, round, bossX, bossY, 1f, p.IsParried?step/IsangjinTimeScale:step);

        projectiles.RemoveAll(p => p.X + p.W < -80 || p.Y > H + 80 || p.Y < -300);
    }

    private void RegisterBossParryHit(int scoreBase = 1200,bool basicReflection=true)
    {
        if (round < 2 || bossHitRequired <= 0 || MelodyLastStandActive)
            return;

        bossParryHits++;
        bossDamage += ResolveBossDamage(pendingBossDamage+(basicReflection?cinderVulnerability*.5f:0)) * (cloneActive ? 1.5f : 1f)*PlayerAttackMultiplier;
        pendingBossDamage=1;
        bossRecoil=12;
        Burst(bossX+68, bossY+68, Accent(round), 28);
        audio.Play(cinderKunaiImpact?"cinderhit":echoImpactAudio?"echohit":"impact");
        hitStopTimer = reducedEffects ? 0 : 4;
        if (bossDamage > bossHitRequired)
            bossDamage = bossHitRequired;
        score += Math.Max(1, scoreBase);

        float remaining = Math.Max(0f, bossHitRequired - bossDamage);
        augmentNotice = lifeStealPercent > 0
            ? $"보스 HP {remaining:0.0}/{bossHitRequired} · 흡수 +{PlayerMaxHealth * lifeStealPercent:0}"
            : $"보스 HP {remaining:0.0}/{bossHitRequired}";
        augmentFlashTimer = 40f;

        if ((!cinderKunaiImpact||basicReflection) && lifeStealPercent > 0)
            hp = Math.Min(PlayerMaxHealth, hp + PlayerMaxHealth * lifeStealPercent);

        if(TryBeginMelodyLastStand())return;
        if(!PracticeLocksPhase&&round==4&&bossDamage>=10&&!menheraMode&&bossDamage<bossHitRequired)EnterMenheraMode();
        if (!PracticeLocksPhase && round == 3 && !purgaeTransformed && bossDamage >= bossHitRequired * 0.5f && bossDamage < bossHitRequired)
            TriggerPurgaeTransformation();

        TriggerParryEffect(bossX + (round == 4 ? 77.5f : 67.5f),
            bossY + (round == 4 ? 77.5f : 67.5f), 68f);
        TriggerCameraShake(8f, 12f);
        TriggerScreenFlash(14);

        if (bossDamage >= bossHitRequired){if(practiceActive){practiceClears++;bossDamage=0;bossParryHits=0;ResetCinderRound();augmentNotice="연습 처치 · 계속 진행";}else BeginBossDefeat();}
    }

    private void BeginRoundTransition(int nextRound, bool bossDefeated, bool playCue=true)
    {
        ResetTruePurgaeEncounter();ResetIsangjinClock();
        CancelAbilityEchoes();
        CancelMeteorStorm(true);
        meteorBarrageTimer=0;meteorShotsRemaining=0;
        transitionReward = string.Empty;
        dashActive=false; cinematicTimer=0; sparkActive=false; activeTimer=0;
        pendingDraft=nextRound==2?1:nextRound==3?2:nextRound==4?3:0;
        audio.CancelLaserCharge();
        hasGate=false;skillFxTimer=0;parrySlices.Clear();
        state = GameState.RoundTransition;
        if(playCue)audio.Play("round");
        hitStopTimer = 0;
        invuln = 0;
        damageFxTimer = 0;
        screenFlashTimer = 0;
        cameraShakeTimer = 0;
        transitionNextRound = nextRound;
        transitionHasBossDefeat = bossDefeated;
        roundTransitionTimer = 0f;

        // 전환 동안 기존 공격과 장애물을 모두 정리해서 다음 라운드가 깔끔하게 시작된다.
        projectiles.Clear();
        obstacles.Clear();
        risingObstacles.Clear();
        laserActive = false;
        purgaeSkill2Active = false;
        Array.Fill(purgaeSkill2BallVisible, false);
        melodyStep = -1;
    }

    private void UpdateRoundTransition(float step)
    {
        roundTransitionTimer += step;
        if (roundTransitionTimer >= RoundTransitionTotalFrames)
        {
            if(round==4&&transitionNextRound==5&&!practiceActive){BeginStageTravel(0);return;}
            if (transitionNextRound >= 2 && transitionNextRound <= 5)
            {
                if(pendingDraft>0){OpenSkillCards();return;}
                BeginStageTravel(transitionNextRound);
            }
            else
            {
                BeginStageTravel(-1);
                roundTransitionTimer = 0;
            }
        }
    }

    private void TriggerPurgaeTransformation()
    {
        warningTimer=135;encounterMessage="파괴옥 각성  ·  붉은 구슬은 화상을 남깁니다";
        purgaeTransformed = true;
        armorHits=0;armorExposed=0;
        audio.Play("charge");
        Burst(bossX+68, bossY+68, Coral, 40);
        purgaeSkill1Cooldown = 90f;
        // 변신은 스킬2의 쿨타임/진행을 건드리지 않는다.
        bossMessage = "파괴옥 변신";
        bossShoutTimer = 90f;
        BeginPhaseIntro(1);
        flashTimer = 18f;
        TriggerScreenFlash(24);
        TriggerCameraShake(9f, 16f);
    }

    private void UpdateParryHits()
    {
        if (ParryActive && !LaserPriorityWindow)
        {
            RectangleF parryZone = KickHitBox;

            Projectile? target = null;
            float bestDistance = float.MaxValue;

            foreach (Projectile p in projectiles)
            {
                if (p.IsParried || (!CanKickProjectile(p,parryZone)&&!TrueStoneTouchesKick(p,parryZone)))
                    continue;

                float distance = MathF.Abs(p.X - px);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = p;
                }
            }

            if (target != null)
            {
            if(CinderBurning&&target.Type!=AttackType.Meteor){projectiles.Remove(target);QueueCinderKunai();}
                else if(target.Type==AttackType.Meteor)target.ReflectMeteorUp();else target.ReflectToBoss();
                meteorParryNoBossDamage=target.Type==AttackType.Meteor;
                try{ParryFeedback();}finally{meteorParryNoBossDamage=false;}
                if(state!=GameState.Round)return;
                score += target.Type is AttackType.BlackBall or AttackType.StoneChunk or AttackType.Fireball ? 600 : 800;
                if (target.Type == AttackType.Meteor)
                {
                    // 메테오를 발차기하면 다음 스킬2의 공격력이 잠시 50% 감소한다.
                    skill2DamageDebuffTimer = Math.Max(skill2DamageDebuffTimer, 360f);
                    augmentNotice = "메테오 발차기! 스킬2 공격력 50% 감소";
                    augmentFlashTimer = 60f;
                }
                TriggerParryEffect(px + PlayerWidth * 0.5f, py + PlayerHeight * 0.5f, 42f + parryRangeBonus);
                TriggerCameraShake(2.5f, 5f);
                flashTimer = 8;
                // A meteor kick stays active for its original, finite window,
                // reflecting the dense cluster without demanding one tap per rock.
                if(target.Type!=AttackType.Meteor)parryWindow = 0;
                parryCooldown = 0;
            }
        }

        if (state!=GameState.Round || PhaseIntroActive || round < 2 || projectiles.Count == 0)
            return;

        RectangleF bossRect = BossHitBox;

        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (projectile.IsMeteorUpward || !projectile.IsParried || !bossRect.IntersectsWith(projectile.Rect))
                continue;

            projectiles.RemoveAt(i);
            RegisterBossParryHit(1200);
            if (state != GameState.Round || PhaseIntroActive) return;
        }
    }

    private void CheckCollisions()
    {
        if(DodgeActive)return;
        RectangleF playerRect = PlayerHitBox;

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            RectObj obstacle = obstacles[i];
            if (!playerRect.IntersectsWith(obstacle.Rect))
                continue;

            if (obstacle.Kind==ObstacleKind.Floating || !ParryActive)
            {
                ReceiveHit(400, "충돌", Coral);
            }
            obstacles.RemoveAt(i);
        }

        for (int i = risingObstacles.Count - 1; i >= 0; i--)
        {
            RisingObstacle obstacle = risingObstacles[i];
            if (obstacle.HasHitPlayer || !obstacle.IsActive || !playerRect.IntersectsWith(obstacle.Rect))
                continue;

            if (!ParryActive)
            {
                obstacle.HasHitPlayer = true;
                ReceiveHit(650, "장애물", Color.FromArgb(255,190,100));
            }
        }

        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (DodgeActive || projectile.IsParried || (!playerRect.IntersectsWith(ProjectileDamageBounds(projectile))&&!TrueStoneTouchesKick(projectile,playerRect)))
                continue;

            if (ParryActive)
                continue;

            // 보스 기믹의 강공격/일반 공격은 기존 피해량 유지.
            {
                bool received = invuln <= 0 && hp > 0 && !(activeSkill==ActivePower.Dodge && activeTimer>0);
                ReceiveHit(projectile.Damage, projectile.BurnOnHit ? "화상" : "피격", projectile.BurnOnHit ? Coral : Accent(round));
                if (received && projectile.BurnOnHit)
                {
                    burnTimer = Math.Max(burnTimer, PurgaeBurnDuration);
                    burnTickTimer = PurgaeBurnTickInterval;
                }
            }
            projectiles.RemoveAt(i);
        }
    }

    public void SelectCookie(int index)
    {
        // Ignore stale clicks once a run has started.
        if (state != GameState.CookieSelect) return;
        selectedCookie = Math.Clamp(index, 0, 1);
        audio.Play("select");
        parryCombo = 0; comboTimer = 0;
        score = 0;clearTimeSeconds=0;
        ResetRebuild(true);
        parryCooldownReduction = 0f;
        lifeStealPercent = 0.05f;
        ResetPremiumRun();ResetFinalBossRun();
        parryRangeBonus = 0f;
        BeginOpeningStory();
        Invalidate();
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (!heldKeys.Add(e.KeyCode)) return;
        if(state==GameState.StoryScene&&e.KeyCode is not(Keys.Escape or Keys.M or Keys.P)){if(!gamePaused)HandleStoryJourneyKey(e.KeyCode);return;}
        if(HandlePracticeKey(e.KeyCode))return;
        if (e.KeyCode == Keys.P && (state==GameState.Round || state==GameState.RoundTransition || state==GameState.StoryScene)) { SetPaused(!gamePaused); return; }
        if (e.KeyCode == Keys.M) { audio.Toggle(); return; }
        if (e.KeyCode == Keys.F) { reducedEffects = !reducedEffects; return; }
        if (e.KeyCode == Keys.R && state == GameState.GameOver) { RetryRound(); return; }
        if(HandleFinalBossChoiceKey(e.KeyCode))return;
        if(!gamePaused&&HandleMelodyLastStandKey(e.KeyCode))return;
        if(!gamePaused&&QueueTruePurgaeFrozenAction(e.KeyCode))return;
        if(!gamePaused&&HandleIsangjinClockKey(e.KeyCode))return;
        if (e.KeyCode == Keys.Q && state == GameState.Round && !gamePaused && !PhaseIntroActive && !bossBriefingActive) { UseActiveSkill(); return; }
        if (e.KeyCode == Keys.Escape)
        {
            Application.Exit();
            return;
        }

        if (gamePaused) return;
        if(HandleEncounterKey(e.KeyCode))return;
        if(state==GameState.Ending){HandleEndingKey(e.KeyCode);return;}
        if(state==GameState.AugmentSelect&&pendingDraft==3){HandleForgeKey(e.KeyCode);return;}
        if(state==GameState.AugmentSelect){HandleSkillChoiceKey(e.KeyCode);return;}
        if (e.KeyCode == Keys.Enter)
        {
            if (state == GameState.Title)
            {
                OpenLobbySelection();
            }
            else if (state == GameState.GameOver || state == GameState.Ending)
                state = GameState.Title;
            Focus();
            return;
        }

        if (state == GameState.CookieSelect)
        {
            if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)
                SelectCookie(0);
            else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)
                SelectCookie(1);
            return;
        }

        if (state != GameState.Round || sceneFadeTimer > 0 || PhaseIntroActive)
            return;

        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.ShiftKey)
            StartParry();
        else if (e.KeyCode == Keys.Up)
            Jump();
        else if (e.KeyCode == Keys.Down)
            sliding = true;
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        heldKeys.Remove(e.KeyCode);
        if(TruePurgaeTimeFrozen||MelodyLastStandActive||IsangjinClockActive)return;
        if (e.KeyCode == Keys.Down)
            sliding = false;
    }

    public void HandleMouseDown(MouseEventArgs e)
    {
        Focus();

        if (e.Button != MouseButtons.Left)
            return;
        if(gamePaused||IsangjinClockActive)return;

        float scale = Math.Max(0.001f, Math.Min(Width / (float)W, Height / (float)H));
        int x = (int)((e.X - (Width-W*scale)/2) / scale);
        int y = (int)((e.Y - (Height-H*scale)/2) / scale);

        if(state==GameState.StoryScene){HandleStoryJourneyKey(Keys.Enter);return;}
        if(state==GameState.FinalBossSelect){HandleFinalBossChoiceMouse(x,y);return;}

        if(state==GameState.Title&&LobbyPracticeBounds.Contains(x,y)){OpenPractice();return;}
        if(state==GameState.Title&&LobbyStartBounds.Contains(x,y)){OpenLobbySelection();return;}
        if(state==GameState.PracticeSelect){if(new Rectangle(325,466,350,55).Contains(x,y))StartPractice();else if(new Rectangle(105,148,790,130).Contains(x,y))practicePreset=(practicePreset+(x<500?PracticeNames.Length-1:1))%PracticeNames.Length;return;}
        if(bossBriefingActive||bossDefeatActive){HandleEncounterKey(Keys.Enter);return;}
        if(state==GameState.Ending){HandleRescueEndingClick();return;}
        if (state == GameState.CookieSelect)
        {
            if (new Rectangle(70, 165, 400, 350).Contains(x, y))
                SelectCookie(0);
            else if (new Rectangle(530, 165, 400, 350).Contains(x, y))
                SelectCookie(1);
            return;
        }

        if (state == GameState.AugmentSelect)
        {
            if(pendingDraft==3){HandleForgeMouse(x,y);return;}
            for (int i = 0; i < DraftCardCount; i++)
            {
                if (DraftCardRect(i).Contains(x, y))
                {
                    ChooseAugment(i);
                    return;
                }
            }
        }
    }

    private void OpenAugmentSelection() {
        OpenSkillCards();
    }

    private void ChooseAugment(int index) {
        SelectSkillCard(index);
    }

    private static string GetAugmentName(AugmentType type)
    {
        return type switch
        {
            AugmentType.ParryCooldownReduction => "발차기 쿨타임 감소",
            AugmentType.LifeSteal => "체력 흡수",
            AugmentType.ParryRange => "발차기 확장",
            AugmentType.Resonance => "공명 증폭",
            AugmentType.LandingPulse => "중량 충격파",
            AugmentType.Recovery => "충격 완충",
            _ => "증강"
        };
    }

    private static string[] GetAugmentDescription(AugmentType type)
    {
        return type switch
        {
            AugmentType.ParryCooldownReduction => new[]
            {
                "발차기를 사용한 뒤의 대기시간이",
                "한 번 고를 때마다 0.2초씩 감소합니다."
            },
            AugmentType.LifeSteal => new[]
            {
                "발차기 반사 공격이 보스에게 적중하면",
                "현재 흡수율에 최대 HP의 2%를 추가로 회복합니다."
            },
            AugmentType.ParryRange => new[]
            {
                "발차기 판정 범위가 조금 더 넓어집니다.",
                "같은 증강을 다시 고르면 계속 누적됩니다."
            },
            _ => Array.Empty<string>()
        };
    }



    private void Jump()
    {
        if (onGround || jumps < 2)
        {
            vy = -21.5f;
            onGround = false;
            jumps++;
            jumpBuffer = 0;
            audio.Play("jump");
            Burst(px+26, py+75, Paper, 8);
        }
        else jumpBuffer = 7;
    }

    private float GetParryCooldownSeconds()
    {
        return Math.Max(0.2f, (BaseParryCooldown - parryCooldownReduction) / GameFps);
    }

    private void StartParry()
    {
        // 약 0.17초 판정창 + 기본 1초 재사용 대기시간 (증강으로 더 감소)
        if (parryCooldown > 0 || parryWindow > 0)
            return;

        BeginKickPose();
        parryWindow = MeteorSceneActive ? 18 : 10;
        parryCooldown = Math.Max(12f, BaseParryCooldown - parryCooldownReduction); // 기본 1초, 증강마다 0.2초 감소
        player3ShowTimer = 0; // Dedicated kick windup, contact and recovery poses.
        flashTimer = 5;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeGpuPrototype();
            DisposeTrueStopSurfaces();DisposePurgaeAnimation();DisposeLaserBloom();DisposeHighFidelityAuras();DisposeSkillSpectacle();DisposeEncounterFlow();DisposeKickCombat();DisposeRescueEnding();DisposeJourneyResources();DisposeSoulsInterface();DisposeLobbyArt();DisposeMoonwalkDodge();
            DisposeArena();
            DisposePremium();
            audio.Dispose();
            player1?.Dispose(); player2?.Dispose(); player3?.Dispose();
            enemy1?.Dispose(); enemy2?.Dispose(); enemy3?.Dispose(); enemy7?.Dispose(); enemy4?.Dispose(); enemy5?.Dispose();
            backgroundCache?.Dispose();
            textShadowBrush.Dispose();
            bossName18Font.Dispose();
            bossName21Font.Dispose();
            bossTimerFont.Dispose();
            bossUltimateFont.Dispose();
            bossChargeFont.Dispose();
            melodyNoteFont.Dispose();
            melodyFinalNoteFont.Dispose();
            melodyBeatFont.Dispose();
            melodyFinalBeatFont.Dispose();
            uiLabelFont.Dispose();
            uiHpFont.Dispose();
            uiParryFont.Dispose();
            uiSmallFont.Dispose();
            uiBeatFont.Dispose();
            uiShout30Font.Dispose();
            uiShout40Font.Dispose();
            uiAugFont.Dispose();
            uiStatusFont.Dispose();
            playerParryFont.Dispose();
            laserNoteSmallFont.Dispose();
            laserNoteBigFont.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(SyncGpuPrototypeSurface())return;
        var g=e.Graphics;g.Clear(Ink);
        float scale=Math.Max(.001f,Math.Min(Width/(float)W,Height/(float)H));
        if(Math.Abs(panelScale-scale)>.01f){panelScale=scale;foreach(var old in panelSurfaces.Values)old.Dispose();panelSurfaces.Clear();}
        float cacheScale=Math.Min(2.5f,scale);
        if(Math.Abs(cacheScale-nativeArtScale)>.01f){nativeArtScale=cacheScale;backgroundCacheRound=-1;}
        g.TranslateTransform((Width-W*scale)/2,(Height-H*scale)/2);g.ScaleTransform(scale,scale);g.SetClip(new Rectangle(0,0,W,H));
        PaintPremiumFrame(g,scale);
    }

    private void RenderArenaFrame(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        GraphicsState cameraState = g.Save();

        if (state == GameState.Round || state == GameState.AugmentSelect || state == GameState.Ending)
        {
            float shake = !PhaseIntroActive && !reducedEffects && cameraShakeTimer > 0 ? cameraShakePower * (cameraShakeTimer / 18f) : 0f;
            if (shake > 0)
            {
                float sxShake = MathF.Sin(frame * 1.9f) * shake;
                float syShake = MathF.Cos(frame * 2.3f) * shake * 0.65f;
                g.TranslateTransform(sxShake, syShake);
            }
        }

        var mirrorState=g.Save();
        worldMirroring=GravityMirrored&&state==GameState.Round&&!PhaseIntroActive&&!bossBriefingActive&&!IsangjinClockActive;
        if(worldMirroring){g.TranslateTransform(0,H);g.ScaleTransform(1,-1);}
        if(!MelodyLastStandActive&&state is not(GameState.StoryScene or GameState.Title or GameState.CookieSelect))DrawBackground(g);

        switch (state)
        {
            case GameState.StoryScene:
                DrawStoryJourney(g);
                break;
            case GameState.Title:
                DrawModernTitle(g);
                break;
            case GameState.PracticeSelect:
                DrawPracticeMenu(g);
                break;
            case GameState.FinalBossSelect:
                DrawFinalBossChoice(g);
                break;
            case GameState.CookieSelect:
                DrawModernCookieSelect(g);
                break;
            case GameState.Round:
                if(bossBriefingActive)DrawBossBriefing(g);else if(IsangjinClockActive)DrawIsangjinClockScene(g);else DrawWorld(g);
                break;
            case GameState.RoundTransition:
                if(bossDefeatActive)DrawBossDefeat(g);else DrawModernTransition(g);
                break;
            case GameState.AugmentSelect:
                DrawModernAugments(g);
                break;
            case GameState.Ending:
                DrawRescueEnding(g);
                break;
            case GameState.GameOver:
                DrawModernResult(g, false);
                break;
        }

        g.Restore(mirrorState);worldMirroring=false;
        if(state==GameState.Round&&!renderingScene&&!PhaseIntroActive&&!bossBriefingActive&&!MelodyLastStandActive)
        {
            if(IsangjinClockActive){var hud=g.Save();g.SetClip(new Rectangle(0,0,W,99),CombineMode.Intersect);DrawModernHud(g);g.Restore(hud);}
            else DrawModernHud(g);
        }
        if(state==GameState.Round&&!bossBriefingActive&&!MelodyLastStandActive&&!IsangjinClockActive&&!trueStopCapturingBase){DrawPracticeHud(g);DrawHackedFocusOverlay(g);DrawGravityOverlay(g);DrawIsangjinAccelerationHud(g);DrawIsangjinClockTelegraph(g);DrawPhaseIntro(g);}
        g.Restore(cameraState);
        if (sceneFadeTimer > 0)
        {
            using SolidBrush fade = new(Color.FromArgb((int)(255 * sceneFadeTimer / SceneFadeFrames), Ink));
            g.FillRectangle(fade, 0, 0, W, H);
        }
        if(!trueStopCapturingBase)DrawPause(g);
    }



    private void DrawBackground(Graphics g) { DrawArenaBackground(g); }

    private static void DrawHeart(Graphics g, int x, int y, int s, Brush brush)
    {
        g.FillEllipse(brush, x, y, s, s);
        g.FillEllipse(brush, x + s, y, s, s);
        Point[] points =
        {
            new(x - 2, y + s / 2),
            new(x + s * 2 + 2, y + s / 2),
            new(x + s, y + s * 2 + 3)
        };
        g.FillPolygon(brush, points);
    }

    private void DrawWorld(Graphics g)
    {
        if(MelodyLastStandActive){DrawMelodyLastStand(g);return;}
        if(FocusWireframeActive){DrawFocusWorld(g);return;}
        if(!trueStopDrawingDynamic)DrawArenaGround(g);
        if(trueStopCapturingBase)return;
        if(PhaseIntroActive){DrawPlayer(g);return;}
        foreach (RectObj obstacle in obstacles)
        {
            DrawReadableObstacle(g,obstacle.Rect,enemy1,obstacle.Kind==ObstacleKind.Floating);
        }
        foreach (RisingObstacle obstacle in risingObstacles) DrawReadableObstacle(g,obstacle.Rect,enemy2,false);
        DrawLandingDust(g);

        DrawHazardCues(g);if(!trueStopCapturingBase)DrawFinalBossMechanics(g);
        foreach (Projectile projectile in projectiles)
            if(!DodgeActive||projectile.IsParried)DrawPremiumProjectile(g, projectile);

        if (round >= 2 && !trueStopCapturingBase)
            DrawBoss(g);

        if(!DodgeActive)DrawUltimateLaser(g);

        DrawPlayer(g);
        if(DodgeActive){foreach(var projectile in projectiles)if(!projectile.IsParried)DrawPremiumProjectile(g,projectile);DrawUltimateLaser(g);}
        DrawParryEffect(g);
        DrawCombatFeedback(g);
        DrawRebuild(g);
        DrawPremiumEffects(g);
        DrawExpandedAbilityEffects(g);DrawFocusGeometry(g);DrawLaserContactFx(g);
        if(cinematicTimer<=0||reducedEffects)DrawCounterFeedback(g);
    }



    private void DrawUltimateLaser(Graphics g)
    {
        DrawLaserCharge(g);
        if(!laserActive||round!=4)return;
        if(laserReflected){if(cinematicTimer<=0||reducedEffects)DrawCounterLaser(g);return;}
        DrawGrandLaser(g,false);
    }

    private void DrawParryEffect(Graphics g) { DrawImpactDust(g); }

    private void DrawPlayer(Graphics g)
    {
        DrawCloneAura(g,new(px-10,py,PlayerWidth+20,PlayerHeight));DrawGuardAuraBehind(g);
        int baseH = sliding ? 40 : PlayerHeight;
        int baseW = sliding ? 58 : PlayerWidth;
        float runBob = onGround && !sliding ? MathF.Sin(frame * 0.30f) * 1.8f : 0f;

        float squash = playerSquashTimer > 0
            ? playerSquashAmount * (playerSquashTimer / 8f)
            : 0f;
        float stretchX = 1f + squash;
        float stretchY = 1f - squash;

        int drawW = Math.Max(1, (int)(baseW * stretchX));
        int drawH = Math.Max(1, (int)(baseH * stretchY));
        int drawY = (int)(py + (PlayerHeight - drawH) + runBob + (baseH - drawH) * 0.35f);
        int drawX = (int)px - (drawW - PlayerWidth) / 2;

        bool blink = !DodgeActive && invuln > 0 && ((int)(frame / 5f) % 2 == 0);
        if (!blink)
        {
            if(DrawKickActor(g)) { }
            else if(DodgeActive)DrawMoonwalkDodge(g,new(drawX-9,drawY,drawW+18,drawH));
            else if (player3ShowTimer > 0 && player3 != null && activeTimer<=0)
            {
                // 발차기 시 Player3가 나타나며 약 1초 후 원래 쿠키로 돌아간다.
                float t = Math.Clamp(player3ShowTimer / 12f, 0f, 1f);
                float scale = 1f + 0.10f * (1f - t);
                int p3w = (int)(drawW * scale);
                int p3h = (int)(drawH * scale);
                int p3x = (int)(px - (p3w - PlayerWidth) / 2f);
                int p3y = (int)(py + (PlayerHeight - p3h));
                DrawPlayerSprite(g,player3,new Rectangle(p3x,p3y,p3w,p3h),Mint);
            }
            else if (selectedCookie == 0 && motionFrames.Length==8)
            {
                DrawAnimatedPlayer(g,new Rectangle(drawX-9,drawY,drawW+18,drawH));
            }
            else
            {
                if(sliding&&onGround)DrawCookieSlide(g);else DrawSimpleCookie(g, drawX, drawY, drawW, drawH);
            }
        }

    }

    private void DrawMotionTrails(Graphics g)
    {
        if (round <= 1)
            return;

        float speed = GetWorldSpeed();
        int intensity = SkilllessMotionIntensity();
        using Pen trail = new(Color.FromArgb(42 + intensity * 8, 255, 255, 255), 2f);

        for (int i = 0; i < 5; i++)
        {
            float offset = 32f + i * 14f;
            float y = py + 18f + i * 9f;
            g.DrawLine(trail, px - offset - speed * i * 2f, y, px - offset + 20f, y);
        }
    }

    private int SkilllessMotionIntensity() => Math.Clamp((int)(Math.Abs(GetWorldSpeed()) * 0.35f), 1, 6);

    private void DrawSimpleCookie(Graphics g, int x, int y, int w, int h)
    {
        if(wolfPoses.Length==12){DrawWolfActor(g,x,y,w,h);return;}
        if(furiosoCookieMask!=null&&TryDrawCinderGolem(g,furiosoCookieMask,new(x,y,w,h),Paper,1))return;
        DrawFuriosoAttached(g,null,new(x,y,w,h),1,false);DrawCinderAttached(g,null,new(x,y,w,h),1,false);
        using SolidBrush body = new(cloneActive?Color.FromArgb(64,216,106):Color.FromArgb(255, 105, 185));
        using Pen outline = new(Color.White, 3);
        g.FillEllipse(body, x, y, w, h);
        g.DrawEllipse(outline, x, y, w, h);

        using SolidBrush eye = new(Color.FromArgb(35, 20, 30));
        g.FillEllipse(eye, x + w / 4, y + h / 3, 7, 10);
        g.FillEllipse(eye, x + w * 3 / 5, y + h / 3, 7, 10);
        g.DrawArc(outline, x + w / 3, y + h / 2, w / 3, h / 4, 15, 150);
        DrawCinderStone(g,null,new(x,y,w,h),1);
        DrawFuriosoAttached(g,null,new(x,y,w,h),1,true);DrawCinderAttached(g,null,new(x,y,w,h),1,true);
    }

    private void DrawBoss(Graphics g)
    {
        if (round < 2 || dashActive || PhaseIntroActive)
            return;

        if(round==5){DrawFinalBossPortrait(g,new RectangleF(bossX,bossY,135,135));DrawCinderBrand(g,new(bossX,bossY,135,135),1);return;}
        if(round==3){DrawPurgaeWorld(g);return;}
        Image? bossImage = CurrentBossImage;

        int baseSize = round == 4 ? 155 : 135;
        float pulse = 1f + MathF.Sin(frame * 0.09f) * (round == 4 ? 0.075f : 0.04f);
        int size = (int)(baseSize * pulse);
        int bx = (int)(bossX + (bossRecoil>0 ? MathF.Sin(bossRecoil*2)*bossRecoil*.4f : 0));
        int by = (int)(bossY+((round==4&&MelodySequenceActive)?MathF.Sin(frame*.65f)*4:0));

        DrawBossEchoes(g);DrawCrimsonAura(g,bx,by,size);
        if (round == 4)
        {
            DrawMelodyHaze(g,bx,by,size);
        }

        var clockPose=IsangjinClockTelegraphActive?g.Save():null;
        if(clockPose!=null)ApplyIsangjinClockWindup(g,bx+size*.5f,by+size*.5f);
        if (bossImage != null)
        {
            DrawReadableSprite(g,bossImage,new Rectangle(bx,by,size,size),bossRecoil>5?Color.White:round==3&&purgaeTransformed?Color.FromArgb(215,48,59):Gold);
        }
        else
        {
            using SolidBrush fallback = new(round == 4 ? Color.HotPink : Color.MediumPurple);
            g.FillEllipse(fallback, bx, by, size, size);
        }


        DrawCinderBrand(g,new(bx,by,size,size),1);
        if(clockPose!=null)g.Restore(clockPose);
        if (round == 3 && purgaeSkill2Active)
            DrawPurgaeSkill2Balls(g, bossX + 77.5f, bossY + 77.5f);

        if (round == 4 && laserActive)
        {
            // 필살기 레이저가 발동 중일 때는 리듬 안내보다 필살기 상태가 우선한다.
            Font ultFont = bossUltimateFont;
            DrawTextShadow(g, laserReflected ? "레이저 반사!" : "필살기 발동!", ultFont,
                laserReflected ? Brushes.Cyan : Brushes.Gold, bx - 10, by + size + 10);
        }
        else if (round == 4 && MelodySequenceActive)
        {
            // Cadence is drawn by ArenaHud against the actual randomized interval.
        }

    }

    private void DrawPurgaeSkill2Balls(Graphics g, float cx, float cy)
    {
        float radius = 108f;
        for (int i = 0; i < purgaeSkill2BallVisible.Length; i++)
        {
            if (!purgaeSkill2BallVisible[i])
                continue;

            float angle = -MathF.PI / 2f + i * (MathF.PI * 2f / 6f);
            float x = cx + MathF.Cos(angle) * radius;
            float y = cy + MathF.Sin(angle) * radius;
            DrawPurgaeOrb(g,x,y,TruePurgaeEncounter?26:34);
        }
    }

    private void DrawMelodyTelegraph(Graphics g, int cx, int top)
    {
        int pulse = (int)(MathF.Sin(frame * 0.25f) * 6f);
        bool finalBeat = melodyStep == 3;

        using SolidBrush glow = new(Color.FromArgb(55, 255, 255, 255));
        g.FillEllipse(glow, cx - 78 - pulse, top - 22 - pulse, 156 + pulse * 2, 120 + pulse * 2);

        string[] notes = { "♪", "♫", "♪" };
        Font noteFont = finalBeat ? melodyFinalNoteFont : melodyNoteFont;
        Brush noteBrush = finalBeat ? Brushes.Gold : Brushes.White;
        float[] xs = { cx - 65f, cx - 5f, cx + 42f };
        float[] ys = { top + 15f, top - 6f, top + 18f };
        for (int i = 0; i < 3; i++)
            g.DrawString(notes[i], noteFont, noteBrush, xs[i], ys[i]);

        Font beatFont = finalBeat ? melodyFinalBeatFont : melodyBeatFont;
        string beatText = finalBeat
            ? "돼!  ← 지금 SPACE / SHIFT"
            : $"{MelodyWords[Math.Clamp(melodyStep, 0, 3)]}";
        g.DrawString(beatText, beatFont, finalBeat ? Brushes.Yellow : Brushes.White, cx - 70, top + 80);
    }



    private void DrawTextShadow(Graphics g, string text, Font font, Brush brush, float x, float y)
    {
        g.DrawString(text, font, textShadowBrush, x + 2, y + 2);
        g.DrawString(text, font, brush, x, y);
    }












}


public sealed class RisingObstacle
{
    private const float GroundYForObstacle = 500f;

    public float X;
    public readonly int W;
    public readonly int H;

    private readonly float y;

    public bool HasHitPlayer { get; set; }
    public bool IsActive => y + H > 0 && y < GroundYForObstacle + 1;
    public bool IsFinished => X + W < -100f;
    public float Y => y;
    public RectangleF Rect => new(X, y, W, H);

    public RisingObstacle(float x, int width, int height)
    {
        X = x;
        W = width;
        H = height;
        // enemy2는 등장하자마자 지면에 올라온 2칸 높이로 고정한다.
        // 상하로 움직이지 않고, 스테이지만 좌우로 흐른다.
        y = GroundYForObstacle - H;
    }

    public void Update(float step, float worldSpeed)
    {
        // 세로 위치는 절대 변하지 않고, 호출부에서 계산한 월드 이동량만큼
        // 왼쪽으로 이동한다. 일반 네모 장애물과 이동량이 완전히 같다.
        X -= worldSpeed;
    }

    public void Draw(Graphics g, Image? enemy2)
    {
        if (enemy2 != null)
        {
            Rectangle dest = new((int)X, (int)Y, W, H);
            g.DrawImage(enemy2, dest);
        }
        else
        {
            using SolidBrush fallback = new(Color.FromArgb(185, 78, 205));
            g.FillRectangle(fallback, Rect);
            using Pen outline = new(Color.White, 2);
            g.DrawRectangle(outline, X, Y, W, H);
        }
    }
}

public sealed class Projectile
{
    public float X;
    public float Y;
    public float PreviousX,PreviousY;
    public readonly AttackType Type;
    public readonly int W;
    public readonly int H;
    public readonly float Damage;
    public readonly bool BurnOnHit;
    public bool IsParried { get; private set; }
    public bool IsFocusedMeteor;
    public bool IsMeteorUpward {get;private set;}
    public float FocusX, FocusY, MeteorAge;
    public float VelocityX=>vx;
    public float VelocityY=>vy;

    private float vx;
    private float vy;

    public RectangleF Rect => new(X, Y, W, H);

    public Projectile(
        float x, float y, AttackType type, int size = 34,
        float velocityX = -9f, float velocityY = 0f,
        float damage = 250f, bool burnOnHit = false)
    {
        PreviousX=X=x;
        PreviousY=Y=y;
        Type = type;
        W = size;
        H = size;
        vx = velocityX;
        vy = velocityY;
        Damage = damage;
        BurnOnHit = burnOnHit;
    }

    public void ReflectMeteorUp(){IsParried=true;IsMeteorUpward=true;vx=0;vy=-28f;}

    public void ReflectToBoss()
    {
        IsParried = true;
        vx = 0;
        vy = 0;
    }

    public void Update(float worldSpeed, int round, float bossX, float bossY,
        float projectileSpeedMultiplier = 1f, float step = 1f)
    {
        PreviousX=X;PreviousY=Y;
        if(IsMeteorUpward){X+=vx*step;Y+=vy*step;return;}
        if (IsParried)
        {
            float targetX = bossX + 77.5f;
            float targetY = bossY + 77.5f;
            float cx = X + W / 2f;
            float cy = Y + H / 2f;
            float dx = targetX - cx;
            float dy = targetY - cy;
            float len = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
            const float reflectedSpeed = 17f;
            float travel = reflectedSpeed * step;
            X += dx / len * travel;
            Y += dy / len * travel;
            return;
        }

        if (Type == AttackType.StoneChunk || Type == AttackType.Fireball || Type == AttackType.BlackBall || Type == AttackType.PurgaeSkill2Ball || Type == AttackType.MelodyNote || Type == AttackType.MelodyCluster)
        {
            X += vx * projectileSpeedMultiplier * step;
            Y += vy * projectileSpeedMultiplier * step;
            X -= MathF.Max(0, worldSpeed - 9f * step) * 0.12f * projectileSpeedMultiplier;

            if (Type == AttackType.MelodyNote && !IsParried)
                Y += MathF.Sin((X + Y) * 0.035f) * 0.8f * step;
        }
        else if (Type == AttackType.Meteor)
        {
            MeteorAge+=step;
            if(IsFocusedMeteor)
            {
                // Fixed impact point, real acceleration along its original trajectory.
                float dx=FocusX-(X+W*.5f),dy=FocusY-(Y+H*.5f),length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy));
                float speed=Math.Min(35,MathF.Sqrt(vx*vx+vy*vy)+.65f*step);
                vx=dx/length*speed;vy=dy/length*speed;X+=vx*step;Y+=vy*step;return;
            }
            X += vx * step;
            Y += vy * step;
            vy = MathF.Min(15f, vy + 0.06f * step);
            X -= worldSpeed * 0.35f;
        }
        else
        {
            X -= worldSpeed * projectileSpeedMultiplier;
            Y += 5.5f * step * projectileSpeedMultiplier;
        }
    }

    public void Draw(Graphics g, float frame)
    {
        if (Type == AttackType.Meteor)
        {
            using SolidBrush glow = new(Color.FromArgb(75, 255, 120, 30));
            g.FillEllipse(glow, X - 12, Y - 8, W + 24, H + 20);
            using SolidBrush body = new(IsParried
                ? Color.FromArgb(250, 255, 225, 95)
                : Color.FromArgb(205, 115, 62));
            g.FillEllipse(body, Rect);
            using Pen edge = new(IsParried ? Color.White : Color.OrangeRed, 3);
            g.DrawEllipse(edge, Rect);
            using Pen tail = new(Color.FromArgb(180, 255, 190, 80), 5);
            g.DrawLine(tail, X + W * 0.75f, Y + H * 0.2f, X + W * 0.95f, Y - 22);
            return;
        }

        if (Type == AttackType.MelodyNote)
        {
            using SolidBrush glow = new(IsParried
                ? Color.FromArgb(90, 255, 245, 120)
                : Color.FromArgb(70, 255, 150, 230));
            g.FillEllipse(glow, X - 12, Y - 12, W + 24, H + 24);

            using Font noteFont = new("Segoe UI Symbol", IsParried ? 42 : 36, FontStyle.Bold);
            g.DrawString(IsParried ? "♫" : "♪", noteFont, IsParried ? Brushes.Yellow : Brushes.White, X - 2, Y - 8);
            return;
        }

        if (Type == AttackType.MelodyCluster)
        {
            using SolidBrush glow = new(IsParried
                ? Color.FromArgb(85, 255, 255, 110)
                : Color.FromArgb(70, 255, 110, 205));
            g.FillEllipse(glow, X - 16, Y - 16, W + 32, H + 32);

            using Font noteFont = new("Segoe UI Symbol", IsParried ? 54 : 50, FontStyle.Bold);
            Brush noteBrush = IsParried ? Brushes.Yellow : Brushes.White;
            g.DrawString("♪", noteFont, noteBrush, X + 2, Y + 8);
            g.DrawString("♫", noteFont, noteBrush, X + 28, Y - 2);
            g.DrawString("♪", noteFont, noteBrush, X + 48, Y + 17);
            using Pen outline = new(IsParried ? Color.Yellow : Color.HotPink, 3);
            g.DrawEllipse(outline, X - 6, Y - 4, W + 12, H + 8);
            return;
        }

        if (Type == AttackType.PurgaeSkill2Ball)
        {
            // 스킬2 전용 공: 변신 전 초록 / 변신 후 빨강.
            // BurnOnHit은 변신 후 생성분에만 켜져 있으므로 변신 전 공에는 도트뎀이 없다.
            Color glowColor = BurnOnHit
                ? Color.FromArgb(100, 255, 55, 55)
                : Color.FromArgb(85, 80, 255, 120);
            Color bodyColor = BurnOnHit
                ? Color.FromArgb(235, 220, 35, 40)
                : Color.FromArgb(225, 45, 200, 85);
            Color edgeColor = IsParried
                ? Color.White
                : (BurnOnHit ? Color.OrangeRed : Color.LimeGreen);

            using SolidBrush glow = new(glowColor);
            g.FillEllipse(glow, X - 10, Y - 10, W + 20, H + 20);
            using SolidBrush body = new(bodyColor);
            g.FillEllipse(body, Rect);
            using Pen edge = new(edgeColor, IsParried ? 3 : 2);
            g.DrawEllipse(edge, Rect);
            return;
        }

        if (Type == AttackType.BlackBall)
        {
            using SolidBrush glow = new(IsParried
                ? Color.FromArgb(80, 80, 255, 255)
                : Color.FromArgb(45, 120, 120, 255));
            g.FillEllipse(glow, X - 8, Y - 8, W + 16, H + 16);
            using SolidBrush body = new(IsParried
                ? Color.FromArgb(35, 215, 235)
                : Color.FromArgb(20, 20, 25));
            g.FillEllipse(body, Rect);
            using Pen edge = new(IsParried ? Color.White : Color.DimGray, IsParried ? 3 : 2);
            g.DrawEllipse(edge, Rect);
        }
        else
        {
            using SolidBrush body = new(IsParried ? Color.FromArgb(255, 235, 80) : Color.Gold);
            g.FillRectangle(body, Rect);
            using Pen edge = new(IsParried ? Color.White : Color.Red, 2);
            g.DrawRectangle(edge, X, Y, W, H);
            float pulse = 1f + MathF.Sin(frame * 0.2f) * 0.08f;
            using Pen inner = new(IsParried ? Color.White : Color.FromArgb(220, 255, 240, 90), 2);
            float inset = W * (1 - pulse) * 0.25f;
            g.DrawRectangle(inner, X + inset, Y + inset, W - inset * 2, H - inset * 2);
        }
    }
}

public enum ObstacleKind
{
    Ground,
    Floating
}

public sealed class RectObj
{
    public float X;
    public readonly float Y;
    public readonly int W;
    public readonly int H;
    public readonly ObstacleKind Kind;

    public RectangleF Rect => new(X, Y, W, H);

    public RectObj(float x, float y, int w, int h, ObstacleKind kind = ObstacleKind.Ground)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
        Kind = kind;
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundRectangle(this Graphics g, Brush brush, Rectangle rect, int rx, int ry)
    {
        using GraphicsPath path = Rounded(rect, rx, ry);
        g.FillPath(brush, path);
    }

    public static void FillRoundRectangle(this Graphics g, Brush brush, RectangleF rect, float rx, float ry)
    {
        using GraphicsPath path = Rounded(rect, rx, ry);
        g.FillPath(brush, path);
    }

    public static void DrawRoundRectangle(this Graphics g, Pen pen, Rectangle rect, int rx, int ry)
    {
        using GraphicsPath path = Rounded(rect, rx, ry);
        g.DrawPath(pen, path);
    }

    public static void DrawRoundRectangle(this Graphics g, Pen pen, RectangleF rect, float rx, float ry)
    {
        using GraphicsPath path = Rounded(rect, rx, ry);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath Rounded(Rectangle rect, int rx, int ry)
    {
        return Rounded(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), rx, ry);
    }

    private static GraphicsPath Rounded(RectangleF rect, float rx, float ry)
    {
        float width = Math.Max(1, rect.Width);
        float height = Math.Max(1, rect.Height);
        float arcW = Math.Min(rx * 2, width);
        float arcH = Math.Min(ry * 2, height);

        GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, arcW, arcH, 180, 90);
        path.AddArc(rect.Right - arcW, rect.Y, arcW, arcH, 270, 90);
        path.AddArc(rect.Right - arcW, rect.Bottom - arcH, arcW, arcH, 0, 90);
        path.AddArc(rect.X, rect.Bottom - arcH, arcW, arcH, 90, 90);
        path.CloseFigure();
        return path;
    }
}















