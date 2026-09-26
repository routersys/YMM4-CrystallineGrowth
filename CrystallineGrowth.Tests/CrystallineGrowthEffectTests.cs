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

    [Theory]
    [InlineData(CrystallineGrowthQuality.Balanced, 192, 1200)]
    [InlineData(CrystallineGrowthQuality.High, 288, 2048)]
    [InlineData(CrystallineGrowthQuality.Ultra, 384, 3072)]
    public void QualitySettingsMatchSpecification(CrystallineGrowthQuality quality, int resolution, int maxSteps)
    {
        var settings = CrystallineGrowthSettings.GetQuality(quality);

        Assert.Equal(resolution, settings.GridResolution);
        Assert.Equal(maxSteps, settings.MaxSteps);
    }

    [Theory]
    [InlineData(1920, 1080, 288)]
    [InlineData(1080, 1920, 288)]
    [InlineData(8, 8, 288)]
    [InlineData(4096, 16, 192)]
    [InlineData(100, 100, 384)]
    public void GridSizeCoversCanvas(int width, int height, int resolution)
    {
        var (gridWidth, gridHeight, cellSize) = CrystallineGrowthSettings.GetGridSize(width, height, resolution);

        Assert.True(gridWidth >= CrystallineGrowthSettings.MinimumGridSize);
        Assert.True(gridHeight >= CrystallineGrowthSettings.MinimumGridSize);
        Assert.True(cellSize > 0f);
        Assert.True(gridWidth * cellSize >= width);
        Assert.True(gridHeight * cellSize * CrystallineGrowthSettings.RowStep >= height);
    }

    [Theory]
    [InlineData(0f, 1200, 192)]
    [InlineData(10f, 1200, 368)]
    [InlineData(60f, 2048, 1568)]
    [InlineData(500f, 2048, 2048)]
    [InlineData(500f, 1200, 1200)]
    public void StepCountScalesWithReachAndRespectsCap(float reachCells, int maxSteps, int expected)
    {
        Assert.Equal(expected, CrystallineGrowthSettings.GetStepCount(reachCells, maxSteps));
    }

    [Fact]
    public void ParameterMappingsAreMonotonicAndBounded()
    {
        Assert.Equal(CrystallineGrowthSettings.MinimumVaporDensity, CrystallineGrowthSettings.GetVaporDensity(0f), 5);
        Assert.Equal(CrystallineGrowthSettings.MaximumVaporDensity, CrystallineGrowthSettings.GetVaporDensity(1f), 5);
        Assert.Equal(CrystallineGrowthSettings.MinimumVaporDensity, CrystallineGrowthSettings.GetVaporDensity(-5f), 5);
        Assert.Equal(CrystallineGrowthSettings.MaximumVaporDensity, CrystallineGrowthSettings.GetVaporDensity(5f), 5);
        Assert.True(CrystallineGrowthSettings.GetVaporDensity(0.75f) > CrystallineGrowthSettings.GetVaporDensity(0.25f));

        Assert.Equal(CrystallineGrowthSettings.MinimumBeta, CrystallineGrowthSettings.GetAttachmentThreshold(0f), 5);
        Assert.Equal(CrystallineGrowthSettings.MaximumBeta, CrystallineGrowthSettings.GetAttachmentThreshold(1f), 5);
        Assert.True(CrystallineGrowthSettings.GetAttachmentThreshold(0.75f) > CrystallineGrowthSettings.GetAttachmentThreshold(0.25f));

        Assert.Equal(0f, CrystallineGrowthSettings.GetNoiseSigma(0f), 7);
        Assert.Equal(CrystallineGrowthSettings.MaximumSigma, CrystallineGrowthSettings.GetNoiseSigma(1f), 7);
        Assert.Equal(0f, CrystallineGrowthSettings.GetNoiseSigma(-1f), 7);
        Assert.Equal(CrystallineGrowthSettings.MaximumSigma, CrystallineGrowthSettings.GetNoiseSigma(2f), 7);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 3, 2)]
    [InlineData(256, 128, 8)]
    [InlineData(257, 16, 9)]
    public void JumpFloodPassCountCoversLongSide(int width, int height, int expected)
    {
        Assert.Equal(expected, CrystallineGrowthSettings.GetJumpFloodPassCount(width, height));
    }

    [Fact]
    public void TransparentInputYieldsTransparentOutput()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 64;
        const int height = 64;
        var source = new int[width * height];
        var destination = new int[source.Length];
        Array.Fill(destination, -1);
        var parameters = CreateParameters();

        pipeline.Process(source, destination, width, height, in parameters);

        Assert.All(destination, pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void FreezeZeroYieldsTransparentOutput()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 96;
        const int height = 96;
        var source = CreateSquareSource(width, height, 32, 32, 32, 32);
        var destination = new int[source.Length];
        Array.Fill(destination, -1);
        var parameters = CreateParameters(freeze: 0f);

        pipeline.Process(source, destination, width, height, in parameters);

        Assert.All(destination, pixel => Assert.Equal(0, pixel));
    }

    [Fact]
    public void FullFreezeProducesFrost()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var destination = new int[source.Length];
        var parameters = CreateParameters();

        pipeline.Process(source, destination, width, height, in parameters);

        Assert.True(CountLitPixels(destination) > 0);
    }

    [Fact]
    public void GpuPipelineIsDeterministic()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var first = new int[source.Length];
        var second = new int[source.Length];
        var parameters = CreateParameters(branching: 0.8f, noise: 0.5f, seed: 42);

        pipeline.Process(source, first, width, height, in parameters);
        pipeline.Process(source, second, width, height, in parameters);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentCrystals()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var first = new int[source.Length];
        var second = new int[source.Length];

        var parametersA = CreateParameters(noise: 0.5f, seed: 1);
        var parametersB = CreateParameters(noise: 0.5f, seed: 2);
        pipeline.Process(source, first, width, height, in parametersA);
        pipeline.Process(source, second, width, height, in parametersB);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void OutputAlphaStaysPremultipliedAndBounded()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var destination = new int[source.Length];
        var parameters = CreateParameters(frost: 1f, refraction: 1f, specular: 1f);

        pipeline.Process(source, destination, width, height, in parameters);

        foreach (var pixel in destination)
        {
            var alpha = (pixel >> 24) & 255;
            Assert.InRange((pixel >> 16) & 255, 0, alpha);
            Assert.InRange((pixel >> 8) & 255, 0, alpha);
            Assert.InRange(pixel & 255, 0, alpha);
        }
    }

    [Fact]
    public void FreezeIncreasesLitArea()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var partial = new int[source.Length];
        var full = new int[source.Length];

        var partialParameters = CreateParameters(freeze: 0.2f);
        var fullParameters = CreateParameters(freeze: 1f);
        pipeline.Process(source, partial, width, height, in partialParameters);
        pipeline.Process(source, full, width, height, in fullParameters);

        Assert.True(CountLitPixels(full) > CountLitPixels(partial));
    }

    [Fact]
    public void ReachLimitsFrostExtent()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 192;
        const int height = 192;
        var source = CreateSquareSource(width, height, 80, 80, 32, 32);
        var shortReach = new int[source.Length];
        var longReach = new int[source.Length];

        var shortParameters = CreateParameters(reachPixels: 12f);
        var longParameters = CreateParameters(reachPixels: 48f);
        pipeline.Process(source, shortReach, width, height, in shortParameters);
        pipeline.Process(source, longReach, width, height, in longParameters);

        var shortMaxY = MaxLitY(shortReach, width, height);
        var longMaxY = MaxLitY(longReach, width, height);
        Assert.True(shortMaxY > 0);
        Assert.True(longMaxY > shortMaxY);
        Assert.True(shortMaxY <= 80 + 32 + 12 + 8);
    }

    [Fact]
    public void GpuPipelineDoesNotAllocateManagedMemoryAfterWarmup()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 64;
        const int height = 64;
        var source = CreateSquareSource(width, height, 24, 24, 16, 16);
        var destination = new int[source.Length];
        var parameters = CreateParameters();
        pipeline.Process(source, destination, width, height, in parameters);
        pipeline.Process(source, destination, width, height, in parameters);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        pipeline.Process(source, destination, width, height, in parameters);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SharedTexturePipelineMatchesPackedBufferPipeline()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 96;
        const int height = 96;
        var source = CreateSquareSource(width, height, 32, 32, 32, 32);
        var expected = new int[source.Length];
        var parameters = CreateParameters(branching: 0.7f, seed: 11);
        pipeline.Process(source, expected, width, height, in parameters);

        var device = GraphicsDevice.GetDefault();
        using var sourceTexture = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device, width, height);
        using var outputTexture = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device, width, height);
        var sourcePixels = new Bgra32[source.Length];
        for (var index = 0; index < source.Length; index++)
            sourcePixels[index].PackedValue = unchecked((uint)source[index]);
        sourceTexture.CopyFrom(sourcePixels);
        pipeline.ProcessSharedAndWait(sourceTexture, outputTexture, width, height, in parameters);
        var result = new Bgra32[source.Length];
        outputTexture.CopyTo(result);

        for (var index = 0; index < expected.Length; index++)
            Assert.Equal(unchecked((uint)expected[index]), result[index].PackedValue);
    }

    [Fact]
    public void SubmittedSharedTexturePipelineAllocationsAmortizeToZero()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 64;
        const int height = 64;
        var device = GraphicsDevice.GetDefault();
        using var source = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device, width, height);
        using var destination = InteropServices.AllocateSharedReadWriteTexture2D<Bgra32, Float4>(device, width, height);
        var parameters = CreateParameters();
        for (var iteration = 0; iteration < 4; iteration++)
            pipeline.Process(source, destination, width, height, in parameters);
        pipeline.WaitForCompletion();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var minimum = long.MaxValue;
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            pipeline.Process(source, destination, width, height, in parameters);
            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        pipeline.WaitForCompletion();

        Assert.Equal(0, minimum);
    }

    [Theory]
    [InlineData(0.3f)]
    [InlineData(1f)]
    public void VisibleBoundsCoverAllLitPixelsAndMatchFullRender(float freeze)
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 192;
        const int height = 192;
        var source = CreateSquareSource(width, height, 80, 80, 32, 24);
        var full = new int[source.Length];
        var parameters = CreateParameters(freeze: freeze, reachPixels: 32f, seed: 5);
        pipeline.Process(source, full, width, height, in parameters);

        var device = GraphicsDevice.GetDefault();
        using var sourceTexture = device.AllocateReadWriteTexture2D<Bgra32, Float4>(width, height);
        var sourcePixels = new Bgra32[source.Length];
        for (var index = 0; index < source.Length; index++)
            sourcePixels[index].PackedValue = unchecked((uint)source[index]);
        sourceTexture.CopyFrom(sourcePixels);

        pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in parameters);
        Assert.True(pipeline.TryGetVisibleBounds(width, height, in parameters, out var rect));
        Assert.True(rect.Width > 0 && rect.Height > 0);
        Assert.True(rect.X >= 0 && rect.Y >= 0);
        Assert.True(rect.X + rect.Width <= width && rect.Y + rect.Height <= height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (full[y * width + x] == 0)
                    continue;
                Assert.InRange(x, rect.X, rect.X + rect.Width - 1);
                Assert.InRange(y, rect.Y, rect.Y + rect.Height - 1);
            }
        }

        using var outputTexture = device.AllocateReadWriteTexture2D<Bgra32, Float4>(rect.Width, rect.Height);
        pipeline.RenderVisible(sourceTexture, outputTexture, width, height, 0, 0, width, height, rect, in parameters);
        var result = new Bgra32[rect.Width * rect.Height];
        outputTexture.CopyTo(result);

        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                var expected = unchecked((uint)full[(rect.Y + y) * width + rect.X + x]);
                Assert.Equal(expected, result[y * rect.Width + x].PackedValue);
            }
        }
    }

    [Fact]
    public void SimulateCachesStructureUntilInputsChange()
    {
        using var pipeline = CrystallineGrowthPipeline.TryCreate();
        if (pipeline is null)
        {
            Assert.Skip("Direct3D 12 is unavailable.");
            return;
        }

        const int width = 128;
        const int height = 128;
        var source = CreateSquareSource(width, height, 48, 48, 32, 32);
        var device = GraphicsDevice.GetDefault();
        using var sourceTexture = device.AllocateReadWriteTexture2D<Bgra32, Float4>(width, height);
        var sourcePixels = new Bgra32[source.Length];
        for (var index = 0; index < source.Length; index++)
            sourcePixels[index].PackedValue = unchecked((uint)source[index]);
        sourceTexture.CopyFrom(sourcePixels);

        var parameters = CreateParameters(seed: 3);
        Assert.True(pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in parameters));
        Assert.False(pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in parameters));

        var freezeChanged = parameters with { Freeze = 0.5f };
        Assert.False(pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in freezeChanged));

        var seedChanged = parameters with { Seed = 4 };
        Assert.True(pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in seedChanged));

        var movedSource = CreateSquareSource(width, height, 32, 32, 32, 32);
        for (var index = 0; index < movedSource.Length; index++)
            sourcePixels[index].PackedValue = unchecked((uint)movedSource[index]);
        sourceTexture.CopyFrom(sourcePixels);
        Assert.True(pipeline.Simulate(sourceTexture, width, height, 0, 0, width, height, in seedChanged));
    }

    [Fact]
    public void Direct2DInteropProducesFrostFromOpaqueCore()
    {
        using var devices = new GraphicsDevices();
        using var graphicsContext = devices.CreateContext();
        using var interop = CrystallineGrowthGpuInterop.TryCreate(graphicsContext);
        if (interop is null)
        {
            Assert.Skip("Direct3D 11 and Direct3D 12 sharing is unavailable.");
            return;
        }

        using var pipeline = CrystallineGrowthPipeline.TryCreate(interop.Device);
        Assert.NotNull(pipeline);

        const int width = 96;
        const int height = 96;
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

        Assert.True(interop.EnsureResources(width, height));
        var bounds = new RawRectF(0f, 0f, width, height);
        var parameters = CreateParameters();
        for (var iteration = 0; iteration < 2; iteration++)
        {
            interop.RenderInput(inputBitmap, bounds);
            interop.BeginCompute();
            try
            {
                pipeline!.Process(interop.SourceTexture, interop.OutputTexture, width, height, in parameters);
            }
            finally
            {
                interop.EndCompute();
            }
        }
        interop.WaitForIdle();

        using var staging = graphicsContext.DeviceContext.CreateBitmap(
            new SizeI(width, height),
            new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96f,
                96f,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
        staging.CopyFromBitmap(interop.OutputBitmap);
        var mapped = staging.Map(MapOptions.Read);
        try
        {
            var lit = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
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

    private static int CountLitPixels(int[] pixels)
    {
        var count = 0;
        foreach (var pixel in pixels)
        {
            if (((pixel >> 24) & 255) > 8)
                count++;
        }
        return count;
    }

    private static int MaxLitY(int[] pixels, int width, int height)
    {
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                if (((pixels[y * width + x] >> 24) & 255) >= 32)
                    return y;
            }
        }
        return 0;
    }
}
