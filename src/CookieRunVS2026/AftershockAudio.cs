namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    public void StopDepartureSounds()
    {
        for(int i=voices.Count-1;i>=0;i--)if(voices[i].Key=="exitpant"){voices[i].Dispose();voices.RemoveAt(i);}
    }
    private static byte[] MakeExitPant()
    {
        const double duration=1.6;int count=(int)(duration*SampleRate);var dry=new double[count];
        var rng=new Random(66571);double chest=0,air=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,n=rng.NextDouble()*2-1;chest+=.042*(n-chest);air+=.26*(n-air);
            double value=0;
            for(int breath=0;breath<2;breath++)
            {
                double local=t-(breath==0?.02:.81);if(local<0||local>.68)continue;
                double inhale=Math.Sin(Math.Min(1,local/.17)*Math.PI)*Math.Exp(-local*5.3);
                double release=Math.Max(0,local-.14),exhale=local>.14?Math.Pow(Math.Sin(Math.Min(1,release/.49)*Math.PI),.8):0;
                double rough=.79+.13*Math.Sin(local*63)+.08*Math.Sin(local*101);
                double voice=Math.Sin(Math.Tau*116*local+.13*Math.Sin(local*73))*.025;
                value+=((air-chest)*1.18+chest*.84)*inhale*.69
                    +((air-chest)*.91+chest*.93+voice)*exhale*rough;
            }
            dry[i]=value*SmoothAudio(t/.009)*(1-SmoothAudio((t-(duration-.075))/.075));
        }
        return RenderCinematicStem(dry,.36,2200,false,0);
    }

    // Both edits use one pressure performance. There is no new impact or
    // struck pitch at the monochrome cut: its strained crest continues the
    // preceding low roar, then releases into the director's silent breath.
    private static readonly Lazy<byte[]> FinalePressurePcm=new(MakeFinalePressurePcm);

    private static byte[] MakeFinalePressureSurge()
    {
        int count=(int)(FinaleTiming.Impact/60.0*SampleRate);
        return FinalePressurePcm.Value.AsSpan(0,count*2).ToArray();
    }

    private static byte[] MakeFinaleMetal()
    {
        int count=(int)(FinaleTiming.MetalSeconds*SampleRate),audible=(int)(FinaleTiming.MetalAudibleSeconds*SampleRate);
        int start=(int)(FinaleTiming.Impact/60.0*SampleRate);
        var pcm=new byte[count*2];var pressure=FinalePressurePcm.Value;
        // The existing cue gains differ (.78 surge / .95 crest). Compensate
        // here, so crossing frame 112 does not cause an artificial gain jump.
        const double crestGain=.78/.95;
        for(int i=0;i<audible;i++)
        {
            int at=(start+i)*2;
            short sample=(short)Math.Round((short)(pressure[at]|pressure[at+1]<<8)*crestGain);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        // Frames 148..160 remain exact silence, including all room/filter tails.
        return pcm;
    }

    private static byte[] MakeFinalePressurePcm()
    {
        double duration=FinaleTiming.Silence/60.0;
        int count=(int)(duration*SampleRate);var dry=new double[count];
        var rng=new Random(66319);
        double sub=0,body=0,air=0,phase=0,depthPhase=0,stressPhase=0;
        double formant1x1=0,formant1x2=0,formant1y1=0,formant1y2=0;
        double formant2x1=0,formant2x2=0,formant2y1=0,formant2y2=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=rng.NextDouble()*2-1;
            sub+=.012*(n-sub);body+=.095*(n-body);air+=.28*(n-air);
            double strain=SmoothAudio((p-.57)/.43),rise=Math.Pow(p,2.25);
            // An accelerating pressure rise, with no falling chirp or bell
            // partial. The independent deep layer retains weight as it strains.
            phase+=Math.Tau*(43+139*rise+69*strain*strain)/SampleRate;
            depthPhase+=Math.Tau*(36+11*p)/SampleRate;
            stressPhase+=Math.Tau*(91+79*rise+41*strain)/SampleRate;
            double throat=Math.Tanh((Math.Sin(phase+.035*body)+.13*Math.Sin(stressPhase))*2.3);
            double drive=throat*.26+(body-sub)*2.4+n*.055;
            double formant1=FinalePressureResonance(drive,285+650*strain+290*strain*strain,2.1,
                ref formant1x1,ref formant1x2,ref formant1y1,ref formant1y2);
            double formant2=FinalePressureResonance(drive,690+1080*strain,2.8,
                ref formant2x1,ref formant2x2,ref formant2y1,ref formant2y2);
            double force=.24+.76*SmoothAudio(p),grip=.91+.055*Math.Sin(t*23.7)+.035*Math.Sin(t*47.1);
            double chest=sub*4.8+(body-sub)*(1.35+.65*strain)
                +Math.Sin(depthPhase)*(.24-.065*strain)+throat*(.105+.055*strain);
            double edge=formant1*(.60+.85*strain)+formant2*(.13+.65*strain)
                +(air-body)*.11*strain;
            // Broad, non-periodic load fluctuations keep it physical. The
            // crest is sustained strain, not a second transient at the cut.
            double loading=1+.06*Math.Sin(t*8.31+1.1*Math.Sin(t*2.7));
            double release=1-SmoothAudio((t-(duration-.055))/.055);
            dry[i]=(chest+edge)*force*grip*loading*SmoothAudio(t/.026)*release;
        }
        return RenderCinematicStem(dry,.55,3500,true,.075);
    }

    private static double FinalePressureResonance(double input,double frequency,double q,
        ref double x1,ref double x2,ref double y1,ref double y2)
    {
        // Constant-peak bandpass: moving vowel-like resonances supply the
        // tightening upper edge without a wall of unfiltered white noise.
        double omega=Math.Tau*frequency/SampleRate,alpha=Math.Sin(omega)/(2*q),a0=1+alpha;
        double output=(alpha*input-alpha*x2+2*Math.Cos(omega)*y1-(1-alpha)*y2)/a0;
        x2=x1;x1=input;y2=y1;y1=output;return output;
    }

    private static byte[] MakeFinaleAftershock()
    {
        double duration=FinaleTiming.ExplosionSeconds;int count=(int)(duration*SampleRate);var dry=new double[count];
        var rng=new Random(66347);double sub=0,body=0,air=0,phase=0,earth=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,n=rng.NextDouble()*2-1;
            sub+=.009*(n-sub);body+=.093*(n-body);air+=.35*(n-air);earth+=.027*(n-earth);
            phase+=Math.Tau*(34+123*Math.Exp(-t*14))/SampleRate;
            double grain=body-sub,crack=air-body;
            // The leading shell, broad pressure bloom, and rolling aftermath
            // arrive separately, so the finish has a shape rather than hiss.
            double front=(Math.Sin(phase)*.77+sub*5.1)*Math.Exp(-t*2.7)
                +crack*1.45*Math.Exp(-t*32)+grain*1.55*Math.Exp(-t*7.8);
            double rupture=0;
            if(t>.075)
            {
                double x=t-.075;
                rupture=(earth*4.3+grain*2.05+Math.Sin(Math.Tau*94*x)*.18)*SmoothAudio(x/.009)*Math.Exp(-x*7.4);
            }
            double bloom=SmoothAudio(t/.13)*(earth*3.7+sub*2.1+grain*.63
                +Math.Sin(Math.Tau*36.71*t)*.12+Math.Sin(Math.Tau*73.42*t)*.032)*Math.Exp(-t*.80);
            double rubble=grain*.60*(.36+.64*Math.Pow(.5+.5*Math.Sin(t*63)*Math.Sin(t*37),2))
                *SmoothAudio(t/.18)*Math.Exp(-Math.Max(0,t-.32)*1.55);
            double distant=0;
            if(t>.44)
            {
                double x=t-.44;
                distant=(sub*2.2+grain*.68)*SmoothAudio(x/.07)*Math.Exp(-x*2.1);
            }
            dry[i]=(front+rupture+bloom+rubble+distant)*SmoothAudio(t/.0018)
                *(1-SmoothAudio((t-(duration-.55))/.55));
        }
        int[] delays={ (int)(SampleRate*.061),(int)(SampleRate*.173),(int)(SampleRate*.337),
            (int)(SampleRate*.619),(int)(SampleRate*.997),(int)(SampleRate*1.433) };
        double[] gains={.20,.15,.12,.095,.063,.032};var pcm=new byte[count*2];double roomLow=0;
        for(int i=0;i<count;i++)
        {
            double room=0;for(int k=0;k<delays.Length;k++)if(i>=delays[k])room+=dry[i-delays[k]]*gains[k];
            roomLow+=.11*(room-roomLow);
            double t=i/(double)SampleRate,edge=SmoothAudio(t/.0018)*(1-SmoothAudio((t-(duration-.43))/.43));
            short sample=(short)(Math.Tanh((dry[i]+roomLow)*1.57)*.72*edge*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,4300);
    }
}
