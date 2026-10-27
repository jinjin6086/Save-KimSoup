namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private string dialogueVoiceIdentity="";
    private int dialogueVoiceVisible,dialogueVoiceSyllable;
    private float dialogueVoiceWait;

    private (string Speaker,string Text,int Visible)? CurrentDialogueVoice
    {
        get
        {
            if(state==GameState.StoryScene&&JourneyHasDialogue)
            {var line=JourneyLines[journeyLine];return(line.Speaker,line.Text,JourneyVisible);}
            if(state==GameState.Round&&bossBriefingActive&&bossTalkPhase==1&&!bossTalkHelp)
            {var line=BossConversationLines[bossTalkLine];return(line.Boss?BriefingBossName:"나",line.Text,BossConversationVisible);}
            if(state==GameState.Ending&&!endingFinal&&endingPendingScene<0)
                return(EndingSpeakers[endingScene][endingLine],EndingWords[endingScene][endingLine],EndingVisibleCharacters);
            if(MelodyLastStandActive&&lastStandPhase==3)
            {
                float t=lastStandClock/LastStandMemoryFrames;string text=LastStandMemoryLines[LastStandMemoryIndex];
                return("나",text,Math.Clamp((int)((t*LastStandMemoryLines.Length%1)*LastStandMemoryFrames/LastStandMemoryLines.Length/2.2f),0,text.Length));
            }
            if(MelodyLastStandActive&&lastStandPhase==10&&lastStandClock>=28)
            {const string text="내가 그딴 돼지보다 내가 훨씬 낫잖아";return("강로디언",text,Math.Clamp((int)((lastStandClock-28)/3),0,text.Length));}
            return null;
        }
    }
    private static int DialogueVoiceKind(string speaker)=>speaker.Contains("이상진")?1:speaker.Contains("파괴옥")?2:speaker.Contains("강로디언")?3:speaker=="김스프"?4:0;
    private void UpdateDialogueVoice(float step)
    {
        dialogueVoiceWait=Math.Max(0,dialogueVoiceWait-step);
        var line=CurrentDialogueVoice;
        if(line==null)
        {
            if(dialogueVoiceIdentity.Length>0)audio.StopDialogueVoice();
            dialogueVoiceIdentity="";dialogueVoiceVisible=0;return;
        }
        var current=line.Value;string identity=current.Speaker+"\n"+current.Text;
        if(identity!=dialogueVoiceIdentity)
        {
            audio.StopDialogueVoice();dialogueVoiceIdentity=identity;dialogueVoiceVisible=0;
            dialogueVoiceWait=0;dialogueVoiceSyllable=0;
        }
        int visible=Math.Clamp(current.Visible,0,current.Text.Length);
        int added=visible-dialogueVoiceVisible;
        if(added>0&&added<=5&&dialogueVoiceWait<=0&&visible>0&&char.IsLetterOrDigit(current.Text[visible-1]))
        {
            audio.PlayDialogueVoice(DialogueVoiceKind(current.Speaker),dialogueVoiceSyllable++%3);
            dialogueVoiceWait=5.5f;
        }
        // Revealing a whole sentence is silent; it never queues a burst of
        // backlog beeps. The reveal count, not render frequency, drives speech.
        dialogueVoiceVisible=visible;
    }
}

internal sealed partial class GameAudio
{
    private void PrepareDialogueSounds()
    {
        for(int person=0;person<5;person++)for(int variant=0;variant<3;variant++)
            clips[$"talk{person}{variant}"]=MakeDialogueSyllable(person,variant);
    }
    internal void PlayDialogueVoice(int person,int variant)
    {
        StopDialogueVoice();Play($"talk{Math.Clamp(person,0,4)}{Math.Clamp(variant,0,2)}");
    }
    internal void StopDialogueVoice()
    {
        for(int i=voices.Count-1;i>=0;i--)
            if(voices[i].Key.StartsWith("talk",StringComparison.Ordinal)){voices[i].Dispose();voices.RemoveAt(i);}
    }
    private static byte[] MakeDialogueSyllable(int person,int variant)
    {
        const double duration=.061;int n=(int)(duration*SampleRate);var pcm=new byte[n*2];
        double fundamental=new[]{122d,172,83,248,208}[person]*(1+(variant-1)*.035);
        for(int i=0;i<n;i++)
        {
            double t=i/(double)SampleRate,phase=Math.Tau*fundamental*t,value=0;
            for(int harmonic=1;harmonic<=8;harmonic++)
            {
                double f=harmonic*fundamental;
                double formant=.24+.66*Math.Exp(-Math.Pow((f-(person==2?510:person==3?990:760))/370,2));
                value+=Math.Sin(phase*harmonic+.08*Math.Sin(phase*.7))*formant/Math.Pow(harmonic,1.5);
            }
            double envelope=Math.Pow(Math.Sin(Math.PI*t/duration),1.4);
            // Rounded, band-limited glottal tones; no square wave, white-noise
            // layer, pitch-shifted combat SFX or per-device volume writes.
            short sample=(short)(Math.Tanh(value*1.2)*envelope*.17*short.MaxValue);
            pcm[i*2]=(byte)sample;pcm[i*2+1]=(byte)(sample>>8);
        }
        return pcm;
    }
}
