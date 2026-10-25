namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    public void StopTimeStopSounds()
    {
        for(int i=voices.Count-1;i>=0;i--)
            if(voices[i].Key.StartsWith("timestop",StringComparison.Ordinal))
            {voices[i].Dispose();voices.RemoveAt(i);}
    }
    public void PlayTimeStopHold()
    {
        Collect();
        if(!voices.Any(v=>v.Key=="timestophold"))Play("timestophold");
    }
    // Original, deterministic PCM synthesis. No sampled dialogue, OST or game audio.
    // Register these four buffers once at startup; the encounter owns their timing.
    private static byte[] MakeTimeStopStart()
    {
        const double duration=.90;
        var samples=new double[(int)(duration*SampleRate)];var random=new Random(26341);
        double low=0,mid=0,kickPhase=0,metalPhase=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;
            low+=.045*(n-low);mid+=.29*(n-mid);
            kickPhase+=Math.Tau*(37+122*Math.Exp(-t*30))/SampleRate;
            metalPhase+=Math.Tau*(66+905*Math.Exp(-t*5.8))/SampleRate;
            double body=Math.Sin(kickPhase)*Math.Exp(-t*7.8)*.82;
            double fractured=(mid-low)*Math.Exp(-t*29)*1.12;
            double bell=(Math.Sin(metalPhase)+Math.Sin(metalPhase*1.419)*.45+Math.Sin(metalPhase*2.173)*.21)*Math.Exp(-t*5.5)*.29;
            double sweep=Math.Sin(Math.PI*Math.Clamp(t/.48,0,1));
            double vacuum=(low*.7+(mid-low)*.38)*sweep*sweep*(.75+.25*Math.Sin(t*Math.Tau*11));
            double tick=TimeStopTick(t-.685,.043)*.22+TimeStopTick(t-.803,.048)*.29;
            double attack=Math.Min(1,t/.004),tail=Math.Min(1,(duration-t)/.055);
            samples[i]=(body+fractured+bell+vacuum+tick)*attack*tail;
        }
        TimeStopRoom(samples,.073,.17,.119,.09);
        return EncodeTimeStop(samples,.72,3400);
    }

    private static byte[] MakeTimeStopHold()
    {
        const double duration=1.0;
        var samples=new double[(int)(duration*SampleRate)];var random=new Random(26343);
        double low=0,mid=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;
            low+=.018*(n-low);mid+=.13*(n-mid);
            double emptyAir=(mid-low)*.15+low*.45;
            double suspended=(Math.Sin(t*Math.Tau*68)+Math.Sin(t*Math.Tau*73.7)*.64)*.10;
            double slowPulse=.79+.21*Math.Sin(t*Math.Tau*1.7);
            double tick=TimeStopTick(t-.235,.035)*.09+TimeStopTick(t-.735,.032)*.07;
            double edge=Math.Min(1,t/.045)*Math.Min(1,(duration-t)/.085);
            samples[i]=(emptyAir+suspended*slowPulse+tick)*edge;
        }
        return EncodeTimeStop(samples,.33,1900);
    }

    private static byte[] MakeTimeStopRelease()
    {
        const double duration=.60;
        var samples=new double[(int)(duration*SampleRate)];var random=new Random(26347);
        double low=0,mid=0,kickPhase=0,fracturePhase=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;
            low+=.05*(n-low);mid+=.34*(n-mid);
            double rise=Math.Clamp(t/.135,0,1),vacuum=(low*.65+(mid-low)*.8)*rise*rise*Math.Max(0,1-t/.155)*2.3;
            double local=Math.Max(0,t-.135),clack=0,kick=0,shards=0;
            if(t>=.135)
            {
                fracturePhase+=Math.Tau*(330+1460*Math.Exp(-local*53))/SampleRate;
                clack=((mid-low)*1.5+Math.Sin(fracturePhase)*.29)*Math.Exp(-local*31)*Math.Min(1,local/.0018);
                shards=(Math.Sin(fracturePhase*1.733)*.13+low*.25)*Math.Exp(-local*13);
            }
            if(t>=.157)
            {
                double impact=t-.157;kickPhase+=Math.Tau*(44+126*Math.Exp(-impact*31))/SampleRate;
                kick=Math.Sin(kickPhase)*Math.Exp(-impact*10)*Math.Min(1,impact/.003)*.88;
            }
            double edge=Math.Min(1,t/.009)*Math.Min(1,(duration-t)/.06);
            samples[i]=(vacuum+clack+shards+kick)*edge;
        }
        TimeStopRoom(samples,.067,.12,.103,.055);
        return EncodeTimeStop(samples,.72,3700);
    }

    private static byte[] MakeTimeStopStone()
    {
        const double duration=.10;
        var samples=new double[(int)(duration*SampleRate)];var random=new Random(26351);
        double low=0,mid=0,phase=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;
            low+=.07*(n-low);mid+=.42*(n-mid);phase+=Math.Tau*(96+280*Math.Exp(-t*74))/SampleRate;
            double knock=(Math.Sin(phase)*.52+(mid-low)*1.28)*Math.Exp(-t*48);
            double travel=(mid-low)*.23*Math.Sin(Math.PI*t/duration)*Math.Exp(-t*12);
            double grit=low*Math.Exp(-t*26)*.6;
            samples[i]=(knock+travel+grit)*Math.Min(1,t/.0023)*Math.Min(1,(duration-t)/.02);
        }
        return EncodeTimeStop(samples,.48,3100);
    }

    private static double TimeStopTick(double t,double duration)
    {
        if(t<=0||t>=duration)return 0;
        double ring=Math.Sin(Math.Tau*1110*t)+Math.Sin(Math.Tau*1743*t)*.31;
        double wood=Math.Sin(Math.Tau*410*t)*.46;
        return (ring*.56+wood)*Math.Exp(-t*125)*Math.Min(1,t/.0015)*Math.Min(1,(duration-t)/.009);
    }
    private static void TimeStopRoom(double[] samples,double firstDelay,double firstGain,double secondDelay,double secondGain)
    {
        int first=(int)(firstDelay*SampleRate),second=(int)(secondDelay*SampleRate);
        // Feed-forward taps use dry values; bounded tails cannot build feedback.
        for(int i=samples.Length-1;i>=first;i--)
            samples[i]+=samples[i-first]*firstGain+(i>=second?samples[i-second]*secondGain:0);
    }
    private static byte[] EncodeTimeStop(double[] samples,double gain,double cutoff)
    {
        var pcm=new byte[samples.Length*2];
        for(int i=0;i<samples.Length;i++)
        {
            short sample=(short)(Math.Tanh(samples[i]*1.5)*gain*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,cutoff);
    }
}
