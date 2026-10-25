namespace CookieRunVS2026;

// Immutable, fixed-rate sound data. All interpolation occurs during preparation.
internal sealed class PreparedSound
{
    internal string Key { get; }
    internal float[] Samples { get; }
    internal PreparedSound(string key,float[] samples){Key=key;Samples=samples;}
    internal static PreparedSound FromMono22050(string key,byte[] pcm)
    {
        int count=pcm.Length/2;var samples=new float[count*2];
        // Half-sample, Hann-windowed sinc. Even samples retain the exact source;
        // 32 neighbouring taps reconstruct odd samples without imaging or rate
        // tricks. Padding is zero, so exact authored silence stays silent.
        ReadOnlySpan<double> kernel=HalfSampleKernel;
        for(int i=0;i<count;i++)
        {
            samples[i*2]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
            double value=0;
            for(int tap=0;tap<kernel.Length;tap++)
            {
                int at=i+tap-15;
                if((uint)at<(uint)count)value+=(short)(pcm[at*2]|pcm[at*2+1]<<8)*kernel[tap];
            }
            samples[i*2+1]=(float)(value/32768.0);
        }
        return new PreparedSound(key,samples);
    }
    private static readonly double[] HalfSampleKernel=MakeKernel();
    private static double[] MakeKernel()
    {
        var kernel=new double[32];double sum=0;
        for(int i=0;i<kernel.Length;i++)
        {
            double distance=i-15.5;
            kernel[i]=Math.Sin(Math.PI*distance)/(Math.PI*distance)*(.5+.5*Math.Cos(Math.PI*distance/16));
            sum+=kernel[i];
        }
        for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
        return kernel;
    }
}

// A cancellation token is intentionally independent of the UI's voice list.
// The worker owns the cursor; a removed/priority-stolen cue fades for 5 ms.
internal sealed class SoftwareSoundVoice
{
    internal readonly PreparedSound Sound;
    internal readonly float Gain;
    internal int Cursor,StopFrames;
    private int stopRequested,completed;
    internal bool Completed=>Volatile.Read(ref completed)!=0;
    internal bool StopRequested=>Volatile.Read(ref stopRequested)!=0;
    internal SoftwareSoundVoice(PreparedSound sound,float gain){Sound=sound;Gain=gain;}
    internal void Stop()=>Volatile.Write(ref stopRequested,1);
    internal void Complete()=>Volatile.Write(ref completed,1);
}

