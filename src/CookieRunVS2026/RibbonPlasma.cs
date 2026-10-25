using System.Drawing.Imaging;
namespace CookieRunVS2026;
public sealed partial class GamePanel
{
    private unsafe bool PrepareRibbonPlasma()
    {
        string path=AssetPath("RibbonLaserHD.png");if(!File.Exists(path))return false;
        using var sheet=new Bitmap(path);var keys=new Bitmap[4];var data=new BitmapData[4];
        try
        {
            for(int i=0;i<4;i++)
            {
                // The authored rows have different transparent margins. Align
                // the actual luminous axes; a blind quarter-sheet crop pulls
                // the neighbouring row into the top of a beam.
                float[] axes={192/1086f,465/1086f,757/1086f,996/1086f};
                float center=axes[i]*sheet.Height,half=114/1086f*sheet.Height;
                float cropTop=Math.Max(0,center-half),cropBottom=Math.Min(sheet.Height,center+half);
                keys[i]=new Bitmap(PlasmaWidth,PlasmaHeight,PixelFormat.Format32bppPArgb);
                using(var kg=System.Drawing.Graphics.FromImage(keys[i]))
                {
                    kg.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    float left=160/1448f*sheet.Width;
                    kg.DrawImage(sheet,new RectangleF(0,(cropTop-center+half)/(half*2)*PlasmaHeight,PlasmaWidth,(cropBottom-cropTop)/(half*2)*PlasmaHeight),new RectangleF(left,cropTop,sheet.Width-left,cropBottom-cropTop),GraphicsUnit.Pixel);
                }
                data[i]=keys[i].LockBits(new(0,0,PlasmaWidth,PlasmaHeight),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            }
            Parallel.For(0,PlasmaFrames,beamWorkers,f=>
            {
                var blue=new Bitmap(PlasmaWidth,PlasmaHeight,PixelFormat.Format32bppPArgb);
                var pink=new Bitmap(PlasmaWidth,PlasmaHeight,PixelFormat.Format32bppPArgb);
                var bd=blue.LockBits(new(0,0,PlasmaWidth,PlasmaHeight),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                var pd=pink.LockBits(new(0,0,PlasmaWidth,PlasmaHeight),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                float phase=f/(float)PlasmaFrames*MathF.Tau,key=f/10f;int a=(int)key,b=(a+1)%4;float blend=SmoothUnit(key-a);
                try{for(int y=0;y<PlasmaHeight;y++)for(int x=0;x<PlasmaWidth;x++)
                {
                    float u=x/(float)(PlasmaWidth-1),v=y/(float)(PlasmaHeight-1)*2-1;
                    float sx=Math.Clamp(x+MathF.Sin(u*8-phase)*MathF.Sin(u*MathF.PI)*8,0,PlasmaWidth-1.001f);
                    float throat=.20f+.80f*SmoothUnit(u/.055f);
                    float sy=(y-PlasmaHeight*.5f)/throat+PlasmaHeight*.5f+MathF.Sin(u*11-phase*2)*2.4f+MathF.Sin(u*29+phase*3)*.65f;
                    if(sy<0||sy>=PlasmaHeight-1)continue;
                    int ix=(int)sx,iy=(int)sy;float fx=sx-ix,fy=sy-iy;
                    byte* pa=(byte*)data[a].Scan0+iy*data[a].Stride+ix*4,pb=(byte*)data[b].Scan0+iy*data[b].Stride+ix*4;
                    float Channel(byte* p,int stride,int k)=>Mix(Mix(p[k],p[k+4],fx),Mix(p[k+stride],p[k+stride+4],fx),fy);
                    float alpha=Mix(Channel(pa,data[a].Stride,3),Channel(pb,data[b].Stride,3),blend)/255;
                    float head=1-.065f*(1-MathF.Sqrt(Math.Max(0,1-Math.Min(1,v*v))));
                    float feather=SmoothUnit(u/.007f)*(1-SmoothUnit((u-head+.015f)/.015f))*(1-SmoothUnit((MathF.Abs(v)-.93f)/.07f));
                    if(alpha<.003f||feather<.001f)continue;
                    float red=Mix(Channel(pa,data[a].Stride,2),Channel(pb,data[b].Stride,2),blend)/(255*alpha);
                    float green=Mix(Channel(pa,data[a].Stride,1),Channel(pb,data[b].Stride,1),blend)/(255*alpha);
                    float cyan=Mix(Channel(pa,data[a].Stride,0),Channel(pb,data[b].Stride,0),blend)/(255*alpha);
                    float value=Math.Clamp(Math.Max(red,Math.Max(green,cyan)),0,1);
                    float white=SmoothUnit((Math.Min(red,Math.Min(green,cyan))-.59f)/.39f);
                    float opacity=alpha*feather;byte* bo=(byte*)bd.Scan0+y*bd.Stride+x*4,po=(byte*)pd.Scan0+y*pd.Stride+x*4;
                    // Warm foil from the reference is cooled to saturated blue;
                    // dark navy folds and pale core remain separate materials.
                    bo[0]=(byte)(Mix(24+214*value,255,white)*opacity);bo[1]=(byte)(Mix(9+111*value,249,white)*opacity);bo[2]=(byte)(Mix(2+20*value,224,white)*opacity);bo[3]=(byte)(255*opacity);
                    po[0]=(byte)(Mix(16+140*value,252,white)*opacity);po[1]=(byte)(Mix(4+46*value,234,white)*opacity);po[2]=(byte)(Mix(24+230*value,255,white)*opacity);po[3]=(byte)(255*opacity);
                }}finally{blue.UnlockBits(bd);pink.UnlockBits(pd);}
                BakeInnerPlasmaGrains(blue,f,true);BakeInnerPlasmaGrains(pink,f,false);apotheosisBlue[f]=blue;apotheosisPink[f]=pink;
            });
        }
        finally{for(int i=0;i<4;i++){if(data[i]!=null)keys[i].UnlockBits(data[i]);keys[i]?.Dispose();}}
        return true;
    }
}
