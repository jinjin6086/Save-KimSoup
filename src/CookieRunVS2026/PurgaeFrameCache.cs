using System.Buffers;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CookieRunVS2026;

public sealed partial class GamePanel
{
    private sealed record PurgaePackedFrame(Bitmap Image,Rectangle Bounds);
    private readonly PurgaePackedFrame[][] purgaeMotionFrames={Array.Empty<PurgaePackedFrame>(),Array.Empty<PurgaePackedFrame>(),Array.Empty<PurgaePackedFrame>()};
    private PurgaePackedFrame[] purgaeShinyOrbs=Array.Empty<PurgaePackedFrame>();
    private Bitmap? trueStopStandSprite;
    private const int PurgaeOrbFrames=48,PurgaeOrbSize=112;
    // Predecoded, premultiplied, tightly bounded images. No large PNG source
    // rectangles, color conversion or rotating high-resolution art at runtime.
    private static PurgaePackedFrame[] ReadPurgaeFrames(string path,int expectedSize,int expectedCount)
    {
        using var file=File.OpenRead(path);using var zip=new BrotliStream(file,CompressionMode.Decompress);
        using var r=new BinaryReader(zip);
        if(r.ReadInt32()!=0x31464150||r.ReadInt32()!=expectedSize||r.ReadInt32()!=expectedCount)throw new InvalidDataException("Invalid Purgae frame bundle");
        var frames=new PurgaePackedFrame[expectedCount];byte[] buffer=ArrayPool<byte>.Shared.Rent(expectedSize*expectedSize*4);
        try
        {
            for(int i=0;i<frames.Length;i++)
            {
                var bounds=new Rectangle(r.ReadInt16(),r.ReadInt16(),r.ReadInt16(),r.ReadInt16());
                if(bounds.X<0||bounds.Y<0||bounds.Width<1||bounds.Height<1||bounds.Right>expectedSize||bounds.Bottom>expectedSize)throw new InvalidDataException("Invalid Purgae frame bounds");
                int stride=bounds.Width*4,length=stride*bounds.Height;zip.ReadExactly(buffer.AsSpan(0,length));
                var image=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb);
                var pixels=image.LockBits(new(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                try{for(int y=0;y<image.Height;y++)Marshal.Copy(buffer,y*stride,pixels.Scan0+y*pixels.Stride,stride);}
                finally{image.UnlockBits(pixels);}
                frames[i]=new(image,bounds);
            }
            return frames;
        }
        catch{foreach(var frame in frames)frame?.Image.Dispose();throw;}
        finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
    private void LoadPurgaeOrbs()
    {
        if(purgaeShinyOrbs.Length>0)return;
        string path=AssetPath("PurgaeShinyOrbs.paf");if(File.Exists(path))purgaeShinyOrbs=ReadPurgaeFrames(path,PurgaeOrbSize,PurgaeOrbFrames*3);
    }
    private void PrepareTimeStopStand()
    {
        if(trueStopStandSprite!=null||enemy4==null)return;
        trueStopStandSprite=new Bitmap(160,160,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(trueStopStandSprite);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        using var alpha=new ImageAttributes();var matrix=new ColorMatrix{Matrix33=.55f};alpha.SetColorMatrix(matrix);
        g.DrawImage(enemy4,new Rectangle(0,0,160,160),0,0,enemy4.Width,enemy4.Height,GraphicsUnit.Pixel,alpha);
    }
    private static int PurgaeFrameIndex(int from,int to,int frame)
    {
        if(frame<=0)return from;
        if(frame>=PurgaeTweenFrames-1)return to;
        int edge=Array.IndexOf(PurgaeEdges,(from,to));return edge<0?from:8+edge*(PurgaeTweenFrames-2)+frame-1;
    }
}
