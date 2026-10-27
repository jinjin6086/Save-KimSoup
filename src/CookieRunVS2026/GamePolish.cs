using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private static readonly Color Ink = Color.FromArgb(11, 13, 23);
    private static readonly Color Paper = Color.FromArgb(224, 218, 203);
    private static readonly Color Mint = Color.FromArgb(129, 207, 178);
    private static readonly Color Coral = Color.FromArgb(255, 125, 105);
    private readonly GameAudio audio = new();
    private readonly List<ImpactParticle> impactParticles = new();
    private readonly HashSet<Keys> heldKeys = new();
    private float menuClock, damageFxTimer, comboTimer, jumpBuffer, hpTrail = MaxHealth;
    private int parryCombo;
    private bool reducedEffects;
    private string damageLabel = "";
    private Color damageColor = Coral;

    public void ReleaseInputs() { heldKeys.Clear(); sliding = false;audio.StopSliding();slideSoundActive=false; }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key == Keys.Up || key == Keys.Down || key == Keys.Space || base.IsInputKey(keyData);
    }

    private void UpdatePolish(float step)
    {
        menuClock += step;
        landingDustTimer = Math.Max(0, landingDustTimer-step);
        audio.Collect();
        if (state != GameState.Round) return;
        UpdateRebuildFx(step);UpdateKickCombat(step);
        jumpBuffer = Math.Max(0, jumpBuffer - step);
        damageFxTimer = Math.Max(0, damageFxTimer - step);
        comboTimer = Math.Max(0, comboTimer - step);
        if (comboTimer == 0) parryCombo = 0;
        hpTrail += (hp - hpTrail) * (1 - MathF.Pow(0.94f, step));
        for (int i = impactParticles.Count - 1; i >= 0; i--)
        {
            var p = impactParticles[i];
            p.Life -= step;
            p.X += p.Vx * step; p.Y += p.Vy * step;
            p.Vy += (p.Kind == 3 ? -0.10f : p.Kind > 0 ? 0.20f : 0.06f) * step;
            p.Vx *= MathF.Pow(0.96f, step);
            if (p.Life <= 0) impactParticles.RemoveAt(i);
        }
    }

    private void Burst(float x, float y, Color color, int count, int kind = 0)
    {
        if (reducedEffects) count = Math.Min(count, 6);
        for (int i = 0; i < count && impactParticles.Count < 180; i++)
        {
            float angle = rng.NextSingle() * MathF.Tau;
            float speed = 1.5f + rng.NextSingle() * 6;
            impactParticles.Add(new ImpactParticle {
                X=x, Y=y, Vx=MathF.Cos(angle)*speed, Vy=MathF.Sin(angle)*speed-1,
                Life=24+rng.Next(16), Color=color, Kind=kind, Size=2+rng.NextSingle()*4
            });
        }
    }

    private void ReceiveHit(float damage, string label, Color color, bool unavoidable = false, bool damageOverTime = false)
    {
        if (activeSkill==ActivePower.Dodge && activeTimer>0 && !isangjinClockDamage){TriggerMoonwalkAvoid(bossX);return;}
        if ((!unavoidable && invuln > 0) || hp <= 0) return;
        if(practiceActive)practiceHits++;
        hp = Math.Max(0, hp - damage);
        damageFxTimer = 38f;
        damageLabel = $"−{damage:0}  {label}";
        damageColor = color;
        if(damageOverTime)
        {
            // A burn tick damages health without impact stun, input delay,
            // fresh contact invulnerability or cancelling a reflection chain.
            Burst(px+PlayerWidth/2,py+PlayerHeight/2,color,8,3);
            if(!TrySurvive()){state=GameState.GameOver;audio.CancelLaserCharge();audio.Play("down");}
            return;
        }
        // Damage never grants grace frames, in any round. Explicit dodge,
        // revival and cinematic protections keep their separate existing roles.
        parryCombo = 0; comboTimer = 0;
        int kind = label switch { "충돌"=>1, "장애물"=>2, "화상"=>3, _=>0 };
        Burst(px + PlayerWidth/2, py + PlayerHeight/2, color, 26, kind);
        hitStopTimer = Math.Max(hitStopTimer, reducedEffects ? 0 : 4f);
        TriggerCameraShake(reducedEffects ? 0 : 4.5f, 9);
        audio.Play(label == "충돌" || label == "장애물" ? "crash" : "hurt");
        if (!TrySurvive()) { state = GameState.GameOver; audio.CancelLaserCharge(); audio.Play("down"); }
    }

    private void ParryFeedback()
    {
        if(practiceActive)practiceReflections++;
        RegisterParryStyle(parryWindow >= 6);
        parryCombo++; comboTimer = 240;
        KickImpact(parryWindow>=6);
        hitStopTimer = Math.Max(hitStopTimer, reducedEffects ? 0 : 3f);
        audio.Play(parryWindow>=6?"perfect":"parry");
    }

    private void DrawCombatFeedback(Graphics g)
    {
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var p in impactParticles)
        {
            int alpha = (int)(220 * Math.Clamp(p.Life / 20, 0, 1));
            var pen = ParticlePen(p.Color,alpha,p.Kind==1?3:2);
            if (p.Kind == 1)
                g.DrawLine(pen, p.X-p.Size, p.Y-p.Size, p.X+p.Size, p.Y+p.Size);
            else if (p.Kind == 2)
            {
                shardPoints[0]=new(p.X-p.Size,p.Y-p.Size*.5f);shardPoints[1]=new(p.X+p.Size*.6f,p.Y-p.Size);
                shardPoints[2]=new(p.X+p.Size,p.Y+p.Size*.4f);shardPoints[3]=new(p.X-p.Size*.3f,p.Y+p.Size);
                g.FillPolygon(ParticleBrush(p.Color,alpha),shardPoints);
            }
            else if (p.Kind == 3)
            {
                using var ember = new SolidBrush(Color.FromArgb(alpha,p.Color));
                g.FillEllipse(ember,p.X,p.Y,p.Size,p.Size*1.6f);
            }
            else g.DrawLine(pen, p.X, p.Y, p.X-p.Vx*2, p.Y-p.Vy*2);
        }
        g.SmoothingMode = smoothing;
        if (damageFxTimer > 0)
        {
            float life = damageFxTimer / 38f;
            // Weighted contact stays local. Fading red edges communicate damage
            // without placing a second target-shaped ring around the runner.
            using var left=new LinearGradientBrush(new Rectangle(0,0,65,H),Color.FromArgb((int)(72*life),Coral),Color.Transparent,0f);
            using var right=new LinearGradientBrush(new Rectangle(W-65,0,65,H),Color.Transparent,Color.FromArgb((int)(72*life),Coral),0f);
            g.FillRectangle(left,0,0,65,H);g.FillRectangle(right,W-65,0,65,H);
            if(life>.60f)DrawKickLight(g,new(px-13,py+8,89,60),(life-.60f)*.28f);
            Label(g, damageLabel, new RectangleF(20, py-48-(1-life)*22, 310, 32), 16, damageColor);
        }
        if (parryCombo > 0 && comboTimer > 0)
            Label(g, $"{parryCombo:00}  발차기 연속", new RectangleF(24, 114, 260, 30), 13, Mint, false);
    }

    private void DrawHazardCues(Graphics g)
    {
        foreach (var o in obstacles)
        {
            if (o.X < 430 || o.X > 960) continue;
            bool low = o.Kind == ObstacleKind.Ground;
            Label(g,low ? "↑ 점프" : "↓ 슬라이드",new(o.X-35,o.Y-29,o.W+70,23),12,low?Coral:Mint);
        }
        foreach (var o in risingObstacles)
            if(o.X>430 && o.X<960) Label(g,"↑ 점프",new(o.X-25,o.Y-28,o.W+50,23),12,Coral);
        if(round==3 && purgaeSkill2Active && purgaeSkill2ThrowTimer<14 && purgaeSkill2NextBall<6)
        {
            int i=purgaeSkill2NextBall;
            float angle=-MathF.PI/2+i*MathF.Tau/6;
            float x=bossX+77.5f+MathF.Cos(angle)*108;
            float y=bossY+77.5f+MathF.Sin(angle)*108;
            using var line=new Pen(Color.FromArgb(110,purgaeTransformed?Coral:Mint),1) {DashStyle=DashStyle.Dash};
            g.DrawLine(line,x,y,px+26,py+38);
            using var ring=new Pen(purgaeTransformed?Coral:Mint,2);
            g.DrawEllipse(ring,x-21,y-21,42,42);
        }
    }

    // Every title is laid out in a bounded rectangle, including Korean fallback fonts.
    private void Label(Graphics g,string text,RectangleF bounds,float size,Color color,bool centered=true,bool bold=true)
    {
        if(worldMirroring)
        {
            var saved=g.Save();g.TranslateTransform(0,bounds.Top+bounds.Bottom);g.ScaleTransform(1,-1);worldMirroring=false;
            try{Label(g,text,bounds,size,color,centered,bold);}finally{worldMirroring=true;g.Restore(saved);}return;
        }
        float fitted=size;
        if(text.Length*size>bounds.Width-6)
        {
            var key=(text,(int)bounds.Width,(int)bounds.Height,size,bold);
            if(!labelSizes.TryGetValue(key,out fitted))
            {
                fitted=size;
                while(fitted>8&&g.MeasureString(text,LabelFont(fitted,bold)).Width>bounds.Width-4) fitted--;
                if(labelSizes.Count>=384)labelSizes.Clear();
                labelSizes[key]=fitted;
            }
        }
        using var brush=new SolidBrush(color);
        g.DrawString(text,LabelFont(fitted,bold),brush,bounds,centered?centerLabel:leftLabel);
    }

    private static Color Accent(int r) => r switch { 2=>Color.FromArgb(127,190,255), 3=>Mint, 4=>Color.FromArgb(244,153,214), _=>Mint };
    private string RoundName(int r) => r switch { 1=>"1라운드", 2=>"이상진", 3=>"파괴옥", 4=>"강로디언",5=>FinalBossName, _=>"김스프를 구해라!" };
    private string RoundHint(int r) => r switch {
        2=>"날아오는 공을 발차기로 반사하세요.",
        3=>"초록 구슬을 반사하세요. 각성 후에는 화상을 조심하세요.",
        4=>"탈 · 덕 · 안 · 돼!  마지막 박자에 발차기하세요.",
        5=>"보스 소개에서 공격 반사와 회피 방법을 확인하세요.",
        _=>"낮은 장애물은 점프, 높은 장애물은 슬라이드로 피하세요."
    };

    private void DrawShell(Graphics g) { DrawArenaGround(g); }

    private void DrawModernTitle(Graphics g) { PremiumTitle(g); }

    private void DrawModernTransition(Graphics g) { PremiumTransition(g); }

    private void DrawModernCookieSelect(Graphics g) { PremiumSelect(g); }



    private void DrawModernResult(Graphics g,bool won) { PremiumResult(g,won); }

    private void DrawModernHud(Graphics g) { PremiumHud(g); }

    private void DrawModernAugments(Graphics g) { DrawSkillCards(g); }

    private sealed class ImpactParticle
    {
        public float X,Y,Vx,Vy,Life,Size;
        public int Kind;
        public Color Color;
    }
}

