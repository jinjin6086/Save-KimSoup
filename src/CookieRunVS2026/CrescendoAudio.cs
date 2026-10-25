namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    // Kept for the director's timing API. The latest mix contract requires
    // the idle music level throughout every interaction, including this finale.
    public void SetFinaleMix(float gain){}
    public void StopClashSounds()
    {
        for(int i=voices.Count-1;i>=0;i--)if(voices[i].Key.StartsWith("clash",StringComparison.Ordinal))
        {voices[i].Dispose();voices.RemoveAt(i);}
    }
    public void PlayClashSustain(){Collect();if(!voices.Any(v=>v.Key=="clashsustain"))Play("clashsustain");}
    public void PlayClashSurge(){StopClashSounds();SetFinaleMix(.13f);Play("clashsurge");}
    public void PlayClashBreak(){StopClashSounds();SetFinaleMix(0);Play("clashbreak");}
    public void PlayClashExplosion(){StopClashSounds();SetFinaleMix(.12f);Play("clashexplosion");}

    private static byte[] MakeClashSound(int kind)
    {
        if(kind==10)return MakeFinalePressureSurge();
        if(kind==11)return MakeFinaleMetal();
        if(kind==12)return MakeFinaleAftershock();
        // The director owns every edit point. These are sound design stems,
        // not extra timers: the 150/300/210-frame shots retain their lengths.
        double duration=kind switch{0=>2.5,1=>.52,2=>3.5,3=>2.5,4=>.15,5=>2.2,6=>1.2,7=>5,8=>.72,10=>112.0/60,_=>.9};
        int count=(int)(duration*SampleRate);var dry=new double[count];var rng=new Random(24191+kind*67);
        double sub=0,body=0,air=0,phase=0,edgePhase=0,upperPhase=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=rng.NextDouble()*2-1;
            sub+=.011*(n-sub);body+=.085*(n-body);air+=.34*(n-air);
            double grain=body-sub,wind=air-body;
            double f=kind switch{0=>32+132*p*p,1=>34+151*Math.Exp(-t*20),2=>61+165*p*p,
                3=>49+3.4*Math.Sin(t*5.1),4=>72+146*Math.Exp(-t*61),5=>33+147*Math.Exp(-t*8),
                6=>37+83*Math.Exp(-t*5),7=>110,8=>44+20*Math.Exp(-t*28),10=>39+219*p*p,_=>36+172*Math.Exp(-t*19)};
            phase+=Math.Tau*f/SampleRate;edgePhase+=Math.Tau*(f*1.503+2.7)/SampleRate;upperPhase+=Math.Tau*(f*3.019+9)/SampleRate;
            double value;
            switch(kind)
            {
                case 0:
                {
                    // A single needle of light, then progressively heavier
                    // intake pulses; the upper register is free for the score.
                    double pull=SmoothAudio(p),strain=.75+.25*Math.Pow(Math.Sin(t*17+p*18),2);
                    double pressure=Math.Pow(.5+.5*Math.Sin(Math.Tau*(1.1*t+1.45*t*t)),5);
                    value=(Math.Sin(phase)*.22+Math.Sin(edgePhase)*.095+sub*5.1+grain*(.5+1.1*p))*pull*strain;
                    value+=(sub*2.8+Math.Sin(phase*.5)*.22)*pressure*pull;
                    value+=Math.Sin(upperPhase+.8*Math.Sin(t*29))*.032*pull;
                    value+=CinematicPing(t,1568,.31,5.3);
                    value+=Math.Sin(Math.Tau*(102*t+48*t*t))*.035*SmoothAudio((p-.48)/.42);
                    break;
                }
                case 1:
                {
                    // An approaching pressure front followed by a dry crack.
                    double suction=Math.Pow(1-p,.6),crack=Math.Exp(-t*34);
                    value=(sub*7.7+grain*2.7+Math.Sin(phase)*.56)*Math.Exp(-t*5.2)
                        +wind*1.15*crack+Math.Sin(edgePhase)*.10*suction;
                    break;
                }
                case 2:
                {
                    double resolve=SmoothAudio((p-.27)/.47),rise=SmoothAudio(p/.60);
                    double bodyPulse=.86+.10*Math.Sin(t*12.7)+.04*Math.Sin(t*29);
                    value=(sub*3.9+grain*(.95+p*.8)+Math.Sin(phase+.42*Math.Sin(edgePhase))*.15)*rise*bodyPulse;
                    value+=(Math.Sin(Math.Tau*293.665*t)*.043+Math.Sin(Math.Tau*440*t)*.027
                        +Math.Sin(Math.Tau*587.33*t)*.014)*resolve;
                    value+=CinematicPing(t,1244.51,.29,4.8);
                    value+=Math.Sin(upperPhase)*.024*rise;
                    break;
                }
                case 3:
                {
                    // Energy rolls rather than hisses. It sits below the
                    // scored pulse and leaves clear attacks for each QTE tap.
                    double breathing=.74+.14*Math.Sin(t*11.1)+.07*Math.Sin(t*27.3);
                    double vortex=.5+.5*Math.Sin(t*41+.8*Math.Sin(t*5.7));
                    value=(sub*4.8+grain*1.8+Math.Sin(phase+.26*Math.Sin(edgePhase))*.16)*breathing;
                    value+=wind*.085*vortex+Math.Sin(upperPhase)*.022;
                    break;
                }
                case 4:
                {
                    double leather=Math.Exp(-t*45),knock=Math.Exp(-t*27);
                    value=Math.Sin(phase)*.48*knock+grain*1.55*leather+wind*.36*Math.Exp(-t*74);
                    value+=Math.Sin(Math.Tau*472*t)*.065*Math.Exp(-t*31);
                    break;
                }
                case 5:
                    value=(Math.Sin(phase)*.58+sub*6.7+grain*2.0)*Math.Exp(-t*1.9)
                        +wind*1.1*Math.Exp(-t*17);
                    if(t>.21)value+=(grain*1.5+Math.Sin(Math.Tau*76*(t-.21))*.23)*Math.Exp(-(t-.21)*12);
                    break;
                case 6:
                    value=(Math.Sin(phase)*.46+sub*5.4+grain*1.0)*Math.Exp(-t*3.7);
                    value+=Math.Sin(Math.Tau*(188*t-36*t*t))*.095*Math.Exp(-t*4.1);
                    break;
                case 7:
                {
                    // A restrained remembered promise. The heartbeat and words
                    // carry the close-up; harmonic resolution is earned later.
                    double hope=SmoothAudio((p-.57)/.31),breath=Math.Sin(Math.PI*p);
                    value=(Math.Sin(Math.Tau*146.83*t)*.048+Math.Sin(Math.Tau*220*t)*.027
                        +Math.Sin(Math.Tau*174.61*t)*.017*(1-hope)
                        +Math.Sin(Math.Tau*293.665*t)*.022*hope)*breath;
                    value+=sub*.34*breath;
                    break;
                }
                case 8:
                {
                    double second=t-.185;
                    value=(Math.Sin(Math.Tau*56*t)*.77+sub*1.9)*Math.Exp(-t*34);
                    if(second>0)value+=(Math.Sin(Math.Tau*43*second)*.54+sub*1.35)*Math.Exp(-second*30);
                    break;
                }
                case 10:
                {
                    // The score resolves underneath this rising physical push.
                    // Increasing density, not continuous treble, makes room for
                    // the cold metallic cut at precisely frame 112.
                    double force=.34+.66*SmoothAudio(p),pulse=.82+.18*Math.Sin(t*22+p*24);
                    value=(sub*5.4+grain*(1.5+1.1*p)+Math.Sin(phase)*.25+Math.Sin(edgePhase)*.11)*force*pulse;
                    value+=wind*.37*p*p+Math.Sin(upperPhase)*.035*p;
                    value+=(Math.Sin(Math.Tau*67*t)*.31+grain*1.3)*Math.Exp(-t*19);
                    double crest=Math.Pow(.5+.5*Math.Sin(Math.Tau*(2*t+2.7*t*t)),7);
                    value+=(grain*1.6+sub*2.0)*crest*p;
                    break;
                }
                default:
                    // The first blue beam has a solid, short front and a
                    // luminous falling tail instead of a piercing synth beep.
                    value=(Math.Sin(phase)*.64+sub*6.1+grain*2.5)*Math.Exp(-t*4.0)
                        +wind*1.25*Math.Exp(-t*21)+Math.Sin(edgePhase)*.11*Math.Exp(-t*5.3);
                    break;
            }
            double attack=kind is 1 or 4 or 5 or 9?.0018:kind==8?.003:.012;
            double fade=kind==3?.065:kind==7?.15:.035;
            dry[i]=value*SmoothAudio(t/attack)*(1-SmoothAudio((t-(duration-fade))/fade));
        }
        double gain=kind switch{0=>.49,1=>.60,2=>.43,3=>.24,4=>.38,5=>.59,6=>.48,7=>.33,8=>.40,10=>.53,_=>.60};
        double cutoff=kind==8?820:kind==7?1700:kind is 1 or 5 or 9?4100:kind==4?3200:kind==3?2550:3650;
        return RenderCinematicStem(dry,gain,cutoff,kind is not (4 or 8),kind==7?.23:kind==3?.12:.19);
    }

    private static double CinematicPing(double time,double pitch,double gain,double decay)
    {
        // Unequal partial decay and slight detuning provide an original struck
        // glass/metal onset. Brightness is momentary, not a continuous alarm.
        return (Math.Sin(Math.Tau*pitch*time)*Math.Exp(-time*decay)
            +Math.Sin(Math.Tau*(pitch*1.502+1.7)*time)*.23*Math.Exp(-time*(decay+5))
            +Math.Sin(Math.Tau*(pitch*2.011)*time)*.045*Math.Exp(-time*(decay+12)))
            *gain*SmoothAudio(time/.0016);
    }

    private static byte[] RenderCinematicStem(double[] dry,double gain,double cutoff,bool room,double roomGain)
    {
        int count=dry.Length;var pcm=new byte[count*2];
        int d1=(int)(SampleRate*.041),d2=(int)(SampleRate*.097),d3=(int)(SampleRate*.181),d4=(int)(SampleRate*.293);
        double roomLow=0;
        for(int i=0;i<count;i++)
        {
            double echo=0;
            if(room)
            {
                if(i>=d1)echo+=dry[i-d1]*roomGain;
                if(i>=d2)echo+=dry[i-d2]*roomGain*.67;
                if(i>=d3)echo+=dry[i-d3]*roomGain*.41;
                if(i>=d4)echo+=dry[i-d4]*roomGain*.23;
            }
            roomLow+=.19*(echo-roomLow);
            double fade=1-SmoothAudio((i-(count-SampleRate*.032))/(SampleRate*.032));
            short sample=(short)(Math.Tanh((dry[i]+roomLow)*1.55)*gain*fade*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,cutoff);
    }

    private static double SmoothAudio(double value){value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
}
