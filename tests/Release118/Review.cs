using CookieRunVS2026;
using System.Reflection;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Diagnostics;

static class Review
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly List<object> results=new();static string output="";
    static object Get(object p,string n)=>p.GetType().GetField(n,F)!.GetValue(p)!;
    static T G<T>(object p,string n)=>(T)Get(p,n);
    static T P<T>(object p,string n)=>(T)p.GetType().GetProperty(n,F)!.GetValue(p)!;
    static void Set(object p,string n,object v){var f=p.GetType().GetField(n,F)!;f.SetValue(p,f.FieldType.IsEnum?Enum.ToObject(f.FieldType,v):v);}
    static object? Call(object p,string n,params object[] a){try{return p.GetType().GetMethod(n,F)!.Invoke(p,a);}catch(TargetInvocationException e){throw e.InnerException!;}}
    static void Check(bool b,string title){results.Add(new{pass=b,title});if(!b)throw new Exception(title);}
    static void Tap(GamePanel p,Keys k){p.HandleKeyDown(new(k));p.HandleKeyUp(new(k));}
    static void Tick(GamePanel p,int frames){for(int i=0;i<frames;i++)p.UpdateGame(1f/60);}
    static void Start(GamePanel p,int round)
    {
        Set(p,"practiceActive",false);Set(p,"selectedCookie",0);Set(p,"cloneActive",false);Set(p,"cloneUsed",false);
        Call(p,"SetPaused",false);Call(p,"StartRound",round);Call(p,"ResetEncounterFlow");Set(p,"sceneFadeTimer",0f);
        Set(p,"activeSkill",0);Set(p,"activeTimer",0f);Set(p,"passiveSkill",0);Set(p,"invuln",0f);Set(p,"lifeStealPercent",0f);
    }
    static void Shot(GamePanel p,string n)
    {
        using var b=new Bitmap(1600,960,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(b);
        g.ScaleTransform(1.6f,1.6f);Call(p,"RenderArenaFrame",g);b.Save(Path.Combine(output,n+".png"));
    }
    static void Beam(GamePanel p,float timer)
    {
        Set(p,"melodyStep",3);Set(p,"melodyInterval",42f);Set(p,"melodyWordTimer",42f);
        Call(p,"BeginMelodyLaserApproach");Set(p,"melodyWordTimer",timer);
    }
    [STAThread]static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);Environment.CurrentDirectory=Path.GetFullPath(args[1]);
        try
        {
            using var p=new GamePanel{Size=new(1600,960)};Call(Get(p,"audio"),"Toggle");
            Set(p,"lobbyClock",20f);Shot(p,"01-lobby-open");Set(p,"lobbyClock",86f);Shot(p,"02-lobby-middle");Set(p,"lobbyClock",150f);Shot(p,"03-lobby-closed");
            var frames=G<Bitmap[]>(p,"lobbyResolveFrames");Check(frames.Length==25&&frames.All(x=>x.Width==810&&x.Height==1080),"25 high-resolution, aspect-fitted lobby frames");
            frames[0].Save(Path.Combine(output,"lobby-key-open.png"));frames[12].Save(Path.Combine(output,"lobby-key-middle.png"));frames[24].Save(Path.Combine(output,"lobby-key-closed.png"));
            foreach(var pair in new[]{(2,15),(3,20),(4,40)}){Start(p,pair.Item1);Check(G<int>(p,"bossHitRequired")==pair.Item2,$"Round {pair.Item1} HP {pair.Item2}");}
            Start(p,3);Set(p,"bossDamage",8f);Call(p,"RegisterBossParryHit",1200,true);Check(!G<bool>(p,"purgaeTransformed"),"Purgae HP11 remains normal");
            Call(p,"RegisterBossParryHit",1200,true);Check(G<bool>(p,"purgaeTransformed"),"Purgae HP10 transforms");
            Start(p,3);Set(p,"bossDamage",8f);Set(p,"pendingBossDamage",3f);Call(p,"RegisterBossParryHit",1200,true);Check(G<bool>(p,"purgaeTransformed"),"Purgae crossing HP10 transforms");
            Start(p,4);Set(p,"bossDamage",18f);Call(p,"RegisterBossParryHit",1200,true);Check(!G<bool>(p,"menheraMode"),"Rodian HP21 remains normal");
            Call(p,"RegisterBossParryHit",1200,true);Check(G<bool>(p,"menheraMode"),"Rodian HP20 transforms");
            Start(p,4);Set(p,"bossDamage",18f);Set(p,"pendingBossDamage",4f);Call(p,"RegisterBossParryHit",1200,true);Check(G<bool>(p,"menheraMode"),"Rodian crossing below HP20 transforms");

            Start(p,2);Check(G<float>(p,"IsangjinBeatWindow")==8,"W/E window exactly eight frames");
            foreach(float offset in new[]{-8f,8f,8.01f})
            {
                Call(p,"ResetIsangjinClock");Set(p,"isangjinClockPhase",4);Set(p,"isangjinClockTimer",36+offset);G<Keys[]>(p,"IsangjinBeatKeys")[0]=Keys.W;
                Tap(p,Keys.W);Check(G<int>(p,"isangjinClockPhase")== (offset>8?6:4),$"W/E timing edge {offset}");
            }
            Call(p,"ResetIsangjinClock");Set(p,"isangjinClockPhase",4);G<Keys[]>(p,"IsangjinBeatKeys")[0]=Keys.W;Set(p,"isangjinClockTimer",36f);Tap(p,Keys.E);
            Check(G<int>(p,"isangjinClockPhase")==6,"Wrong direction fails");Call(p,"UpdateIsangjinClock",24f);Check(G<float>(p,"hp")==2000,"Rhythm failure damage remains 3000");Call(p,"UpdateIsangjinClock",1f);Check(G<float>(p,"hp")==2000,"Rhythm failure damage only once");
            Start(p,2);Call(p,"BeginIsangjinClock");Set(p,"isangjinClockPhase",4);Set(p,"isangjinClockTimer",0f);
            var times=G<float[]>(p,"IsangjinBeatTimes");Check(times.Length==6&&times[^1]==150,"Six doubled-speed beats retained");
            for(int i=0;i<6;i++){Set(p,"isangjinClockTimer",times[i]);Tap(p,G<Keys[]>(p,"IsangjinBeatKeys")[i]);}
            Check(G<int>(p,"isangjinClockPhase")==5,"Six correct randomized directional beats succeed");Call(p,"UpdateIsangjinClock",52f);Check(G<float>(p,"bossDamage")==1,"Rhythm success deals exactly one");Call(p,"UpdateIsangjinClock",1f);Check(G<float>(p,"bossDamage")==1,"Success damage only once");
            Start(p,2);Call(p,"UpdateIsangjinAcceleration",899f);Check(P<float>(p,"IsangjinTimeScale")==1,"Acceleration does not start early");Call(p,"UpdateIsangjinAcceleration",1f);
            Check(P<float>(p,"IsangjinTimeScale")==1.3f&&G<float>(p,"isangjinAccelerationRemaining")==240,"1.3x acceleration every 15 seconds, lasts four");
            Call(p,"UpdateIsangjinAcceleration",240f);Check(P<float>(p,"IsangjinTimeScale")==1,"Acceleration ends at four seconds");
            Start(p,2);Call(p,"Jump");Call(p,"UpdatePlayer",1f);float ordinaryY=G<float>(p,"py");
            Start(p,2);Set(p,"isangjinAccelerationRemaining",240f);Call(p,"Jump");Call(p,"UpdatePlayer",1f);Check(G<float>(p,"py")==ordinaryY,"Player physics remains unaccelerated");
            var list=G<List<Projectile>>(p,"projectiles");var sample=new Projectile(100,100,AttackType.BlackBall,20,-10,0,100);list.Add(sample);Call(p,"UpdateObjects",1.3f);Check(Math.Abs(sample.X-87)<.01f,"Enemy projectile motion accepts accelerated step");

            Start(p,3);foreach(var kind in new[]{(0,false),(1,false),(0,true),(1,true)})
            {
                Set(p,"selectedCookie",kind.Item1);Set(p,"cloneActive",kind.Item2);float min=1000,max=0;
                for(int i=0;i<2500;i++){float d=(float)Call(p,"IncomingProjectileDamage",new Projectile(0,0,AttackType.Meteor,80,0,0,700))!;min=Math.Min(min,d);max=Math.Max(max,d);}
                Check(min==350&&max==700,$"Meteor range 350..700 for skin {kind.Item1}, clone {kind.Item2}");
                Set(p,"hp",P<float>(p,"PlayerMaxHealth"));Set(p,"invuln",0f);list.Clear();var box=P<RectangleF>(p,"PlayerHitBox");list.Add(new(box.X,box.Y,AttackType.Meteor,40,0,0,700));float before=G<float>(p,"hp");Call(p,"CheckCollisions");float lost=before-G<float>(p,"hp");Check(lost>=350&&lost<=700,"Real collision uses shared meteor range");
            }
            Start(p,3);var kick=P<RectangleF>(p,"KickHitBox");var meteor=new Projectile(kick.Right+25,kick.Top,AttackType.Meteor,30,0,0,700);list.Clear();list.Add(meteor);Tap(p,Keys.Space);Call(p,"UpdateParryHits");Check(meteor.IsParried&&G<float>(meteor,"vy")<0,"Meteor generous reflection remains upward");

            Start(p,4);Beam(p,21);float hp=G<float>(p,"hp");Call(p,"TryReflectApproachingMelodyLaser");Check(G<float>(p,"hp")==hp&&!G<bool>(p,"laserActive"),"Visible traveling laser has no early damage");Shot(p,"04-laser-travel");
            Tap(p,Keys.Space);Call(p,"TryReflectApproachingMelodyLaser");Check(!G<bool>(p,"laserReflected"),"Early kick cannot reflect a distant laser");
            Set(p,"parryWindow",0f);Set(p,"melodyLaserPreviousTip",P<PointF>(p,"MelodyLaserTip"));Set(p,"melodyWordTimer",0f);Call(p,"TryReflectApproachingMelodyLaser");
            Check(G<float>(p,"hp")==hp-2000,"Unreflected laser deals 2000 on arrival");Call(p,"TryReflectApproachingMelodyLaser");Call(p,"TriggerMelodyFinale",false);Check(G<float>(p,"hp")==hp-2000,"Laser impact is resolved exactly once");
            foreach(var key in new[]{Keys.Space,Keys.ShiftKey})
            {
                Start(p,4);Beam(p,0);Tap(p,key);Call(p,"TryReflectApproachingMelodyLaser");Check(G<bool>(p,"laserReflected")&&G<float>(p,"parryCooldown")==0,$"Visible head reflects with {key}, cooldown reset");
                Call(p,"UpdateLaser",19f);Check(G<float>(p,"bossDamage")==1,"Counter laser reaches boss");
            }
            Start(p,4);Beam(p,21);kick=P<RectangleF>(p,"KickHitBox");
            var outside=new PointF(kick.Right+5,kick.Top+20);var contact=new PointF(G<float>(p,"px")+26,G<float>(p,"py")+31);
            Set(p,"melodyLaserOrigin",new PointF(outside.X*2-contact.X,outside.Y*2-contact.Y));Set(p,"melodyLaserPreviousTip",outside);Tap(p,Keys.Space);Call(p,"TryReflectApproachingMelodyLaser");
            Check(!G<bool>(p,"laserReflected"),"Laser five pixels outside displayed box cannot reflect");
            foreach(bool rage in new[]{false,true})
            {
                Start(p,4);Set(p,"menheraMode",rage);Call(p,"StartMelodySequence");var words=new HashSet<string>();
                for(int i=0;i<400&&!G<bool>(p,"melodyLaserResolved");i++){Call(p,"UpdateRound4",1f);words.Add(G<string>(p,"bossMessage"));Call(p,"TryReflectApproachingMelodyLaser");if(P<bool>(p,"MelodyLaserApproaching")&&P<float>(p,"MelodyLaserProgress")<.8f)Check(G<float>(p,"hp")==5000,"No pre-arrival damage during actual sequence");}
                Check(new[]{"탈","덕","안","돼!"}.All(x=>words.Any(w=>w.StartsWith(x))),$"All four laser syllables visible, menhera {rage}");
                Check(G<float>(p,"hp")==3000,$"Actual timed sequence hits exactly once, menhera {rage}");
            }
            Start(p,4);Set(p,"activeSkill",6);Call(p,"UseActiveSkill");Beam(p,0);Tap(p,Keys.Space);Call(p,"TryReflectApproachingMelodyLaser");Check(G<bool>(p,"laserReflected"),"Cinder still reflects a laser as a laser");
            Start(p,4);Set(p,"activeSkill",1);Call(p,"UseActiveSkill");Beam(p,0);Call(p,"TryReflectApproachingMelodyLaser");Check(G<float>(p,"hp")==5000&&G<int>(p,"absorbedCount")==3&&!G<bool>(p,"laserActive"),"Absorption consumes arrived laser without damage");
            Start(p,4);Set(p,"activeSkill",2);Call(p,"UseActiveSkill");Beam(p,0);Call(p,"TryReflectApproachingMelodyLaser");Check(G<float>(p,"hp")==5000&&G<float>(p,"dodgeNearHitTimer")>0,"Dodge avoids arrived laser");
            Start(p,4);Set(p,"menheraMode",true);Set(p,"melodyBurstActive",true);Set(p,"melodyBurstRemaining",1);Set(p,"melodyBurstTotal",3);Call(p,"StartMelodySequence");Check(G<int>(p,"melodyStep")==0,"Chained menhera laser preserves all four syllables");
            Start(p,4);kick=P<RectangleF>(p,"KickHitBox");Set(p,"parryRangeBonus",30f);Check(P<RectangleF>(p,"KickHitBox").Width==kick.Width+60,"Range display shares upgraded collision rectangle");

            Start(p,1);Shot(p,"05-hand-placement");float health=G<float>(p,"hp"),stage=G<float>(p,"frame");Call(p,"AdvanceClearTimer",1d);Tick(p,90);
            Check(G<int>(p,"roundOneLesson")==1&&G<float>(p,"hp")==health&&G<float>(p,"frame")==stage&&G<double>(p,"clearTimeSeconds")==0,"Safe onboarding freezes health, stage and clear timer");
            Tap(p,Keys.Up);Tick(p,40);Check(G<int>(p,"roundOneLesson")==2,"Real jump and landing complete jump lesson");
            Tap(p,Keys.Up);Tick(p,7);Tap(p,Keys.Up);Tick(p,60);Check(G<int>(p,"roundOneLesson")==3,"Real airborne second jump completes double-jump lesson");
            p.HandleKeyDown(new(Keys.Down));Tick(p,180);p.HandleKeyUp(new(Keys.Down));Check(G<int>(p,"roundOneLesson")==4,"Held slide enters reflection after at least six learning seconds");
            Shot(p,"06-parry-tutorial");Tick(p,200);Check(G<bool>(p,"roundOneParryMissed")&&G<float>(p,"hp")==health,"Missed harmless tutorial bullet retries");
            for(int i=0;i<400&&G<int>(p,"roundOneParryPhase")!=2;i++)
            {
                var trace=Get(p,"roundOneParryTrace") as Projectile;
                if(trace!=null&&P<RectangleF>(p,"KickHitBox").IntersectsWith(trace.Rect)&&G<float>(p,"parryCooldown")<=0)Tap(p,Keys.Space);
                Tick(p,1);
            }
            Check(G<int>(p,"roundOneParryPhase")==2&&list.Any(x=>x.IsParried),"Actual tutorial kick sends bullet back and shows success");Shot(p,"07-tutorial-return");Tick(p,160);
            Check(G<int>(p,"roundOneLesson")==5&&Convert.ToInt32(Get(p,"activeSkill"))==2,"Q lesson borrows Dodge after reflection");Tap(p,Keys.Q);Tick(p,50);Check(G<bool>(p,"roundOneQDodged"),"Q lesson uses actual near-hit dodge");Shot(p,"08-q-practice");Tick(p,100);
            Check(G<int>(p,"roundOneLesson")==6&&Convert.ToInt32(Get(p,"activeSkill"))==0,"Tutorial returns borrowed skill and resumes original stage");Call(p,"AdvanceClearTimer",1d);Check(G<double>(p,"clearTimeSeconds")==1,"Clear time resumes after tutorial");
            foreach(int lesson in Enumerable.Range(0,6)){Start(p,1);if(lesson>0)Call(p,"SetRoundOneLesson",lesson);Tap(p,Keys.Enter);Check(G<int>(p,"roundOneLesson")==6&&list.Count==0&&!G<bool>(p,"roundOneSkillBorrowed"),$"ENTER skip cleans tutorial stage {lesson}");}
            Start(p,1);Set(p,"practiceActive",true);Check(!P<bool>(p,"RoundOneOnboardingActive"),"Practice has no mandatory tutorial");

            Start(p,1);Call(p,"BeginOpeningStory");Tap(p,Keys.S);Check(p.State==GameState.Round&&G<int>(p,"round")==1,"S skips opening into first round");
            Start(p,1);Call(p,"BeginFirstGateStory");Tap(p,Keys.S);Check(p.State==GameState.RoundTransition&&G<int>(p,"pendingDraft")==1,"S preserves first-round Q reward");
            Start(p,2);Call(p,"BeginStageTravel",3);Tap(p,Keys.S);Check(G<int>(p,"round")==3&&G<bool>(p,"bossBriefingActive"),"S travel skip keeps next boss interaction");Tap(p,Keys.S);Check(G<int>(p,"bossTalkPhase")==3,"S dialogue skip still offers boss mechanics briefing");Shot(p,"09-boss-briefing");Tap(p,Keys.S);Check(!G<bool>(p,"bossBriefingActive"),"Second S can leave briefing into combat");
            Start(p,3);Call(p,"TriggerPurgaeTransformation");Tap(p,Keys.S);Check(!P<bool>(p,"PhaseIntroActive")&&!G<bool>(p,"bossBriefingActive"),"S phase skip clears cinematic and phase dialogue");
            Start(p,4);Check(!(bool)Call(p,"TrySkipStory")!,"S cannot skip ordinary combat");
            Set(p,"bossDamage",39.5f);Call(p,"TryBeginMelodyLastStand");Call(p,"SetLastStandPhase",4,"");Check(!(bool)Call(p,"TrySkipStory")!,"S cannot bypass final laser QTE");
            Check(G<int>(p,"LastStandTarget")==70&&G<float>(p,"LastStandLimit")==600,"QTE remains ten seconds and seventy presses");
            for(int i=0;i<70;i++){Tick(p,8);Tap(p,Keys.Space);}Tick(p,41);Check(G<int>(p,"lastStandPhase")==5,"70 presses still succeed");Tick(p,405);Check(G<bool>(p,"bossDefeatActive"),"QTE success reaches visible post-QTE scene");
            Call(p,"BeginRescueEnding");Tap(p,Keys.S);Check(G<bool>(p,"endingFinal")&&G<int>(p,"endingScene")==3,"S ending skip reaches victory screen");
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{checks=results.Count,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {results.Count} targeted checks");return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{error=e.ToString(),results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(e);return 1;}
    }
}
