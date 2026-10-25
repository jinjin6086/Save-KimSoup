using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CookieRunVS2026;

/// <summary>Small package-free D3D11 compositor. All COM ownership stays in this class.</summary>
internal sealed unsafe class GpuVfxDevice : IDisposable
{
    private const uint Bgra8 = 87;
    private const uint ShaderResource = 0x8, RenderTarget = 0x20;
    private const uint CpuWrite = 0x10000, CpuRead = 0x20000;
    private readonly object gate = new();
    private nint device, context, swapChain, backBuffer, output, outputView;
    private nint sceneTexture, sceneView, vertexShader, pixelShader, sampler, constantsBuffer, rasterizer, captureTexture;
    private nint blueBeamTexture, blueBeamView, pinkBeamTexture, pinkBeamView;
    private nint collisionTexture, collisionView, detonationTexture, detonationView;
    private int width, height, sceneWidth, sceneHeight;
    private bool disposed, hasFrame;

    public string BackendName { get; private set; } = "Direct3D 11";
    public bool IsHardware { get; private set; }

    public GpuVfxDevice(IntPtr hwnd, int width, int height, string shaderPath)
    {
        if (hwnd == IntPtr.Zero) throw new ArgumentException("A valid control HWND is required.", nameof(hwnd));
        this.width = Math.Max(1, width);
        this.height = Math.Max(1, height);
        try
        {
            CreateDevice(hwnd);
            CreateShaders(File.ReadAllText(shaderPath), shaderPath);
            CreatePipelineResources();
            CreateOutputResources();
        }
        catch { Dispose(); throw; }
    }

