namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool practiceActive,practiceProtect=true,practiceFreeQ=true;
    private int practicePreset,practiceQ=1,practicePassive,practiceHits,practiceReflections,practiceClears;
    private float practiceClock;
    private static readonly string[] PracticeNames={"기본 조작 · 장애물","이상진 · 탄환 / 중력반전","파괴옥 · 초록 구슬","파괴옥 · 광폭화","파괴옥 · 메테오","강로디언 · 음표","강로디언 · 탈덕안돼","강로디언 · 멘헤라 돌진","종결 · (진) 파괴옥","종결 · 잿불 거인","종결 · 앙코르 인형","강로디언 · 전체 전투","강로디언 · 최후발악 QTE","이상진 · 시간정지 / 시계 리듬"};
    private static readonly string[] PracticeTips={"↑ 두 번 점프 / ↓ 슬라이딩. 장애물은 발차기로 없앨 수 없습니다.","검은 탄환은 반사. 상하반전은 10초마다 4초. 가속은 적만 ×2. 시간정지는 W/E로 시계 리듬.","초록 구슬은 발차기로 반사합니다. 흡수 중에도 발차기를 쓸 수 있습니다.","화염탄 반사. 5회 적중마다 5초 동안 방어가 해제됩니다.","중앙으로 이동한 뒤 집중 낙하. 피격 시 최대 체력 50~100%. 메테오는 위로 반사합니다.","음표가 다가올 때 SPACE. 정확한 반사와 Q 효과를 익혀보세요.","탈 · 덕 · 안 · 돼! 마지막 ‘돼!’에 발차기. 피해는 2,000입니다.","빨간 선을 보고 점프 / 슬라이딩. 돌진은 발차기로 반사할 수 없습니다.","이상진 소환 뒤 시간정지. 해제 직전 발차기를 준비하고 사방의 돌 탄환을 연속 반사하세요.","화염탄은 반사. 붉게 경고하는 분화 위치를 피합니다.","음표는 반사. 아래쪽 붉은 줄은 점프, 위쪽 줄은 슬라이딩.","음표 / 레이저 / 돌진, 체력 10 감소 시 멘헤라 변신까지 연습합니다.","SPACE를 떼었다 다시 누르며 연타. 10초 동안 70회로 파란 레이저를 밀어붙이세요.","왼쪽 시계는 W · 오른쪽은 E. 중앙에 겹칠 때 6회. 판정 ±8프레임 · 속도 ×2 · 실패 −3,000, 성공 보스 −1."};
    private bool PracticeLocksPhase=>practiceActive&&practicePreset!=11&&practicePreset!=12;
    private string PracticeQHelp=>(ActivePower)practiceQ switch
    {
        ActivePower.Absorb=>"4초간 흡수 → Q를 다시 눌러 발사. 발차기도 가능합니다.",
        ActivePower.Dodge=>"1.8초간 문워크 회피. 불투명 상태로 발차기도 가능합니다.",
        ActivePower.Eat=>"5초간 닿은 장애물을 먹고 350 회복. 탄환은 먹지 않습니다.",
        ActivePower.Slow=>"4초간 집중. 적 속도 48%, 실제 몸·발차기·공격 판정을 표시합니다.",
        ActivePower.Echo=>"6초간 반사에 유령 옆차기 추가. 0.47초 뒤 피해 0.65.",
        ActivePower.Cinder=>"6초간 암석·용암 변신. 피해1 불 쿠나이, 2적중당 취약+1. 메테오 제외.",
        _=>"Q 없이 기본 조작과 보스의 공격을 연습합니다."
    };
    private string PracticePassiveHelp=>(PassivePower)practicePassive switch
    {
        PassivePower.Feast=>"5회 반사마다 체력 10% 회복, 보스 피해 1. 메테오는 회복만.",
        PassivePower.Truce=>"체력 500 미만에서 최대 체력의 50% 회복. H로 시험.",
        PassivePower.Clone=>"사망 직전 초록 클론으로 계승. 체력 50% · 공격 150%. J로 시험.",
        PassivePower.Precision=>"정확한 반사 3회마다 강한 잔향 옆차기. 메테오는 3% 회복만.",
        PassivePower.Bastion=>"공백",
        PassivePower.Flow=>"반사마다 Q 대기 −1초와 흡기 연출. Q를 먼저 사용해 보세요.",
        _=>"패시브 없이 연습합니다."
    };
    private void OpenPractice()
    {
        practiceActive=false;SetPaused(false);ReleaseInputs();audio.CancelLaserCharge();audio.StopSliding();ResetEncounterFlow();phaseIntroKind=0;
        state=GameState.PracticeSelect;sceneFadeTimer=0;
    }
    private void StartPractice()
    {
        practiceActive=true;practiceHits=practiceReflections=practiceClears=0;practiceClock=0;
        ResetPremiumRun();ResetRebuild(true);selectedCookie=0;
        activeSkill=(ActivePower)practiceQ;passiveSkill=(PassivePower)practicePassive;
        parryCooldownReduction=parryRangeBonus=0;lifeStealPercent=.05f;score=0;
        int level=practicePreset==0?1:practicePreset is 1 or 13?2:practicePreset<=4?3:practicePreset<=7||practicePreset is 11 or 12?4:5;
        if(level==5)finalBossKind=practicePreset-8;
        StartRound(level);ResetEncounterFlow();sceneFadeTimer=0;warningTimer=0;phaseIntroKind=0;
        if(practicePreset==3||practicePreset==4){purgaeTransformed=true;purgaeSkill2Cooldown=0;purgaeSkill1Cooldown=practicePreset==4?0:220;}
        if(practicePreset==7){menheraMode=true;melodySequenceCooldown=99999;dashCooldown=25;}
        if(practicePreset==6)melodySequenceCooldown=15;
        hp=PlayerMaxHealth;audio.Play("select");
        if(practicePreset==13)isangjinClockCooldown=90;
        if(practicePreset==12){bossDamage=bossHitRequired-.5f;TryBeginMelodyLastStand();}
    }
    private bool HandlePracticeKey(Keys key)
    {
        if(state==GameState.Title&&key==Keys.T){OpenPractice();return true;}
        if(state==GameState.PracticeSelect)
        {
            if(key==Keys.Escape){state=GameState.Title;return true;}
            if(key==Keys.Left)practicePreset=(practicePreset+PracticeNames.Length-1)%PracticeNames.Length;
            if(key==Keys.Right)practicePreset=(practicePreset+1)%PracticeNames.Length;
            if(key==Keys.Q)practiceQ=(practiceQ+1)%(ActivePowerCount+1);
            if(key==Keys.E){practicePassive=practicePassive switch{0=>1,1=>2,2=>3,3=>4,4=>6,_=>0};if(practicePassive==(int)PassivePower.Flow)practiceFreeQ=false;}
            if(key==Keys.D)practiceProtect=!practiceProtect;
            if(key==Keys.C)practiceFreeQ=!practiceFreeQ;
            if(key==Keys.Enter)StartPractice();
            return true;
        }
        if(!practiceActive)return false;
        if(key==Keys.Escape){OpenPractice();return true;}
        if(key==Keys.Tab||key==Keys.R&&state==GameState.GameOver){StartPractice();return true;}
        if(!gamePaused&&state==GameState.Round&&!PhaseIntroActive&&!IsangjinClockActive)
        {
            if(key==Keys.H){hp=400;TrySurvive();return true;}
            if(key==Keys.J){invuln=0;ReceiveHit(PlayerMaxHealth+1,"연습 피격",Coral,true);return true;}
        }
        return false;
    }
    private void UpdatePracticeEncounter(float step)
    {
        practiceClock+=step;
        if(practiceFreeQ&&activeTimer<=0)activeCooldown=0;
        switch(practicePreset)
        {
            case 0:
                obstacleSpawnTimer-=step;
                if(obstacleSpawnTimer<=0){if(nextObstacleIsRising)SpawnRisingObstacle();else SpawnEnemy1Obstacle();nextObstacleIsRising=!nextObstacleIsRising;obstacleSpawnTimer=95;}
                break;
            case 1:case 13:UpdateRound2(step);break;
            case 2:purgaeSkill1Cooldown=99999;UpdateRound3(step);break;
            case 3:UpdateRound3(step);break;
            case 4:
                purgaeSkill2Active=false;Array.Fill(purgaeSkill2BallVisible,false);
                if(!MeteorSceneActive){attackTimer+=step;if(attackTimer>90){attackTimer=0;StartMeteorStorm();}}
                break;
            case 5:
                attackTimer+=step;if(attackTimer>=60){attackTimer-=60;SpawnMelodyNote();}break;
            case 6:dashCooldown=99999;UpdateRound4(step);break;
            case 7:
                if(dashActive)UpdateMelodyDash(step);
                else{UpdateMenheraWander(step);dashCooldown-=step;if(dashCooldown<=0)StartMelodyDash();}
                break;
            case 11:UpdateRound4(step);break;
            default:UpdateFinalBoss(step);break;
        }
    }
    private void DrawPracticeMenu(Graphics g)
    {
        DrawSoulsMenuBase(g,.94f);Label(g,"TRAINING",new(100,30,800,22),12,Gold);
        SoulsHeading(g,"연습 모드",new(100,62,800,49),32,Paper);DrawSoulsRule(g,350,125,300);
        PanelSurface(g,new(105,148,790,130),Gold);
        Label(g,"←     "+PracticeNames[practicePreset]+"     →",new(128,166,744,37),22,Paper);
        Label(g,PracticeTips[practicePreset],new(134,212,732,44),12,Steel,true,false);
        Label(g,$"Q   능력: {(practiceQ==0?"없음":ExpandedActiveName(practiceQ))}",new(120,285,760,30),18,Paper);
        Label(g,PracticeQHelp,new(120,320,760,22),11,Steel);
        Label(g,$"E   특성: {(practicePassive==0?"없음":ExpandedPassiveName(practicePassive))}",new(120,351,760,30),18,Paper);
        Label(g,PracticePassiveHelp,new(120,386,760,22),11,Steel);
        Label(g,$"D   체력 보호 {(practiceProtect?"켜짐":"꺼짐")}     /     C   Q 대기시간 {(practiceFreeQ?"없음":"적용")}",new(120,426,760,30),14,Gold);
        PanelSurface(g,new(325,466,350,55),Gold,true);Label(g,"ENTER  연습 시작",new(325,466,350,55),18,Paper);
        Label(g,"전투: TAB 재시작 · ESC 연습 설정 · H 체력 400 · J 치명타 받기",new(100,546,800,25),11,Steel);
    }
    private void DrawPracticeHud(Graphics g)
    {
        if(!practiceActive||state!=GameState.Round)return;
        PanelSurface(g,new(340,20,232,76),Gold);
        Label(g,$"연습  ·  반사 {practiceReflections} / 피격 {practiceHits} / 처치 {practiceClears}",new(348,27,216,18),10,Paper);
        Label(g,$"체력 보호 {(practiceProtect?"ON":"OFF")}  ·  Q 대기 {(practiceFreeQ?"없음":"적용")}",new(348,50,216,18),9.5f,Steel);
        Label(g,"TAB 재시작  ·  ESC 설정",new(348,72,216,18),9.5f,Gold);
    }
}
