namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private Image? rodianEntrySource;
    private Bitmap? rodianEntryFrame;
    private const int RodianEntryHoldFrames=42;
    private RectangleF rodianEntryBounds;
    private static float SmootherUnit(float value){float t=Math.Clamp(value,0,1);return t*t*t*(t*(t*6-15)+10);}
    private void CaptureRodianQteEntry()
    {
        rodianEntryFrame?.Dispose();rodianEntryFrame=new Bitmap((int)MathF.Ceiling(W*nativeArtScale),(int)MathF.Ceiling(H*nativeArtScale));
        using(var g=Graphics.FromImage(rodianEntryFrame)){g.ScaleTransform(nativeArtScale,nativeArtScale);DrawBackground(g);DrawWorld(g);DrawModernHud(g);DrawPracticeHud(g);}
        rodianEntrySource=menheraMode?melodyMenhera??enemy5:enemy5;
        float size=(int)(155*(1+MathF.Sin(frame*.09f)*.075f));
        float x=(int)(bossX+(bossRecoil>0?MathF.Sin(bossRecoil*2)*bossRecoil*.4f:0));
        rodianEntryBounds=new(x,(int)bossY,size,size);
    }
    private void DrawRodianEntryFrame(Graphics g,float opacity)
    {
        if(rodianEntryFrame!=null)DrawEchoFrame(g,rodianEntryFrame,new(0,0,W,H),opacity);
    }
    private RectangleF SmoothRodianEntryBounds(RectangleF target)
    {
        if(lastStandPhase!=10||rodianEntrySource==null)return target;
        float t=SmootherUnit(lastStandClock/48);
        return new(Mix(rodianEntryBounds.X,target.X,t),Mix(rodianEntryBounds.Y,target.Y,t),Mix(rodianEntryBounds.Width,target.Width,t),Mix(rodianEntryBounds.Height,target.Height,t));
    }
}
