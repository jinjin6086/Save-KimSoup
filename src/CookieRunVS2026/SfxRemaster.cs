namespace CookieRunVS2026;

// Original, offline-synthesized layers. No per-frame DSP or imported recordings:
// the audio thread only plays the finished PCM buffers registered below.
internal sealed partial class GameAudio
{
    private void ApplySoundRemaster()
    {
        clips["parry"]=SfxBoot(false);
        clips["perfect"]=SfxBoot(true);
        clips["jump"]=SfxAir(.25,1709,.27,false);
        clips["kickswing"]=SfxAir(.25,1717,.21,true);
        clips["dodge"]=SfxAir(.25,1721,.29,false);
        for(int terrain=0;terrain<3;terrain++)for(int foot=0;foot<2;foot++)
            clips[$"step{terrain}{foot}"]=SfxStep(terrain,foot);
        clips["landsoft"]=SfxEarth(.39,.45);
        clips["landmid"]=SfxEarth(.50,.72);
        clips["landheavy"]=SfxEarth(.65,1);
        clips["crash"]=SfxEarth(.36,.67);
        clips["meteorhit"]=SfxContact(.16,.40,73,1817);
        clips["impact"]=SfxContact(.24,.36,94,1823);
        clips["hurt"]=SfxContact(.26,.35,63,1831);
        clips["volley"]=SfxContact(.12,.23,121,1837);
        clips["slidein"]=SfxSlide(.48,0);
        clips["slidebody"]=SfxSlide(1.4,1);
        clips["slideout"]=SfxSlide(.19,2);
        clips["inhale"]=SfxBreath();
        clips["absorb"]=SfxAbsorb();
        clips["eat"]=SfxEat();
        clips["reverse"]=SfxRelease(.43,.49,1907);
        clips["clone"]=SfxRelease(.56,.39,1913);
        clips["down"]=SfxContact(.45,.46,43,1931);
        clips["charge"]=SfxCharge(.30,false);
        clips["select"]=SfxUi(.16,false);
        clips["round"]=SfxUi(.58,true);
        clips["reward"]=SfxUi(.58,true);
        clips["bossintro"]=SfxBoss(.52,0);
        clips["bossdefeat"]=SfxBoss(.96,1);
        clips["phase"]=SfxBoss(.7,2);
        clips["phasebreak"]=SfxBoss(.7,3);
        clips["menhera"]=SfxBoss(.8,4);
        clips["gravity"]=SfxGravity();
        clips["dashwarn"]=SfxWarning(.07,0);
        clips["beat"]=SfxWarning(.08,1);
        clips["final"]=SfxWarning(.15,2);
        clips["beat0"]=SfxWarning(.07,3);
        clips["beat1"]=SfxWarning(.07,4);
        clips["beat2"]=SfxWarning(.09,5);
        clips["dash"]=SfxDash();
        clips["beam"]=SfxBeam(.88,false);
        clips["counterbeam"]=SfxBeam(.55,true);
        for(int frames=26;frames<=62;frames++)clips["lasercharge"+frames]=SfxCharge(frames/60.0,true);
        clips["furiosostart"]=SfxEcho(0);
        clips["echoflight"]=SfxEcho(1);
        clips["echohit"]=SfxEcho(2);
        clips["cinderstart"]=SfxFire(0);
        clips["cinderthrow"]=SfxFire(1);
        clips["cinderhit"]=SfxFire(2);
        clips["cinderbrand"]=SfxFire(3);

        // The freeze/release rhythm, focus glitch and independent departures
        // already have authored cues. Keep their buffers' exact duration and
        // structure while giving them the same headroom and softened top end.
        foreach(string key in new[]{"focusenter","timestopstart","timestophold","timestoprelease","timestopstone","bossburst2","bossburst3","ruleblue","ruleorange","rulesafe"})
            if(clips.TryGetValue(key,out var pcm))clips[key]=SfxMaster(pcm,key.StartsWith("bossburst",StringComparison.Ordinal)?.61:.43,3700);
    }

