namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    public void PlayEchoFlight()
    {
        Collect();
        // Echo and precision can trigger together. A single shared electric
        // passage stays readable and cannot consume two of the eight voices.
        if(!voices.Any(v=>v.Key=="echoflight"))Play("echoflight");
    }
    public void StopEchoFlight()
    {
        for(int i=voices.Count-1;i>=0;i--)if(voices[i].Key=="echoflight")
        {voices[i].Dispose();voices.RemoveAt(i);}
    }
    private static byte[] MakeEchoStart()
    {
        // A rounded, clearly pitched "ping" announces the six-second aura.
        // The inharmonic upper partials die first; the lower ring leaves a
        // spectral tail instead of an arcade alarm or a piercing sine beep.
        const double duration=.82;
        int count=(int)(duration*SampleRate);var dry=new double[count];var random=new Random(27531);
        double phase=0,low=0,mid=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;
            phase+=Math.Tau*(1235+155*Math.Exp(-t*65))/SampleRate;
            low+=.04*(n-low);mid+=.21*(n-mid);
            double strike=(Math.Sin(phase)*.62+Math.Sin(phase*2.414)*.17*Math.Exp(-t*12)
                +Math.Sin(phase*3.73)*.055*Math.Exp(-t*26))*Math.Exp(-t*7.5);
            double swell=t>.045?Math.Sin(Math.Min(1,(t-.045)/.42)*Math.PI)*Math.Exp(-Math.Max(0,t-.18)*5):0;
            double wake=(mid-low)*1.1*swell+Math.Sin(t*Math.Tau*176)*.035*swell;
            dry[i]=(strike+wake)*Math.Min(1,t/.004)*Math.Min(1,(duration-t)/.08);
        }
        var pcm=new byte[count*2];int d1=(int)(.107*SampleRate),d2=(int)(.219*SampleRate);
        for(int i=0;i<count;i++)
        {
            double value=dry[i]+(i>=d1?dry[i-d1]*.25:0)+(i>=d2?dry[i-d2]*.11:0);
            double edge=Math.Min(1,(count-1-i)/(SampleRate*.045));
            short sample=(short)(Math.Tanh(value*1.35)*edge*.43*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,3900);
    }
    private static byte[] MakeEchoFlight()
    {
        // The launch keeps the attack's straight 28-frame travel readable.
        // Its pitch falls through the ringing aura without repeating the ping.
        const double duration=28.0/60;
        int count=(int)(duration*SampleRate);var dry=new double[count];var random=new Random(8137);
        double low=0,mid=0,phase=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=random.NextDouble()*2-1;
            low+=.047*(n-low);mid+=.20*(n-mid);phase+=Math.Tau*(710-370*p)/SampleRate;
            double airy=(mid-low)*1.8+Math.Sin(phase+Math.Sin(phase*.501)*.4)*.18;
            double env=Math.Min(1,t/.009)*Math.Pow(Math.Max(0,1-p),1.35)*(.85+.15*Math.Sin(t*48));
            dry[i]=airy*env;
        }
        var pcm=new byte[count*2];
        int d1=(int)(.061*SampleRate),d2=(int)(.128*SampleRate),d3=(int)(.213*SampleRate);
        for(int i=0;i<count;i++)
        {
            double value=dry[i]+(i>=d1?dry[i-d1]*.44:0)+(i>=d2?dry[i-d2]*.24:0)+(i>=d3?dry[i-d3]*.12:0);
            double edge=Math.Min(1,(count-1-i)/(SampleRate*.028));
            short sample=(short)(Math.Tanh(value*1.7)*edge*.36*short.MaxValue);pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,2800);
    }
    private static byte[] MakeEchoImpact()
    {
        const double duration=.24;
        var pcm=new byte[(int)(duration*SampleRate)*2];var random=new Random(1889);
        double low=0,mid=0,high=0,phase=0;
        for(int i=0;i<pcm.Length/2;i++)
        {
            double t=(double)i/SampleRate,n=random.NextDouble()*2-1;
            low+=.045*(n-low);mid+=.26*(n-mid);high+=.55*(n-high);
            phase+=Math.Tau*(112+140*Math.Exp(-t*38))/SampleRate;
            double boot=Math.Sin(phase)*Math.Exp(-t*29)*.54;
            double snap=(high-mid)*Math.Exp(-t*57)*1.75;
            double leather=(mid-low)*Math.Exp(-t*30)*.92;
            double electric=Math.Sin(phase*4.07)*Math.Exp(-t*43)*.14;
            double env=Math.Min(1,t/.0018)*Math.Min(1,(duration-t)/.024);
            short s=(short)(Math.Tanh((boot+snap+leather+electric)*1.55)*env*.39*short.MaxValue);
            pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);
        }
        // Higher contact transients and less sub-bass than the absorption cannon.
        return RoundPlayerClip(pcm,3600);
    }
}