// Device-independent, deterministic PCM renderer. Music is its own unchanged
// stem: only the effects bus uses lookahead/headroom. Never normalize the sum.
internal sealed class SoftwareAudioMixer
{
    internal const int SampleRate=44100,Channels=2,BlockFrames=441;
    internal const float EffectCeiling=.60f;
    private const int StopRampFrames=221,MaximumVoices=24;
    private readonly object gate=new();
    private readonly List<SoftwareSoundVoice> sounds=new(MaximumVoices);
    private readonly float[] effects=new float[BlockFrames],delayedEffects=new float[BlockFrames];
    private MusicTrack? music,outgoing;
    private int fadeFrame,fadeLength=1;
    private bool paused;
    private float delayedPeak,effectGain=1;
    private long renderedFrames;
    private int acceptedSounds,droppedSounds;
    internal int MusicKey { get {lock(gate)return music?.Key??0;} }
    internal int MusicFrame { get {lock(gate)return music?.Cursor??0;} }
    internal float MusicGain { get {lock(gate)return music?.Gain??0;} }
    internal long RenderedFrames { get {lock(gate)return renderedFrames;} }
    internal int ActiveSounds { get {lock(gate)return sounds.Count(v=>!v.Completed&&!v.StopRequested);} }
    internal int AcceptedSounds { get {lock(gate)return acceptedSounds;} }
    internal int DroppedSounds { get {lock(gate)return droppedSounds;} }
    internal float EffectLimiterGain { get {lock(gate)return effectGain;} }
    internal SoftwareSoundVoice? Play(PreparedSound sound,float gain)
    {
        lock(gate)
        {
            if(paused)return null;
            // At most 12 live cues plus their short cancellation tails. A burst
            // queued before rendering cannot create an unbounded work backlog.
            if(sounds.Count>=MaximumVoices)
            {
                int replace=sounds.FindIndex(v=>v.StopRequested||v.Completed);
                if(replace<0){droppedSounds++;return null;}
                sounds[replace].Complete();sounds.RemoveAt(replace);
            }
            var voice=new SoftwareSoundVoice(sound,Math.Clamp(gain,0,1));
            sounds.Add(voice);acceptedSounds++;return voice;
        }
    }
    internal void SetMusic(int key,byte[] pcm,float gain,double crossfadeSeconds)
    {
        if(pcm.Length==0||pcm.Length%4!=0)throw new ArgumentException("Music must be 44100 Hz stereo 16-bit PCM.",nameof(pcm));
        lock(gate)
        {
            if(music?.Key==key)return;
            outgoing=music;music=new MusicTrack(key,pcm,Math.Clamp(gain,0,.30f));
            fadeLength=Math.Max(1,(int)(SampleRate*crossfadeSeconds));fadeFrame=0;
        }
    }
    internal void StopMusic(){lock(gate){music=outgoing=null;fadeFrame=0;}}
    internal void StopSounds()
    {
        lock(gate)
        {
            foreach(var voice in sounds)voice.Complete();sounds.Clear();
            Array.Clear(effects);Array.Clear(delayedEffects);delayedPeak=0;effectGain=1;
        }
    }
    internal void SetPaused(bool value){lock(gate)paused=value;}
    internal void Render(Span<short> destination,Span<float> musicStem=default,Span<float> effectStem=default)
    {
        if(destination.Length!=BlockFrames*Channels)throw new ArgumentException("Render one 10 ms stereo block.",nameof(destination));
        if(!musicStem.IsEmpty&&musicStem.Length!=destination.Length)throw new ArgumentException("Stem length must match output.",nameof(musicStem));
        if(!effectStem.IsEmpty&&effectStem.Length!=destination.Length)throw new ArgumentException("Stem length must match output.",nameof(effectStem));
        lock(gate)
        {
            if(paused){destination.Clear();musicStem.Clear();effectStem.Clear();return;}
            Array.Clear(effects);
            for(int v=sounds.Count-1;v>=0;v--)
            {
                var voice=sounds[v];var data=voice.Sound.Samples;
                for(int frame=0;frame<BlockFrames&&voice.Cursor<data.Length;frame++)
                {
                    if(voice.StopRequested&&voice.StopFrames==0)voice.StopFrames=StopRampFrames;
                    float stopGain=voice.StopFrames>0?voice.StopFrames/(float)StopRampFrames:1;
                    effects[frame]+=data[voice.Cursor++]*voice.Gain*stopGain;
                    if(voice.StopFrames>0&&--voice.StopFrames==0){voice.Complete();break;}
                }
                if(voice.Cursor>=data.Length)voice.Complete();
                if(voice.Completed)sounds.RemoveAt(v);
            }
            float nextPeak=0;
            for(int frame=0;frame<BlockFrames;frame++)nextPeak=Math.Max(nextPeak,Math.Abs(effects[frame]));
            // One block of SFX-only lookahead anticipates transients. The gain at
            // BOTH ends is bounded for the delayed block, allowing a smooth ramp
            // without clipping or ever touching the music gain. Release 120 ms.
            float peak=Math.Max(delayedPeak,nextPeak);
            float required=peak>EffectCeiling?EffectCeiling/peak:1;
            float endGain=required<effectGain?required:Math.Min(required,effectGain+(1-effectGain)*.079955585f);
            for(int frame=0;frame<BlockFrames;frame++)
            {
                float gain=effectGain+(endGain-effectGain)*(frame+1)/BlockFrames;
                float effect=delayedEffects[frame]*gain;
                float left=0,right=0;
                if(music!=null)
                {
                    float incoming=fadeFrame>=fadeLength?1:fadeFrame/(float)fadeLength;
                    // Complementary equal-amplitude gains reserve stable peak
                    // headroom when different rounds crossfade. No action fades.
                    music.Read(out float ml,out float mr);left=ml*music.Gain*incoming;right=mr*music.Gain*incoming;
                    if(outgoing!=null)
                    {
                        outgoing.Read(out ml,out mr);left+=ml*outgoing.Gain*(1-incoming);right+=mr*outgoing.Gain*(1-incoming);
                    }
                    if(fadeFrame<fadeLength)fadeFrame++;
                    else outgoing=null;
                }
                int index=frame*2;
                if(!musicStem.IsEmpty){musicStem[index]=left;musicStem[index+1]=right;}
                if(!effectStem.IsEmpty){effectStem[index]=effect;effectStem[index+1]=effect;}
                // .30 music + .60 effects is bounded below PCM full scale.
                destination[index]=(short)Math.Round(Math.Clamp(left+effect,-1,1)*32767);
                destination[index+1]=(short)Math.Round(Math.Clamp(right+effect,-1,1)*32767);
            }
            effectGain=endGain;delayedPeak=nextPeak;
            Array.Copy(effects,delayedEffects,BlockFrames);
            renderedFrames+=BlockFrames;
        }
    }
    private sealed class MusicTrack
    {
        internal readonly int Key;
        internal readonly byte[] Pcm;
        internal readonly float Gain;
        internal int Cursor;
        internal MusicTrack(int key,byte[] pcm,float gain){Key=key;Pcm=pcm;Gain=gain;}
        internal void Read(out float left,out float right)
        {
            int at=Cursor*4;
            left=(short)(Pcm[at]|Pcm[at+1]<<8)/32768f;
            right=(short)(Pcm[at+2]|Pcm[at+3]<<8)/32768f;
            if(++Cursor>=Pcm.Length/4)Cursor=0;
        }
    }
}
