using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly record struct RushLane(float Left,float Right)
    {
        public float Slope=>(Right-Left)/W;
        public float Y(float x)=>Left+Slope*x;
    }
    private const int MaxRushCount=20;
    private readonly RushLane[] rushRoutes=new RushLane[MaxRushCount];
    private readonly bool[] rushHits=new bool[MaxRushCount],rushSounds=new bool[MaxRushCount];
    private int rushCount,rushRevealed;
    private float rushClock;
    private readonly float[] rushLaunchTimes=new float[MaxRushCount];
    private readonly int[] rushKinds=new int[MaxRushCount];
    private readonly bool[] rushRouteLocked=new bool[MaxRushCount];
    private float RushLaunch(int index)=>rushLaunchTimes[index];
    private string RushKindName(int kind)=>kind switch{0=>"하체 집중 · 점프",1=>"중앙 압박 · 점프로 이탈",2=>"상체 집중 · 숙이기",_=>"공중 저격 · 낙하 / 2단 점프"};
    private void LockVariableRush(int index)
    {
        int kind=rushKinds[index];
        float y=kind switch{0=>477,1=>451,2=>432,_=>onGround?320:Math.Clamp(py+30+vy*9,135,445)};
        y+=rng.Next(-5,6);float slope=(rng.NextSingle()-.5f)*.10f,anchor=px+26;
        rushRoutes[index]=new(y-slope*anchor,y+slope*(W-anchor));rushRouteLocked[index]=true;
    }
    private const float CascadeTravel=5;
    private int RevealedRushCount
    {
        get{int n=0;while(n<rushCount&&rushClock>=RushLaunch(n)-27)n++;return n;}
    }
    private int meteorPhase,meteorRainTotal,meteorRainEmitted;
    private float meteorSceneClock,meteorHomeX=150;
    private bool meteorParryNoBossDamage;
    private bool MeteorSceneActive=>meteorPhase>0;

    private void StartRushCascade()
    {
        fullRush=dashActive=true;dashPass=0;dashTimer=rushClock=0;rushRevealed=0;
        rushCount=rng.Next(16,MaxRushCount+1);
        int[] kinds={0,1,2,3};
        for(int i=3;i>0;i--){int j=rng.Next(i+1);(kinds[i],kinds[j])=(kinds[j],kinds[i]);}
        float launch=36;
        for(int i=0;i<rushCount;i++)
        {
            if(i>0)launch+=i%3==0?rng.Next(20,32):rng.Next(5,10);
            rushLaunchTimes[i]=launch; rushKinds[i]=kinds[(i/3)%4];
            rushRoutes[i]=new(320,320);rushRouteLocked[i]=false;
            if(rushKinds[i]!=3)LockVariableRush(i);
            rushHits[i]=rushSounds[i]=false;
        }
        projectiles.RemoveAll(p=>!p.IsParried);obstacles.Clear();risingObstacles.Clear();warningTimer=0;
    }
    private void UpdateRushCascade(float step)
    {
        rushClock+=step;dashTimer=rushClock;
        for(int i=0;i<rushCount;i++)
            if(!rushRouteLocked[i]&&rushClock>=RushLaunch(i)-30)LockVariableRush(i);
        int revealed=RevealedRushCount;
        if(revealed>rushRevealed){rushRevealed=revealed;audio.Play("dashwarn");}
        for(int i=0;i<rushCount;i++)
        {
            float launch=RushLaunch(i);
            if(!rushSounds[i]&&rushClock>=launch){rushSounds[i]=true;audio.Play("dash");}
            float cross=launch+CascadeTravel*(i%2==0?(W+100-(px+26))/(W+200):(px+26+100)/(W+200));
            if(!rushHits[i]&&rushClock>=cross)
            {
                rushHits[i]=true;float y=rushRoutes[i].Y(px+26),top=sliding?py+36:py+8,bottom=py+PlayerHeight-4;
                if(y+17>=top&&y-17<=bottom)ReceiveHit(1000,"돌진",Coral,true);
            }
        }
        dashPass=0;while(dashPass+1<rushCount&&rushClock>=RushLaunch(dashPass+1))dashPass++;
        dashY=rushRoutes[dashPass].Y(px+26);dashSlope=rushRoutes[dashPass].Slope;
        if(rushClock>=RushLaunch(rushCount-1)+CascadeTravel+12)
        {
            dashActive=fullRush=false;dashCooldown=rng.Next(220,291);obstacleSpawnTimer=65;
        }
    }
    private void DrawRushCascade(Graphics g)
    {
        int revealed=RevealedRushCount,next=0;
        while(next<rushCount&&rushClock>RushLaunch(next)+CascadeTravel)next++;
        for(int i=0;i<revealed;i++)
        {
            float age=rushClock-RushLaunch(i);if(age>CascadeTravel+10)continue;
            var route=rushRoutes[i];bool active=age>=0&&age<=CascadeTravel;
            int alpha=active?230:i==next?175:85;
            if(age>CascadeTravel)alpha=(int)(120*Math.Max(0,1-(age-CascadeTravel)/10));
            using var band=new Pen(Color.FromArgb(reducedEffects?10:active?85:18,255,60,92),34);
            using var edge=new Pen(Color.FromArgb(alpha,255,85,110),active?3:1.5f);
            g.DrawLine(band,-60,route.Y(-60),W+60,route.Y(W+60));
            g.DrawLine(edge,-60,route.Y(-60)-17,W+60,route.Y(W+60)-17);g.DrawLine(edge,-60,route.Y(-60)+17,W+60,route.Y(W+60)+17);
            if(age<0)
            {
                float tagX=285+(i%3)*150,tagY=route.Y(tagX)-27;
                Label(g,$"{i+1:00}",new(tagX,tagY,33,24),12,i==next?Paper:Coral);
            }
            if(active)
            {
                float t=age/CascadeTravel,x=i%2==0?W+100-t*(W+200):-100+t*(W+200);
                using var core=new Pen(Color.FromArgb(245,255,226,219),5);g.DrawLine(core,-60,route.Y(-60),W+60,route.Y(W+60));
                DrawMelodyRushBody(g,x,route.Y(x),i,route.Slope);
            }
        }
        if(next<rushCount)Label(g,RushKindName(rushKinds[next]),new(315,150,370,24),13,Paper);
        Label(g,$"RAPID RUSH  {Math.Min(next+1,rushCount)} / {rushCount}",new(335,112,330,32),20,Coral);
    }
    private void StartMeteorStorm()
    {
        if(MeteorSceneActive)return;
        meteorHomeX=px;meteorPhase=1;meteorSceneClock=0;meteorRainTotal=rng.Next(27,37);meteorRainEmitted=0;
        meteorShotsRemaining=meteorRainTotal;meteorSpawnTimer=0;meteorBarrageTimer=1000;
        projectiles.Clear();obstacles.Clear();risingObstacles.Clear();meteorImpacts.Clear();impactParticles.Clear();
        purgaeSkill2Active=false;Array.Fill(purgaeSkill2BallVisible,false);attackTimer=0;warningTimer=0;bossShoutTimer=0;
        audio.Play("charge");
    }
    private void UpdateMeteorStorm(float step)
    {
        if(!MeteorSceneActive)return;
        meteorSceneClock+=step;
        if(meteorPhase==1)
        {
            float t=Math.Clamp(meteorSceneClock/50,0,1),ease=t*t*(3-2*t);
            px=meteorHomeX+(W*.5f-PlayerWidth*.5f-meteorHomeX)*ease;
            if(t>=1){meteorPhase=2;meteorSceneClock=0;meteorSpawnTimer=0;}
            return;
        }
        if(meteorPhase==2)
        {
            px=W*.5f-PlayerWidth*.5f;meteorSpawnTimer-=step;
            while(meteorShotsRemaining>0&&meteorSpawnTimer<=0)
            {
                // Every meteor converges on the centered player; no empty side lanes.
                float offset=0;
                float target=px+PlayerWidth*.5f+offset+rng.Next(-6,7),x=target+rng.Next(-150,151),y=-70-rng.Next(0,45);
                float dx=target-x,dy=GroundY+6-y,length=MathF.Sqrt(dx*dx+dy*dy),speed=16+rng.NextSingle()*2;
                projectiles.Add(new(x-22,y-22,AttackType.Meteor,44,dx/length*speed,dy/length*speed,PurgaeSkill1MeteorDamage)
                    {IsFocusedMeteor=true,FocusX=target,FocusY=GroundY+6});
                meteorRainEmitted++;meteorShotsRemaining--;meteorSpawnTimer+=4;
            }
            bool incoming=projectiles.Any(p=>p.Type==AttackType.Meteor&&!p.IsParried);
            if(meteorShotsRemaining==0&&!incoming){meteorPhase=3;meteorSceneClock=0;}
            return;
        }
        float progress=Math.Clamp(meteorSceneClock/44,0,1),e=progress*progress*(3-2*progress);
        px=(W*.5f-PlayerWidth*.5f)+(meteorHomeX-(W*.5f-PlayerWidth*.5f))*e;
        if(progress>=1)
        {
            px=meteorHomeX;meteorPhase=0;meteorBarrageTimer=0;obstacleSpawnTimer=90;attackTimer=0;
            purgaeSkill2Cooldown=Math.Max(90,purgaeSkill2Cooldown);
        }
    }
    private void CancelMeteorStorm(bool restorePosition)
    {
        if(restorePosition&&MeteorSceneActive)px=meteorHomeX;
        meteorPhase=0;meteorSceneClock=0;meteorBarrageTimer=0;meteorShotsRemaining=0;
    }
}

