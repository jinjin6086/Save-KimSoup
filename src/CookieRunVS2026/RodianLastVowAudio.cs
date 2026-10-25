namespace CookieRunVS2026;

internal sealed partial class GameAudio
{
    // These original pressure and breath textures support the written dialogue;
    // they are not sampled character speech or an actor impersonation.
    private static byte[] MakeRodianLastVowSound(bool rage)
    {
        double duration=rage?1.2:2.5;int count=(int)(duration*SampleRate);var dry=new double[count];
        var rng=new Random(rage?77413:77417);double low=0,body=0,air=0,phase=0;
        for(int i=0;i<count;i++)
        {
            double t=i/(double)SampleRate,p=t/duration,n=rng.NextDouble()*2-1;
            low+=.012*(n-low);body+=.095*(n-body);air+=.28*(n-air);
            phase+=Math.Tau*(rage?38+138*Math.Exp(-t*15):45-14*p)/SampleRate;
            double value;
            if(rage)
            {
                // The outburst begins when the red line appears, then winds
                // back up into the charge rather than dwindling beforehand.
                double impact=Math.Exp(-t*4.0),strain=.80+.14*Math.Sin(t*57)+.06*Math.Sin(t*89);
                value=(Math.Sin(phase)*.55+low*6.0+(body-low)*1.65)*impact*strain;
                value+=(air-body)*.69*Math.Exp(-t*26);
                value+=Math.Sin(Math.Tau*(379*t+29*t*t)+.45*Math.Sin(t*67))*.058*Math.Exp(-t*4.8);
                double intake=SmoothAudio((p-.30)/.60);
                value+=(low*2.1+(body-low)*.75+Math.Sin(Math.Tau*(51*t+43*t*t))*.092)*intake;
            }
            else
            {
                double drop=Math.Exp(-t*3.4),breath=SmoothAudio((p-.08)/.18)*(1-SmoothAudio((p-.58)/.40));
                value=(Math.Sin(phase)*.19+low*2.1)*drop+(body-low)*.29*breath;
                value+=(Math.Sin(Math.Tau*92.5*t)*.031+Math.Sin(Math.Tau*138.6*t)*.018)*Math.Sin(p*Math.PI);
                value+=low*.49*breath;
            }
            dry[i]=value*SmoothAudio(t/(rage?.0035:.018))*(1-SmoothAudio((t-(duration-.13))/.13));
        }
        return RenderCinematicStem(dry,rage?.57:.36,rage?3600:1900,true,rage?.18:.24);
    }
}