    private sealed class SfxTexture
    {
        private readonly Random random;
        public double Low,Body,Air,White,Drift,Phase,Phase2,Crackle;
        public SfxTexture(int seed){random=new Random(seed);}
        public void Next()
        {
            White=random.NextDouble()*2-1;
            Low+=.018*(White-Low);Body+=.135*(White-Body);Air+=.29*(White-Air);
            Drift+=.00085*(White-Drift);
            if(random.NextDouble()<.0018)Crackle=.35+random.NextDouble()*.65;
            Crackle*=.91;
        }
        public double Osc(double frequency)
        {Phase+=Math.Tau*frequency/SampleRate;return Math.Sin(Phase);}
        public double Osc2(double frequency)
        {Phase2+=Math.Tau*frequency/SampleRate;return Math.Sin(Phase2);}
        public double Texture=>Body-Low;
        public double Fine=>Air-Body;
    }
    private static double SfxAttack(double t,double seconds=.003)=>Math.Clamp(t/seconds,0,1);
    private static double SfxTail(double t,double duration,double seconds=.025)=>Math.Clamp((duration-t)/seconds,0,1);
    private static double SfxHit(double t,double decay,double attack=.0018)=>t<0?0:SfxAttack(t,attack)*Math.Exp(-t*decay);
    private static byte[] SfxRender(double duration,int seed,double peak,double cutoff,Func<double,SfxTexture,double> sample,double room=0)
    {
        int count=(int)(duration*SampleRate);var dry=new double[count];var texture=new SfxTexture(seed);
        for(int i=0;i<count;i++){texture.Next();dry[i]=sample(i/(double)SampleRate,texture);}
        return SfxEncode(dry,peak,cutoff,room);
    }
    private static byte[] SfxEncode(double[] dry,double peak,double cutoff,double room=0)
    {
        var output=new double[dry.Length];double filter=0,dc=0,max=0;
        double a=1-Math.Exp(-Math.Tau*cutoff/SampleRate),h=1-Math.Exp(-Math.Tau*24/SampleRate);
        int d1=(int)(.039*SampleRate),d2=(int)(.083*SampleRate),d3=(int)(.137*SampleRate);
        for(int i=0;i<dry.Length;i++)
        {
            double v=dry[i];
            if(room>0){if(i>=d1)v+=dry[i-d1]*room;if(i>=d2)v+=dry[i-d2]*room*.53;if(i>=d3)v+=dry[i-d3]*room*.24;}
            filter+=a*(v-filter);dc+=h*(filter-dc);
            double edge=Math.Min(1,i/(SampleRate*.0012))*Math.Min(1,(dry.Length-1-i)/(SampleRate*.014));
            // Only the strongest transients reach the soft knee. Retaining the
            // attack-to-tail ratio is more useful than flattening every sound.
            double body=filter-dc,soft=Math.Abs(body)<.72?body:Math.Sign(body)*(.72+.28*Math.Tanh((Math.Abs(body)-.72)/.28));
            output[i]=soft*edge;max=Math.Max(max,Math.Abs(output[i]));
        }
        double gain=max>0?Math.Min(2.1,peak/max):1;var pcm=new byte[dry.Length*2];
        for(int i=0;i<output.Length;i++)
        {short s=(short)Math.Round(Math.Clamp(output[i]*gain,-peak,peak)*short.MaxValue);pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);}
        return pcm;
    }
    private static byte[] SfxMaster(byte[] pcm,double peak,double cutoff)
    {
        var dry=new double[pcm.Length/2];for(int i=0;i<dry.Length;i++)dry[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768.0;
        return SfxEncode(dry,peak,cutoff);
    }

    private static byte[] SfxBoot(bool perfect)
    {
        double duration=perfect?.29:.23;
        return SfxRender(duration,perfect?2179:2173,perfect?.48:.40,perfect?3350:2750,(t,s)=>
        {
            double heel=s.Osc(58+132*Math.Exp(-t*52))*.72*SfxHit(t,25);
            double leather=s.Texture*.78*SfxHit(t,49)+s.Fine*.21*SfxHit(t-.012,70);
            double sole=s.Low*1.45*SfxHit(t-.008,23);
            double recoil=Math.Sin(t*Math.Tau*126)*.16*SfxHit(t-.021,31);
            // A short high-mid flick marks perfect contact without the old
            // glassy, sustained sparkle that obscured the foot's actual hit.
            double clean=perfect?(Math.Sin(t*Math.Tau*1047)*.11+Math.Sin(t*Math.Tau*1570)*.045)*SfxHit(t-.011,38):0;
            return (heel+leather+sole+recoil+clean)*SfxTail(t,duration);
        },perfect?.035:.015);
    }
    private static byte[] SfxAir(double duration,int seed,double peak,bool kick)
    {
        return SfxRender(duration,seed,peak,2500,(t,s)=>
        {
            double p=t/duration,env=Math.Pow(Math.Sin(Math.PI*p),kick?1.25:1.65)*Math.Exp(-p*.5);
            double sweep=(s.Texture*1.25+s.Fine*(.06+.12*p)+s.Low*.6);
            double cloth=Math.Sin(t*Math.Tau*(kick?124:92))*.07*Math.Exp(-p*4);
            return (sweep+cloth)*env;
        });
    }
    private static byte[] SfxStep(int terrain,int foot)
    {
        const double duration=.16;
        return SfxRender(duration,2201+terrain*13+foot,terrain==1?.145:.13,terrain==1?2300:1950,(t,s)=>
        {
            double solid=s.Osc((terrain==1?112:82)*(1+foot*.067))*.40*SfxHit(t,48);
            double soil=s.Texture*(terrain==0?.67:.42)*SfxHit(t,terrain==0?34:49);
            double secondary=(s.Low*.85+s.Fine*.045)*SfxHit(t-.028,54);
            double grit=terrain==2?s.Crackle*s.Fine*.28*SfxHit(t-.015,23):0;
            return (solid+soil+secondary+grit)*SfxTail(t,duration);
        },terrain==1?.018:0);
    }
    private static byte[] SfxEarth(double duration,double weight)
    {
        double[] fractures={.009,.026,.058,.102,.168,.251,.355,.476};
        return SfxRender(duration,2251+(int)(weight*100),.30+weight*.21,2400,(t,s)=>
        {
            double body=s.Osc(39+90*Math.Exp(-t*37))*.82*SfxHit(t,11.5);
            double earth=s.Low*2.2*SfxHit(t,7.7);
            double cracks=0;
            for(int j=0;j<fractures.Length;j++)
            {
                double age=t-fractures[j];if(age<0)continue;
                double fracture=s.Texture*.68+s.Fine*.13+Math.Sin(age*Math.Tau*(430-j*31))*.16;
                cracks+=fracture*SfxHit(age,j<3?76:36)*(1-j*.102)*(.62+.38*weight);
            }
            double dirt=(s.Body*.28+s.Low*.68)*SfxAttack(t,.055)*Math.Exp(-Math.Max(0,t-.10)*10);
            return (body+earth+cracks+dirt)*SfxTail(t,duration,.055);
        },.02);
    }
    private static byte[] SfxContact(double duration,double peak,double pitch,int seed)
    {
        return SfxRender(duration,seed,peak,3000,(t,s)=>
            (s.Osc(pitch+112*Math.Exp(-t*49))*.70*SfxHit(t,20)+s.Texture*.62*SfxHit(t,35)
                +s.Fine*.13*SfxHit(t-.009,65)+s.Low*.8*SfxHit(t,13))*SfxTail(t,duration),.018);
    }
    private static byte[] SfxSlide(double duration,int part)
    {
        return SfxRender(duration,2293+part,part==0?.24:part==1?.12:.11,2100,(t,s)=>
        {
            double p=t/duration;
            double pressure=.75+.15*Math.Sin(t*35)+.07*Math.Sin(t*81+1.2)+.03*Math.Sin(t*127);
            double grit=s.Texture*.88+s.Low*.58+s.Fine*.065;
            double env=part==0?SfxAttack(t,.012)*Math.Pow(1-p,.85):part==1?SfxAttack(t,.11)*SfxTail(t,duration,.17):SfxAttack(t,.008)*Math.Pow(1-p,1.8);
            double sole=part==0?s.Osc(76)*.16*SfxHit(t,43):0;
            // Cloth/soil drag has no pitched oscillator during the held slide.
            return grit*pressure*env+sole;
        });
    }
    private static byte[] SfxBreath()=>SfxRender(.45,2333,.21,2300,(t,s)=>
    {
        double p=t/.45,env=Math.Pow(Math.Sin(Math.PI*p),1.7),throat=s.Osc(118)*.035;
        return ((s.Texture*1.15+s.Low*.43)*(1+.1*Math.Sin(t*63))+throat)*env;
    });
    private static byte[] SfxAbsorb()=>SfxRender(.32,2341,.30,3100,(t,s)=>
    {
        double p=t/.32,env=Math.Pow(Math.Sin(Math.PI*p),1.3);
        double suction=s.Texture*(.45+p*.3)+s.Low*.43;
        double resonance=s.Osc(116+210*p*p)*.24+s.Osc2(231+106*p)*.07;
        return (suction+resonance)*env;
    },.022);
    private static byte[] SfxEat()=>SfxRender(.22,2347,.28,2100,(t,s)=>
    {
        double jaw=s.Osc(116+155*Math.Exp(-t*38))*.44*SfxHit(t,29);
        double crunch=(s.Texture*.75+s.Fine*.075)*SfxHit(t-.012,39);
        double swallow=s.Low*1.5*SfxHit(t-.05,26);
        return (jaw+crunch+swallow)*SfxTail(t,.22);
    });
    private static byte[] SfxRelease(double duration,double peak,int seed)=>SfxRender(duration,seed,peak,3200,(t,s)=>
    {
        double core=s.Osc(43+149*Math.Exp(-t*19))*.74;
        double pressure=s.Texture*.69+s.Low*1.4+s.Fine*.10*Math.Exp(-t*22);
        double body=(core+pressure)*SfxHit(t,7.8,.004);
        return body*SfxTail(t,duration,.045);
    },.055);
    private static byte[] SfxUi(double duration,bool reward)=>SfxRender(duration,reward?2371:2377,reward?.23:.16,3300,(t,s)=>
    {
        double v=0;double[] notes=reward?new[]{261.626,329.628,391.995}:new[]{392.0,523.251};
        for(int n=0;n<notes.Length;n++)
        {
            double age=t-n*(reward?.095:.045);if(age<0)continue;
            double phase=age*Math.Tau*notes[n];
            v+=(Math.Sin(phase)+Math.Sin(phase*2)*.16+Math.Sin(phase*3)*.055)*SfxHit(age,reward?10:29,.004);
        }
        return v*SfxTail(t,duration,.04);
    },reward?.14:.025);
    private static byte[] SfxBoss(double duration,int kind)=>SfxRender(duration,2393+kind,.48,3400,(t,s)=>
    {
        double p=t/duration,rise=kind==2||kind==4?1:0;
        double hz=rise>0?52+150*p*p:43+139*Math.Exp(-t*15);
        double tone=s.Osc(hz)*.47+s.Osc2(hz*1.498)*.20;
        double noise=s.Low*1.2+s.Texture*(kind==4?.48:.31)+s.Fine*.05;
        double env=rise>0?SfxAttack(t,.025)*(.2+.8*p)*SfxTail(t,duration,.07):SfxHit(t,kind==1?4:6,.006)*SfxTail(t,duration,.06);
        double crease=kind==4?Math.Sin(t*Math.Tau*418)*Math.Sin(t*19)*.08:0;
        return (tone+noise+crease)*env;
    },.055);
    private static byte[] SfxGravity()=>SfxRender(.55,2411,.38,3200,(t,s)=>
    {
        double p=t/.55,env=Math.Pow(Math.Sin(Math.PI*p),.7);
        return (s.Osc(85+530*Math.Pow(p,1.7))*.35+s.Osc2(460-370*p)*.21+s.Texture*.35)*env;
    },.05);
    private static byte[] SfxWarning(double duration,int kind)=>SfxRender(duration,2431+kind,kind==0?.28:.24,4100,(t,s)=>
    {
        double frequency=kind switch{0=>1480,1=>784,2=>1174,3=>523.25,4=>659.25,_=>880};
        double p=t/duration,env=SfxAttack(t,.002)*Math.Pow(1-p,.72)*SfxTail(t,duration,.008);
        double signal=s.Osc(frequency+(kind==0?260*p:0))*.68+s.Osc2(frequency*1.5)*.13;
        return signal*env;
    });
    private static byte[] SfxDash()=>SfxRender(.24,2459,.44,3600,(t,s)=>
    {
        double p=t/.24;
        double rush=(s.Texture*1.3+s.Fine*.15+s.Low*.5)*Math.Pow(Math.Sin(Math.PI*p),.7);
        double body=s.Osc(54+180*Math.Exp(-t*24))*.35*SfxHit(t,12);
        return (rush+body)*SfxTail(t,.24);
    },.028);
    private static byte[] SfxCharge(double duration,bool laser)=>SfxRender(duration,2473+(int)(duration*100),laser?.38:.25,4000,(t,s)=>
    {
        double p=t/duration,f=laser?105+695*p*p:110+260*p;
        double gather=s.Osc(f)*.37+s.Osc2(f*1.007)*.19+Math.Sin(s.Phase*2)*.06;
        double air=s.Texture*(.075+p*.20)+s.Fine*.025*p;
        double onset=laser?(Math.Sin(t*Math.Tau*1480)*.12+Math.Sin(t*Math.Tau*2217)*.025)*SfxHit(t,27):0;
        double env=SfxAttack(t,.008)*(.13+.87*Math.Pow(p,1.1))*SfxTail(t,duration,.04);
        return (gather+air)*env+onset;
    },.035);
    private static byte[] SfxBeam(double duration,bool counter)=>SfxRender(duration,counter?2503:2521,counter?.58:.55,4300,(t,s)=>
    {
        double p=t/duration,f=(counter?91:54)+(counter?150:115)*Math.Exp(-t*24);
        double core=s.Osc(f)*.57+s.Osc2(f*1.009)*.24+Math.Sin(s.Phase*2.01)*.15;
        double tearing=s.Texture*.42+s.Fine*(.055+.12*Math.Exp(-t*30));
        double pressure=s.Low*.88+Math.Sin(t*Math.Tau*(counter?176:91))*.18;
        double env=SfxAttack(t,.0035)*Math.Exp(-t*(counter?2.6:1.25))*SfxTail(t,duration,.07);
        double pulse=.85+.10*Math.Sin(t*54)+.05*Math.Sin(t*93);
        // Counterfire has a brief kick-like ignition and higher harmonics;
        // the pink incoming beam retains a lower, continuous pressure body.
        double ignition=counter?(s.Fine*.17+Math.Sin(t*Math.Tau*137)*.32)*SfxHit(t,48):s.Fine*.12*SfxHit(t,32);
        return (core+tearing+pressure)*env*pulse+ignition;
    },.05);
    private static byte[] SfxEcho(int kind)
    {
        double duration=kind==0?.82:kind==1?28.0/60:.24;
        return SfxRender(duration,2543+kind,kind==0?.43:kind==1?.42:.46,kind==2?4100:3900,(t,s)=>
        {
            double p=t/duration;
            if(kind==0)
            {
                double ping=s.Osc(1244.5+120*Math.Exp(-t*90))*.66*SfxHit(t,7.6,.002);
                double ring=(Math.Sin(s.Phase*2.414)*.12*Math.Exp(-t*16)+Math.Sin(s.Phase*3.73)*.035*Math.Exp(-t*30))*SfxAttack(t,.002);
                double aura=(s.Texture*.38+s.Low*.43)*SfxHit(t-.035,4,.07);
                return (ping+ring+aura)*SfxTail(t,duration,.08);
            }
            if(kind==1)
            {
                double arc=s.Osc(730-440*p)*.22+Math.Sin(s.Phase*2.99+Math.Sin(t*121)*.4)*.07;
                double electric=(s.Texture*.82+s.Fine*.12)*(.82+.18*Math.Sin(t*119));
                return (arc+electric+s.Low*.7)*SfxHit(t,4,.005)*SfxTail(t,duration,.04);
            }
            double contact=s.Osc(112+166*Math.Exp(-t*43))*.55*SfxHit(t,28);
            double snap=s.Fine*.27*SfxHit(t-.006,61)+s.Texture*.57*SfxHit(t,38);
            double arcTail=Math.Sin(t*Math.Tau*1397)*.085*SfxHit(t-.014,31);
            return (contact+snap+arcTail)*SfxTail(t,duration);
        },kind==0?.09:kind==1?.055:.025);
    }
    private static byte[] SfxFire(int kind)
    {
        double duration=kind==0?1.18:kind==1?.23:kind==2?.32:.65;
        return SfxRender(duration,2579+kind,kind==0?.61:kind==1?.33:kind==2?.40:.43,kind==0?3600:3300,(t,s)=>
        {
            double p=t/duration,flame=s.Texture*.75+s.Fine*.10+s.Low*1.3+s.Crackle*s.Texture*.17;
            if(kind==0)
            {
                double ignition=s.Osc(37+115*Math.Exp(-t*17))*.58*SfxHit(t,5.7,.004);
                double flare=flame*SfxHit(t,2.9,.04);
                return (ignition+flare)*SfxTail(t,duration,.21);
            }
            if(kind==1)return (flame*.7+s.Osc(192-112*p)*.16)*Math.Pow(Math.Sin(Math.PI*p),.7)*Math.Exp(-p*1.1);
            if(kind==2)return (flame+s.Osc(57+94*Math.Exp(-t*37))*.45)*SfxHit(t,13)*SfxTail(t,duration,.04);
            return (flame*.8+s.Osc(83+58*Math.Exp(-t*15))*.24+Math.Sin(t*Math.Tau*260)*.09)*SfxHit(t,5,.008)*SfxTail(t,duration,.10);
        },kind==0?.045:kind==3?.055:.018);
    }
}
