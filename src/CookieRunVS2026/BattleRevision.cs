using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private bool fullRush,melodyBurstActive;
    private int melodyBurstRemaining,melodyBurstTotal,armorHits;
    private float armorExposed;
    private Image? melodyDash,melodyAngry;
    private static readonly float[] NormalRushA={477,432,452};
    private static readonly float[] NormalRushB={432,477,452};
    private bool DodgeActive=>activeSkill==ActivePower.Dodge&&activeTimer>0;
    private readonly List<AbsorbTrace> absorbTraces=new();
    private sealed class AbsorbTrace{public float X,Y,Life=16;public Color Color;}

    private void ResetNewBattle(){armorHits=0;armorExposed=0;absorbTraces.Clear();}
    private void UpdateNewBattle(float step){armorExposed=Math.Max(0,armorExposed-step);}
    private void UpdateNewPresentation(float step)
    {
        for(int i=absorbTraces.Count-1;i>=0;i--){absorbTraces[i].Life-=step;if(absorbTraces[i].Life<=0)absorbTraces.RemoveAt(i);}
    }
    private float ResolveBossDamage(float damage)
    {
        if(round!=3||!purgaeTransformed)return damage;
        if(++armorHits>=5){armorHits=0;armorExposed=300;audio.Play("perfect");Burst(bossX+65,bossY+65,Gold,28);}
        return damage*(armorExposed>0?1:.5f);
    }
    private void DrawArmorStatus(Graphics g)
    {
        if(round!=3||!purgaeTransformed)return;
        Label(g,armorExposed>0?$"방어 붕괴  {armorExposed/60:0.0}초":$"피해 50% 감소  ·  붕괴 {armorHits}/5",new(603,74,265,16),10,armorExposed>0?Gold:Steel);
    }
    private void BeginMelodyBurst()
    {
        if(!menheraMode){melodyBurstRemaining=1;return;}
        if(!melodyBurstActive){melodyBurstRemaining=rng.Next(2,4);melodyBurstTotal=melodyBurstRemaining;melodyBurstActive=true;}
    }
    private void FinishMelodyBurstBeat()
    {
        melodyStep=-1;
        if(menheraMode&&--melodyBurstRemaining>0){melodySequenceCooldown=18;return;}
        melodyBurstActive=false;melodySequenceCooldown=menheraMode?rng.Next(330,451):rng.Next(440,561);
    }
    private void CaptureAbsorbVisual(Projectile p)
    {
        if(absorbTraces.Count>=24)absorbTraces.RemoveAt(0);
        absorbTraces.Add(new(){X=p.X+p.W*.5f,Y=p.Y+p.H*.5f,Color=p.Type is AttackType.Meteor or AttackType.Fireball?Gold:p.BurnOnHit?Coral:Mint});
    }
    private void DrawAbsorbFlow(Graphics g)
    {
        float handX=px+39,handY=py+28;
        if(activeSkill==ActivePower.Absorb&&activeTimer>0)
        {
            // Fine incoming dust and light at the hand, rather than rotating geometric rings.
            for(int i=0;i<15;i++)
            {
                float t=(animationClock*.025f+i/15f)%1,fall=(1-t)*(1-t),y=MathF.Sin(i*2.4f)*48*fall;
                float x=handX+160*fall,sy=handY+y+MathF.Sin(t*MathF.PI)*12;
                float radius=1.1f+1.2f*(1-t);
                using var streak=new Pen(Color.FromArgb((int)(110*t),174,230,217),1);
                g.DrawLine(streak,x+9*(1-t),sy-y*.08f,x,sy);
                g.FillEllipse(ParticleBrush(Mint,(int)(170*t)),x-radius,sy-radius,radius*2,radius*2);
            }
            using var glow=new SolidBrush(Color.FromArgb(40,173,242,219));g.FillEllipse(glow,handX-10,handY-10,20,20);
            g.FillEllipse(ParticleBrush(Color.White,180),handX-2,handY-2,4,4);
        }
        foreach(var trace in absorbTraces)
        {
            float t=1-trace.Life/16,ease=1-MathF.Pow(1-t,3),x=trace.X+(handX-trace.X)*ease,y=trace.Y+(handY-trace.Y)*ease-MathF.Sin(t*MathF.PI)*20;
            float radius=6*(1-t)+1;
            g.FillEllipse(ParticleBrush(trace.Color,(int)(230*(1-t))),x-radius,y-radius,radius*2,radius*2);
        }
    }
    private void DrawMelodyRushBody(Graphics g,float x,float y,int pass=-1,float slope=float.NaN)
    {
        if(pass<0)pass=dashPass;if(float.IsNaN(slope))slope=dashSlope;
        var image=melodyDash??enemy5;if(image==null)return;
        var saved=g.Save();g.TranslateTransform(x,y);g.RotateTransform(MathF.Atan(slope)*180/MathF.PI);
        if(pass%2==1)g.ScaleTransform(-1,1);
        if(menheraMode&&!reducedEffects)
            for(int i=3;i>=1;i--)DrawReadableSprite(g,image,new RectangleF(-94+i*38,-54,188,108),Color.HotPink,.20f/(i*.65f));
        DrawReadableSprite(g,image,new RectangleF(-94,-54,188,108),Coral);g.Restore(saved);
    }
    private static string AssetPath(string file)
    {
        string a=Path.Combine(AppContext.BaseDirectory,"Assets",file);return File.Exists(a)?a:Path.Combine(Environment.CurrentDirectory,"Assets",file);
    }
    private void LoadCleanMotionAssets()
    {
        string file=AssetPath("PlayerMotion.png");
        if(File.Exists(file))
        {
            using var sheet=new Bitmap(file);motionFrames=new Bitmap[8];int cw=sheet.Width/4,ch=sheet.Height/2;
            for(int i=0;i<8;i++)motionFrames[i]=ExtractMainSprite(sheet,new Rectangle(i%4*cw,i/4*ch,cw,ch),160,200);
        }
        file=AssetPath("MelodyActions.png");
        if(File.Exists(file))
        {
            using var sheet=new Bitmap(file);
            melodyDash=ExtractMainSprite(sheet,new(0,0,(int)(sheet.Width*.56f),sheet.Height),256,200);
            melodyAngry=ExtractMainSprite(sheet,new((int)(sheet.Width*.60f),0,sheet.Width-(int)(sheet.Width*.60f),sheet.Height),180,220);
        }
    }
    // Keep only the actor's connected alpha component in each cell. This removes neighboring shoes and stray pixels.
    private static Bitmap ExtractMainSprite(Bitmap sheet,Rectangle region,int outW,int outH)
    {
        using var cell=new Bitmap(region.Width,region.Height,PixelFormat.Format32bppArgb);
        using(var cg=Graphics.FromImage(cell))cg.DrawImage(sheet,new Rectangle(0,0,cell.Width,cell.Height),region,GraphicsUnit.Pixel);
        int w=cell.Width,h=cell.Height;int[] pixels=new int[w*h],labels=new int[w*h],queue=new int[w*h];
        var data=cell.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<h;y++)Marshal.Copy(data.Scan0+y*data.Stride,pixels,y*w,w);}finally{cell.UnlockBits(data);}
        int label=0,best=0,bestCount=0,minX=w,minY=h,maxX=0,maxY=0;
        for(int index=0;index<pixels.Length;index++)
        {
            if(labels[index]!=0||(uint)pixels[index]>>24<64)continue;
            label++;int head=0,tail=1,lx=w,ly=h,hx=0,hy=0;queue[0]=index;labels[index]=label;
            while(head<tail)
            {
                int v=queue[head++],x=v%w,y=v/w;lx=Math.Min(lx,x);ly=Math.Min(ly,y);hx=Math.Max(hx,x);hy=Math.Max(hy,y);
                void Visit(int n){if(labels[n]==0&&((uint)pixels[n]>>24)>=64){labels[n]=label;queue[tail++]=n;}}
                if(x>0)Visit(v-1);if(x<w-1)Visit(v+1);if(y>0)Visit(v-w);if(y<h-1)Visit(v+w);
            }
            if(tail>bestCount){bestCount=tail;best=label;minX=lx;minY=ly;maxX=hx;maxY=hy;}
        }
        if(bestCount==0)return new Bitmap(outW,outH,PixelFormat.Format32bppPArgb);
        for(int i=0;i<pixels.Length;i++)if(labels[i]!=best)pixels[i]=0;
        data=cell.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w,data.Scan0+y*data.Stride,w);}finally{cell.UnlockBits(data);}
        var result=new Bitmap(outW,outH,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(result))
        {
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;float sw=maxX-minX+1,sh=maxY-minY+1,f=Math.Min((outW-6)/sw,(outH-6)/sh);
            g.DrawImage(cell,new RectangleF((outW-sw*f)/2,outH-sh*f,sw*f,sh*f),new RectangleF(minX,minY,sw,sh),GraphicsUnit.Pixel);
        }
        return result;
    }
}


