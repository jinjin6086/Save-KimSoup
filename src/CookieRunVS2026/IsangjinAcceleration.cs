namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private float isangjinAccelerationClock,isangjinAccelerationRemaining;
    private const float IsangjinAccelerationPeriod=900,IsangjinAccelerationDuration=240;
    private const float IsangjinAccelerationMultiplier=1.3f;
    private float IsangjinTimeScale=>round==2&&isangjinAccelerationRemaining>0?IsangjinAccelerationMultiplier:1;
    private void ResetIsangjinAcceleration()=>isangjinAccelerationClock=isangjinAccelerationRemaining=0;
    private void UpdateIsangjinAcceleration(float realStep)
    {
        if(round!=2)return;
        // Ability cadence/duration use real simulation frames, independent of acceleration.
        isangjinAccelerationRemaining=Math.Max(0,isangjinAccelerationRemaining-realStep);
        isangjinAccelerationClock+=realStep;
        if(isangjinAccelerationClock>=IsangjinAccelerationPeriod)
        {
            isangjinAccelerationClock-=IsangjinAccelerationPeriod;
            isangjinAccelerationRemaining=IsangjinAccelerationDuration;
            audio.Play("focusenter");
        }
    }
    private void DrawIsangjinAccelerationHud(Graphics g)
    {
        if(round!=2||PhaseIntroActive)return;
        if(isangjinAccelerationRemaining>0)
        {
            Label(g,$"시간 가속 ×{IsangjinAccelerationMultiplier:0.0} · 적만  ·  {isangjinAccelerationRemaining/60:0.0}초",new(340,157,320,26),16,Color.FromArgb(255,192,103));
            using var line=new Pen(Color.FromArgb(125,255,181,88),1);
            for(int i=0;i<7;i++)
            {
                float x=(frame*13+i*149)%W;
                g.DrawLine(line,x,151,x+22,151);g.DrawLine(line,W-x,190,W-x-16,190);
            }
        }
        else if(isangjinAccelerationClock>=IsangjinAccelerationPeriod-90)
            Label(g,$"시간 가속 예고  {(IsangjinAccelerationPeriod-isangjinAccelerationClock)/60:0.0}초",new(340,157,320,26),16,Gold);
    }
}
