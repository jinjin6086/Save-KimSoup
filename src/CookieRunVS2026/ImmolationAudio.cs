namespace CookieRunVS2026;
internal sealed partial class GameAudio
{
    private static byte[] MakeImmolationIgnition()
    {
        const double duration=1.18;int count=(int)(duration*SampleRate);var dry=new double[count];var rng=new Random(810631);double low=0,mid=0,air=0,phase=0,crackle=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,n=rng.NextDouble()*2-1;
            low+=.026*(n-low);mid+=.16*(n-mid);air+=.43*(n-air);
            phase+=Math.Tau*(36+95*Math.Exp(-t*11))/SampleRate;
            if(rng.NextDouble()<.002)crackle=.45+rng.NextDouble()*.65;crackle*=.90;
            double whoomp=(Math.Sin(phase)*.42+low*4.9)*Math.Exp(-t*6.2);
            double swell=Math.Pow(Math.Sin(Math.Min(1,t/.28)*Math.PI*.5),1.4)*Math.Exp(-t*2.8);
            double flames=((mid-low)*1.35+(air-mid)*.34+crackle*n*.55)*swell;
            double tearing=(air-low)*Math.Exp(-t*15)*.65;
            dry[i]=(whoomp+flames+tearing)*Math.Min(1,t/.004);
        }
        var pcm=new byte[count*2];int d=(int)(SampleRate*.053);
        for(int i=0;i<count;i++)
        {
            double v=dry[i];if(i>=d)v+=dry[i-d]*.17;if(i>=d*3)v+=dry[i-d*3]*.07;
            double t=i/(double)SampleRate,fade=1-SmoothAudio((t-.91)/.27);
            short s=(short)(Math.Tanh(v*2.0)*.61*fade*short.MaxValue);pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);
        }
        return RoundPlayerClip(pcm,3300);
    }
    private static byte[] MakeIndependentBossBurst(bool infernal)
    {
        byte[] original=MakeFinaleAftershock();int count=(int)(SampleRate*2.65);var pcm=new byte[count*2];
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,v=(short)(original[i*2]|original[i*2+1]<<8);
            v*=infernal?1:.82;double fade=1-SmoothAudio((t-2.22)/.43);
            short s=(short)(v*fade);pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);
        }
        return RoundPlayerClip(pcm,infernal?3800:2500);
    }
}
