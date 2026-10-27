using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private Bitmap[] wolfPoses=Array.Empty<Bitmap>();
    private float PlayerAttackMultiplier=>selectedCookie==1?1.5f:1;
    private float BasePlayerMaxHealth=>selectedCookie==1?4000:MaxHealth;
    private void LoadWolfPlayer()
    {
        string file=AssetPath("WolfPlayerMotion12.png");if(!File.Exists(file))return;
        using var sheet=new Bitmap(file);int w=sheet.Width/4,h=sheet.Height/3;wolfPoses=new Bitmap[12];
        for(int i=0;i<12;i++)
        {
            int x=i%4*w,y=i/4*h,left=Math.Max(0,x-(i==5?20:3)),right=Math.Min(sheet.Width,x+w+(i==5?30:3));
            wolfPoses[i]=ExtractMainSprite(sheet,new(left,y,right-left,h),256,256);
        }
    }
    private void DisposeWolfPlayer(){foreach(var b in wolfPoses)b.Dispose();wolfPoses=Array.Empty<Bitmap>();}
    private void DrawWolfPose(Graphics g,int pose,RectangleF rect,float alpha=1)
    {
        if(wolfPoses.Length!=12)return;
        DrawPlayerSprite(g,wolfPoses[Math.Clamp(pose,0,11)],rect,Color.FromArgb(171,209,226),alpha);
    }
    private void DrawWolfActor(Graphics g,float x,float y,float width,float height)
    {
        var rect=new RectangleF(x-width*.35f,y-height*.12f,width*1.7f,height*1.12f);
        if(sliding&&onGround){DrawWolfPose(g,5,new(px-31,GroundY-47,125,52));return;}
        if(MelodyLastStandActive){DrawWolfFinale(g);return;}
        if(!onGround){DrawWolfPose(g,4,rect);return;}
        if(bossBriefingActive&&bossTalkClock>=108){DrawWolfPose(g,8,rect);return;}
        float run=(bossBriefingActive?bossTalkClock:animationClock)/7;int pose=(int)run%4,next=(pose+1)%4;float blend=run-MathF.Floor(run);
        DrawWolfPose(g,pose,rect,1-blend);if(blend>.002f)DrawWolfPose(g,next,rect,blend);
    }
    private void DrawWolfKick(Graphics g,float progress)
    {
        int a=progress<1?6:7,b=progress<1?7:8;float t=progress<1?progress:progress-1;
        var rect=new RectangleF(px-28,py-18,114,98);
        DrawWolfPose(g,a,rect,1-t);if(t>.002f)DrawWolfPose(g,b,rect,t);
    }
    private void DrawWolfRepel(Graphics g)
    {
        float t=isangjinClockTimer,open=isangjinClockPhase==5?Math.Clamp((t-20)/12,0,1):0;
        open=open*open*(3-2*open);var rect=new RectangleF(px-26,py-12,105,91);
        DrawWolfPose(g,isangjinClockPhase is 3 or 4 or 5?9:8,rect,1-open);
        if(open>.002f)DrawWolfPose(g,10,rect,open);
    }
    private void DrawWolfFinale(Graphics g)
    {
        var rect=new RectangleF(FinalePlayerAnchorX-32,py-41,130,120);
        int pose=lastStandPhase is 9 or 4 or 5?11:lastStandPhase is 8 or 3?9:8;
        DrawWolfPose(g,pose,rect);
    }
}
