namespace CookieRunVS2026;
internal sealed partial class GameAudio
{
    public void StopCinderFire(){for(int i=voices.Count-1;i>=0;i--)if(voices[i].Key=="cinderloop"){voices[i].Dispose();voices.RemoveAt(i);}}
    private static byte[] MakeCinderSound(int kind)
    {
        if(kind==0)return MakeImmolationIgnition();
        double duration=kind switch{0=>.72,1=>1.2,2=>.23,3=>.32,_=>.65};int count=(int)(duration*SampleRate);
        var dry=new double[count];var rng=new Random(53171+kind*41);double low=0,mid=0,air=0,phase=0,crackle=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=rng.NextDouble()*2-1;
            low+=.025*(n-low);mid+=.15*(n-mid);air+=.42*(n-air);phase+=Math.Tau*(kind==2?170-100*p:kind==3?54+81*Math.Exp(-t*35):65+7*Math.Sin(t*13))/SampleRate;
            if(rng.NextDouble()<(kind==1?.0012:.0018))crackle=rng.NextDouble()*.7;
            crackle*=.94;
            double fire=low*1.5+(mid-low)*.65+(air-mid)*.16+crackle*n;
            double value=kind switch
            {
                0=>(fire+Math.Sin(phase)*.13)*Math.Sin(Math.PI*p)*(.7+.3*p),
                1=>fire*(.73+.15*Math.Sin(t*17)+.12*Math.Sin(t*31)),
                2=>((air-low)*1.1+Math.Sin(phase)*.16)*Math.Sin(p*Math.PI)*Math.Exp(-p*1.6),
                3=>(Math.Sin(phase)*.43+low*3.6+(air-mid)*.7)*Math.Exp(-t*16),
                _=>(fire*1.3+Math.Sin(phase)*.24)*Math.Sin(p*Math.PI)*Math.Exp(-p*1.5)
            };
            dry[i]=value;
        }
        var pcm=new byte[count*2];int delay=(int)(SampleRate*.047);
        for(int i=0;i<count;i++)
        {
            double value=dry[i];if(i>=delay)value+=dry[i-delay]*.13;
            double edge=Math.Min(1,i/(SampleRate*.004))*Math.Min(1,(count-1-i)/(SampleRate*(kind==1?.025:.04)));
            short sample=(short)(Math.Tanh(value*1.8)*(kind==1?.19:.42)*edge*short.MaxValue);pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,kind==1?2700:3600);
    }
}
