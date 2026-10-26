namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private int forgeLevel,forgeFailures;
    private static readonly float[] ForgeOdds={1f,.90f,.70f,.50f,.35f,.20f,.19f,.18f,.17f,.16f,.15f,.14f};
    private float forgeFlash,forgeLock;
    private string forgeMessage="";
    private readonly Rectangle forgeRollRect=new(265,405,220,64),forgeLeaveRect=new(515,405,220,64);
    private int ForgeLimit=>15;
    private float ForgeChance=>ForgeOdds[Math.Min(forgeLevel,ForgeOdds.Length-1)];
    private string ForgeValue(int level)=>$"{Math.Max(12,60-level*6)/60f:0.0}초 / 범위 +{level*8} / 회복 {5+level}%";
    private void OpenForge(){forgeLevel=forgeFailures=0;forgeMessage="성공하면 세 효과가 함께 강화됩니다";forgeFlash=forgeLock=0;ApplyForgeLevel();}
    private void ApplyForgeLevel(){parryCooldownReduction=Math.Min(8,forgeLevel)*6;parryRangeBonus=Math.Min(15,forgeLevel)*8;lifeStealPercent=.05f+Math.Min(15,forgeLevel)*.01f;}
    private void RollForge()
    {
        if(forgeLock>0||forgeLevel>=ForgeLimit||forgeFailures>=3)return;
        bool success=rng.NextSingle()<ForgeChance;forgeLock=24;forgeFlash=60;
        if(success){forgeLevel++;forgeMessage=$"세 효과 강화 성공  ·  +{forgeLevel}";audio.Play("reward");}
        else{forgeLevel=0;forgeFailures++;forgeMessage=forgeFailures>=3?"실패 3회 · 강화 종료. E로 출전하세요":"실패 · 세 효과가 기본 수치로 초기화됩니다";audio.Play("crash");}
        ApplyForgeLevel();
    }
    private void FinishForge(){if(forgeLock>0)return;ApplyForgeLevel();pendingDraft=0;BeginStageTravel(4);}
    private void HandleForgeKey(Keys key){if(key==Keys.Space||key==Keys.Enter)RollForge();if(key==Keys.E)FinishForge();}
    private void HandleForgeMouse(int x,int y){if(forgeRollRect.Contains(x,y))RollForge();else if(forgeLeaveRect.Contains(x,y))FinishForge();}
    private void DrawForge(Graphics g)=>DrawSoulsForge(g);
}
