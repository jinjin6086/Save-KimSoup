namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private static RectangleF ProjectileDamageBounds(Projectile p)=>p.Type==AttackType.Meteor
        ?new(p.X+p.W*.19f,p.Y+p.H*.175f,p.W*.62f,p.H*.65f):p.Rect;

    private static bool CanKickProjectile(Projectile p,RectangleF zone)
    {
        if(p.Type!=AttackType.Meteor)return zone.IntersectsWith(p.Rect);
        // Keep upgrade range, add a wider meteor-only grace margin, then sweep
        // its actual center segment to avoid missing a fast diagonal descent.
        zone.Inflate(30+p.W*.5f,38+p.H*.5f);
        return SegmentTouchesRectangle(p.PreviousX+p.W*.5f,p.PreviousY+p.H*.5f,p.X+p.W*.5f,p.Y+p.H*.5f,zone);
    }
    private static bool SegmentTouchesRectangle(float x0,float y0,float x1,float y1,RectangleF r)
    {
        float enter=0,exit=1;
        static bool Axis(float origin,float delta,float min,float max,ref float enter,ref float exit)
        {
            if(MathF.Abs(delta)<.00001f)return origin>=min&&origin<=max;
            float a=(min-origin)/delta,b=(max-origin)/delta;
            if(a>b)(a,b)=(b,a);enter=Math.Max(enter,a);exit=Math.Min(exit,b);return enter<=exit;
        }
        return Axis(x0,x1-x0,r.Left,r.Right,ref enter,ref exit)&&Axis(y0,y1-y0,r.Top,r.Bottom,ref enter,ref exit);
    }
    private void CleanupGroundMeteors()
    {
        // Called after kick and damage checks so a last-moment kick still wins.
        for(int i=projectiles.Count-1;i>=0;i--)
        {
            var p=projectiles[i];
            if(!p.IsFocusedMeteor||p.IsParried||p.Y+p.H*.5f<GroundY-7)continue;
            SpawnMeteorImpact(p.X+p.W*.5f);projectiles.RemoveAt(i);
        }
    }
}
