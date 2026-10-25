using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    // The rescued friend remains a separate character throughout the epilogue.
    private Bitmap? endingFriend, endingIdle, endingPrison, endingMeadow, endingHome;
    private int endingScene, endingLine, endingPendingScene=-1;
    private float endingSceneClock, endingLineClock, endingFadeClock, endingInputLock, endingFinalClock;
    private bool endingTextRevealed, endingFinal, endingLockSoundPlayed, endingRescueSoundPlayed;
    private const float EndingLockBreakFrame=118,EndingRescueCompleteFrame=286;
    private bool EndingRescueComplete=>endingScene!=0||endingSceneClock>=EndingRescueCompleteFrame;
    private float EndingCageOpen=>EndingSmooth(Math.Clamp((endingSceneClock-130)/76,0,1));
    private readonly StringFormat endingDialogueFormat=new()
    {
        Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Near,
        Trimming=StringTrimming.None,FormatFlags=StringFormatFlags.LineLimit
    };
    private static readonly string[,] EndingSpeakers=
    {
        {"김스프","나"},
        {"김스프","나"},
        {"김스프","나"},
        {"김스프","나"}
    };
    private static readonly string[,] EndingWords=
    {
        {"와 줬구나.\n여기서 못 나가는 줄 알았어.","늦어서 미안해. 이제 괜찮아.\n같이 집에 가자."},
        {"고마워. 네가 와 줄 줄 알았어.\n그런데 이제 좀 배고프다.","나도 배고파.\n집에 가서 스프 끓여 먹자."},
        {"당근은 내가 썰게.\n감자는 너한테 맡길게.","좋아. 다 넣고 같이 끓이자.\n곧 먹을 수 있겠어."},
        {"정말 맛있다.\n이렇게 같이 먹으니까 더 좋네.","앞으로도 같이 먹자.\n많이 먹어."}
    };
    private static readonly string[] EndingChapterNames={"김스프 구출","집으로 가는 길","저녁 준비","함께하는 저녁"};

    private void LoadRescueEnding()
    {
        using var friend=LoadImage("enemy8.png");
        if(friend is Bitmap bitmap)
            endingFriend=ExtractMainSprite(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height),260,245);
        if(player1 is Bitmap idle)
            endingIdle=ExtractMainSprite(idle,new Rectangle(0,0,idle.Width,idle.Height),160,240);
        endingPrison=LoadEndingBackdrop("World2Prison.png");
        endingMeadow=LoadEndingBackdrop("World1Meadow.png");
        endingHome=LoadEndingBackdrop("EndingHome.png");
        PreparePantingMotion();
    }

    private static Bitmap? LoadEndingBackdrop(string filename)
    {
        string path=AssetPath(filename);
        if(!File.Exists(path))return null;
        using var source=new Bitmap(path);
        var result=new Bitmap(W,H,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(result);
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        float scale=Math.Max((float)W/source.Width,(float)H/source.Height);
        float width=source.Width*scale,height=source.Height*scale;
        g.DrawImage(source,new RectangleF((W-width)/2,(H-height)/2,width,height));
        return result;
    }

    private void DisposeRescueEnding()
    {
        DisposePantingMotion();
        endingFriend?.Dispose();endingIdle?.Dispose();endingPrison?.Dispose();endingMeadow?.Dispose();endingHome?.Dispose();
        endingDialogueFormat.Dispose();
    }

    private void BeginRescueEnding()
    {
        state=GameState.Ending;
        endingScene=endingLine=0;endingPendingScene=-1;
        endingSceneClock=endingLineClock=endingFadeClock=endingFinalClock=0;
        endingTextRevealed=endingFinal=false;endingInputLock=24;
        endingLockSoundPlayed=endingRescueSoundPlayed=false;
        ReleaseInputs();
        projectiles.Clear();risingObstacles.Clear();
        laserActive=dashActive=sparkActive=false;melodyStep=-1;
        cinematicTimer=hitStopTimer=cameraShakeTimer=screenFlashTimer=0;
        audio.CancelLaserCharge();audio.Play("reward");
    }

    private void UpdateRescueEnding(float step)
    {
        endingInputLock=Math.Max(0,endingInputLock-step);
        endingSceneClock+=step;endingLineClock+=step;
        if(endingScene==0&&endingPendingScene<0)
        {
            if(!endingLockSoundPlayed&&endingSceneClock>=EndingLockBreakFrame)
            {
                endingLockSoundPlayed=true;audio.Play("parry");
            }
            if(!endingRescueSoundPlayed&&endingSceneClock>=EndingRescueCompleteFrame)
            {
                endingRescueSoundPlayed=true;audio.Play("reward");
            }
        }
        if(endingFinal)endingFinalClock+=step;
        if(endingPendingScene<0)return;
        endingFadeClock+=step;
        if(endingFadeClock>=24&&endingScene!=endingPendingScene)
        {
            endingScene=endingPendingScene;endingLine=0;
            endingSceneClock=endingLineClock=0;endingTextRevealed=false;
        }
        if(endingFadeClock>=48)
        {
            endingPendingScene=-1;endingFadeClock=0;endingInputLock=8;
        }
    }

    private bool HandleEndingKey(Keys key)
    {
        if(state!=GameState.Ending)return false;
        if(key!=Keys.Enter&&key!=Keys.Space)return false;
        if(endingInputLock>0||endingPendingScene>=0||sceneFadeTimer>0)return true;
        endingInputLock=10;
        if(endingFinal)
        {
            if(endingFinalClock<45)return true;
            state=GameState.Title;ReleaseInputs();return true;
        }
        int count=EndingVisibleCharacters;
        if(count<EndingWords[endingScene,endingLine].Length)
        {
            endingTextRevealed=true;return true;
        }
        if(endingLine==0)
        {
            // Showing the text early must not skip the locked cell or the opening door.
            if(endingScene==0&&endingSceneClock<210)return true;
            endingLine=1;endingLineClock=0;endingTextRevealed=false;return true;
        }
        if(!EndingRescueComplete)return true;
        if(endingScene<3)
        {
            endingPendingScene=endingScene+1;endingFadeClock=0;return true;
        }
        endingFinal=true;endingFinalClock=0;audio.Play("reward");return true;
    }

    private bool HandleRescueEndingClick()
    {
        return HandleEndingKey(Keys.Enter);
    }

    private int EndingVisibleCharacters=>endingTextRevealed?EndingWords[endingScene,endingLine].Length:
        Math.Clamp((int)((endingLineClock-12)/1.8f),0,EndingWords[endingScene,endingLine].Length);

    private void DrawRescueEnding(Graphics g)
    {
        var saved=g.Save();
        g.SmoothingMode=SmoothingMode.AntiAlias;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        switch(endingScene)
        {
            case 0:DrawEndingRescue(g);break;
            case 1:DrawEndingRoadHome(g);break;
            default:DrawEndingKitchen(g);break;
        }
        DrawEndingFrame(g);
        if(endingFinal)DrawEndingFinalCard(g);else DrawEndingDialogue(g);
        if(endingPendingScene>=0)
        {
            float fade=endingFadeClock<24?endingFadeClock/24:1-(endingFadeClock-24)/24;
            using var veil=new SolidBrush(Color.FromArgb((int)(255*Math.Clamp(fade,0,1)),6,5,5));
            g.FillRectangle(veil,0,0,W,H);
        }
        g.Restore(saved);
    }

    private void DrawEndingBackdrop(Graphics g,Image? image,Color fallback,float drift=0,float zoom=1.04f)
    {
        g.Clear(fallback);
        if(image!=null)
        {
            float w=W*zoom,h=H*zoom;
            g.DrawImage(image,new RectangleF((W-w)/2+drift,(H-h)/2,w,h));
        }
        using var edges=new GraphicsPath();edges.AddEllipse(-170,-140,1340,850);
        using var shade=new PathGradientBrush(edges){CenterColor=Color.Transparent,SurroundColors=new[]{Color.FromArgb(185,8,6,8)}};
        g.FillRectangle(shade,0,0,W,H);
    }

    private void DrawEndingRescue(Graphics g)
    {
        DrawEndingBackdrop(g,endingPrison,Color.FromArgb(24,26,30),-Math.Min(endingSceneClock*.012f,5));
        using var calm=new SolidBrush(Color.FromArgb(155,7,11,15));g.FillRectangle(calm,0,0,W,H);
        // The cell is staged as actual foreground scenery, with the friend behind its door.
        using(var pool=new GraphicsPath())
        {
            pool.AddEllipse(380,91,530,377);
            using var light=new PathGradientBrush(pool){CenterColor=Color.FromArgb(82,213,196,153),SurroundColors=new[]{Color.Transparent}};
            g.FillPath(light,pool);
        }
        float arrive=EndingSmooth(Math.Clamp(endingSceneClock/94,0,1));
        float escape=EndingSmooth(Math.Clamp((endingSceneClock-205)/76,0,1));
        float playerX=142+304*arrive,friendX=660-128*escape;
        DrawEndingCellRecess(g);
        DrawEndingShadow(g,playerX+34,418,36,9);
        DrawEndingShadow(g,friendX+47,416,47,9);
        float friendBob=escape is >0 and <1?Math.Abs(MathF.Sin(endingSceneClock*.24f))*5:MathF.Sin(endingSceneClock*.045f)*1.3f;
        DrawEndingFriend(g,new(friendX,319-friendBob,101,96),false);
        DrawEndingCellDoor(g);
        float kick=endingSceneClock-99;
        if(kick>=0&&kick<48)DrawEndingLockKick(g,playerX,kick);
        else DrawEndingRunner(g,playerX,289,75,128,endingSceneClock<94,false,false);
        DrawEndingBrokenLock(g);
        DrawEndingMotes(g,Color.FromArgb(208,193,152),false);
        if(EndingRescueComplete)
        {
            float alpha=Math.Clamp((endingSceneClock-EndingRescueCompleteFrame)/24,0,1);
            Label(g,"김스프 구출 완료",new(340,86,320,34),18,Color.FromArgb((int)(235*alpha),232,211,165));
        }
    }

    private void DrawEndingCellRecess(Graphics g)
    {
        using var cavity=new LinearGradientBrush(new RectangleF(551,129,302,290),Color.FromArgb(238,6,10,13),Color.FromArgb(213,30,34,36),90);
        g.FillRectangle(cavity,551,129,302,290);
        using var stone=new SolidBrush(Color.FromArgb(52,54,54));
        g.FillRectangle(stone,531,124,24,299);g.FillRectangle(stone,850,124,24,299);
        using var mortar=new Pen(Color.FromArgb(19,22,24),2);
        for(int i=0;i<7;i++)
        {
            float y=145+i*42;g.DrawLine(mortar,532,y,554,y);g.DrawLine(mortar,851,y+13,874,y+13);
        }
        using var rearBars=new Pen(Color.FromArgb(70,92,96,96),3);
        for(int i=0;i<8;i++)g.DrawLine(rearBars,574+i*35,143,574+i*35,402);
        using var floor=new LinearGradientBrush(new RectangleF(520,404,372,26),Color.FromArgb(67,63,55),Color.FromArgb(24,25,26),90);
        g.FillPolygon(floor,new[]{new PointF(549,402),new PointF(850,402),new PointF(884,426),new PointF(520,426)});
        using var seam=new Pen(Color.FromArgb(118,149,135,109),1);
        g.DrawLine(seam,548,402,850,402);
        using var castShadow=new Pen(Color.FromArgb(40,3,4,5),7);
        for(int i=0;i<8;i++)g.DrawLine(castShadow,576+i*34,405,509+i*37,437);
    }

    private void DrawEndingCellDoor(Graphics g)
    {
        float opened=EndingCageOpen,angle=opened*1.3f;
        float left=832-262*MathF.Cos(angle),shear=MathF.Sin(angle)*17;
        var topLeft=new PointF(left,151+shear);var topRight=new PointF(832,151);
        var bottomLeft=new PointF(left,406+shear);var bottomRight=new PointF(832,406);
        using var doorShade=new SolidBrush(Color.FromArgb((int)(58*(1-opened*.75f)),7,10,13));
        g.FillPolygon(doorShade,new[]{topLeft,topRight,bottomRight,bottomLeft});
        using var darkFrame=new Pen(Color.FromArgb(11,16,20),11){LineJoin=LineJoin.Round};
        using var steel=new Pen(Color.FromArgb(91,102,107),5){LineJoin=LineJoin.Round};
        using var gleam=new Pen(Color.FromArgb(160,171,163,138),1.1f);
        var corners=new[]{topLeft,topRight,bottomRight,bottomLeft,topLeft};
        g.DrawLines(darkFrame,corners);g.DrawLines(steel,corners);
        for(int i=1;i<9;i++)
        {
            float u=i/9f,x=left+(832-left)*u,dy=shear*(1-u);
            g.DrawLine(darkFrame,x,155+dy,x,402+dy);
            g.DrawLine(steel,x,155+dy,x,402+dy);
            g.DrawLine(gleam,x-1.5f,156+dy,x-1.5f,401+dy);
        }
        for(int i=0;i<3;i++)
        {
            float y=196+i*86;
            g.DrawLine(darkFrame,left,y+shear,832,y);g.DrawLine(steel,left,y+shear,832,y);
            g.DrawLine(gleam,left,y+shear-2,831,y-2);
        }
        using var hinge=new SolidBrush(Color.FromArgb(149,126,86));
        using var hingeShade=new SolidBrush(Color.FromArgb(27,31,34));
        for(int i=0;i<3;i++)
        {
            float y=167+i*106;g.FillRectangle(hingeShade,825,y,15,22);g.FillRectangle(hinge,831,y+2,3,18);
        }
        if(endingSceneClock<EndingLockBreakFrame)DrawEndingPadlock(g,581,316,0,1);
    }

    private void DrawEndingLockKick(Graphics g,float x,float time)
    {
        int pose=time<12?0:time<27?1:2;
        if(selectedCookie==0&&kickFrames.Length==3)
        {
            // The same kicking actor used in combat makes the lock break readable.
            float width=147,shift=pose==1?12:0;
            g.DrawImage(kickFrames[pose],new RectangleF(x-11+shift,270,width,width));
        }
        else
        {
            DrawEndingRunner(g,x,289,75,128,false,false,false);
            float extension=pose==1?49:23;
            using var leg=new Pen(Color.FromArgb(235,161,182),18){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(leg,x+43,372,x+70+extension,340);
        }
    }

    private static void DrawEndingPadlock(Graphics g,float x,float y,float rotation,float alpha)
    {
        var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(rotation);
        int opacity=(int)(255*Math.Clamp(alpha,0,1));
        using var hoop=new Pen(Color.FromArgb(opacity,157,150,130),3);
        using var metal=new LinearGradientBrush(new RectangleF(-9,-1,21,22),Color.FromArgb(opacity,176,138,76),Color.FromArgb(opacity,67,53,34),25);
        g.DrawArc(hoop,-6,-11,13,17,180,180);g.FillRoundRectangle(metal,new RectangleF(-9,-1,21,22),3,3);
        using var keyhole=new SolidBrush(Color.FromArgb(opacity,27,24,20));
        g.FillEllipse(keyhole,-1,6,5,5);g.FillRectangle(keyhole,0,9,3,5);
        g.Restore(saved);
    }

    private void DrawEndingBrokenLock(Graphics g)
    {
        float t=endingSceneClock-EndingLockBreakFrame;
        if(t<0)return;
        if(t<43)
        {
            float fall=Math.Min(t,31);
            DrawEndingPadlock(g,581-fall*1.2f,316+fall*.7f+fall*fall*.075f,-t*5,1-Math.Clamp((t-29)/14,0,1));
        }
        if(t>=40)return;
        int count=reducedEffects?8:25;
        for(int i=0;i<count;i++)
        {
            float angle=-2.8f+i*2.39996f,speed=1.2f+(i%6)*.56f;
            float life=19+i%19,k=Math.Clamp(1-t/life,0,1);
            if(k<=0)continue;
            float x=584+MathF.Cos(angle)*speed*t,y=318+MathF.Sin(angle)*speed*t+t*t*.06f;
            Color color=i%4==0?Color.FromArgb(250,225,174):Color.FromArgb(167,130,75);
            using var shard=new Pen(Color.FromArgb((int)(235*k),color),i%3==0?1.9f:1){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawLine(shard,x,y,x-MathF.Cos(angle)*3,y-MathF.Sin(angle)*3);
        }
    }

    private void DrawEndingRoadHome(Graphics g)
    {
        DrawEndingBackdrop(g,endingMeadow,Color.FromArgb(80,58,48),-12+MathF.Sin(endingSceneClock*.0012f)*10,1.07f);
        using(var dusk=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(80,211,112,56),Color.FromArgb(90,36,22,48),90))g.FillRectangle(dusk,0,0,W,H);
        using(var path=new LinearGradientBrush(new Rectangle(0,363,W,89),Color.FromArgb(150,75,51,39),Color.FromArgb(220,33,26,28),90))g.FillRectangle(path,0,398,W,56);
        float stride=MathF.Sin(endingSceneClock*.26f);
        float x=360+MathF.Sin(endingSceneClock*.006f)*13;
        DrawEndingShadow(g,x+33,417,33,8);DrawEndingShadow(g,x+145,417,40,8);
        DrawEndingRunner(g,x,299+Math.Abs(stride)*2,69,117,true,false,false);
        DrawEndingFriend(g,new(x+107,337+Math.Abs(MathF.Sin(endingSceneClock*.22f))*5,87,81),false);
        // Sparse passing grass in the foreground makes the walk feel continuous.
        using var grass=new Pen(Color.FromArgb(85,16,27,23),2);
        int blades=reducedEffects?8:22;
        for(int i=0;i<blades;i++)
        {
            float gx=((i*63-endingSceneClock*1.7f)%1100+1100)%1100-50;
            g.DrawBezier(grass,gx,441,gx+4,426,gx+5,425,gx+9,430);
        }
        DrawEndingMotes(g,Color.FromArgb(244,205,132),true);
    }

    private void DrawEndingKitchen(Graphics g)
    {
        float drift=MathF.Sin(endingSceneClock*.0014f)*4;
        DrawEndingBackdrop(g,endingHome,Color.FromArgb(55,36,25),drift,1.025f);
        using var warmth=new SolidBrush(Color.FromArgb(28,255,181,99));g.FillRectangle(warmth,0,0,W,H);
        bool meal=endingScene==3;
        float breathe=MathF.Sin(endingSceneClock*.045f)*1.5f;
        DrawEndingShadow(g,374,415,65,13);DrawEndingShadow(g,649,410,66,13);
        DrawEndingRunner(g,322,225+breathe,110,185,false,meal,false);
        DrawEndingFriend(g,new(586,243-breathe,130,122),true);
        if(endingHome!=null)
        {
            // Repaint the actual painted table in front of the seated characters.
            // This preserves the kitchen's material detail and avoids a second table.
            var foreground=g.Save();g.SetClip(new RectangleF(0,355,W,H-355));
            g.DrawImage(endingHome,new RectangleF((W-W*1.025f)/2+drift,(H-H*1.025f)/2,W*1.025f,H*1.025f));
            g.Restore(foreground);
            DrawEndingSteam(g,365,386,19);DrawEndingSteam(g,651,386,19);
            if(!meal)DrawEndingSteam(g,637,211,27);
        }
        else DrawEndingTable(g,meal);
        DrawEndingMotes(g,Color.FromArgb(247,203,140),false);
    }

    private void DrawEndingTable(Graphics g,bool meal)
    {
        var table=new RectangleF(248,357,507,89);
        using(var underside=new SolidBrush(Color.FromArgb(255,42,24,16)))g.FillRectangle(underside,table.X+15,table.Y+27,table.Width-30,78);
        using(var wood=new LinearGradientBrush(table,Color.FromArgb(123,75,42),Color.FromArgb(69,39,24),90))g.FillRoundRectangle(wood,table,12,12);
        using(var rim=new Pen(Color.FromArgb(190,212,159,96),1.4f))g.DrawLine(rim,table.X+11,table.Y+2,table.Right-11,table.Y+2);
        using(var grain=new Pen(Color.FromArgb(55,34,16,8),1))
            for(int i=0;i<5;i++)g.DrawBezier(grain,table.X+9,table.Y+19+i*13,table.X+120,table.Y+8+i*13,table.Right-95,table.Y+27+i*13,table.Right-7,table.Y+18+i*13);
        // Two servings make the rescue and the shared dinner unambiguous.
        DrawEndingBowl(g,365,383,meal?46:33);
        DrawEndingBowl(g,645,383,meal?46:33);
        DrawEndingPot(g,503,meal?385:375,meal?42:59);
        using var spoon=new Pen(Color.FromArgb(214,184,136),3){StartCap=LineCap.Round,EndCap=LineCap.Round};
        g.DrawLine(spoon,315,379,340,388);g.DrawLine(spoon,695,379,674,388);
        if(!meal)
        {
            // An understated stirring motion, driven by scene time rather than input.
            var saved=g.Save();g.TranslateTransform(507,342);g.RotateTransform(MathF.Sin(endingSceneClock*.045f)*12);
            g.DrawLine(spoon,0,21,15,-20);g.Restore(saved);
        }
    }

    private void DrawEndingPot(Graphics g,float x,float y,float radius)
    {
        using var black=new SolidBrush(Color.FromArgb(41,35,31));
        using var metal=new LinearGradientBrush(new RectangleF(x-radius,y-28,radius*2,48),Color.FromArgb(102,89,71),Color.FromArgb(37,32,28),90);
        g.FillRoundRectangle(metal,new RectangleF(x-radius,y-23,radius*2,42),9,9);
        using var handles=new Pen(Color.FromArgb(53,43,34),6);
        g.DrawArc(handles,x-radius-15,y-12,22,18,60,250);g.DrawArc(handles,x+radius-7,y-12,22,18,-120,250);
        g.FillEllipse(black,x-radius,y-28,radius*2,24);
        using var soup=new SolidBrush(Color.FromArgb(213,147,58));g.FillEllipse(soup,x-radius+4,y-25,radius*2-8,18);
        DrawEndingVegetables(g,x,y-17,radius*.7f);
        DrawEndingSteam(g,x,y-28,radius*.7f);
    }

    private void DrawEndingBowl(Graphics g,float x,float y,float radius)
    {
        using var ceramic=new LinearGradientBrush(new RectangleF(x-radius,y-13,radius*2,33),Color.FromArgb(223,198,155),Color.FromArgb(112,83,59),90);
        g.FillPie(ceramic,x-radius,y-21,radius*2,46,0,180);
        using var edge=new SolidBrush(Color.FromArgb(246,223,179));g.FillEllipse(edge,x-radius,y-17,radius*2,24);
        using var soup=new SolidBrush(Color.FromArgb(224,166,74));g.FillEllipse(soup,x-radius+4,y-14,radius*2-8,18);
        DrawEndingVegetables(g,x,y-5,radius*.68f);DrawEndingSteam(g,x,y-17,radius*.5f);
    }

    private void DrawEndingVegetables(Graphics g,float x,float y,float radius)
    {
        for(int i=0;i<6;i++)
        {
            float a=i*2.399f,r=radius*(.25f+(i%3)*.22f);
            Color c=i%3==0?Color.FromArgb(113,143,65):i%3==1?Color.FromArgb(231,109,46):Color.FromArgb(239,211,141);
            using var vegetable=new SolidBrush(c);g.FillEllipse(vegetable,x+MathF.Cos(a)*r-3,y+MathF.Sin(a)*r*.25f-2,6,4);
        }
    }

    private void DrawEndingSteam(Graphics g,float x,float y,float spread)
    {
        int count=reducedEffects?2:4;
        for(int i=0;i<count;i++)
        {
            float t=(endingSceneClock/155+i*.25f)%1;
            float sx=x+(i-(count-1)*.5f)*spread*.55f+MathF.Sin(t*5+i)*4;
            float sy=y-9-t*49;
            using var steam=new Pen(Color.FromArgb((int)(80*MathF.Sin(t*MathF.PI)),255,236,206),2.5f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(steam,sx,sy+15,sx-8,sy+8,sx+9,sy-3,sx+2,sy-14);
        }
    }

    private void DrawEndingRunner(Graphics g,float x,float y,float width,float height,bool walking,bool eating,bool flip)
    {
        var saved=g.Save();
        if(flip){g.TranslateTransform(x+width,y);g.ScaleTransform(-1,1);x=0;y=0;}
        if(selectedCookie==1&&wolfPoses.Length==12)
            DrawWolfPose(g,walking?(int)(endingSceneClock/9)%4:eating?9:8,new(x-width*.2f,y,width*1.4f,height));
        else if(selectedCookie==0&&!walking&&!eating&&endingIdle!=null)
            g.DrawImage(endingIdle,new RectangleF(x,y,width,height));
        else if(selectedCookie==0&&motionFrames.Length>=8)
        {
            int pose=eating?6+(int)(endingSceneClock/30)%2:walking?(int)(endingSceneClock/9)%4:0;
            g.DrawImage(motionFrames[pose],new RectangleF(x,y,width,height));
        }
        else DrawSimpleCookie(g,(int)x,(int)y,(int)width,(int)height);
        g.Restore(saved);
    }

    private void DrawEndingFriend(Graphics g,RectangleF target,bool faceLeft)
    {
        var saved=g.Save();
        if(faceLeft){g.TranslateTransform(target.Right+target.Left,0);g.ScaleTransform(-1,1);}
        if(endingFriend!=null)g.DrawImage(endingFriend,target);
        else
        {
            using var pig=new SolidBrush(Color.FromArgb(213,100,107));g.FillEllipse(pig,target);
            Label(g,"김스프",target,18,Color.FromArgb(255,233,209));
        }
        g.Restore(saved);
    }

    private static void DrawEndingShadow(Graphics g,float x,float y,float rx,float ry)
    {
        using var shadow=new SolidBrush(Color.FromArgb(98,12,8,10));g.FillEllipse(shadow,x-rx,y-ry,rx*2,ry*2);
    }

    private void DrawEndingMotes(Graphics g,Color color,bool traveling)
    {
        int count=reducedEffects?9:25;
        for(int i=0;i<count;i++)
        {
            float x=((i*137.7f+MathF.Sin(i+endingSceneClock*.006f)*14-(traveling?endingSceneClock*.18f:0))%1040+1040)%1040-20;
            float y=106+(i*63.3f)%284+MathF.Sin(endingSceneClock*.012f+i*1.2f)*9;
            int alpha=(int)(14+34*(.5f+.5f*MathF.Sin(i+endingSceneClock*.018f)));
            g.FillEllipse(ParticleBrush(color,alpha),x,y,i%4==0?3:1.8f,i%4==0?3:1.8f);
        }
    }

    private void DrawEndingFrame(Graphics g)
    {
        using var black=new SolidBrush(Color.FromArgb(255,8,7,7));
        g.FillRectangle(black,0,0,W,57);g.FillRectangle(black,0,439,W,161);
        using var line=new Pen(Color.FromArgb(80,202,171,119),.8f);
        g.DrawLine(line,48,57,952,57);g.DrawLine(line,48,439,952,439);
        Label(g,"EPILOGUE",new(48,16,152,23),10,Color.FromArgb(184,154,108),false,false);
        Label(g,EndingChapterNames[endingScene],new(280,12,440,31),17,Color.FromArgb(237,220,187));
        Label(g,$"{endingScene+1:00}  /  04",new(832,16,118,22),10,Color.FromArgb(156,137,111),true,false);
    }

    private void DrawEndingDialogue(Graphics g)
    {
        string text=EndingWords[endingScene,endingLine];int visible=EndingVisibleCharacters;
        bool ready=visible>=text.Length;
        bool waitingForRescue=endingScene==0&&(endingLine==0?endingSceneClock<210:!EndingRescueComplete);
        string hint=waitingForRescue&&ready?"문을 여는 중...":ready?"ENTER / SPACE   다음":"ENTER / SPACE   문장 보기";
        DrawStoryDialogue(g,EndingSpeakers[endingScene,endingLine],text,visible,hint,ready&&!waitingForRescue,endingSceneClock);
    }

    private void DrawEndingFinalCard(Graphics g)
    {
        float t=EndingSmooth(Math.Clamp(endingFinalClock/90,0,1));
        using(var quiet=new SolidBrush(Color.FromArgb((int)(155*t),8,6,5)))g.FillRectangle(quiet,0,57,W,382);
        Color title=Color.FromArgb((int)(255*t),245,226,186),soft=Color.FromArgb((int)(235*t),205,186,151);
        Label(g,"김스프 구출 성공",new(210,160,580,64),37,title);
        Label(g,"무사히 집으로 돌아와 따뜻한 스프를 나눠 먹었다.",new(130,234,740,41),19,soft,true,false);
        using(var rule=new Pen(Color.FromArgb((int)(110*t),204,173,118),1))g.DrawLine(rule,380,302,620,302);
        Label(g,"HAPPY END",new(270,322,460,36),15,title);
        Label(g,$"구출 성공  ·  클리어 시간  {ClearTimeText}",new(150,470,700,31),15,Color.FromArgb(211,188,148));
        Label(g,"긴 하루가 끝나고, 두 사람의 평범한 저녁이 시작됐다.",new(120,513,760,31),16,Color.FromArgb(234,222,199),true,false);
        if(endingFinalClock>=45)Label(g,"ENTER / SPACE   타이틀로",new(270,564,460,23),12,Color.FromArgb(166,148,119),true,false);
    }

    private static float EndingSmooth(float value)=>value*value*(3-2*value);
}
