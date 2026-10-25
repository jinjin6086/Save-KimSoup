using System.Diagnostics;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Official QTE renderer. Simulation remains in the existing controller;
    // neither GPU time nor FPS drives battle or input judgment.
    private bool gpuPrototypeEnabled=true, gpuPrototypeDrawing;
    private GpuVfxSurface? gpuPrototypeSurface;
    private GpuVfxDevice? gpuVfxDevice;
    private Bitmap? gpuPrototypeBase;
    private string gpuPrototypeFailure="";
    private double gpuPrototypeFrameMs;
    private int gpuPrototypeFrames;
    private Task<GpuVfxDevice>? gpuPrototypeWarmUp;
    private int gpuPrototypeUploadStage;
    private bool gpuPrototypeDisposed;
    private readonly float[] gpuPrototypeConstants=new float[64];
    private bool GpuPrototypeActive=>gpuPrototypeEnabled&&MelodyLastStandActive&&gpuPrototypeFailure.Length==0;
    private string GpuPrototypeBackend=>gpuVfxDevice?.BackendName??(gpuPrototypeFailure.Length>0?"GDI fallback":"GPU initializing");

    internal void StartGpuPrototypeReplay()
    {
        SetPaused(false);ReleaseInputs();practicePreset=12;StartPractice();Invalidate(true);
    }
    private bool HandleGpuPrototypeKey(Keys key)
    {
        if(key==Keys.F8)
        {
            gpuPrototypeEnabled=!gpuPrototypeEnabled;
            if(!gpuPrototypeEnabled&&gpuPrototypeSurface!=null)gpuPrototypeSurface.Visible=false;
            Invalidate(true);return true;
        }
        if(key==Keys.F9){StartGpuPrototypeReplay();return true;}
        return false;
    }
    private bool SyncGpuPrototypeSurface()
    {
        bool show=GpuPrototypeActive&&gpuVfxDevice!=null&&gpuPrototypeUploadStage==3;
        if(show)
        {
            EnsureGpuPrototypeSurface();
            if(!gpuPrototypeSurface!.Visible)
            {gpuPrototypeSurface.Visible=true;gpuPrototypeSurface.BringToFront();gpuPrototypeSurface.Invalidate();}
        }
        else if(gpuPrototypeSurface?.Visible==true)
        {gpuPrototypeSurface.Visible=false;Invalidate();}
        return show;
    }
    // An opaque child receives WM_PAINT instead of its covered parent. Its
    // lifetime must therefore follow simulation transitions, not parent paints.
    internal void RefreshGpuVfxLifecycle()
    {
        if(gpuPrototypeDisposed||IsDisposed)return;
        if(gpuPrototypeEnabled&&gpuPrototypeFailure.Length==0&&state==GameState.Round&&round==4)
            AdvanceGpuPrototypeWarmUp();
        if(SyncGpuPrototypeSurface())gpuPrototypeSurface!.Invalidate();
    }
    private void EnsureGpuPrototypeSurface()
    {
        if(gpuPrototypeSurface!=null)return;
        gpuPrototypeSurface=new GpuVfxSurface(this){Dock=DockStyle.Fill,TabStop=false,Visible=false};
        Controls.Add(gpuPrototypeSurface);
    }
    private void AdvanceGpuPrototypeWarmUp()
    {
        try
        {
            EnsureGpuPrototypeSurface();
            if(gpuVfxDevice==null)
            {
                if(gpuPrototypeWarmUp==null)
                {
                    // Shader compilation/device creation never blocks a QTE
                    // input tick. The worker does not touch any GDI bitmap.
                    nint handle=gpuPrototypeSurface!.Handle;
                    int width=Math.Max(1,ClientSize.Width),height=Math.Max(1,ClientSize.Height);
                    string shader=Path.Combine(AppContext.BaseDirectory,"Assets","GpuVfx.hlsl");
                    gpuPrototypeWarmUp=Task.Run(()=>
                    {
                        var device=new GpuVfxDevice(handle,width,height,shader);
                        try
                        {
                            // Drivers may defer native pipeline compilation to
                            // the first Draw. Prime that same shader while the
                            // child is hidden, using worker-owned pixels only.
                            using var primer=new Bitmap(1,1,PixelFormat.Format32bppArgb);
                            device.Render(primer,new float[64]);return device;
                        }
                        catch{device.Dispose();throw;}
                    });
                }
                if(!gpuPrototypeWarmUp.IsCompleted)return;
                gpuVfxDevice=gpuPrototypeWarmUp.GetAwaiter().GetResult();gpuPrototypeWarmUp=null;
                if(!gpuVfxDevice.IsHardware)throw new NotSupportedException("하드웨어 GPU 가속을 사용할 수 없어 기존 화면으로 전환합니다.");
            }
            // Upload immutable authored slices once, in separate warm-up ticks.
            // LockBits stays on the UI thread, away from concurrent GDI drawing.
            switch(gpuPrototypeUploadStage)
            {
                case 0:PrepareApotheosisPlasma();gpuVfxDevice.SetBeamTextures(apotheosisBlue,apotheosisPink);break;
                case 1:gpuVfxDevice.SetCollisionTextures(collisionVolume);break;
                case 2:gpuVfxDevice.SetDetonationTextures(detonationVolume);break;
                default:return;
            }
            gpuPrototypeUploadStage++;
        }
        catch(Exception error){FailGpuPrototype(error);}
    }
    private void FailGpuPrototype(Exception error)
    {
        gpuPrototypeFailure=error.Message;
        gpuVfxDevice?.Dispose();gpuVfxDevice=null;gpuPrototypeUploadStage=0;
        if(gpuPrototypeSurface!=null)gpuPrototypeSurface.Visible=false;
        Invalidate();
    }
    private void PaintGpuPrototypeSurface(Graphics fallback)
    {
        if(!GpuPrototypeActive)
        {
            // A final queued child paint can arrive after success, title or
            // GameOver. Reveal the normal game instead of leaving an opaque
            // black child on top of the aftermath/next screen.
            SyncGpuPrototypeSurface();Invalidate();return;
        }
        try{RenderGpuPrototypeFrame();}
        catch(Exception error){FailGpuPrototype(error);}
    }
    private void RenderGpuPrototypeFrame()
    {
        int width=Math.Max(1,ClientSize.Width),height=Math.Max(1,ClientSize.Height);
        EnsureGpuPrototypeSurface();
        if(gpuVfxDevice==null)
        {
            // Diagnostic capture can explicitly request a synchronous frame.
            // Ordinary painting only starts after asynchronous warm-up.
            gpuVfxDevice=gpuPrototypeWarmUp?.GetAwaiter().GetResult()??new GpuVfxDevice(gpuPrototypeSurface!.Handle,width,height,
                Path.Combine(AppContext.BaseDirectory,"Assets","GpuVfx.hlsl"));
            gpuPrototypeWarmUp=null;
            try
            {
                if(!gpuVfxDevice.IsHardware)throw new NotSupportedException("하드웨어 GPU 가속을 사용할 수 없어 기존 화면으로 전환합니다.");
                PrepareApotheosisPlasma();gpuVfxDevice.SetBeamTextures(apotheosisBlue,apotheosisPink);
                gpuVfxDevice.SetCollisionTextures(collisionVolume);gpuVfxDevice.SetDetonationTextures(detonationVolume);
                gpuPrototypeUploadStage=3;
            }
            catch{gpuVfxDevice.Dispose();gpuVfxDevice=null;throw;}
        }
        if(gpuPrototypeUploadStage<3)
        {
            if(gpuPrototypeUploadStage==0){PrepareApotheosisPlasma();gpuVfxDevice.SetBeamTextures(apotheosisBlue,apotheosisPink);}
            if(gpuPrototypeUploadStage<=1)gpuVfxDevice.SetCollisionTextures(collisionVolume);
            gpuVfxDevice.SetDetonationTextures(detonationVolume);gpuPrototypeUploadStage=3;
        }
        gpuVfxDevice.Resize(width,height);
        if(gpuPrototypeBase==null||gpuPrototypeBase.Width!=width||gpuPrototypeBase.Height!=height)
        {
            gpuPrototypeBase?.Dispose();gpuPrototypeBase=new(width,height,PixelFormat.Format32bppArgb);
        }
        long start=Stopwatch.GetTimestamp();
        using(var bg=Graphics.FromImage(gpuPrototypeBase))
        {
            bg.Clear(Ink);
            float scale=Math.Max(.001f,Math.Min(width/(float)W,height/(float)H));
            if(Math.Abs(panelScale-scale)>.01f){panelScale=scale;foreach(var old in panelSurfaces.Values)old.Dispose();panelSurfaces.Clear();}
            float cacheScale=Math.Min(2.5f,scale);
            if(Math.Abs(cacheScale-nativeArtScale)>.01f){nativeArtScale=cacheScale;backgroundCacheRound=-1;}
            bg.TranslateTransform((width-W*scale)/2,(height-H*scale)/2);bg.ScaleTransform(scale,scale);bg.SetClip(new Rectangle(0,0,W,H));
            gpuPrototypeDrawing=true;
            try{PaintPremiumFrame(bg,scale);}finally{gpuPrototypeDrawing=false;}
        }
        gpuVfxDevice.Render(gpuPrototypeBase,BuildGpuPrototypeConstants(width,height));
        double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        gpuPrototypeFrameMs=gpuPrototypeFrames++==0?elapsed:gpuPrototypeFrameMs*.94+elapsed*.06;
    }
    // Test hook reads the actual D3D output. Normal play never performs GPU readback.
    private Bitmap RenderGpuPrototypeCapture()
    {
        RenderGpuPrototypeFrame();return gpuVfxDevice!.CaptureFrame();
    }
    private float[] BuildGpuPrototypeConstants(int width,int height)
    {
        Array.Clear(gpuPrototypeConstants);
        float[] c=gpuPrototypeConstants;
        float scale=Math.Max(.001f,Math.Min(width/(float)W,height/(float)H));
        float ox=(width-W*scale)*.5f,oy=(height-H*scale)*.5f;
        var cam=LastStandCamera;
        float shake=FinaleShake;
        float sx=MathF.Sin(lastStandRealClock*2.7f)*shake,sy=MathF.Cos(lastStandRealClock*2.1f)*shake*.5f;
        PointF Screen(PointF p)=>new((ox+(500+(p.X-cam.X)*cam.Zoom+sx)*scale)/width,
            (oy+(300+(p.Y-cam.Y)*cam.Zoom+sy)*scale)/height);
        void Put(int row,float x,float y,float z,float w){int i=row*4;c[i]=x;c[i+1]=y;c[i+2]=z;c[i+3]=w;}
        void Beam(int row,PointF start,PointF end){var a=Screen(start);var b=Screen(end);Put(row,a.X,a.Y,b.X,b.Y);}
        float HalfWidth(PointF a,PointF b,float nominal)=>Math.Min(nominal,
            MathF.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y))*.118f)*1.5f*.5f*cam.Zoom*scale;
        PointF hero=FinaleHeroSocket,boss=FinaleBossSocket;
        // The gathering pose has raised palms; the firing pose uses the
        // original socket. This offsets only the visual gathering light.
        if(lastStandPhase==8)hero=new(hero.X-10*SmoothUnit(lastStandClock/85),hero.Y+9*SmoothUnit(lastStandClock/85));
        float dx=boss.X-lastHeroX,dy=boss.Y-lastHeroY;
        PointF contact=new(lastHeroX+dx*lastStandPressure,lastHeroY+dy*lastStandPressure);
        PointF blueEnd=contact,pinkEnd=contact;
        float blue=0,pink=0,clash=0,pinkCharge=0,heroCharge=0,finish=0,mode=0,impact=0,explosion=0,explosionAge=0;
        float blueWidth=72+Math.Min(1,lastStandPulse/9)*6,pinkWidth=84;
        switch(lastStandPhase)
        {
            case 1:mode=1;pinkCharge=SmoothUnit(lastStandClock/150);break;
            case 2:case 3:case 8:
                mode=2;pink=1;pinkWidth=78;
                float p=lastStandPhase==2?SmoothUnit(lastStandClock/12):1;
                float proximity=lastStandPhase==3?Mix(37,28,SmoothUnit(lastStandClock/LastStandMemoryFrames)):lastStandPhase==8?28:37;
                float length=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy)),t=p*(1-proximity/length);
                pinkEnd=new(Mix(boss.X,lastHeroX,t),Mix(boss.Y,lastHeroY,t));contact=pinkEnd;
                if(lastStandPhase==8)heroCharge=SmoothUnit(lastStandClock/LastStandGatherFrames);
                break;
            case 9:case 4:case 5:
                mode=lastStandPhase==5?4:3;
                float pressure=lastStandPhase==9?.045f:lastStandPressure;
                contact=new(lastHeroX+dx*pressure,lastHeroY+dy*pressure);blueEnd=pinkEnd=contact;
                blue=lastStandPhase==9?SmoothUnit(lastStandClock/9):1;pink=1;clash=(1.08f+Math.Min(1,lastStandPulse/9)*.13f)*blue;
                if(lastStandPhase==5)
                {
                    finish=1;pink=1-SmoothUnit((lastStandClock-55)/63);
                    if(lastStandClock>=FinaleTiming.Impact&&lastStandClock<FinaleTiming.Explosion)
                    {
                        impact=lastStandClock<FinaleTiming.WhiteEnd?1:lastStandClock<FinaleTiming.NegativeEnd?2:3;
                        blue=pink=clash=0;
                    }
                    if(lastStandClock>=FinaleTiming.Explosion)
                    {
                        explosion=1;explosionAge=(lastStandClock-FinaleTiming.Explosion)/60;
                        blue=1-SmoothUnit((lastStandClock-FinaleTiming.Explosion-38)/110);
                        blueEnd=new(boss.X+(boss.X-hero.X)*.40f,boss.Y+(boss.Y-hero.Y)*.40f);
                        pink=clash=0;
                    }
                }
                break;
            case 6:mode=2;pink=1-SmoothUnit(lastStandClock/44);pinkWidth=92;pinkEnd=new(lastHeroX-90,lastHeroY+dy*.1f);contact=pinkEnd;break;
        }
        // Preserve the authored ink silhouettes, pause and the F alternative.
        // Those frames remain intact in the uploaded scene rather than being
        // overwritten by a generic post-process flash.
        float impactDetail=impact;
        if(impact>0){impact=0;mode=0;}
        var cp=Screen(contact);var br=FinaleBossBounds;var blast=Screen(new(br.X+br.Width*.5f,br.Y+br.Height*.48f));
        Put(0,width,height,lastStandRealClock/60,reducedEffects?.48f:1);
        Beam(1,hero,blueEnd);Beam(2,boss,pinkEnd);
        Put(3,cp.X,cp.Y,HalfWidth(hero,blueEnd,blueWidth),mode);
        Put(4,lastStandClock/60,clash,pinkCharge,finish);
        Put(5,heroCharge,HalfWidth(boss,pinkEnd,pinkWidth),blue,pink);
        Put(6,blast.X,blast.Y,explosionAge,explosion);
        Put(7,ox/width,oy/height,W*scale/width,H*scale/height);
        Put(8,impact,reducedEffects?.24f:1,reducedEffects?.5f:1,Math.Min(1,lastStandPulse/9));
        Put(9,1,lastStandRealClock*1.31f%PlasmaFrames,lastStandRealClock*1.43f%PlasmaFrames,0);
        float spread=(1+Math.Min(1,lastStandPulse/9)*.075f+MathF.Sin(lastStandRealClock*.23f)*.018f)*clash;
        Put(10,1,lastStandRealClock*1.27f%40,89*spread*cam.Zoom*scale,128*spread*cam.Zoom*scale);
        if(!reducedEffects&&impactDetail>0&&impactDetail<3)
        {
            var inkCenter=Screen(new(lastBossX,lastBossY));
            Put(13,1,impactDetail<1.5f?1:0,(impactDetail<1.5f?117:143)*cam.Zoom*scale,(lastStandClock-FinaleTiming.Impact)/60);
            Put(14,inkCenter.X,inkCenter.Y,14+(lastStandClock-FinaleTiming.Impact)*.46f,0);
        }
        if(explosion>0)
        {
            float age=lastStandClock-FinaleTiming.Explosion;
            float progress=age<46?.60f*SmoothUnit(age/46):.60f+.40f*SmoothUnit((age-46)/194);
            float fade=1-SmoothUnit((progress-.30f)/.70f),radius=32+MathF.Pow(progress,.43f)*270;
            Put(11,1,Math.Clamp(progress*35,0,35),radius*1.23f*cam.Zoom*scale,radius*.91f*cam.Zoom*scale);
            Put(12,fade,progress,0,0);
        }
        return c;
    }
    private void DrawGpuPrototypeBadge(Graphics g,bool active)
    {
        string mode=active?"GPU TEST":"기존 화면 비교";
        string detail=gpuPrototypeFailure.Length>0?"GPU 시작 실패 · 기존 화면 사용":"F8 비교 / F9 다시 재생";
        Label(g,mode+"  ·  "+detail,new(355,31,610,15),8,Color.FromArgb(177,198,220),false,false);
    }
    private void DisposeGpuPrototype()
    {
        gpuPrototypeDisposed=true;
        // A background creation task owns no game bitmaps. On close it can
        // finish independently and release its native resources without a UI
        // wait (DXGI creation may itself send a window message).
        if(gpuPrototypeWarmUp!=null)
        {
            _=gpuPrototypeWarmUp.ContinueWith(task=>{if(task.Status==TaskStatus.RanToCompletion)task.Result.Dispose();else _=task.Exception;},TaskScheduler.Default);
            gpuPrototypeWarmUp=null;
        }
        gpuVfxDevice?.Dispose();gpuVfxDevice=null;gpuPrototypeBase?.Dispose();gpuPrototypeBase=null;
        gpuPrototypeSurface?.Dispose();gpuPrototypeSurface=null;
    }
    private sealed class GpuVfxSurface : Control
    {
        private readonly GamePanel owner;
        internal GpuVfxSurface(GamePanel owner)
        {
            this.owner=owner;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.Opaque,true);
            SetStyle(ControlStyles.Selectable,false);DoubleBuffered=false;
        }
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e)=>owner.PaintGpuPrototypeSurface(e.Graphics);
    }
}
