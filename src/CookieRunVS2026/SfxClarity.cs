namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    // Prepared once, before playback. The mixer receives finished PCM, never
    // a running denoiser or a generated buffer on the gameplay thread.
    private void ApplySfxClarity()
    {
        foreach(string key in clips.Keys.ToArray())
            clips[key]=ClarifySound(clips[key],key);
    }

    private static byte[] ClarifySound(byte[] pcm,string key)
    {
        int count=pcm.Length/2;if(count==0)return pcm;
        bool warning=key.StartsWith("lasercharge",StringComparison.Ordinal)
            ||key is "dashwarn" or "beat" or "beat0" or "beat1" or "beat2" or "final";
        bool ring=warning||key is "furiosostart" or "clashbreak" or "clashcharge" or "clashhero";
        bool air=key is "jump" or "kickswing" or "dodge" or "slidein" or "slidebody"
            or "slideout" or "inhale" or "exitpant" or "dash";
        bool sustained=key is "clashsustain" or "timestophold";
        double presence=ring?.98:air?.77:sustained?.60:.83;
        double edge=ring?.72:air?.40:sustained?.24:.43;
        double floor=ring?.0025:air?.0035:.0045;
        var silence=new bool[count];
        // Keep authored silent edit points exact, rather than letting filter
        // memory spill into the 12-frame QTE silence before the explosion.
        for(int i=0;i<count;)
        {
            if(pcm[i*2]!=0||pcm[i*2+1]!=0){i++;continue;}
            int start=i;while(i<count&&pcm[i*2]==0&&pcm[i*2+1]==0)i++;
            if(i-start>=48)Array.Fill(silence,true,start,i-start);
        }
        var result=new byte[pcm.Length];
        double dc=0,low=0,body=0,top=0,fast=0,slow=0,gain=1;
        double dcA=1-Math.Exp(-Math.Tau*19/SampleRate);
        double lowA=1-Math.Exp(-Math.Tau*145/SampleRate);
        double bodyA=1-Math.Exp(-Math.Tau*1550/SampleRate);
        double topA=1-Math.Exp(-Math.Tau*4200/SampleRate);
        double fastA=1-Math.Exp(-1/(SampleRate*.0018));
        double slowA=1-Math.Exp(-1/(SampleRate*.045));
        for(int i=0;i<count;i++)
        {
            if(silence[i]){dc=low=body=top=fast=slow=0;gain=1;continue;}
            double x=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768.0;
            dc+=dcA*(x-dc);x-=dc;
            low+=lowA*(x-low);body+=bodyA*(x-body);top+=topA*(x-top);
            fast+=fastA*(Math.Abs(x)-fast);slow+=slowA*(Math.Abs(x)-slow);
            double attack=Math.Clamp((fast-slow)/(slow+.013),0,1);
            // Broadband texture remains in the contact front. The held beam
            // and decay lose hiss, while their tonal body keeps its weight.
            double upper=presence+(.98-presence)*attack;
            double airGain=edge+(.87-edge)*attack;
            double value=low*.96+(body-low)+(top-body)*upper+(x-top)*airGain;
            double target=slow>=floor?1:.38+.62*Math.Pow(Math.Clamp(slow/floor,0,1),.65);
            gain+=(target>gain?.04:.0035)*(target-gain);
            value*=gain;
            // This is a safety knee, not peak normalization: quiet textures
            // and reverberation never get amplified back into foreground hiss.
            if(Math.Abs(value)>.82)value=Math.Sign(value)*(.82+.13*Math.Tanh((Math.Abs(value)-.82)/.13));
            double fade=Math.Min(1,i/(SampleRate*.001))*Math.Min(1,(count-1-i)/(SampleRate*.005));
            short sample=(short)Math.Round(Math.Clamp(value*fade,-.95,.95)*short.MaxValue);
            result[i*2]=(byte)sample;result[i*2+1]=(byte)(sample>>8);
        }
        return result;
    }
}