// Original synthesized PCM effects share one fixed-rate software mixer.
// Interaction gains never modify the Windows music/session volume.
internal sealed partial class GameAudio : IDisposable
{
    private readonly Dictionary<string,byte[]> clips = new();
    private readonly List<Voice> voices = new();
    private readonly Dictionary<string,long> lastSounds = new();
    private bool paused;
    public bool Muted { get; private set; }
    public bool DeviceUnavailable => audioOutput?.Failed ?? false;
    public int ActiveVoices => voices.Count(v=>!v.Playback.Completed);
    private const int SampleRate=22050;
    public GameAudio()
    {
        clips["cinderstart"]=MakeCinderSound(0);clips["cinderthrow"]=MakeCinderSound(2);clips["cinderhit"]=MakeCinderSound(3);clips["cinderbrand"]=MakeCinderSound(4);
        clips["phase"]= MakeBeam(.7,true);
        clips["phasebreak"]=MakeBeam(.7,false);
        clips["bossintro"]=MakeSoftAbility(.52,96,49,.32,.27,429);
        clips["bossdefeat"]=MakeBossDefeat();clips["bossburst2"]=MakeIndependentBossBurst(false);clips["bossburst3"]=MakeIndependentBossBurst(true);
        clips["gravity"]=Make(.55,160,760,.25,.22);
        clips["counterbeam"]=MakeGrandBeam(.55,false,true);
        clips["meteorhit"]=Make(.16,130,43,.85,.22);
        clips["menhera"]=MakeBeam(.8,false);
        clips["dashwarn"]=Make(.07,1550,1700,.01,.17);
        clips["dash"]=Make(.24,900,85,.75,.24);
        clips["absorb"]=MakeSoftAbility(.32,95,228,.45,.17,411);
        clips["eat"]=MakeSoftAbility(.22,260,96,.62,.18,412);
        clips["dodge"]=MakeWhoosh();
        clips["clone"]=MakePlayerRelease(.56,.24);
        clips["parry"]=MakeKick(false);
        clips["kickswing"]=MakeWhoosh();
        for(int terrain=0;terrain<3;terrain++)for(int foot=0;foot<2;foot++)clips[$"step{terrain}{foot}"]=MakeFootstep(terrain,foot);
        clips["impact"]=MakeSoftAbility(.24,136,54,.43,.26,418);
        clips["crash"]=MakeSoilLanding(.36,.67);
        clips["hurt"]=MakeSoftAbility(.26,148,66,.30,.24,419);
        clips["jump"]=MakeWhoosh();
        clips["select"]=MakeSoftAbility(.16,225,280,.06,.13,420);
        clips["round"]=MakeRoundedReward();
        clips["beat"]=Make(0.08,780,780,0.02,0.12);
        clips["final"]=Make(0.15,1150,1150,0.10,0.17);
        clips["down"]=MakeSoftAbility(.45,160,45,.18,.22,421);
        clips["charge"]=MakeSoftAbility(.30,135,350,.15,.14,422);
        clips["beat0"]=Make(.07,520,440,.06,.16);
        clips["beat1"]=Make(.07,660,540,.06,.16);
        clips["beat2"]=Make(.09,880,680,.08,.19);
        clips["beam"]=MakeGrandBeam(.88,false);
        clips["echoflight"]=MakeEchoFlight();clips["furiosostart"]=MakeEchoStart();clips["echohit"]=MakeEchoImpact();clips["focusenter"]=MakeFocusEntry();
        clips["timestopstart"]=MakeTimeStopStart();clips["timestophold"]=MakeTimeStopHold();clips["timestoprelease"]=MakeTimeStopRelease();clips["timestopstone"]=MakeTimeStopStone();
        clips["clashdespair"]=MakeRodianLastVowSound(false);clips["clashrage"]=MakeRodianLastVowSound(true);
        clips["clashcharge"]=MakeClashSound(0);clips["clashapproach"]=MakeClashSound(1);clips["clashhero"]=MakeClashSound(2);clips["clashsustain"]=MakeClashSound(3);clips["clashtap"]=MakeClashSound(4);clips["clashfinish"]=MakeClashSound(5);clips["clashfail"]=MakeClashSound(6);clips["clashmemory"]=MakeClashSound(7);clips["clashheart"]=MakeClashSound(8);clips["clashignite"]=MakeClashSound(9);
        clips["clashsurge"]=MakeClashSound(10);clips["clashbreak"]=MakeClashSound(11);clips["clashexplosion"]=MakeClashSound(12);
        clips["exitpant"]=MakeExitPant();
        clips["slidein"]=MakeSlide(true);clips["slidebody"]=MakeSlide(false);clips["slideout"]=MakeSlideRelease();clips["inhale"]=MakeInhale();
        clips["landsoft"]=MakeSoilLanding(.39,.45);
        clips["landmid"]=MakeSoilLanding(.50,.72);
        clips["landheavy"]=MakeSoilLanding(.65,1.0);
        clips["perfect"]=MakeKick(true);
        clips["reverse"]=MakePlayerRelease(.43,.28);
        clips["reward"]=MakeRoundedReward();
        clips["ruleblue"]=MakeCue(2);
        clips["ruleorange"]=MakeCue(3);
        clips["rulesafe"]=Make(.12,620,940,0,.11);
        clips["volley"]=Make(.12,180,80,.32,.15);
        for(int frames=26;frames<=62;frames++) clips["lasercharge"+frames]=MakeGrandBeam(frames/60.0,true);
        ApplySoundRemaster();
        ApplyOriginalSkillSounds();
        ApplySfxClarity();
        PrepareDialogueSounds();
        PrepareSoundResources();
        audioOutput=new SteadyWaveOutput(softwareMixer);
    }
    public void PlayLaserCharge(int frames) => Play("lasercharge"+Math.Clamp(frames,26,62));
    public void CancelLaserCharge()
    {
        for(int i=voices.Count-1;i>=0;i--)
            if(voices[i].Key.StartsWith("lasercharge",StringComparison.Ordinal)) { voices[i].Dispose();voices.RemoveAt(i); }
    }

