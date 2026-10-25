using System.Drawing.Drawing2D;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly record struct BriefingRule(string Title,string Detail,string Key,Color Color);
    private static readonly Color BriefingReflect=Color.FromArgb(224,195,128);
    private static readonly Color BriefingEvade=Color.FromArgb(225,115,102);
    private static readonly Color BriefingMove=Color.FromArgb(164,190,199);

    private string BriefingBossName=>briefingRound==5?FinalBossName:RoundName(briefingRound);
    private string[] BriefingSettings=>briefingRound switch
    {
        2=>new[]{"감옥을 지키는 간수.","중력과 시간을 조작하며 탄환을 발사한다."},
        3=>new[]{"돌과 불을 다루는 지옥의 괴물.","체력이 절반이 되면 광폭화한다."},
        4=>new[]{"공연장을 떠나지 못하는 아이돌.","자신을 떠나는 사람은 절대 놓아주지 않는다."},
        5=>FinalBossSetting.Split('\n',StringSplitOptions.RemoveEmptyEntries),
        _=>new[]{"장애물을 피하며 앞으로 달리세요.","보스의 공격은 발차기로 되돌려줄 수 있습니다."}
    };
    private BriefingRule[] BriefingRules=>briefingRound switch
    {
        2=>new[]{
            new BriefingRule("탄환은 발차기로 반사","부채꼴로 날아오는 탄환이 닿기 직전에 발차기.","SPACE",BriefingReflect),
            new BriefingRule("바닥은 점프 · 공중은 슬라이드","지면 장애물은 ↑, 떠 있는 장애물은 ↓로 피하세요.","↑ / ↓",BriefingMove),
            new BriefingRule("10초마다 화면 상하반전","4초 뒤 돌아옵니다. 점프·슬라이드 키는 그대로.","↕",BriefingEvade),
            new BriefingRule("15초마다 적 시간 가속 ×2","4초간 적 공격·장애물만 2배. 플레이어 속도는 그대로.","×2",BriefingEvade),
            new BriefingRule("시간정지 · 시계 리듬","W / E 빠른 리듬 6회. 실패 −3,000 · 성공 보스 −1.","W / E",BriefingReflect)},
        3=>new[]{
            new BriefingRule("돌덩이 · 화염구 · 구슬 반사","공격이 가까워지면 발차기로 보스에게 돌려주세요.","SPACE",BriefingReflect),
            new BriefingRule("광폭화 후 붉은 구슬 주의","맞으면 화상을 입습니다. 반사하거나 피하세요.","주의",BriefingEvade),
            new BriefingRule("집중 메테오는 위로 반사","플레이어 위치에 쏟아집니다. 보스 피해는 없습니다.","SPACE",BriefingReflect),
            new BriefingRule("광폭화 중 보스 피해 50% 감소","5회 명중할 때마다 방어가 5초 동안 사라집니다.","5회",BriefingReflect)},
        4=>new[]{
            new BriefingRule("음표는 발차기로 반사","대사를 외우는 동안에도 음표가 계속 날아옵니다.","SPACE",BriefingReflect),
            new BriefingRule("탈·덕·안·돼! 마지막 박자 반사","마지막 ‘돼!’에 맞춰 발차기하면 레이저를 반사합니다.","돼!",BriefingReflect),
            new BriefingRule("빨간 직선 돌진은 반사 불가","경고선을 보고 점프·슬라이드로 회피. 피해 1,000.","↑ / ↓",BriefingEvade),
            new BriefingRule("체력 1 미만이면 최후발악","슬로우 레이저 뒤 SPACE 연타. 10초 안에 50회로 반격.","SPACE",BriefingReflect)},
        5=>CreateFinalBriefingRules(),
        _=>new[]{
            new BriefingRule("바닥 장애물","위쪽 방향키로 뛰어넘으세요.","↑",BriefingMove),
            new BriefingRule("떠 있는 장애물","아래쪽 방향키를 누른 채로 지나가세요.","↓",BriefingMove),
            new BriefingRule("발차기","SPACE 또는 SHIFT로 적 공격을 반사합니다.","SPACE",BriefingReflect),
            new BriefingRule("체력 관리","반사한 공격이 보스에게 맞으면 체력 5% 회복.","5%",BriefingReflect)}
    };
    private BriefingRule[] CreateFinalBriefingRules()
    {
        var source=FinalBossRules;
        var rules=new BriefingRule[Math.Min(4,source.Length)];
        for(int i=0;i<rules.Length;i++)
        {
            string[] parts=source[i].Split('|',2);
            string title=parts.Length==2?parts[0]:$"공격 {i+1}";
            string detail=parts.Length==2?parts[1]:source[i];
            bool dodge=(title+detail).Contains("반사 불가");
            string key=title.Contains("2단 점프")?"↑↑":title.Contains("슬라이드")?"↑ / ↓":title.Contains("점프")?"↑":title.Contains("발차기 반사")?"SPACE":dodge?"회피":title.Contains("클리어")?"목표":title.Contains("예고")?"예고":"정보";
            Color color=dodge||title.Contains("점프")||title.Contains("슬라이드")?BriefingEvade:key=="SPACE"?BriefingReflect:BriefingMove;
            rules[i]=new(title,detail,key,color);
        }
        return rules;
    }
    private void DrawBossBriefing(Graphics g)
    {
        if(!bossBriefingActive)return;
        if(!bossTalkHelp&&bossTalkPhase!=3){DrawBossConversation(g);return;}
        DrawSoulsMenuBase(g,.98f);
        using(var tint=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(155,16,13,10),Color.FromArgb(240,5,6,7),0f))g.FillRectangle(tint,0,0,W,H);
        float entrance=1-MathF.Pow(1-Math.Clamp(briefingClock/27,0,1),3);
        var saved=g.Save();g.TranslateTransform(0,16*(1-entrance));
        bool drawingFinalBoss=briefingRound==5&&briefingClock<96;
        Label(g,$"라운드 {briefingRound} · 보스 소개",new(44,33,912,26),12,Gold);
        SoulsHeading(g,drawingFinalBoss?"종결 보스 추첨":BriefingBossName,new(44,67,912,51),36,Paper);
        DrawSoulsRule(g,233,130,534);

        PanelSurface(g,new(44,155,300,322),Gold);
        if(drawingFinalBoss)
        {
            SoulsHeading(g,"?",new(90,200,210,136),107,Gold);
            Label(g,"보스 3명 중 1명 선택",new(70,354,250,26),13,Steel,true,false);
        }
        else DrawBossAttackIllustration(g,new(54,165,280,239));
        DrawSoulsRule(g,86,405,216);
        var settings=drawingFinalBoss?new[]{"마지막 상대를 고르는 중입니다.","선택 후 기믹을 확인하고 시작하세요."}:BriefingSettings;
        for(int i=0;i<Math.Min(2,settings.Length);i++)Label(g,settings[i],new(59,419+i*22,270,22),11.5f,i==0?Paper:Steel,true,false);

        if(drawingFinalBoss)DrawFinalBossDraw(g);
        else
        {
            BriefingRule[] rules=BriefingRules;
            for(int i=0;i<rules.Length;i++)DrawBriefingRule(g,rules[i],i);
        }
        g.Restore(saved);

        DrawSoulsRule(g,247,510,506);
        bool ready=briefingClock>=(briefingRound==5?120:30);
        DrawSoulsKey(g,new(352,535,66,28),"ENTER",ready);
        DrawSoulsKey(g,new(427,535,66,28),"SPACE",ready);
        Label(g,bossTalkPhase==3?"전투 시작":"대화로 돌아가기",new(511,534,192,30),15,ready?Paper:Steel,false);
        Label(g,"소개 화면에서는 전투와 체력 감소가 멈춥니다.",new(250,572,500,20),10,Steel,true,false);
    }
    private void DrawFinalBossDraw(Graphics g)
    {
        string[] names={"(진) 파괴옥","잿불 골렘","앵콜 인형"};
        int selected=(int)(briefingClock/8)%names.Length;
        for(int i=0;i<names.Length;i++)
        {
            float y=165+i*103;
            bool highlighted=i==selected;
            PanelSurface(g,new(367,y,589,88),Gold,highlighted);
            Label(g,$"0{i+1}",new(388,y+26,52,31),16,highlighted?Gold:Steel);
            SoulsHeading(g,names[i],new(456,y+17,342,50),28,highlighted?Paper:Steel,false);
            Label(g,highlighted?"선택 중":"",new(812,y+28,119,26),12,Gold);
        }
    }
    private void DrawBriefingRule(Graphics g,BriefingRule rule,int index)
    {
        bool compact=briefingRound==2;
        float x=367,y=155+index*(compact?65:82);
        PanelSurface(g,new(x,y,589,compact?60:76),Gold);
        using(var edge=new Pen(Color.FromArgb(158,rule.Color),2))g.DrawLine(edge,x+1,y+12,x+1,y+(compact?48:61));
        DrawSoulsKey(g,new(x+15,y+(compact?16:23),69,30),rule.Key,true);
        Label(g,rule.Title,new(x+99,y+(compact?4:10),472,27),compact?14.5f:16,rule.Color,false);
        Label(g,rule.Detail,new(x+99,y+(compact?29:39),472,25),compact?11:12,Steel,false,false);
    }
}
