using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Reserve the end of the final syllable for the laser. A nearby note may
    // still hit, but cannot spend the player's reflection window before it.
    private bool LaserPriorityWindow=>round==4&&!laserActive&&MelodySequenceActive&&melodyStep==3&&melodyWordTimer>0&&melodyWordTimer<=12;
    private float slideAudioClock;
    private bool slideSoundActive;
    private void UpdateSlideSound(float step)
    {
        bool active=sliding&&onGround&&state==GameState.Round&&!gamePaused&&!PhaseIntroActive&&!bossBriefingActive&&sceneFadeTimer<=0;
        if(!active)
        {
            if(slideSoundActive){audio.StopSliding();if(state==GameState.Round&&!gamePaused&&onGround)audio.Play("slideout");}
            slideSoundActive=false;slideAudioClock=0;return;
        }
        slideAudioClock-=step;
        if(!slideSoundActive){audio.Play("slidein");slideAudioClock=18;}
        else if(slideAudioClock<=0){if(audio.ActiveVoices<6)audio.Play("slidebody");slideAudioClock=70;}
        slideSoundActive=true;
    }
    private void DrawLaserCharge(Graphics g)
    {
        if(round!=4||!MelodySequenceActive||melodyStep!=3||laserActive)return;
        float duration=Math.Max(1,melodyInterval),t=Math.Clamp(1-melodyWordTimer/duration,0,1);
        float cx=bossX+65,cy=bossY+65;
        Color c=menheraMode?Color.FromArgb(255,75,119):Color.FromArgb(255,153,213);
        DrawKickLight(g,new(cx-16-t*30,cy-12-t*19,32+t*60,24+t*38),t*.8f);
        for(int i=0;i<(reducedEffects?4:17);i++)
        {
            float p=(animationClock*.034f+i/17f)%1,a=i*2.4f+animationClock*.01f,r=10+(1-p)*(52+t*30);
            float x=cx+MathF.Cos(a)*r,y=cy+MathF.Sin(a)*r*.65f;
            g.DrawLine(ParticlePen(c,(int)(170*t*p),1),x,y,x+MathF.Cos(a)*6,y+MathF.Sin(a)*4);
        }
        if(melodyWordTimer<=12)
        {
            using var trace=new Pen(Color.FromArgb((int)(110+t*90),c),1.3f){DashStyle=DashStyle.Dash};
            g.DrawLine(trace,cx,cy,px+26,py+32);
            Label(g,"돼! 에 맞춰 발차기",new(px-67,py-65,190,24),12,Paper);
        }
    }
    private void DrawGrandLaser(Graphics g,bool reflected)
    {
        float x=reflected?counterOriginX:bossX+65,y=reflected?counterOriginY:bossY+65;
        float dx=reflected?counterTargetX-x:MathF.Cos(LiveLaserAngle)*1160,dy=reflected?counterTargetY-y:MathF.Sin(LiveLaserAngle)*1160;
        float age=laserTimer,attack=Math.Clamp((age+1)/3,0,1);
        float release=reflected?Math.Clamp((21-age)/6,0,1):Math.Clamp((58-age)/16,0,1);
        float energy=attack*release,width=(reflected?72:menheraMode?84:76)/3f;
        DrawApotheosisBeam(g,x,y,x+dx,y+dy,width,energy,reflected);
        if(reflected)DrawApotheosisCollision(g,counterTargetX,counterTargetY,energy*.72f);
    }
}

internal sealed partial class GameAudio
{
    public void StopSliding(){for(int i=voices.Count-1;i>=0;i--)if(voices[i].Key.StartsWith("slide",StringComparison.Ordinal)){voices[i].Dispose();voices.RemoveAt(i);}}
    private static byte[] MakeGrandBeam(double duration,bool charge,bool counter=false)
    {
        int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(charge?734:counter?738:736);
        double low=0,mid=0,phase=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,p=t/duration,n=random.NextDouble()*2-1;low+=.035*(n-low);mid+=.23*(n-mid);
            double f=charge?65+180*p*p:counter?112+170*Math.Exp(-t*16):57+91*Math.Exp(-t*20);
            phase+=Math.Tau*f/SampleRate;
            double tone=Math.Sin(phase)*.46+Math.Sin(phase*1.013)*.24+Math.Sin(phase*2.01)*.13+Math.Sin(phase*3)*.08;
            double crack=(mid-low)*.52*(charge?.12+p*.35:Math.Exp(-t*10)+.28);
            double env=charge?Math.Min(1,t/.035)*(.15+p*.75):Math.Min(1,t/.006)*Math.Exp(-t*1.7);
            env*=Math.Min(1,(duration-t)/.07);
            double value=Math.Tanh((tone+crack+low*.8)*1.6)*env*(charge?.25:counter?.43:.40);
            short sample=(short)(value*short.MaxValue);pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return RoundPlayerClip(pcm,charge?2200:3000);
    }
    private static byte[] MakeSlide(bool entry)
    {
        double duration=entry?.48:1.4;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(entry?1951:1957);double low=0,mid=0,fine=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,n=random.NextDouble()*2-1;low+=.033*(n-low);mid+=.14*(n-mid);fine+=.31*(n-fine);
            double cloth=(mid-low)*1.25+low*.66+(fine-mid)*.17;
            double grain=.84+.08*Math.Sin(t*43)+.05*Math.Sin(t*71+1.7)+.03*Math.Sin(t*117);
            double env=entry?Math.Min(1,t/.018)*Math.Pow(Math.Max(0,1-t/duration),.7):Math.Min(1,t/.14)*Math.Min(1,(duration-t)/.19);
            double sole=entry?Math.Sin(Math.Tau*81*t)*Math.Exp(-t*31)*.27:0;
            double signal=Math.Tanh((cloth*grain+sole)*1.65)*env*(entry?.23:.105);
            short s=(short)(signal*short.MaxValue);pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);
        }
        return RoundPlayerClip(pcm,1550);
    }
    private static byte[] MakeSlideRelease()
    {
        const double duration=.19;var pcm=new byte[(int)(duration*SampleRate)*2];var random=new Random(1961);double low=0,mid=0;
        for(int i=0;i<pcm.Length/2;i++)
        {
            double t=i/(double)SampleRate,n=random.NextDouble()*2-1;low+=.04*(n-low);mid+=.16*(n-mid);
            double env=Math.Min(1,t/.007)*Math.Pow(1-t/duration,2);
            short v=(short)(Math.Tanh(((mid-low)*1.1+low*.7)*1.5)*env*.095*short.MaxValue);pcm[i*2]=(byte)v;pcm[i*2+1]=(byte)(v>>8);
        }
        return RoundPlayerClip(pcm,1500);
    }
    private static byte[] MakeInhale()
    {
        const double duration=.45;int count=(int)(duration*SampleRate);var pcm=new byte[count*2];var random=new Random(759);double low=0,mid=0;
        for(int i=0;i<count;i++)
        {
            double t=(double)i/SampleRate,p=t/duration,n=random.NextDouble()*2-1;low+=.045*(n-low);mid+=.14*(n-mid);
            short s=(short)(Math.Tanh((mid-low)*2.1+low*.4)*Math.Pow(Math.Sin(Math.PI*p),1.6)*.18*short.MaxValue);pcm[i*2]=(byte)s;pcm[i*2+1]=(byte)(s>>8);
        }
        return pcm;
    }
}
