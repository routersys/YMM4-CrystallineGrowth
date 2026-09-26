using System.Runtime.InteropServices;
using ComputeWeave;
using ComputeWeave.Interop;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using PixelFormat = Vortice.DCommon.PixelFormat;

namespace CrystallineGrowth.Tests;

public sealed class CrystallineGrowthEffectTests
{
    private static double ValueAt(YukkuriMovieMaker.Commons.Animation animation) => animation.GetValue(0, 1, 30);

    private static CrystallineGrowthPipeline.Parameters CreateParameters(
        CrystallineGrowthQuality quality = CrystallineGrowthQuality.Balanced,
        float freeze = 1f,
        float branching = 0.6f,
        float facet = 0.4f,
        float reachPixels = 40f,
        float noise = 0.25f,
        float frost = 0.7f,
        float refraction = 0.4f,
        float specular = 0.5f,
        int seed = 0)
        => new(quality, freeze, branching, facet, reachPixels, noise, frost, refraction, specular, 0.8f, 0.89f, 1f, seed);

    [Fact]
    public void DefaultParameterValuesMatchSpecification()
    {
        var effect = new CrystallineGrowthEffect();

        Assert.Equal(100d, ValueAt(effect.Amount), 6);
        Assert.Equal(100d, ValueAt(effect.Freeze), 6);
        Assert.Equal(60d, ValueAt(effect.Branching), 6);
        Assert.Equal(40d, ValueAt(effect.Facet), 6);
        Assert.Equal(25d, ValueAt(effect.Reach), 6);
        Assert.Equal(25d, ValueAt(effect.Noise), 6);
        Assert.Equal(70d, ValueAt(effect.Frost), 6);
        Assert.Equal(40d, ValueAt(effect.Refraction), 6);
        Assert.Equal(50d, ValueAt(effect.Specular), 6);
        Assert.Equal(CrystallineGrowthQuality.High, effect.Quality);
        Assert.Equal(0, effect.Seed);
        Assert.Equal(System.Windows.Media.Color.FromArgb(255, 205, 228, 255), effect.IceColor);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1234, 1234)]
    public void SeedClampsNegativeInputToZero(int input, int expected)
    {
        var effect = new CrystallineGrowthEffect { Seed = input };

        Assert.Equal(expected, effect.Seed);
    }

    [Fact]
    public void CreateExoVideoFiltersReturnsEmpty()
    {
        var effect = new CrystallineGrowthEffect();

        Assert.Empty(effect.CreateExoVideoFilters(0, null!));
    }

    [Fact]
    public void Direct2DInteropProducesFrostAfterGrowingFullHdOutput()
    {
        using var devices = new GraphicsDevices();
        using var graphicsContext = devices.CreateContext();
        using var scheduler = ComputeExternalQueueScheduler.Create();
        using var provider = CrystallineGrowthInteropProvider.TryCreate(graphicsContext, scheduler, out var interopDevice);
        if (provider is null || interopDevice is null)
        {
            Assert.Skip("Direct3D 11 and Direct3D 12 sharing is unavailable.");
            return;
        }

        using var domain = interopDevice.RegisterExternalDomain(provider);
        using var resourceSet = CrystallineGrowthResourceSet.Create(interopDevice, domain);
        using var pipeline = CrystallineGrowthPipeline.TryCreate(interopDevice);
        Assert.NotNull(pipeline);

        const int width = 96;
        const int height = 96;
        const int fullHdWidth = 1920;
        const int fullHdHeight = 1080;
        var pixels = CreateSquareSource(width, height, 32, 32, 32, 32);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        using var inputBitmap = graphicsContext.DeviceContext.CreateBitmap(
            new SizeI(width, height),
            new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.None));
        try
        {
            inputBitmap.CopyFromMemory(handle.AddrOfPinnedObject(), width * sizeof(int));
        }
        finally
        {
            handle.Free();
        }

        Assert.True(resourceSet.TryEnsureSource(width, height, out _));
        var parameters = CreateParameters();
        var renderContext = provider.RenderContext;
        CrystallineGrowthPipeline.PixelRect visible = default;
        for (var iteration = 0; iteration < 2; iteration++)
        {
            using (var borrow = resourceSet.BeginSourceExternalOperation())
            {
                var previousTarget = renderContext.Target;
                using var sourceBitmap = new ID2D1Bitmap1(borrow.DangerousGetView().AddRefBitmap());
                renderContext.Target = sourceBitmap;
                renderContext.BeginDraw();
                renderContext.Clear(null);
                renderContext.DrawImage(
                    inputBitmap,
                    new System.Numerics.Vector2(0f, 0f),
                    null,
                    InterpolationMode.NearestNeighbor,
                    CompositeMode.SourceCopy);
                renderContext.EndDraw();
                renderContext.Target = previousTarget;
            }

            pipeline!.Simulate(
                resourceSet.GetSourceComputeBinding(), width, height, 0, 0, width, height, in parameters);
            Assert.True(pipeline.TryGetVisibleBounds(width, height, in parameters, out visible));
            Assert.True(resourceSet.TryEnsureOutput(
                iteration == 0 ? visible.Width : fullHdWidth,
                iteration == 0 ? visible.Height : fullHdHeight,
                out _));
            pipeline.RenderVisible(
                resourceSet.GetSourceComputeBinding(), resourceSet.GetOutputComputeBinding(), width, height, 0, 0, width, height, visible, in parameters);

            if (iteration == 0)
            {
                using var retiredLease = resourceSet.AcquireOutputExternalViewLease();
                Assert.Equal(visible.Width, retiredLease.Width);
                Assert.Equal(visible.Height, retiredLease.Height);
            }
        }

        using var outputLease = resourceSet.AcquireOutputExternalViewLease();
        Assert.Equal(fullHdWidth, outputLease.Width);
        Assert.Equal(fullHdHeight, outputLease.Height);
        using var staging = graphicsContext.DeviceContext.CreateBitmap(
            new SizeI(visible.Width, visible.Height),
            new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
        using var outputBitmap = new ID2D1Bitmap1(outputLease.DangerousGetView().AddRefBitmap());
        staging.CopyFromBitmap(Vortice.Mathematics.Int2.Zero, outputBitmap, new RectI(0, 0, visible.Width, visible.Height));
        var mapped = staging.Map(MapOptions.Read);
        try
        {
            var lit = 0;
            for (var y = 0; y < visible.Height; y++)
            {
                for (var x = 0; x < visible.Width; x++)
                {
                    var actual = Marshal.ReadInt32(mapped.Bits + (nint)(y * mapped.Pitch + x * sizeof(int)));
                    var alpha = (actual >> 24) & 255;
                    Assert.InRange((actual >> 16) & 255, 0, alpha);
                    Assert.InRange((actual >> 8) & 255, 0, alpha);
                    Assert.InRange(actual & 255, 0, alpha);
                    if (alpha > 0)
                        lit++;
                }
            }
            Assert.True(lit > 0);
        }
        finally
        {
            staging.Unmap();
        }
    }

    private static int[] CreateSquareSource(int width, int height, int left, int top, int squareWidth, int squareHeight)
    {
        var source = new int[width * height];
        for (var y = top; y < top + squareHeight; y++)
        {
            for (var x = left; x < left + squareWidth; x++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;
                source[y * width + x] = unchecked((int)0xFFC0C0C0);
            }
        }
        return source;
    }
}
