using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private readonly Image?[] bossAttackIllustrations=new Image?[6];
    private Bitmap? previewClone;
    private static readonly string[] BossAttackFiles={"BossAttackIsangjin.png","BossAttackPurgae.png","BossAttackRodian.png","BossAttackTruePurgae.png","BossAttackGolem.png","BossAttackDoll.png"};
    private static readonly string[] BossAttackCaptions={"부채꼴 탄환","돌 구슬 · 메테오","탈덕안돼 레이저","시간정지 · 돌 탄환","화염 분출","붉은 줄 · 음표"};
    private void LoadBossAttackIllustrations()
    {
        for(int i=0;i<BossAttackFiles.Length;i++)bossAttackIllustrations[i]=LoadImage(BossAttackFiles[i]);
    }
    private void DisposeShowcaseAssets()
    {
        foreach(var image in bossAttackIllustrations)image?.Dispose();
        Array.Clear(bossAttackIllustrations);previewClone?.Dispose();previewClone=null;
    }
    private void DrawBossAttackIllustration(Graphics g,RectangleF bounds)
    {
        int index=briefingRound==5?3+FinalBossKind:briefingRound-2;
        if((uint)index>=bossAttackIllustrations.Length)return;
        Image? image=bossAttackIllustrations[index];
        if(image==null)return;
        var saved=g.Save();g.SetClip(bounds,CombineMode.Intersect);
        float zoom=reducedEffects?1:1.025f+MathF.Sin(briefingClock*.009f)*.01f;
        float scale=Math.Max(bounds.Width/image.Width,bounds.Height/image.Height)*zoom;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.DrawImage(image,bounds.X+(bounds.Width-image.Width*scale)/2,bounds.Y+(bounds.Height-image.Height*scale)/2,image.Width*scale,image.Height*scale);
        using(var shade=new LinearGradientBrush(new RectangleF(bounds.X,bounds.Bottom-54,bounds.Width,54),Color.Transparent,Color.FromArgb(231,7,7,8),90))
            g.FillRectangle(shade,bounds.X,bounds.Bottom-54,bounds.Width,54);
        StoryLabel(g,"대표 공격  ·  "+BossAttackCaptions[index],new(bounds.X+9,bounds.Bottom-28,bounds.Width-18,23),13,Paper);
        g.Restore(saved);
    }
    private void PreviewSprite(Graphics g,Image? image,RectangleF bounds,float opacity=1)
    {
        if(image==null)return;
        if(opacity>=.999f)g.DrawImage(image,bounds);
        else
        {
            using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=Math.Clamp(opacity,0,1)});
            g.DrawImage(image,Rectangle.Round(bounds),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
        }
    }
    private Bitmap? PreviewGreenClone()
    {
        if(previewClone!=null||motionFrames.Length==0)return previewClone;
        var source=motionFrames[0];previewClone=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(previewClone);using var attributes=new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[]{new[]{.10f,.28f,.11f,0,0},new[]{.20f,.56f,.22f,0,0},new[]{.04f,.10f,.04f,0,0},new[]{0f,0,0,1f,0},new[]{.015f,.14f,.025f,0,1f}}));
        g.DrawImage(source,new Rectangle(0,0,source.Width,source.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        return previewClone;
    }
    private static readonly string[] ActivePreviewCaptions={"","흡수 → 스파크 볼","문워크 · 반사 가능","장애물 → 체력 회복","적만 느려집니다","반사 → 유령 옆차기","불 쿠나이 → 취약"};
    private static readonly string[] PassivePreviewCaptions={"","5회 반사 → 흡혈","위기 → 50% 회복","초록 클론 · 150%","정확한 반사 → 잔향","","반사 → Q 대기 −1초"};
    private void DrawSkillPreview(Graphics g,RectangleF bounds,bool active,int id)
    {
        // Illustrative loops share real game sprites/materials, but never invoke
        // combat updates, random generators, sound, damage or skill-state setters.
        var saved=g.Save();g.SetClip(bounds,CombineMode.Intersect);g.TranslateTransform(bounds.X,bounds.Y);
        g.ScaleTransform(bounds.Width/264,bounds.Height/65);
        using(var background=new LinearGradientBrush(new RectangleF(0,0,264,65),Color.FromArgb(245,10,13,18),Color.FromArgb(242,23,20,20),0f))g.FillRectangle(background,0,0,264,65);
        float clock=reducedEffects?48:cardClock,u=(clock%150)/150;
        int pose=motionFrames.Length==0?0:(int)(clock/8)%motionFrames.Length;
        Image? hero=motionFrames.Length>0?motionFrames[pose]:player1;
        RectangleF body=new(25,8,39,51);
        Color tint=active?id switch{1=>Color.FromArgb(102,222,204),2=>Color.FromArgb(155,206,234),3=>Color.FromArgb(147,221,139),4=>Color.FromArgb(79,208,255),5=>Color.FromArgb(83,149,255),_=>CinderOrange}:id==3?Color.FromArgb(111,231,143):Gold;
        using(var ground=new Pen(Color.FromArgb(58,tint),1))g.DrawLine(ground,8,59,256,59);
        bool arrowShot=(active&&id is 1 or 5 or 6)||(!active&&id is 1 or 4);
        if(arrowShot)PreviewSprite(g,enemy3,new(113,13,28,44),.75f);
        if(active&&id==5&&furiosoFireBack.Length>0&&furiosoFireBack[0]!=null)
            PreviewSprite(g,furiosoFireBack[(int)(clock/3)%32],new(11,-9,66,74),.7f);
        if(active&&id==6&&cinderGolemArt.Count>0)hero=cinderGolemArt[pose%cinderGolemArt.Count];
        if(active&&id==2)
        {
            body.X+=MathF.Sin(u*MathF.Tau)*5;
            if(moonwalkPoses.Length==6)hero=moonwalkPoses[(int)(clock/7)%6];
        }
        if(!active&&id==3)
        {
            PreviewSprite(g,hero,body,Math.Clamp(1-u*2,0,.75f));
            PreviewSprite(g,PreviewGreenClone(),new(48+u*18,8,39,51),Math.Clamp(u*2,0,1));
        }
        else PreviewSprite(g,hero,body,1);
        if(active&&id==3)
        {
            float x=126-u*61;PreviewSprite(g,enemy2,new(x,29,26*(1-u*.5f),28*(1-u*.5f)),1-u*.6f);
            using var straw=new Pen(Color.FromArgb(212,192,152),2.3f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawBezier(straw,58,23,69,21,70,30,83,33);
            PreviewRestore(g,83,13,u,tint);
        }
        if(active&&id==4)
        {
            using var hitbox=new Pen(tint,1){DashStyle=DashStyle.Dash};g.DrawRectangle(hitbox,31,14,25,41);
            for(int n=0;n<3;n++){float x=114-((u*.48f+n*.29f)%1)*43;PreviewOrb(g,x,22+n*9,2.7f,tint);}
            using var scan=new Pen(Color.FromArgb(45,tint),.7f);for(int y=1;y<65;y+=5)g.DrawLine(scan,0,y,264,y);
        }
        else if(active&&id==2)
            for(int n=0;n<3;n++){float x=128-((u+n*.26f)%1)*99;PreviewOrb(g,x,17+n*11,3,Coral);}
        else if(arrowShot)
        {
            if(active&&id==1&&u<.5f)
                for(int n=0;n<3;n++){float x=124-((u*2+n*.25f)%1)*60;PreviewOrb(g,x,18+n*10,3,tint);}
            else
            {
                float travel=active&&id==1?(u-.5f)*2:u;
                float x=64+Math.Max(0,travel)*63;
                if((active&&id==5)||(!active&&id==4))
                {
                    var frames=active?echoPoseFrames:preciseEchoPoseFrames;
                    Image? kick=frames.Length>0?frames[Math.Min(frames.Length-1,(int)(u*frames.Length))]:phantomKick;
                    PreviewSprite(g,kick,new(x-12,14,58,37),.9f);
                }
                else if(active&&id==6&&cinderKunaiFrames[0]!=null)
                    PreviewSprite(g,cinderKunaiFrames[(int)(clock/3)%16],new(x-9,16,49,25));
                else PreviewOrb(g,x,32,active?7:4,tint);
            }
        }
        if(!active&&id is 1 or 2)
        {
            if(id==1)for(int i=0;i<5;i++){using var p=new SolidBrush(i<1+(int)(u*5)?tint:Color.FromArgb(40,tint));g.FillEllipse(p,75+i*9,12,4,4);}
            PreviewRestore(g,48,30,u,tint);
            if(id==1)DrawKissHeart(g,114-u*64,25+MathF.Sin(u*MathF.PI)*9,5,MathF.Sin(u*MathF.PI),Color.FromArgb(235,123,178));
        }
        if(!active&&id==6)
        {
            for(int i=0;i<8;i++)
            {
                float t=(u+i*.125f)%1,x=47+MathF.Cos(i*2.4f)*(44*(1-t)),y=31+MathF.Sin(i*2.4f)*(28*(1-t));
                g.FillEllipse(ParticleBrush(Color.FromArgb(180,206,212),(int)(80*MathF.Sin(t*MathF.PI))),x,y,5,3);
            }
            StoryLabel(g,"Q  −1초",new(78,22,67,24),12,tint);
        }
        string caption=(active?ActivePreviewCaptions:PassivePreviewCaptions)[id];
        StoryLabel(g,caption,new(144,15,116,37),13,tint,true);
        if(active&&id==6)StoryLabel(g,"취약 +"+(1+(int)(u*3)),new(151,42,105,18),10,Steel,true);
        g.Restore(saved);
    }
    private void PreviewOrb(Graphics g,float x,float y,float radius,Color color)
    {
        using var path=new GraphicsPath();path.AddEllipse(x-radius*2,y-radius*2,radius*4,radius*4);
        using var glow=new PathGradientBrush(path){CenterColor=Color.FromArgb(150,color),SurroundColors=new[]{Color.Transparent}};g.FillPath(glow,path);
        using var core=new SolidBrush(color);g.FillEllipse(core,x-radius,y-radius,radius*2,radius*2);
    }
    private static void PreviewRestore(Graphics g,float x,float y,float u,Color tint)
    {
        using var p=new Pen(Color.FromArgb((int)(220*MathF.Sin(u*MathF.PI)),tint),1.7f);
        g.DrawLine(p,x,y-5-u*9,x,y+5-u*9);g.DrawLine(p,x-5,y-u*9,x+5,y-u*9);
    }
}
