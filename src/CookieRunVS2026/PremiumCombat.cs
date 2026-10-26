using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private enum ActivePower { None, Absorb, Dodge, Eat, Slow, Echo, Cinder }
    private enum PassivePower { None, Feast, Truce, Clone, Precision, Bastion, Flow }
    private ActivePower activeSkill;
    private PassivePower passiveSkill;
    private float activeTimer, activeCooldown, pendingBossDamage=1, skillVisual, cinematicTimer;
    private int absorbedCount, passiveParries;
    private bool truceUsed, cloneUsed, cloneActive;
    private float PlayerMaxHealth=>BasePlayerMaxHealth*(cloneActive?.5f:1);
    private int pendingDraft, selectedCard=-1;
    private readonly int[] hiddenCards={1,2,3,4,5,6};
    private int skillCardFocus;
    private const float SkillChoiceRevealFrames=75;
    private float cardReveal, cardClock;
    private ActivePower checkpointActive;
    private PassivePower checkpointPassive;
    private bool checkpointClone, checkpointCloneUsed;
    private float dashCooldown=410,dashTimer,dashY,dashSlope;
    private bool dashActive,dashChecked;
    private int dashPass,dashBeep;
    private bool sparkActive;
    private float sparkX,sparkY,sparkDamage;
    private float animationClock;
    private Image? meteorArt;
    private Bitmap[] motionFrames=Array.Empty<Bitmap>();
    private Bitmap? cinemaFrame;
    private readonly ImageAttributes grayscale=new();
    private readonly ImageAttributes spriteAlpha=new();
    private readonly ColorMatrix spriteMatrix=new();

    private void LoadPremiumAssets()
    {
        LoadBossAttackIllustrations();
        LoadMeteorTails();
        meteorArt=LoadImage("MeteorCore.png");
        if(meteorArt!=null)
        {
            var original=meteorArt;var filtered=new Bitmap(112,112,PixelFormat.Format32bppPArgb);
            using(var cg=Graphics.FromImage(filtered)){cg.CompositingMode=CompositingMode.SourceCopy;cg.InterpolationMode=InterpolationMode.HighQualityBicubic;cg.DrawImage(original,new Rectangle(0,0,112,112));}
            meteorArt=filtered;original.Dispose();
        }
        enemy6=LoadImage("enemy6.png");
        LoadCleanMotionAssets();LoadSurgeMotion();
        grayscale.SetColorMatrix(new ColorMatrix(new[]{new[]{.299f,.299f,.299f,0,0},new[]{.587f,.587f,.587f,0,0},new[]{.114f,.114f,.114f,0,0},new[]{0,0,0,1f,0},new[]{0,0,0,0,1f}}));
    }
    private void DisposePremium(){DisposeMeteorTails();DisposeSurgeMotion();purgaeRockArt?.Dispose();DisposeReadability();enemy6?.Dispose();melodyDash?.Dispose();melodyAngry?.Dispose();foreach(var p in panelSurfaces.Values)p.Dispose();meteorArt?.Dispose();foreach(var f in motionFrames)f.Dispose();cinemaFrame?.Dispose();grayscale.Dispose();spriteAlpha.Dispose();}
    private void ResetPremiumRun(){activeSkill=ActivePower.None;passiveSkill=PassivePower.None;cloneActive=cloneUsed=false;pendingDraft=0;forgeLevel=0;ResetAbilityExpansionRound();}
    private void ResetPremiumRound()
    {
        CancelMeteorStorm(false);ResetEncore();ResetBattleDirector();ResetRunnerMotion();
        ResetNewBattle();ResetAbilityExpansionRound();
        if(passiveSkill==PassivePower.Bastion)passiveSkill=PassivePower.None;
        activeTimer=activeCooldown=skillVisual=cinematicTimer=0;focusEntryTimer=focusExitTimer=0;absorbedCount=passiveParries=0;
        truceUsed=false;dashActive=sparkActive=false;dashCooldown=410;dashTimer=0;
        selectedCard=-1;pendingDraft=0;pendingBossDamage=1;
        // Clone persists through later rounds, so its revive cannot be farmed by advancing.
    }
    private void SavePremiumCheckpoint(){checkpointActive=activeSkill;checkpointPassive=passiveSkill;checkpointClone=cloneActive;checkpointCloneUsed=cloneUsed;}
    private void RestorePremiumCheckpoint(){activeSkill=checkpointActive;passiveSkill=checkpointPassive;cloneActive=checkpointClone;cloneUsed=checkpointCloneUsed;}
    private void UpdatePremiumPresentation(float step)
    {
        cinematicTimer=Math.Max(0,cinematicTimer-step);skillVisual=Math.Max(0,skillVisual-step);
        focusEntryTimer=FocusActive?Math.Max(0,focusEntryTimer-step):0;
        focusExitTimer=FocusReturning?Math.Max(0,focusExitTimer-step):0;
        UpdateRunnerMotion(step);UpdateSlideSound(step);
        UpdateDirectorPresentation(step);
        UpdateEncorePresentation(step);
        UpdateNewPresentation(step);
        animationClock+=step*(cinematicTimer>0?.24f:1);
    }
    private void UpdatePremiumCombat(float step)
    {
        UpdateNewBattle(step);
        UpdateMeteorBarrage(step*AbilityEnemyTimeScale);
        activeCooldown=Math.Max(0,activeCooldown-step);
        if(activeTimer>0)
        {
            activeTimer=Math.Max(0,activeTimer-step);
            if(activeTimer==0&&activeSkill==ActivePower.Absorb)ReleaseAbsorption();
            else if(activeTimer==0&&activeSkill==ActivePower.Slow)EndHackedFocus();
        }
        if(sparkActive)
        {
            float dx=bossX+65-sparkX,dy=bossY+65-sparkY,len=MathF.Sqrt(dx*dx+dy*dy);
            if(len<25*step+48)
            {
                sparkActive=false;pendingBossDamage=sparkDamage;RegisterBossParryHit(1800,false);
                Burst(bossX+65,bossY+65,Mint,60);audio.Play("beam");
            }
            else {sparkX+=dx/len*25*step;sparkY+=dy/len*25*step;}
        }
        if(state==GameState.Round&&!PhaseIntroActive)UpdateExpandedAbilities(step);
    }
    private void OpenSkillCards()
    {
        if(pendingDraft==0)return;
        state=GameState.AugmentSelect;ReleaseInputs();cardClock=cardReveal=0;selectedCard=-1;skillCardFocus=0;
        if(pendingDraft==3){OpenForge();return;}
        ShuffleSkillDraft();
        audio.Play("reward");
    }
    private void SelectSkillCard(int index)
    {
        if(state!=GameState.AugmentSelect||pendingDraft is not (1 or 2)||selectedCard>=0||index<0||index>=DraftCardCount||cardClock<12)return;
        if(!DraftCardAvailable(index))return;
        selectedCard=skillCardFocus=index;cardReveal=0;
        if(pendingDraft==1)activeSkill=(ActivePower)hiddenCards[index];else passiveSkill=(PassivePower)hiddenCards[index];
        audio.Play("reward");
    }
    private void UpdateCards(float step)
    {
        if(pendingDraft==3){forgeFlash=Math.Max(0,forgeFlash-step);forgeLock=Math.Max(0,forgeLock-step);return;}
        cardClock+=step;if(selectedCard<0)return;cardReveal+=step;
        if(cardReveal>=SkillChoiceRevealFrames)FinishSkillChoice();
    }
    private void FinishSkillChoice()
    {
        if(selectedCard<0||cardReveal<24)return;
        int next=transitionNextRound;pendingDraft=0;BeginStageTravel(next);
    }
    private void HandleSkillChoiceKey(Keys key)
    {
        if(selectedCard>=0){if(key is Keys.Enter or Keys.Space)FinishSkillChoice();return;}
        if(cardClock<12)return;
        int code=(int)key;
        if(code>=(int)Keys.D1&&code<=(int)Keys.D6){SelectSkillCard(code-(int)Keys.D1);return;}
        if(code>=(int)Keys.NumPad1&&code<=(int)Keys.NumPad6){SelectSkillCard(code-(int)Keys.NumPad1);return;}
        if(key==Keys.Right)skillCardFocus=NextDraftCard(1);
        else if(key==Keys.Left)skillCardFocus=NextDraftCard(-1);
        else if(key is Keys.Up or Keys.Down)skillCardFocus=NextDraftCard(3);
        else if(key is Keys.Enter or Keys.Space)SelectSkillCard(skillCardFocus);
    }
    private void UseActiveSkill()
    {
        if(sceneFadeTimer>0||activeSkill==ActivePower.None)return;
        if(activeSkill==ActivePower.Absorb&&activeTimer>0){ReleaseAbsorption();return;}
        if(activeCooldown>0||activeTimer>0)return;
        activeTimer=activeSkill switch{ActivePower.Absorb=>240,ActivePower.Dodge=>108,ActivePower.Eat=>300,ActivePower.Slow=>240,_=>360};
        activeCooldown=activeSkill switch{ActivePower.Absorb=>840,ActivePower.Dodge=>720,ActivePower.Eat=>900,ActivePower.Echo=>1080,_=>960};
        absorbedCount=0;skillVisual=50;
        audio.Play(activeSkill switch{ActivePower.Absorb=>"absorb",ActivePower.Dodge=>"dodge",ActivePower.Slow=>"focusenter",ActivePower.Eat=>"eat",ActivePower.Echo=>"furiosostart",ActivePower.Cinder=>"cinderstart",_=>"charge"});
        if(activeSkill==ActivePower.Dodge){burnTimer=0;}
        if(activeSkill==ActivePower.Slow)BeginHackedFocus();
    }
    private void ReleaseAbsorption()
    {
        activeTimer=0;sparkActive=true;sparkX=px+30;sparkY=py+30;
        sparkDamage=1+Math.Min(12,absorbedCount)*.6f;skillVisual=45;audio.Play("reverse");
    }
    private void ConsumeNearbyObjects()
    {
        if(activeTimer<=0)return;
        if(activeSkill==ActivePower.Absorb && !ParryActive)
        {
            RectangleF zone=new(px-38,py-38,PlayerWidth+100,PlayerHeight+76);
            for(int i=projectiles.Count-1;i>=0;i--)
                if(!projectiles[i].IsParried&&zone.IntersectsWith(projectiles[i].Rect))
                {CaptureAbsorbVisual(projectiles[i]);projectiles.RemoveAt(i);absorbedCount=Math.Min(12,absorbedCount+1);audio.Play("absorb");}
        }
        if(activeSkill==ActivePower.Eat)
        {
            RectangleF mouth=new(px-10,py-15,PlayerWidth+65,PlayerHeight+25);
            int count=0;
            for(int i=obstacles.Count-1;i>=0;i--)if(mouth.IntersectsWith(obstacles[i].Rect)){obstacles.RemoveAt(i);count++;}
            for(int i=risingObstacles.Count-1;i>=0;i--)if(risingObstacles[i].IsActive&&mouth.IntersectsWith(risingObstacles[i].Rect)){risingObstacles.RemoveAt(i);count++;}
            if(count>0){hp=Math.Min(PlayerMaxHealth,hp+count*350);skillVisual=24;audio.Play("eat");Burst(px+45,py+25,Color.LightGreen,20);}
        }
    }
    private void OnPremiumParry()
    {
        OnExpandedReflection(parryWindow>=6);
        if(passiveSkill!=PassivePower.Feast)return;
        if(++passiveParries<5)return;passiveParries=0;
        hp=Math.Min(PlayerMaxHealth,hp+PlayerMaxHealth*.1f);skillVisual=60;
        if(!meteorParryNoBossDamage){pendingBossDamage=1;RegisterBossParryHit(600,false);}audio.Play("eat");
        augmentNotice=meteorParryNoBossDamage?"폭식  ·  체력 10% 회복":"폭식  ·  보스 체력 흡수 / 체력 10% 회복";augmentFlashTimer=90;
    }
    private bool TrySurvive()
    {
        if(hp<500&&passiveSkill==PassivePower.Truce&&!truceUsed)
        {
            truceUsed=true;bossDamage*=.5f;hp=PlayerMaxHealth;skillVisual=65;audio.Play("reward");
            augmentNotice="무승부  ·  서로 회복";augmentFlashTimer=120;
        }
        if(hp>0)return true;
        if(passiveSkill==PassivePower.Clone&&!cloneUsed)
        {
            cloneUsed=cloneActive=true;hp=PlayerMaxHealth;hpTrail=hp;invuln=90;burnTimer=0;skillVisual=80;
            Burst(px+26,py+38,Color.FromArgb(101,255,135),60);audio.Play("clone");
            augmentNotice="클론 등장  ·  최대 체력 50% / 공격력 50%";augmentFlashTimer=140;return true;
        }
        if(practiceActive&&practiceProtect){hp=PlayerMaxHealth;hpTrail=hp;invuln=90;burnTimer=0;augmentNotice="연습 보호 · 체력 회복";augmentFlashTimer=70;return true;}
        return false;
    }
    private void StartMelodyDash()
    {
        if(menheraMode){StartRushCascade();return;}
        fullRush=false;
        dashActive=true;dashPass=0;dashTimer=0;dashChecked=false;dashBeep=-1;
        dashPattern=dashPattern==0?1:0;LockDashPath();
        projectiles.RemoveAll(p=>!p.IsParried);obstacles.Clear();risingObstacles.Clear();
        warningTimer=65;encounterMessage="3연속 돌진  ·  붉은 경로를 피하세요";
    }
    private void LockDashPath()
    {
        // Fixed world-space lanes, independent of player position and current action.
        if(fullRush)return;
        dashY=dashPattern==0?NormalRushA[dashPass]:NormalRushB[dashPass];
        dashSlope=dashPass%2==0?.055f:-.045f;
    }
    private void UpdateMelodyDash(float step)
    {
        if(fullRush){UpdateRushCascade(step);return;}
        float old=dashTimer;dashTimer+=step;
        float warning=DashWarning,travel=DashTravel;
        int beep=(int)(Math.Min(dashTimer,warning-1)/(warning/3));
        if(dashTimer<warning&&beep!=dashBeep){dashBeep=beep;audio.Play("dashwarn");}
        if(old<warning&&dashTimer>=warning)audio.Play("dash");
        float cross=dashPass%2==0?warning+travel*(W+100-(px+26))/(W+200):warning+travel*(px+26+100)/(W+200);
        if(!dashChecked&&dashTimer>=cross)
        {
            dashChecked=true;float lineY=DashLaneY(px+26);
            float top=sliding?py+36:py+8,bottom=py+PlayerHeight-4;
            if(lineY+17>=top&&lineY-17<=bottom)ReceiveHit(1000,"돌진",Coral,true);
        }
        if(dashTimer>=warning+travel+10)
        {
            if(++dashPass>=3){dashActive=false;fullRush=false;dashCooldown=menheraMode?rng.Next(220,291):rng.Next(340,451);obstacleSpawnTimer=65;}
            else{dashTimer=0;dashChecked=false;dashBeep=-1;LockDashPath();}
        }
    }
    private string ActiveName=>ExpandedActiveName((int)activeSkill);
    private string PassiveName=>ExpandedPassiveName((int)passiveSkill);
}






