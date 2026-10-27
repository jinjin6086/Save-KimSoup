using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // Independent cinematic coordinates leave battle AI and collision positions
    // untouched. The boss remains visibly airborne over a longer diagonal clash.
    private float FinalePlayerAnchorX=>px-80;
    private const float FinaleBossAnchorX=820,FinaleBossAnchorY=70;
    private static readonly string[] LastStandMemoryLines={
        "얼마나 쏴 대는거야..", "슬슬 나도 한계인 것 같은데..", "아니지... 아니야....!",
        "그치만 아직 구하지 못했잖아..", "그깟 돼지가 아니란 말이야..", "내 가족이라고….!"};
    private int LastStandMemoryIndex=>Math.Clamp((int)(lastStandClock/LastStandMemoryFrames*LastStandMemoryLines.Length),0,LastStandMemoryLines.Length-1);
    private void DrawFinaleSweat(Graphics g,RectangleF actor)
    {
        for(int i=0;i<3;i++)
        {
            float age=(lastStandClock*.022f+i*.33f)%1;
            float x=actor.X+actor.Width*(.66f+i*.025f),y=actor.Y+actor.Height*(.12f+age*.10f);
            using var drop=new GraphicsPath();float size=1.5f+i*.3f;
            drop.AddBezier(x,y-size*3,x-size*1.5f,y,x-size,y+size*2,x,y+size*2);
            drop.AddBezier(x,y+size*2,x+size,y+size*2,x+size*1.5f,y,x,y-size*3);drop.CloseFigure();
            using var water=new SolidBrush(Color.FromArgb((int)(195*MathF.Sin(age*MathF.PI)),173,223,245));g.FillPath(water,drop);
        }
    }

    private Bitmap[] moonwalkPoses=Array.Empty<Bitmap>();
    private float moonwalkClock,dodgeNearHitTimer,dodgeNearHitDirection=1;
    private readonly HashSet<Projectile> moonwalkTelegraphs=new();
    private float drainKissTimer;
    private bool drainKissHasBoss;
    private PointF drainKissBoss;
    private void LoadMoonwalkDodge()
    {
        string file=AssetPath("PlayerMoonwalk6.png");if(!File.Exists(file))return;
        using var sheet=new Bitmap(file);int w=sheet.Width/3,h=sheet.Height/2;
        moonwalkPoses=new Bitmap[6];
        for(int i=0;i<6;i++)moonwalkPoses[i]=ExtractMainSprite(sheet,new(i%3*w,i/3*h,w,h),220,280);
    }
    private void DisposeMoonwalkDodge(){foreach(var image in moonwalkPoses)image.Dispose();moonwalkPoses=Array.Empty<Bitmap>();}
    private void BeginMoonwalkDodge(){moonwalkClock=dodgeNearHitTimer=0;moonwalkTelegraphs.Clear();}
    private void UpdateMoonwalkDodge(float step)
    {
        dodgeNearHitTimer=Math.Max(0,dodgeNearHitTimer-step);
        if(!DodgeActive)return;
        moonwalkClock+=step;
        RectangleF body=PlayerHitBox;
        foreach(var projectile in projectiles)
        {
            if(projectile.IsParried||moonwalkTelegraphs.Contains(projectile))continue;
            var near=body;near.Inflate(projectile.W*.5f+3,projectile.H*.5f+3);
            float cx=projectile.X+projectile.W*.5f,cy=projectile.Y+projectile.H*.5f;
            if(!SegmentTouchesRectangle(cx,cy,cx+projectile.VelocityX*3,cy+projectile.VelocityY*3,near))continue;
            moonwalkTelegraphs.Add(projectile);TriggerMoonwalkAvoid(cx);
        }
        foreach(var obstacle in obstacles)
        {
            var near=body;near.Inflate(13,3);
            if(near.IntersectsWith(obstacle.Rect))TriggerMoonwalkAvoid(obstacle.X);
        }
        foreach(var obstacle in risingObstacles)
            if(obstacle.IsActive&&body.IntersectsWith(obstacle.Rect))TriggerMoonwalkAvoid(obstacle.X);
    }
    private void TriggerMoonwalkAvoid(float sourceX)
    {
        if(dodgeNearHitTimer>0)return;
        dodgeNearHitTimer=14;dodgeNearHitDirection=sourceX>=px?-1:1;
    }
    private void DrawMoonwalkDodge(Graphics g,RectangleF target)
    {
        float near=dodgeNearHitTimer>0?MathF.Sin((1-dodgeNearHitTimer/14)*MathF.PI):0;
        float slide=-MathF.Sin(moonwalkClock*.085f)*4;
        float height=target.Height,width=height*.75f;
        var actor=new RectangleF(px-8+slide+near*dodgeNearHitDirection*8,py+PlayerHeight-height,width,height);
        var saved=g.Save();float cx=actor.X+actor.Width*.5f,foot=actor.Bottom;
        g.TranslateTransform(cx,foot);g.RotateTransform(-4-near*16);g.TranslateTransform(-cx,-foot);
        if(selectedCookie==1&&wolfPoses.Length==12)
            DrawPlayerSprite(g,wolfPoses[3-(int)(moonwalkClock/7)%4],actor,Mint,1);
        else if(moonwalkPoses.Length==6)
            DrawPlayerSprite(g,moonwalkPoses[(int)(moonwalkClock/7)%6],actor,Mint,1);
        else if(motionFrames.Length>0)
            DrawPlayerSprite(g,motionFrames[3-(int)(moonwalkClock/7)%4],actor,Mint,1);
        g.Restore(saved);
        if(near>.01f)
        {
            using var slip=new Pen(Color.FromArgb((int)(130*near),204,231,237),1.1f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(slip,actor.Right+7,actor.Y+12,actor.Right+18,actor.Y+22,actor.Right+18,actor.Y+38,actor.Right+6,actor.Y+49);
        }
    }
    private void ResetRevampedPassives(){drainKissTimer=0;moonwalkTelegraphs.Clear();dodgeNearHitTimer=0;}
    private void UpdateRevampedPassives(float step){drainKissTimer=Math.Max(0,drainKissTimer-step);}
    private void BeginDrainKiss(bool boss)
    {
        drainKissTimer=54;drainKissHasBoss=boss;drainKissBoss=new(bossX+65,bossY+65);
    }
    private static GraphicsPath KissHeart(float x,float y,float size)
    {
        var heart=new GraphicsPath();
        heart.AddBezier(x,y+size*.75f,x-size*1.2f,y-size*.02f,x-size*.62f,y-size*.95f,x,y-size*.35f);
        heart.AddBezier(x,y-size*.35f,x+size*.62f,y-size*.95f,x+size*1.2f,y-size*.02f,x,y+size*.75f);
        heart.CloseFigure();return heart;
    }
    private void DrawKissHeart(Graphics g,float x,float y,float size,float alpha,Color color)
    {
        using var shape=KissHeart(x,y,size);
        using var light=new PathGradientBrush(shape){CenterColor=Color.FromArgb((int)(240*alpha),255,244,250),SurroundColors=new[]{Color.FromArgb((int)(205*alpha),color)}};
        g.FillPath(light,shape);
    }
    private void DrawDrainKiss(Graphics g)
    {
        if(drainKissTimer<=0)return;
        float age=54-drainKissTimer,fade=SmoothUnit(drainKissTimer/16);
        PointF player=new(px+30,py+29),boss=drainKissHasBoss?drainKissBoss:new(px+45,py-35);
        if(drainKissHasBoss&&age<15)
        {
            float t=SmoothUnit(age/15),x=Mix(player.X,boss.X,t),y=Mix(player.Y,boss.Y,t)-MathF.Sin(t*MathF.PI)*28;
            DrawKissHeart(g,x,y,10,.95f,Color.FromArgb(235,94,157));
        }
        if(age>=12)
        {
            float hit=SmoothUnit((age-12)/8)*(1-SmoothUnit((age-27)/20));
            DrawKissHeart(g,boss.X,boss.Y,17,hit*.9f,Color.FromArgb(242,107,171));
            int count=reducedEffects?4:10;
            for(int i=0;i<count;i++)
            {
                float t=Math.Clamp((age-15-i*1.6f)/27,0,1);if(t<=0||t>=1)continue;
                float wave=MathF.Sin(t*MathF.PI)*MathF.Sin(i*2.2f+t*4)*17;
                float x=Mix(boss.X,player.X,t),y=Mix(boss.Y,player.Y,t)+wave;
                DrawKissHeart(g,x,y,3.5f+i%3,MathF.Sin(t*MathF.PI)*fade,Color.FromArgb(152,237,179));
                g.FillEllipse(ParticleBrush(Color.FromArgb(242,166,206),(int)(115*fade)),x+4,y-6,1.5f,1.5f);
            }
        }
        if(age>32)
        {
            float settle=MathF.Sin(Math.Clamp((age-32)/22,0,1)*MathF.PI);
            DrawLaserBloom(g,player.X,player.Y,23,33,Color.FromArgb(111,246,159),settle*.22f);
        }
    }
    private void DrawCloneAura(Graphics g,RectangleF actor)
    {
        if(!cloneActive)return;
        Color green=Color.FromArgb(77,236,123);
        float cx=actor.X+actor.Width*.5f,cy=actor.Y+actor.Height*.58f;
        DrawLaserBloom(g,cx,cy,actor.Width*.57f,actor.Height*.61f,green,.13f);
        int count=reducedEffects?7:17;
        for(int i=0;i<count;i++)
        {
            float t=(animationClock*.015f+i/(float)count)%1;
            float side=i%2==0?-1:1,x=cx+side*(actor.Width*.42f+MathF.Sin(t*7+i)*4);
            float y=actor.Bottom-t*actor.Height*1.07f,size=.8f+(1-t)*1.5f;
            g.FillEllipse(ParticleBrush(i%4==0?Color.FromArgb(205,255,215):green,(int)(145*MathF.Sin(t*MathF.PI))),x,y,size,size*1.8f);
        }
    }
}