    /// <summary>Uploads authored animation slices once; the caller retains ownership of the bitmaps.</summary>
    public void SetBeamTextures(IReadOnlyList<Bitmap> blue, IReadOnlyList<Bitmap> pink)
    {
        ArgumentNullException.ThrowIfNull(blue);
        ArgumentNullException.ThrowIfNull(pink);
        lock (gate)
        {
            ThrowIfDisposed();
            nint nextBlueTexture = 0, nextBlueView = 0, nextPinkTexture = 0, nextPinkView = 0;
            try
            {
                CreateBeamTextureArray(blue, "blue", out nextBlueTexture, out nextBlueView);
                CreateBeamTextureArray(pink, "pink", out nextPinkTexture, out nextPinkView);
                nint* empty = stackalloc nint[2];
                empty[0] = 0; empty[1] = 0;
                ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[8])(context, 1, 2, empty);
                Release(ref blueBeamView); Release(ref blueBeamTexture);
                Release(ref pinkBeamView); Release(ref pinkBeamTexture);
                blueBeamTexture = nextBlueTexture; nextBlueTexture = 0;
                blueBeamView = nextBlueView; nextBlueView = 0;
                pinkBeamTexture = nextPinkTexture; nextPinkTexture = 0;
                pinkBeamView = nextPinkView; nextPinkView = 0;
            }
            finally
            {
                Release(ref nextPinkView); Release(ref nextPinkTexture);
                Release(ref nextBlueView); Release(ref nextBlueTexture);
            }
        }
    }

    /// <summary>Uploads the authored collision animation once, without taking bitmap ownership.</summary>
    public void SetCollisionTextures(IReadOnlyList<Bitmap> contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        lock (gate)
        {
            ThrowIfDisposed();
            nint nextTexture = 0, nextView = 0;
            try
            {
                CreateBeamTextureArray(contact, "collision", out nextTexture, out nextView);
                nint empty = 0;
                ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[8])(context, 3, 1, &empty);
                Release(ref collisionView); Release(ref collisionTexture);
                collisionTexture = nextTexture; nextTexture = 0;
                collisionView = nextView; nextView = 0;
            }
            finally { Release(ref nextView); Release(ref nextTexture); }
        }
    }

    public void SetDetonationTextures(IReadOnlyList<Bitmap> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        lock (gate)
        {
            ThrowIfDisposed();nint nextTexture=0,nextView=0;
            try
            {
                CreateBeamTextureArray(frames,"detonation",out nextTexture,out nextView);
                nint empty=0;
                ((delegate* unmanaged[Stdcall]<nint,uint,uint,nint*,void>)Vtable(context)[8])(context,4,1,&empty);
                Release(ref detonationView);Release(ref detonationTexture);
                detonationTexture=nextTexture;nextTexture=0;detonationView=nextView;nextView=0;
            }
            finally{Release(ref nextView);Release(ref nextTexture);}
        }
    }

    public void Render(Bitmap scene, ReadOnlySpan<float> constants)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (constants.Length > 64) throw new ArgumentException("VfxConstants contains at most 16 float4 values.", nameof(constants));
        lock (gate)
        {
            ThrowIfDisposed();
            UploadScene(scene);
            Span<float> padded = stackalloc float[64];
            padded.Clear();
            constants.CopyTo(padded);
            fixed (float* data = padded)
                ((delegate* unmanaged[Stdcall]<nint, nint, uint, void*, void*, uint, uint, void>)Vtable(context)[48])
                    (context, constantsBuffer, 0, null, data, 0, 0);

            nint view = outputView;
            ((delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)Vtable(context)[33])(context, 1, &view, 0);
            var viewport = new Viewport { Width = width, Height = height, MaxDepth = 1 };
            ((delegate* unmanaged[Stdcall]<nint, uint, Viewport*, void>)Vtable(context)[44])(context, 1, &viewport);
            ((delegate* unmanaged[Stdcall]<nint, nint, void>)Vtable(context)[43])(context, rasterizer);
            ((delegate* unmanaged[Stdcall]<nint, nint, void>)Vtable(context)[17])(context, 0); // SV_VertexID: no layout or vertex buffer.
            ((delegate* unmanaged[Stdcall]<nint, uint, void>)Vtable(context)[24])(context, 4); // TRIANGLELIST
            ((delegate* unmanaged[Stdcall]<nint, nint, nint*, uint, void>)Vtable(context)[11])(context, vertexShader, null, 0);
            ((delegate* unmanaged[Stdcall]<nint, nint, nint*, uint, void>)Vtable(context)[9])(context, pixelShader, null, 0);
            nint resource = sceneView;
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[8])(context, 0, 1, &resource);
            nint* beamResources = stackalloc nint[4];
            beamResources[0] = blueBeamView; beamResources[1] = pinkBeamView; beamResources[2] = collisionView;
            beamResources[3]=detonationView;
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[8])(context, 1, 4, beamResources);
            nint sampleState = sampler;
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[10])(context, 0, 1, &sampleState);
            nint constantState = constantsBuffer;
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[16])(context, 0, 1, &constantState);
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, void>)Vtable(context)[13])(context, 3, 0);

            // Keep output independent of Present: capture never reads a discarded swap-chain buffer.
            ((delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)Vtable(context)[33])(context, 0, null, 0);
            ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Vtable(context)[47])(context, backBuffer, output);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, int>)Vtable(swapChain)[8])(swapChain, 0, 0), "IDXGISwapChain.Present");
            hasFrame = true;
        }
    }

    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0) return;
        lock (gate)
        {
            ThrowIfDisposed();
            if (width == newWidth && height == newHeight) return;
            ((delegate* unmanaged[Stdcall]<nint, uint, nint*, nint, void>)Vtable(context)[33])(context, 0, null, 0);
            Release(ref outputView);
            Release(ref output);
            Release(ref backBuffer);
            Release(ref captureTexture);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, uint, uint, uint, int>)Vtable(swapChain)[13])
                (swapChain, 0, (uint)newWidth, (uint)newHeight, Bgra8, 0), "IDXGISwapChain.ResizeBuffers");
            width = newWidth;
            height = newHeight;
            hasFrame = false;
            CreateOutputResources();
        }
    }

    /// <summary>Returns the last complete GPU output. Synchronous readback is performed only on request.</summary>
    public Bitmap CaptureFrame()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!hasFrame) throw new InvalidOperationException("Render a frame before capturing it.");
            if (captureTexture == 0)
            {
                var desc = TextureDescription(width, height, 3, 0, CpuRead); // STAGING
                nint created = 0;
                Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void*, nint*, int>)Vtable(device)[5])
                    (device, &desc, null, &created), "Create capture texture");
                captureTexture = created;
            }
            ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Vtable(context)[47])(context, captureTexture, output);
            MappedSubresource mapped;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedSubresource*, int>)Vtable(context)[14])
                (context, captureTexture, 0, 1, 0, &mapped), "Map capture texture"); // READ waits for the GPU copy.
            Bitmap? frame = null;
            try
            {
                frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var bits = frame.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < height; y++)
                        Buffer.MemoryCopy((byte*)mapped.Data + (nuint)y * mapped.RowPitch,
                            (byte*)bits.Scan0 + y * bits.Stride, Math.Abs(bits.Stride), width * 4L);
                }
                finally { frame.UnlockBits(bits); }
                return frame;
            }
            catch { frame?.Dispose(); throw; }
            finally
            {
                ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Vtable(context)[15])(context, captureTexture, 0);
            }
        }
    }

    private void CreateDevice(nint hwnd)
    {
        var desc = new SwapChainDesc
        {
            BufferDesc = new ModeDesc { Width = (uint)width, Height = (uint)height, Format = Bgra8 },
            SampleDesc = new SampleDesc { Count = 1 },
            BufferUsage = RenderTarget,
            BufferCount = 2,
            OutputWindow = hwnd,
            Windowed = 1,
            SwapEffect = 0 // DISCARD, supported by both hardware and WARP.
        };
        uint level = 0xb000; // Shader Model 5 requires feature level 11_0.
        uint chosen = 0;
        nint createdDevice = 0, createdContext = 0, createdSwap = 0;
        int hr = D3D11CreateDeviceAndSwapChain(0, 1, 0, 0x20, &level, 1, 7, &desc,
            &createdSwap, &createdDevice, &chosen, &createdContext);
        IsHardware = hr >= 0;
        if (hr < 0)
        {
            Release(ref createdContext); Release(ref createdDevice); Release(ref createdSwap);
            hr = D3D11CreateDeviceAndSwapChain(0, 5, 0, 0x20, &level, 1, 7, &desc,
                &createdSwap, &createdDevice, &chosen, &createdContext);
        }
        // Assign even on failure so constructor cleanup also covers partially created resources.
        device = createdDevice; context = createdContext; swapChain = createdSwap;
        Check(hr, "D3D11CreateDeviceAndSwapChain (hardware then WARP)");
        BackendName = IsHardware ? $"Direct3D 11 GPU · {AdapterName()}" : "Direct3D 11 WARP · CPU software rasterizer";

        // Prevent the legacy DXGI Alt+Enter handler from entering exclusive fullscreen.
        var factoryId = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        nint factory = 0;
        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtable(swapChain)[6])
                (swapChain, &factoryId, &factory), "Get DXGI factory");
            Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, int>)Vtable(factory)[8])
                (factory, hwnd, 2), "Disable exclusive-fullscreen shortcut");
        }
        finally { Release(ref factory); }
    }

    private string AdapterName()
    {
        nint dxgiDevice = 0, adapter = 0;
        try
        {
            var id = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtable(device)[0])(device, &id, &dxgiDevice), "Query IDXGIDevice");
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtable(dxgiDevice)[7])(dxgiDevice, &adapter), "Get DXGI adapter");
            AdapterDesc desc;
            Check(((delegate* unmanaged[Stdcall]<nint, AdapterDesc*, int>)Vtable(adapter)[8])(adapter, &desc), "Get DXGI adapter description");
            return new string(desc.Description).TrimEnd('\0');
        }
        finally { Release(ref adapter); Release(ref dxgiDevice); }
    }

    private void CreateShaders(string source, string sourcePath)
    {
        byte[] vsCode = ShaderBytes(source, sourcePath, "VSMain", "vs_5_0");
        byte[] psCode = ShaderBytes(source, sourcePath, "PSMain", "ps_5_0");
        fixed(byte* vs=vsCode)
        fixed(byte* ps=psCode)
        {
            nint created = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, void*, nuint, nint, nint*, int>)Vtable(device)[12])
                (device, vs, (nuint)vsCode.Length, 0, &created), "Create vertex shader");
            vertexShader = created; created = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, void*, nuint, nint, nint*, int>)Vtable(device)[15])
                (device, ps, (nuint)psCode.Length, 0, &created), "Create pixel shader");
            pixelShader = created;
        }
    }

    // Release builds carry the exact optimization-level-3 bytecode beside the
    // readable source. Validate its source hash before reuse; an edited/missing
    // cache safely recompiles instead of silently rendering a stale effect.
    private static byte[] ShaderBytes(string source,string sourcePath,string entry,string profile)
    {
        string cache=Path.ChangeExtension(sourcePath,profile.StartsWith("vs",StringComparison.Ordinal)?"vs.cso":"ps.cso");
        byte[] digest=SHA256.HashData(Encoding.UTF8.GetBytes(source));
        try
        {
            byte[] bytes=File.ReadAllBytes(cache);
            if(bytes.Length>40&&bytes.Length<16*1024*1024&&bytes.AsSpan(0,8).SequenceEqual("GPUVFX01"u8)&&bytes.AsSpan(8,32).SequenceEqual(digest))
                return bytes[40..];
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
        nint code=Compile(source,sourcePath,entry,profile);
        try
        {
            byte[] bytes=new byte[checked((int)BlobSize(code))];
            Marshal.Copy((nint)BlobData(code),bytes,0,bytes.Length);return bytes;
        }
        finally{Release(ref code);}
    }

    private static nint Compile(string source, string sourcePath, string entry, string profile)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        nint code = 0, errors = 0;
        try
        {
            fixed (byte* data = bytes)
            {
                int hr = D3DCompile(data, (nuint)bytes.Length, sourcePath, 0, 0, entry, profile,
                    (1u << 11) | (1u << 15), 0, &code, &errors); // strictness + optimization level 3
                if (hr < 0)
                {
                    string details = errors == 0 ? "No compiler diagnostics." :
                        Marshal.PtrToStringAnsi((nint)BlobData(errors), checked((int)BlobSize(errors))) ?? "";
                    throw new InvalidOperationException($"HLSL {entry} compilation failed (0x{hr:X8}): {details}");
                }
            }
            nint result = code; code = 0; return result;
        }
        finally { Release(ref code); Release(ref errors); }
    }

    private void CreatePipelineResources()
    {
        var bufferDesc = new BufferDesc { ByteWidth = 16 * 16, BindFlags = 4 };
        nint created = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, BufferDesc*, void*, nint*, int>)Vtable(device)[3])
            (device, &bufferDesc, null, &created), "Create VFX constant buffer");
        constantsBuffer = created; created = 0;
        var sampleDesc = new SamplerDesc
        {
            Filter = 0x15, // MIN_MAG_MIP_LINEAR
            AddressU = 3, AddressV = 3, AddressW = 3, // CLAMP
            MaxAnisotropy = 1, ComparisonFunc = 1, MaxLod = float.MaxValue
        };
        Check(((delegate* unmanaged[Stdcall]<nint, SamplerDesc*, nint*, int>)Vtable(device)[23])
            (device, &sampleDesc, &created), "Create linear scene sampler");
        sampler = created; created = 0;
        var rasterDesc = new RasterizerDesc { FillMode = 3, CullMode = 1, DepthClipEnable = 1 };
        Check(((delegate* unmanaged[Stdcall]<nint, RasterizerDesc*, nint*, int>)Vtable(device)[22])
            (device, &rasterDesc, &created), "Create fullscreen rasterizer");
        rasterizer = created;
    }

    private void CreateOutputResources()
    {
        var textureId = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        nint created = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)Vtable(swapChain)[9])
            (swapChain, 0, &textureId, &created), "Get swap-chain back buffer");
        backBuffer = created; created = 0;
        var desc = TextureDescription(width, height, 0, RenderTarget, 0);
        Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void*, nint*, int>)Vtable(device)[5])
            (device, &desc, null, &created), "Create persistent GPU output");
        output = created; created = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)Vtable(device)[9])
            (device, output, null, &created), "Create GPU output render-target view");
        outputView = created;
    }

    private void UploadScene(Bitmap scene)
    {
        if (scene.Width != sceneWidth || scene.Height != sceneHeight)
        {
            nint empty = 0;
            ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, void>)Vtable(context)[8])(context, 0, 1, &empty);
            Release(ref sceneView); Release(ref sceneTexture);
            var desc = TextureDescription(scene.Width, scene.Height, 2, ShaderResource, CpuWrite); // DYNAMIC
            nint created = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void*, nint*, int>)Vtable(device)[5])
                (device, &desc, null, &created), "Create CPU scene upload texture");
            sceneTexture = created; created = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, void*, nint*, int>)Vtable(device)[7])
                (device, sceneTexture, null, &created), "Create scene shader-resource view");
            sceneView = created;
            sceneWidth = scene.Width; sceneHeight = scene.Height;
        }

        MappedSubresource mapped;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedSubresource*, int>)Vtable(context)[14])
            (context, sceneTexture, 0, 4, 0, &mapped), "Map CPU scene upload"); // WRITE_DISCARD
        try
        {
            var bits = scene.LockBits(new Rectangle(0, 0, sceneWidth, sceneHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < sceneHeight; y++)
                    Buffer.MemoryCopy((byte*)bits.Scan0 + y * bits.Stride,
                        (byte*)mapped.Data + (nuint)y * mapped.RowPitch, mapped.RowPitch, sceneWidth * 4L);
            }
            finally { scene.UnlockBits(bits); }
        }
        finally { ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Vtable(context)[15])(context, sceneTexture, 0); }
    }

    private void CreateBeamTextureArray(IReadOnlyList<Bitmap> frames, string name, out nint texture, out nint view)
    {
        texture = 0; view = 0;
        if (frames.Count is < 1 or > 2048)
            throw new ArgumentException($"The {name} animation must contain 1 to 2048 frames.", nameof(frames));
        Bitmap first = frames[0] ?? throw new ArgumentException($"The {name} animation contains a null frame.", nameof(frames));
        int frameWidth = first.Width, frameHeight = first.Height;
        if (frameWidth > 16384 || frameHeight > 16384)
            throw new ArgumentException($"The {name} frame exceeds the D3D11 texture size limit.", nameof(frames));
        var locked = new Dictionary<Bitmap, BitmapData>(ReferenceEqualityComparer.Instance);
        var initialData = new SubresourceData[frames.Count];
        try
        {
            // Initial data remains locked until CreateTexture2D has copied every array slice.
            for (int i = 0; i < frames.Count; i++)
            {
                Bitmap frame = frames[i] ?? throw new ArgumentException($"The {name} animation contains a null frame.", nameof(frames));
                if (frame.Width != frameWidth || frame.Height != frameHeight)
                    throw new ArgumentException($"Every {name} animation frame must have the same dimensions.", nameof(frames));
                // Preserve PArgb radiance; repeated references can reuse the same locked pixels.
                if (!locked.TryGetValue(frame, out var bits))
                {
                    bits = frame.LockBits(new Rectangle(0, 0, frameWidth, frameHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    locked.Add(frame, bits);
                }
                if (bits.Stride <= 0)
                    throw new ArgumentException($"The {name} animation requires top-down BGRA bitmap rows.", nameof(frames));
                initialData[i] = new SubresourceData
                {
                    Data = bits.Scan0, RowPitch = (uint)bits.Stride,
                    SlicePitch = checked((uint)(bits.Stride * frameHeight))
                };
            }
            var desc = TextureDescription(frameWidth, frameHeight, 1, ShaderResource, 0); // IMMUTABLE
            desc.ArraySize = (uint)frames.Count;
            nint created = 0;
            fixed (SubresourceData* data = initialData)
                Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, SubresourceData*, nint*, int>)Vtable(device)[5])
                    (device, &desc, data, &created), $"Create {name} beam texture array");
            texture = created; created = 0;
            // A single-slice input still needs an ARRAY view to match Texture2DArray in HLSL.
            var srvDesc = new ShaderResourceViewDesc
            {
                Format = Bgra8, Dimension = 5, MipLevels = 1, ArraySize = (uint)frames.Count
            };
            Check(((delegate* unmanaged[Stdcall]<nint, nint, ShaderResourceViewDesc*, nint*, int>)Vtable(device)[7])
                (device, texture, &srvDesc, &created), $"Create {name} beam texture-array view");
            view = created;
        }
        catch { Release(ref view); Release(ref texture); throw; }
        finally
        {
            foreach (var entry in locked) entry.Key.UnlockBits(entry.Value);
        }
    }

    private static Texture2DDesc TextureDescription(int width, int height, uint usage, uint bind, uint cpu) => new()
    {
        Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = Bgra8,
        SampleDesc = new SampleDesc { Count = 1 }, Usage = usage, BindFlags = bind, CpuAccessFlags = cpu
    };

    private static void** Vtable(nint instance) => *(void***)instance;
    private static void* BlobData(nint blob) => ((delegate* unmanaged[Stdcall]<nint, void*>)Vtable(blob)[3])(blob);
    private static nuint BlobSize(nint blob) => ((delegate* unmanaged[Stdcall]<nint, nuint>)Vtable(blob)[4])(blob);
    private static void Check(int hr, string operation)
    {
        if (hr < 0) throw new InvalidOperationException($"{operation} failed (HRESULT 0x{hr:X8}).", Marshal.GetExceptionForHR(hr));
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    private static void Release(ref nint instance)
    {
        nint released = instance; instance = 0;
        if (released != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Vtable(released)[2])(released);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            if (context != 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, void>)Vtable(context)[110])(context); // ClearState releases context references.
                ((delegate* unmanaged[Stdcall]<nint, void>)Vtable(context)[111])(context);
            }
            Release(ref captureTexture); Release(ref sceneView); Release(ref sceneTexture);
            Release(ref detonationView); Release(ref detonationTexture);
            Release(ref collisionView); Release(ref collisionTexture);
            Release(ref pinkBeamView); Release(ref pinkBeamTexture);
            Release(ref blueBeamView); Release(ref blueBeamTexture);
            Release(ref outputView); Release(ref output); Release(ref backBuffer);
            Release(ref rasterizer); Release(ref constantsBuffer); Release(ref sampler);
            Release(ref pixelShader); Release(ref vertexShader);
            Release(ref swapChain); Release(ref context); Release(ref device);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct ModeDesc
    { public uint Width, Height; public Rational RefreshRate; public uint Format, ScanlineOrdering, Scaling; }
    [StructLayout(LayoutKind.Sequential)] private struct SampleDesc { public uint Count, Quality; }
    [StructLayout(LayoutKind.Sequential)] private struct SwapChainDesc
    {
        public ModeDesc BufferDesc; public SampleDesc SampleDesc; public uint BufferUsage, BufferCount;
        public nint OutputWindow; public int Windowed; public uint SwapEffect, Flags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Texture2DDesc
    {
        public uint Width, Height, MipLevels, ArraySize, Format; public SampleDesc SampleDesc;
        public uint Usage, BindFlags, CpuAccessFlags, MiscFlags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BufferDesc
    { public uint ByteWidth, Usage, BindFlags, CpuAccessFlags, MiscFlags, StructureByteStride; }
    [StructLayout(LayoutKind.Sequential)] private struct SubresourceData { public nint Data; public uint RowPitch, SlicePitch; }
    [StructLayout(LayoutKind.Sequential)] private struct ShaderResourceViewDesc
    { public uint Format, Dimension, MostDetailedMip, MipLevels, FirstArraySlice, ArraySize; }
    [StructLayout(LayoutKind.Sequential)] private struct MappedSubresource { public nint Data; public uint RowPitch, DepthPitch; }
    [StructLayout(LayoutKind.Sequential)] private struct Viewport { public float TopLeftX, TopLeftY, Width, Height, MinDepth, MaxDepth; }
    [StructLayout(LayoutKind.Sequential)] private struct SamplerDesc
    {
        public uint Filter, AddressU, AddressV, AddressW; public float MipLodBias; public uint MaxAnisotropy, ComparisonFunc;
        public float BorderR, BorderG, BorderB, BorderA, MinLod, MaxLod;
    }
    [StructLayout(LayoutKind.Sequential)] private struct RasterizerDesc
    {
        public uint FillMode, CullMode; public int FrontCounterClockwise, DepthBias;
        public float DepthBiasClamp, SlopeScaledDepthBias;
        public int DepthClipEnable, ScissorEnable, MultisampleEnable, AntialiasedLineEnable;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct AdapterDesc
    {
        public fixed char Description[128]; public uint VendorId, DeviceId, SubSysId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public uint AdapterLuidLow; public int AdapterLuidHigh;
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDeviceAndSwapChain(nint adapter, uint driverType, nint software, uint flags,
        uint* featureLevels, uint featureLevelCount, uint sdkVersion, SwapChainDesc* swapChainDesc,
        nint* swapChain, nint* device, uint* chosenFeatureLevel, nint* immediateContext);

    [DllImport("d3dcompiler_47.dll", ExactSpelling = true, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(void* source, nuint sourceSize, string sourceName,
        nint defines, nint include, string entryPoint, string target, uint flags1, uint flags2, nint* code, nint* errors);
}
