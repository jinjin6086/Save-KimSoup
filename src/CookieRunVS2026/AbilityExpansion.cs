using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // IDs stay stable in checkpoints; the retired passive ID remains reserved as a blank slot.
    private const int ActivePowerCount=6, PassivePowerCount=6;
    private readonly List<AbilityEcho> abilityEchoes=new(8);
    private int preciseReflections,flowRefunds;
    private float abilityPulse,wardBreakFx;
    private bool echoImpactAudio;
    private const float EchoFlightFrames=28;
    private sealed class AbilityEcho
    {
        public float X,Y,TargetX,TargetY,Delay=EchoFlightFrames,Damage;
        public bool Precision;
    }

    private float AbilityEnemyTimeScale=>activeSkill==ActivePower.Slow&&activeTimer>0?.48f:1f;

    private void ResetAbilityExpansionRound()
    {
        ResetCinderRound();CancelAbilityEchoes();preciseReflections=flowRefunds=0;
        abilityPulse=wardBreakFx=breathFxTimer=0;
    }

    private void ShuffleSkillDraft()
    {
        int count=pendingDraft==1?ActivePowerCount:PassivePowerCount;
        for(int i=0;i<count;i++)hiddenCards[i]=i+1;
    }

    private void UpdateExpandedAbilities(float step)
    {
        breathFxTimer=Math.Max(0,breathFxTimer-step);abilityPulse=Math.Max(0,abilityPulse-step);wardBreakFx=Math.Max(0,wardBreakFx-step);
        UpdateEchoContacts(step);UpdateCinderCombat(step);
        for(int i=abilityEchoes.Count-1;i>=0;i--)
        {
            AbilityEcho echo=abilityEchoes[i];echo.Delay-=step;
            if(echo.Delay>0)continue;
            abilityEchoes.RemoveAt(i);
            if(round<2||bossHitRequired<=0)continue;
            pendingBossDamage=echo.Damage;
            AddEchoContact(echo);
            echoImpactAudio=true;
            try{RegisterBossParryHit(echo.Precision?650:400,false);}finally{echoImpactAudio=false;}
            abilityPulse=24;
            if(state!=GameState.Round||PhaseIntroActive)break;
        }
        if(abilityEchoes.Count==0)audio.StopEchoFlight();
        else audio.PlayEchoFlight();
    }

    private void QueueAbilityEcho(float damage,bool precision)
    {
        // Only reflections of attacks that can hurt a boss create damaging echoes.
        // Upward meteor reflections never enter this queue, even in a meteor barrage.
        if(meteorParryNoBossDamage||round<2||abilityEchoes.Count>=8)return;
        // The line is locked at launch. Neither a moving boss nor an animation
        // easing function can bend the kick away from this straight trajectory.
        abilityEchoes.Add(new(){X=px+44,Y=py+42,TargetX=bossX+65,TargetY=bossY+65,Damage=damage,Precision=precision});
        audio.PlayEchoFlight();
    }

    private void OnExpandedReflection(bool perfect)
    {
        if(activeSkill==ActivePower.Echo&&activeTimer>0)QueueAbilityEcho(.65f,false);
        if(passiveSkill==PassivePower.Precision&&perfect)
        {
            if(++preciseReflections>=3)
            {
                preciseReflections=0;abilityPulse=35;
                if(meteorParryNoBossDamage)
                {
                    hp=Math.Min(PlayerMaxHealth,hp+PlayerMaxHealth*.03f);
                    augmentNotice="발톱을 깎다  ·  메테오 반사 / 체력 3% 회복";
                }
                else
                {
                    QueueAbilityEcho(1.2f,true);
                    augmentNotice="발톱을 깎다  ·  잔향 발차기 준비";
                }
                augmentFlashTimer=80;
            }
        }
        if(passiveSkill==PassivePower.Flow)
        {
            float refunded=Math.Min(60,activeCooldown);
            activeCooldown=Math.Max(0,activeCooldown-60);
            if(refunded>0){flowRefunds++;abilityPulse=16;breathFxTimer=42;audio.Play("inhale");}
        }
    }

    private static string ExpandedActiveName(int value)=>(ActivePower)value switch
    {
        ActivePower.Absorb=>"빨아들이기",ActivePower.Dodge=>"피한다",ActivePower.Eat=>"건강해진다",
        ActivePower.Slow=>"느려.",ActivePower.Echo=>"퓨리오소",ActivePower.Cinder=>"타오르는 뱃살",
        _=>"1라운드 클리어 후 획득"
    };
    private static string ExpandedPassiveName(int value)=>(PassivePower)value switch
    {
        PassivePower.Feast=>"다먹어주자",PassivePower.Truce=>"무승부로 하지 않을래..?",PassivePower.Clone=>"이판사판이다!!",
        PassivePower.Precision=>"발톱을 깎다",PassivePower.Bastion=>"공백",PassivePower.Flow=>"살을 빼다",
        _=>"2라운드 클리어 후 획득"
    };
    private string CardName(int value)=>pendingDraft==1?ExpandedActiveName(value):ExpandedPassiveName(value);
    private string[] CardLines(int value)=>pendingDraft==1?((ActivePower)value switch
    {
        ActivePower.Absorb=>new[]{"4초 동안 날아오는 공격 흡수","Q를 다시 눌러 스파크 볼 발사","피해 1 + 흡수당 0.6 / 최대 12개","재사용 14초 · 발차기도 사용 가능"},
        ActivePower.Dodge=>new[]{"1.8초 동안 모든 공격 회피","장애물 · 레이저 · 돌진도 회피","공격이 몸 앞을 스쳐 지나갑니다","재사용 12초"},
        ActivePower.Eat=>new[]{"5초 동안 닿은 장애물을 먹기","장애물 하나당 체력 350 회복","빨대로 장애물을 쏙 빨아들입니다","재사용 15초 · 탄환은 제외"},
        ActivePower.Slow=>new[]{"4초 동안 적과 장애물 속도 48%","달리기와 발차기는 원래 속도","레이저 · 돌진 · 메테오도 느려짐","재사용 16초 · 무적은 아닙니다"},
        ActivePower.Echo=>new[]{"6초 동안 파란 아우라와 잔향 발차기","반사 성공 0.47초 뒤 피해 0.65","메테오는 위로 돌려보내기만 합니다","재사용 18초 · 분신 피해 50% 적용"},
        ActivePower.Cinder=>new[]{"6초간 암석화 · 흐르는 용암","피해 1 불 쿠나이 · 메테오 제외","2적중당 취약 +1 · 반사 피해 +0.5","재사용 16초 · 피해 감소 없음"},
        _=>Array.Empty<string>()
    }):((PassivePower)value switch
    {
        PassivePower.Feast=>new[]{"발차기 반사 5회마다 폭식 발동","보스 체력 1 흡수 · 체력 10% 회복","메테오 반사 때는 회복만 적용","성공 횟수는 전투 화면에서 확인"},
        PassivePower.Truce=>new[]{"체력이 500 미만이면 발동","자신의 체력을 전부 회복","보스가 받은 피해의 50% 회복","라운드당 한 번 발동"},
        PassivePower.Clone=>new[]{"쓰러지기 직전 클론 소환","최대 체력 2,500 / 공격력 50%","클론이 다음 라운드도 이어갑니다","게임당 한 번 발동"},
        PassivePower.Precision=>new[]{"정확한 발차기 반사 3회마다","0.47초 뒤 강한 잔향 발차기 추가","추가 피해 1.2 · 분신 피해 50% 적용","메테오는 보스 피해 없이 체력 3% 회복"},
        PassivePower.Bastion=>new[]{"", "", "", ""},
        PassivePower.Flow=>new[]{"발차기로 공격을 반사할 때마다","Q 재사용 대기시간이 1초 감소","메테오와 레이저 반사도 인정","대기시간은 0초 아래로 줄지 않음"},
        _=>Array.Empty<string>()
    });

    private string ActiveHudStatus=>activeSkill==ActivePower.None?"라운드 1 보상에서 선택":
        activeTimer>0?activeSkill switch
        {
            ActivePower.Absorb=>$"흡수 {absorbedCount} / 12  ·  다시 눌러 발사",
            ActivePower.Slow=>$"느려. {activeTimer/60:0.0}초  ·  적 속도 48%",
            ActivePower.Echo=>$"잔향 {activeTimer/60:0.0}초  ·  반사에 추가 타격",
            ActivePower.Cinder=>$"암석화 {activeTimer/60:0.0}초  ·  메테오 제외",
            _=>$"지속 {activeTimer/60:0.0}초"
        }:activeCooldown>0?$"재사용 {activeCooldown/60:0.0}초":"사용 준비";
    private string PassiveHudStatus=>passiveSkill switch
    {
        PassivePower.Feast=>$"반사 {passiveParries} / 5",
        PassivePower.Truce=>truceUsed?"이번 라운드 사용 완료":"체력 500 미만 · 1회 회복",
        PassivePower.Clone=>cloneUsed?"클론으로 전투 중":"쓰러지면 클론 계승 · 1회",
        PassivePower.Precision=>$"정확한 반사 {preciseReflections} / 3",
        PassivePower.Bastion=>"공백",
        PassivePower.Flow=>$"반사마다 Q 대기 −1초 · 이번 라운드 {flowRefunds}회",
        _=>"라운드 2 보상에서 선택"
    };

    private void DrawExpandedAbilityEffects(Graphics g)
    {
        if(state!=GameState.Round)return;

        
        foreach(var echo in abilityEchoes)DrawSpectralEcho(g,echo);
        DrawEchoContacts(g);DrawCinderProjectiles(g);
        DrawInhale(g);
    }
}