    private static byte[] MakePrism(bool perfect)
    {
        double duration=perfect?.40:.25;int count=(int)(duration*SampleRate);
        var pcm=new byte[count*2];var random=new Random(941);double low=0,phase=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,noise=random.NextDouble()*2-1;low+=.2*(noise-low);
            phase+=Math.Tau*(950+4200*Math.Exp(-t*46))/SampleRate;
            double slash=(noise-low)*Math.Exp(-t*60)*.9;
            double metal=Math.Sin(phase)*Math.Exp(-t*19)*.38;
            double bell=(Math.Sin(t*Math.Tau*2310)+.6*Math.Sin(t*Math.Tau*3447))*Math.Exp(-t*(perfect?12:23))*.27;
            double punch=Math.Sin(t*Math.Tau*110)*Math.Exp(-t*55)*.4;
            double sparkle=perfect?Math.Sin(t*Math.Tau*4620)*Math.Exp(-t*15)*.13:0;
            double end=Math.Min(1,(duration-t)*55),attack=Math.Min(1,t/.001);
            short value=(short)(Math.Tanh((slash+metal+bell+punch+sparkle)*1.8)*.34*end*attack*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return pcm;
    }

    private static byte[] MakeCue(int kind)
    {
        int count=(int)(SampleRate*.36);var pcm=new byte[count*2];double phase=0;
        double[] notes=kind==1?new[]{440d,660,880}:kind==2?new[]{660d,520,420}:new[]{420d,560,740};
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate;int n=Math.Min(2,(int)(t/.105));double local=t-n*.105;
            phase+=Math.Tau*notes[n]/SampleRate;double env=Math.Min(1,local/.003)*Math.Exp(-local*24)*Math.Min(1,(.36-t)*30);
            short value=(short)((Math.Sin(phase)+Math.Sin(phase*2)*.2)*env*.13*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return pcm;
    }

    // A rising unstable oscillator, then a layered detuned beam with a short
    // noise transient, sub-bass and decaying echoes. All samples are synthesized.
    private static byte[] MakeBeam(double duration,bool charge)
    {
        int count=(int)(SampleRate*duration);
        var data=new byte[count*2];var delay=new double[(int)(SampleRate*.072)];
        var random=new Random(731);double phase=0,detune=0,lowNoise=0;
        for(int i=0;i<count;i++)
        {
            double seconds=(double)i/SampleRate,t=seconds/duration;
            double freq=charge?180+1350*t*t:95+1800*Math.Exp(-seconds*16);
            phase+=freq/SampleRate;detune+=freq*1.027/SampleRate;
            double saw=(phase%1)*2-1;
            double second=Math.Sin(detune*Math.Tau)+.3*Math.Sin(phase*Math.Tau*3);
            double noise=random.NextDouble()*2-1;
            lowNoise+=.15*(noise-lowNoise);
            double envelope=charge?Math.Min(1,t*20)*(.2+.8*t)*Math.Min(1,(1-t)*28):Math.Min(1,seconds/.008)*Math.Pow(1-t,1.45);
            double signal;
            if(charge) signal=(saw*.55+second*.18+lowNoise*.20)*(.75+.25*Math.Sin(seconds*47*Math.Tau));
            else
            {
                double attack=Math.Exp(-seconds*24);
                double bass=Math.Sin(Math.Tau*(62*seconds+5*Math.Sin(seconds*8)));
                signal=(saw*.34+second*.24+noise*attack*.65+lowNoise*.22+bass*.36)*(.83+.17*Math.Sin(seconds*31*Math.Tau));
            }
            double value=Math.Tanh(signal*2.8)*envelope;
            int d=i%delay.Length;double echoed=value+delay[d]*.28;delay[d]=echoed;
            double fade=Math.Min(1,(1-t)*20);
            short sample=(short)(Math.Clamp(echoed*(charge?.24:.39)*fade,-.70,.70)*short.MaxValue);
            data[i*2]=(byte)sample;data[i*2+1]=(byte)(sample>>8);
        }
        return data;
    }

    private static byte[] MakeWhoosh()
    {
        const double duration=.25;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(628);
        double low=0,body=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,n=random.NextDouble()*2-1;
            low+=.026*(n-low);body+=(.11+.08*(1-t/duration))*(n-body);
            double envelope=Math.Pow(Math.Sin(Math.PI*t/duration),1.6)*Math.Exp(-t*3.2);
            double cloth=(body-low)*2.8+low*.55;
            short value=(short)(Math.Tanh(cloth)*envelope*.24*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return RoundPlayerClip(pcm,1500);
    }
    private static byte[] MakeSoilLanding(double duration,double weight)
    {
        int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(1621);
        double low=0,mid=0,powder=0,phase=0;
        double[] fractureTimes={.014,.037,.071,.121,.187,.275,.382};
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,n=random.NextDouble()*2-1;
            low+=.022*(n-low);mid+=.18*(n-mid);powder+=.065*(n-powder);
            phase+=Math.Tau*(42+64*Math.Exp(-t*29))/SampleRate;
            double thud=Math.Sin(phase)*Math.Exp(-t*12)*.85;
            double earth=low*Math.Exp(-t*7.5)*4.0;
            double fracture=0;
            // Offset cracks and falling clods create a broken earth tail.
            // Their band-limited grain avoids the old brittle glass-like snap.
            for(int j=0;j<fractureTimes.Length;j++)
            {
                double age=t-fractureTimes[j];if(age<0)continue;
                double env=Math.Min(1,age/.0025)*Math.Exp(-age*(j<3?56:35));
                double crack=(mid-low)*1.55+Math.Sin(age*Math.Tau*(740-j*67))*.12;
                fracture+=crack*env*(1-j*.105)*(.65+weight*.45);
            }
            double debris=powder*Math.Exp(-Math.Max(0,t-.10)*8)*Math.Min(1,t/.06)*.95;
            double envelope=Math.Min(1,t/.003)*Math.Min(1,(duration-t)*24);
            short value=(short)(Math.Tanh((thud+earth+fracture+debris)*1.6)*envelope*(.22+.16*weight)*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return RoundPlayerClip(pcm,1750);
    }
    // One-time synthesis filters; Play only looks up the already built buffers.
    // Two cascaded one-pole filters round the high end without removing the
    // low leather/soil body. A short final fade prevents end-of-buffer clicks.
    private static byte[] RoundPlayerClip(byte[] pcm,double cutoff)
    {
        double a=1-Math.Exp(-Math.Tau*cutoff/SampleRate),first=0,second=0,dc=0;
        int count=pcm.Length/2;
        for(int i=0;i<count;i++)
        {
            double input=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768.0;
            first+=a*(input-first);second+=a*(first-second);dc+=.0007*(second-dc);
            double edge=Math.Min(1,i/(SampleRate*.0015))*Math.Min(1,(count-1-i)/(SampleRate*.012));
            short sample=(short)(Math.Clamp((second-dc)*edge,-.9,.9)*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return pcm;
    }
    private static byte[] MakeSoftAbility(double duration,double start,double end,double texture,double gain,int seed)
    {
        int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(seed);
        double phase=0,low=0,mid=0;
        for(int i=0;i<count;i++)
        {
            double seconds=(double)i/SampleRate,t=seconds/duration,n=random.NextDouble()*2-1;
            low+=.03*(n-low);mid+=.16*(n-mid);
            phase+=Math.Tau*(start+(end-start)*(1-Math.Pow(1-t,1.5)))/SampleRate;
            double body=Math.Sin(phase)+Math.Sin(phase*1.997)*.13;
            double textureBody=(mid-low)*2.2+low*.75;
            double envelope=Math.Min(1,seconds/.007)*Math.Pow(1-t,2.1);
            short sample=(short)(Math.Tanh((body*(1-texture)+textureBody*texture)*1.45)*envelope*gain*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,1550);
    }
    private static byte[] MakePlayerRelease(double duration,double gain)
    {
        int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(817);
        double phase=0,low=0,mid=0,echo=0;
        for(int i=0;i<count;i++)
        {
            double seconds=(double)i/SampleRate,t=seconds/duration,n=random.NextDouble()*2-1;
            low+=.026*(n-low);mid+=.19*(n-mid);
            phase+=Math.Tau*(48+125*Math.Exp(-seconds*18))/SampleRate;
            double thrust=Math.Sin(phase)*Math.Exp(-seconds*7.5)+(mid-low)*Math.Exp(-seconds*10)*2.8;
            double mantle=low*Math.Exp(-seconds*5)*2.1;
            echo+=.0018*(thrust-echo);
            double env=Math.Min(1,seconds/.008)*Math.Min(1,(1-t)*14);
            short sample=(short)(Math.Tanh((thrust+mantle+echo*.55)*1.6)*env*gain*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,1900);
    }
    private static byte[] MakeRoundedReward()
    {
        const double duration=.58;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];
        double[] notes={220,277.1826,329.6276};
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,value=0;
            for(int j=0;j<notes.Length;j++)
            {
                double age=t-j*.105;if(age<0)continue;
                double phase=age*Math.Tau*notes[j];
                double env=Math.Min(1,age/.006)*Math.Exp(-age*10);
                value+=(Math.Sin(phase)+.16*Math.Sin(phase*2)+.055*Math.Sin(phase*3))*env;
            }
            short sample=(short)(Math.Tanh(value*.8)*Math.Min(1,(duration-t)*30)*.16*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,2100);
    }
    private static byte[] MakeBossDefeat()
    {
        const double duration=.96;byte[] pcm=MakeSoilLanding(duration,1.0);
        double phase=0;
        for(int i=0;i<pcm.Length/2;i++)
        {
            double t=(double)i/SampleRate,existing=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768.0;
            phase+=Math.Tau*(38+61*Math.Exp(-t*10))/SampleRate;
            double body=(Math.Sin(phase)*.22+Math.Sin(phase*1.5)*.055)*Math.Exp(-t*4.3);
            double env=Math.Min(1,t/.006)*Math.Min(1,(duration-t)*20);
            short value=(short)(Math.Tanh((existing*.72+body)*1.25)*env*.7*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return RoundPlayerClip(pcm,1800);
    }
    private static byte[] MakeLanding(double duration,double weight)
    {
        int count=(int)(SampleRate*duration);var pcm=new byte[count*2];var random=new Random(930);
        double low=0,phase=0;double[] fractureTimes={.003,.016,.034,.062,.098,.148,.215,.31};
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,noise=random.NextDouble()*2-1;low+=.10*(noise-low);
            double dry=noise-low,fracture=0;
            for(int j=0;j<fractureTimes.Length;j++)
            {
                double age=t-fractureTimes[j];
                if(age>=0)
                {
                    double env=Math.Min(1,age/.0005)*Math.Exp(-age*(j<3?105:52));
                    double ring=Math.Sin(age*Math.Tau*(1850-j*150))*.23+Math.Sin(age*Math.Tau*(3100-j*170))*.12;
                    fracture+=(dry*.95+ring)*env*(1-j*.075);
                }
            }
            phase+=Math.Tau*(44+77*Math.Exp(-t*25))/SampleRate;
            double thump=Math.Sin(phase)*Math.Exp(-t*15)*.30;
            double grit=low*Math.Exp(-t*7)*.95;
            double debris=dry*Math.Exp(-Math.Max(0,t-.15)*9)*(.12+.12*Math.Sin(t*141)*Math.Sin(t*67));
            double fade=Math.Min(1,t/.0007)*Math.Min(1,(duration-t)*18);
            short value=(short)(Math.Tanh((fracture+thump+grit+debris)*1.65)*(.21+.23*weight)*fade*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return pcm;
    }

    private static byte[] Make(double duration,double f0,double f1,double noise,double gain)
    {
        int samples=(int)(SampleRate*duration);
        byte[] pcm=new byte[samples*2];
        var random=new Random(42);double phase=0;
        for(int i=0;i<samples;i++)
        {
            double t=(double)i/samples;
            phase+=2*Math.PI*(f0+(f1-f0)*t)/SampleRate;
            double envelope=Math.Min(1,t/0.035)*Math.Pow(1-t,2.5);
            double tone=Math.Sin(phase)*0.75+Math.Sin(phase*2.01)*0.25;
            short value=(short)(Math.Clamp((tone*(1-noise)+(random.NextDouble()*2-1)*noise)*envelope*gain,-1,1)*short.MaxValue);
            pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);
        }
        return pcm;
    }
    public void Toggle()
    {
        Muted=!Muted;
        if(Muted)StopAll();
        else if(requestedMusic>0)StartMusic(requestedMusic);
    }
    public void Play(string name)
    {
        Collect();
        if(paused||Muted||DeviceUnavailable)return;
        long now=Environment.TickCount64;
        // Coalescing identical physical contacts avoids coherent stacked copies.
        int interval=SoundRepeatInterval(name);
        if(interval>0&&lastSounds.TryGetValue(name,out long last)&&now-last<interval)return;
        if(name=="beam")CancelLaserCharge();
        if(!soundResources.TryGetValue(name,out var sound)||!ReserveSoundVoice(name))return;
        lastSounds[name]=now;
        var playback=softwareMixer.Play(sound,SoundGain(name));
        if(playback!=null)voices.Add(new Voice(name,playback));
    }
    public void Collect()
    {
        UpdateMusic();
        for(int i=voices.Count-1;i>=0;i--)
            if(voices[i].Playback.Completed){voices[i].Dispose();voices.RemoveAt(i);}
    }
    public void SetPaused(bool value)
    {
        paused=value;softwareMixer.SetPaused(value);
    }
    private void StopAll()
    {
        StopMusic();foreach(var voice in voices)voice.Dispose();voices.Clear();
        softwareMixer.StopSounds();lastSounds.Clear();
    }
    public void Dispose()
    {
        StopAll();audioOutput?.Dispose();audioOutput=null;
    }
    private sealed class Voice : IDisposable
    {
        public readonly string Key;
        public readonly SoftwareSoundVoice Playback;
        public Voice(string key,SoftwareSoundVoice playback){Key=key;Playback=playback;}
        public void Dispose()=>Playback.Stop();
    }
}
