namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private int trueStopPattern,trueStopCycle;
    private readonly List<TrueStopLane> trueStopLanes=new(4);
    private sealed class TrueStopLane { public float At,Y;public bool Fired; }
    private int TrueStopTotal=>trueStopPattern switch{0=>24,1=>30,2=>24,_=>36};
    private string TrueStopPatternName=>trueStopPattern switch{0=>"천륜 포위",1=>"이중 시계",2=>"정지된 창",_=>"종말의 초침"};
    private float TrueStopBuildInterval=>(TrueStopDuration-36)/TrueStopTotal;
    private float TrueStopReleaseDuration=>trueStopPattern switch{0=>160,1=>245,2=>240,_=>305};
    private void BuildTrueStopFormation()
    {
        trueStopStones.Clear();trueStopLanes.Clear();
        int count=TrueStopTotal;
        for(int i=0;i<count;i++)
        {
            int ring=trueStopPattern==1?i/15:0,index=trueStopPattern==1?i%15:i;
            int perRing=trueStopPattern==1?15:count;
            float direction=ring==1?-1:1,angle=-MathF.PI*.5f+direction*index*MathF.Tau/perRing;
            float radius=165+ring*68+(trueStopPattern==3?i%3*17:0);
            float x=Math.Clamp(trueStopX+MathF.Cos(angle)*radius,30,W-30);
            float y=Math.Clamp(trueStopY+MathF.Sin(angle)*radius*.71f,128,GroundY-15);
            float distance=MathF.Sqrt((trueStopX-x)*(trueStopX-x)+(trueStopY-y)*(trueStopY-y));
            float speed=trueStopPattern==3?44:40;
            float arrival=trueStopPattern switch{1=>14+index*6+ring*115,2=>16+i*7,_=>14+i*5.5f};
            trueStopStones.Add(new(){X=x,Y=y,FireAt=Math.Max(0,arrival-distance/speed),Speed=speed});
        }
        if(trueStopPattern>=2)
        {
            int lanes=trueStopPattern==3?4:3;
            for(int i=0;i<lanes;i++)trueStopLanes.Add(new(){At=80+i*65,Y=i%2==0?GroundY-15:GroundY-65});
        }
    }
    private void UpdateTrueStopLanes()
    {
        foreach(var lane in trueStopLanes)
        {
            if(lane.Fired||trueStopClock<lane.At)continue;
            lane.Fired=true;audio.Play("dash");
            var bounds=new RectangleF(0,lane.Y-11,W,22);
            if(!DodgeActive&&bounds.IntersectsWith(PlayerHitBox))ReceiveHit(950,"천륜창",Gold,true);
        }
    }
    private bool TrueStoneTouchesKick(Projectile p,RectangleF zone)
    {
        if(!TruePurgaeStone(p))return false;
        zone.Inflate(p.W*.5f,p.H*.5f);
        return SegmentTouchesRectangle(p.PreviousX+p.W*.5f,p.PreviousY+p.H*.5f,p.X+p.W*.5f,p.Y+p.H*.5f,zone);
    }
    private void DrawTrueStopLanes(Graphics g,bool frozen)
    {
        if(trueStopPattern<2)return;
        for(int i=0;i<trueStopLanes.Count;i++)
        {
            var lane=trueStopLanes[i];float age=trueStopClock-lane.At;
            if(frozen&&trueStopLanes.Take(i).Any(l=>l.Y==lane.Y))continue;
            bool warn=frozen||age>=-45&&age<0,live=!frozen&&age>=0&&age<9;
            if(!warn&&!live)continue;
            Color color=live?Color.FromArgb(255,236,170):Color.FromArgb(215,177,85);
            using var fill=new SolidBrush(Color.FromArgb(live?95:18,color));g.FillRectangle(fill,0,lane.Y-11,W,22);
            using var edge=new Pen(color,live?2:1){DashStyle=live?System.Drawing.Drawing2D.DashStyle.Solid:System.Drawing.Drawing2D.DashStyle.Dash};
            g.DrawRectangle(edge,1,lane.Y-11,W-2,22);
            string order=frozen?string.Join(" / ",trueStopLanes.Select((l,n)=>(l,n)).Where(v=>v.l.Y==lane.Y).Select(v=>(v.n+1).ToString())):(i+1).ToString();
            Label(g,$"{order} · {(lane.Y>GroundY-40?"↑ 점프":"↓ 슬라이드")} · 반사 불가",new(510,lane.Y-34,290,22),11,Gold);
        }
    }
}
