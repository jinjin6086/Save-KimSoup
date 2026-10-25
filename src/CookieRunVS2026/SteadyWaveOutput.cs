using System.Runtime.InteropServices;

namespace CookieRunVS2026;

// Exactly one device stream for the complete game. The renderer runs independently
// of WinForms, so a render/shader/dialogue stall cannot starve musical playback.
internal sealed class SteadyWaveOutput : IDisposable
{
    private const int BufferCount=4,HeaderDone=1,CallbackEvent=0x50000;
    private readonly SoftwareAudioMixer mixer;
    private readonly AutoResetEvent changed=new(false);
    private readonly Thread worker;
    private readonly NativeBlock[] blocks=new NativeBlock[BufferCount];
    private readonly short[] rendered=new short[SoftwareAudioMixer.BlockFrames*2];
    private IntPtr handle;
    private int stopping,disposed,failed,queued,underruns,submittedBlocks;
    private long maximumRenderMicroseconds;
    private uint initialNativeVolume;
    internal bool Failed=>Volatile.Read(ref failed)!=0;
    internal bool Ready=>Volatile.Read(ref handle)!=IntPtr.Zero;
    internal IntPtr NativeHandle=>Volatile.Read(ref handle);
    internal int QueuedBlocks=>Volatile.Read(ref queued);
    internal int Underruns=>Volatile.Read(ref underruns);
    internal int SubmittedBlocks=>Volatile.Read(ref submittedBlocks);
    internal long MaximumRenderMicroseconds=>Interlocked.Read(ref maximumRenderMicroseconds);
    internal uint InitialNativeVolume=>initialNativeVolume;
    internal bool WorkerAlive=>worker.IsAlive;
    internal int NativeVolumeWrites=>0;
    internal SteadyWaveOutput(SoftwareAudioMixer mixer)
    {
        this.mixer=mixer;
        worker=new Thread(Run){IsBackground=true,Name="Game PCM output",Priority=ThreadPriority.AboveNormal};
        worker.Start();
    }
    private void Run()
    {
        uint headerSize=(uint)Marshal.SizeOf<WaveHeader>();bool began=false;
        try
        {
            var format=new WaveFormat{FormatTag=1,Channels=2,SamplesPerSec=44100,AvgBytesPerSec=176400,BlockAlign=4,BitsPerSample=16};
            if(waveOutOpen(out var opened,uint.MaxValue,ref format,changed.SafeWaitHandle.DangerousGetHandle(),IntPtr.Zero,CallbackEvent)!=0)
            {Volatile.Write(ref failed,1);return;}
            Volatile.Write(ref handle,opened);
            // Never write waveOutSetVolume: Windows preserves the user's mixer
            // setting. Music/SFX gains live only in the software PCM renderer.
            waveOutGetVolume(opened,out initialNativeVolume);
            waveOutPause(opened);
            for(int i=0;i<blocks.Length;i++)
            {
                var block=new NativeBlock(rendered.Length*2,headerSize);blocks[i]=block;
                if(waveOutPrepareHeader(opened,block.Header,headerSize)!=0){Volatile.Write(ref failed,1);return;}
                block.Prepared=true;
                if(!Submit(block,opened,headerSize))return;
            }
            waveOutRestart(opened);began=true;
            while(Volatile.Read(ref stopping)==0)
            {
                // WOM_DONE signals buffer completion. The bounded 8 ms fallback
                // also handles drivers which coalesce or omit callback signals.
                changed.WaitOne(8);
                int done=0;
                foreach(var block in blocks)
                    if(block!=null&&(Marshal.ReadInt32(block.Header,WaveHeaderFlagsOffset)&HeaderDone)!=0)done++;
                if(done==BufferCount&&began)Interlocked.Increment(ref underruns);
                Volatile.Write(ref queued,BufferCount-done);
                foreach(var block in blocks)
                {
                    if(Volatile.Read(ref stopping)!=0)break;
                    if(block!=null&&(Marshal.ReadInt32(block.Header,WaveHeaderFlagsOffset)&HeaderDone)!=0)
                        if(!Submit(block,opened,headerSize))return;
                }
            }
        }
        catch(Exception exception) when(exception is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        {Volatile.Write(ref failed,1);}
        finally
        {
            var closing=Interlocked.Exchange(ref handle,IntPtr.Zero);
            if(closing!=IntPtr.Zero)
            {
                waveOutReset(closing);
                foreach(var block in blocks)
                {
                    if(block==null)continue;
                    if(block.Prepared)waveOutUnprepareHeader(closing,block.Header,headerSize);
                    block.Dispose();
                }
                waveOutClose(closing);
            }
            Volatile.Write(ref queued,0);
            changed.Dispose();
        }
    }
    private bool Submit(NativeBlock block,IntPtr output,uint headerSize)
    {
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        mixer.Render(rendered);
        long elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1_000_000/System.Diagnostics.Stopwatch.Frequency;
        if(elapsed>maximumRenderMicroseconds)Interlocked.Exchange(ref maximumRenderMicroseconds,elapsed);
        Marshal.Copy(rendered,0,block.Data,rendered.Length);
        if(waveOutWrite(output,block.Header,headerSize)!=0){Volatile.Write(ref failed,1);return false;}
        Interlocked.Increment(ref submittedBlocks);Interlocked.Increment(ref queued);return true;
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        Volatile.Write(ref stopping,1);
        try{changed.Set();}catch(ObjectDisposedException){}
        // No UI lock is held while joining; reset/close belongs to the output
        // thread, so buffers can never be freed under a driver callback.
        if(Thread.CurrentThread!=worker)worker.Join();
    }
    private static readonly int WaveHeaderFlagsOffset=(int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));
    private sealed class NativeBlock : IDisposable
    {
        internal readonly IntPtr Data,Header;
        internal bool Prepared;
        internal NativeBlock(int bytes,uint headerSize)
        {
            Data=Marshal.AllocHGlobal(bytes);Header=Marshal.AllocHGlobal((int)headerSize);
            Marshal.StructureToPtr(new WaveHeader{Data=Data,BufferLength=(uint)bytes},Header,false);
        }
        public void Dispose(){Marshal.FreeHGlobal(Header);Marshal.FreeHGlobal(Data);}
    }
    [StructLayout(LayoutKind.Sequential,Pack=2)]private struct WaveFormat
    {
        public ushort FormatTag,Channels;public uint SamplesPerSec,AvgBytesPerSec;public ushort BlockAlign,BitsPerSample,ExtraSize;
    }
    [StructLayout(LayoutKind.Sequential)]private struct WaveHeader
    {
        public IntPtr Data;public uint BufferLength,BytesRecorded;public IntPtr User;public uint Flags,Loops;public IntPtr Next,Reserved;
    }
    [DllImport("winmm.dll")]private static extern uint waveOutOpen(out IntPtr output,uint device,ref WaveFormat format,IntPtr callback,IntPtr instance,uint flags);
    [DllImport("winmm.dll")]private static extern uint waveOutGetVolume(IntPtr output,out uint volume);
    [DllImport("winmm.dll")]private static extern uint waveOutPrepareHeader(IntPtr output,IntPtr header,uint size);
    [DllImport("winmm.dll")]private static extern uint waveOutWrite(IntPtr output,IntPtr header,uint size);
    [DllImport("winmm.dll")]private static extern uint waveOutUnprepareHeader(IntPtr output,IntPtr header,uint size);
    [DllImport("winmm.dll")]private static extern uint waveOutReset(IntPtr output);
    [DllImport("winmm.dll")]private static extern uint waveOutClose(IntPtr output);
    [DllImport("winmm.dll")]private static extern uint waveOutPause(IntPtr output);
    [DllImport("winmm.dll")]private static extern uint waveOutRestart(IntPtr output);
}
