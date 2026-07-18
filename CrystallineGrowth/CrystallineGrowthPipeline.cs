using System.Runtime.InteropServices;
using ComputeSharp;

namespace CrystallineGrowth;

internal sealed class CrystallineGrowthPipeline : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly ReadWriteBuffer<int> _scratch;
    private readonly ReadBackBuffer<int> _scratchReadBack;
    private readonly int[] _boundsMinX;
    private readonly int[] _boundsMinY;
    private readonly int[] _boundsMaxX;
    private readonly int[] _boundsMaxY;
    private ReadWriteBuffer<int>? _mask;
    private ReadWriteBuffer<int>? _stateA;
    private ReadWriteBuffer<int>? _stateB;
    private ReadWriteBuffer<int>? _birth;
    private ReadWriteBuffer<int>? _reachMask;
    private ReadWriteBuffer<int>? _jumpFloodA;
    private ReadWriteBuffer<int>? _jumpFloodB;
    private ReadWriteBuffer<float>? _boundaryMass;
    private ReadWriteBuffer<float>? _crystalMass;
    private ReadWriteBuffer<float>? _diffusiveA;
    private ReadWriteBuffer<float>? _diffusiveB;
    private ReadBackBuffer<int>? _birthReadBack;
    private int[]? _cachedBirth;
    private int _cachedMaxBirth;
    private int _cachedAttached;
    private StructureKey? _structureKey;
    private int _gridWidth;
    private int _gridHeight;
    private ReadWriteTexture2D<Bgra32, Float4>? _packedSource;
    private ReadWriteTexture2D<Bgra32, Float4>? _packedOutput;
    private int _packedWidth;
    private int _packedHeight;

    private CrystallineGrowthPipeline(GraphicsDevice device)
    {
        _device = device;
        _scratch = device.AllocateReadWriteBuffer<int>(CrystallineGrowthSettings.ScratchLength);
        _scratchReadBack = device.AllocateReadBackBuffer<int>(CrystallineGrowthSettings.ScratchLength);
        _boundsMinX = new int[CrystallineGrowthSettings.MaximumStepCount + 1];
        _boundsMinY = new int[CrystallineGrowthSettings.MaximumStepCount + 1];
        _boundsMaxX = new int[CrystallineGrowthSettings.MaximumStepCount + 1];
        _boundsMaxY = new int[CrystallineGrowthSettings.MaximumStepCount + 1];
    }

    public static CrystallineGrowthPipeline? TryCreate()
    {
        try
        {
            return new CrystallineGrowthPipeline(GraphicsDevice.GetDefault());
        }
        catch
        {
            return null;
        }
    }

    public static CrystallineGrowthPipeline? TryCreate(GraphicsDevice device)
    {
        try
        {
            return new CrystallineGrowthPipeline(device);
        }
        catch
        {
            return null;
        }
    }

    internal void WaitForCompletion()
    {
        _device.For(1, new FillIntShader(_scratch, 0, 0));
    }

    public void Process(ReadOnlySpan<int> source, Span<int> destination, int width, int height, in Parameters parameters)
    {
        var pixelCount = checked(width * height);
        EnsureGridFor(width, height, parameters.Quality);
        EnsurePackedTextures(width, height);
        var sourceTexture = _packedSource!;
        var outputTexture = _packedOutput!;
        sourceTexture.CopyFrom(MemoryMarshal.Cast<int, Bgra32>(source[..pixelCount]));
        using (ComputeContext context = _device.CreateComputeContext())
            RecordFullPipeline(in context, sourceTexture, outputTexture, width, height, in parameters);
        outputTexture.CopyTo(MemoryMarshal.Cast<int, Bgra32>(destination[..pixelCount]));
    }

    public void Process(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> destination,
        int width,
        int height,
        in Parameters parameters)
    {
        EnsureGridFor(width, height, parameters.Quality);
        using ComputeContext context = _device.CreateComputeContext();
        RecordFullPipeline(in context, source, destination, width, height, in parameters);
        context.Submit();
    }

    internal void ProcessSharedAndWait(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> destination,
        int width,
        int height,
        in Parameters parameters)
    {
        EnsureGridFor(width, height, parameters.Quality);
        using ComputeContext context = _device.CreateComputeContext();
        RecordFullPipeline(in context, source, destination, width, height, in parameters);
    }

    internal bool Simulate(
        ReadWriteTexture2D<Bgra32, Float4> source,
        int canvasWidth,
        int canvasHeight,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        in Parameters parameters)
    {
        EnsureGridFor(canvasWidth, canvasHeight, parameters.Quality);
        var derived = Derive(canvasWidth, canvasHeight, in parameters);
        using (ComputeContext context = _device.CreateComputeContext())
        {
            RecordSilhouetteStage(in context, source, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, in derived);
            RecordMaskHashStage(in context);
        }
        _scratchReadBack.CopyFrom(_scratch);
        var hashed = _scratchReadBack.Span;
        var key = new StructureKey(
            hashed[6],
            hashed[7],
            canvasWidth,
            canvasHeight,
            parameters.Quality,
            parameters.Seed,
            parameters.ReachPixels,
            parameters.Branching,
            parameters.Facet,
            parameters.Noise);
        if (_structureKey == key)
            return false;

        using (ComputeContext context = _device.CreateComputeContext())
            RecordGrowthStage(in context, in derived, in parameters);
        _scratchReadBack.CopyFrom(_scratch);
        var birthReadBack = _birthReadBack!;
        birthReadBack.CopyFrom(_birth!);
        var scratch = _scratchReadBack.Span;
        _cachedAttached = scratch[0];
        _cachedMaxBirth = scratch[1];
        birthReadBack.Span.CopyTo(_cachedBirth!);
        BuildBoundsPrefix();
        _structureKey = key;
        return true;
    }

    internal bool TryGetVisibleBounds(int canvasWidth, int canvasHeight, in Parameters parameters, out PixelRect rect)
    {
        rect = default;
        if (_cachedAttached <= 0 || _cachedMaxBirth < 0)
            return false;
        var visible = parameters.Freeze * (_cachedMaxBirth + 1);
        if (visible <= 0f)
            return false;

        var derived = Derive(canvasWidth, canvasHeight, in parameters);
        var lastStep = Math.Min((int)MathF.Ceiling(visible) - 1, _cachedMaxBirth);
        if (lastStep < 0 || _boundsMinX[lastStep] == int.MaxValue)
            return false;

        var cellSize = derived.CellSize;
        var rowStep = cellSize * CrystallineGrowthSettings.RowStep;
        var padding = (int)MathF.Ceiling(derived.KernelRadius) + 4;
        var left = Math.Clamp(((int)(_boundsMinX[lastStep] * cellSize) - padding) & ~3, 0, canvasWidth);
        var top = Math.Clamp(((int)(_boundsMinY[lastStep] * rowStep) - padding) & ~3, 0, canvasHeight);
        var right = Math.Clamp((int)MathF.Ceiling((_boundsMaxX[lastStep] + 1.5f) * cellSize) + padding, 0, canvasWidth);
        var bottom = Math.Clamp((int)MathF.Ceiling((_boundsMaxY[lastStep] + 1) * rowStep) + padding, 0, canvasHeight);
        var width = Math.Min((right - left + 3) & ~3, canvasWidth - left);
        var height = Math.Min((bottom - top + 3) & ~3, canvasHeight - top);
        if (width <= 0 || height <= 0)
            return false;

        rect = new PixelRect(left, top, width, height);
        return true;
    }

    internal void RenderVisible(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> output,
        int canvasWidth,
        int canvasHeight,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        PixelRect rect,
        in Parameters parameters)
    {
        var derived = Derive(canvasWidth, canvasHeight, in parameters);
        using ComputeContext context = _device.CreateComputeContext();
        RecordRenderStage(in context, source, output, rect, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, in derived, in parameters);
    }

    private void RecordFullPipeline(
        in ComputeContext context,
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in Parameters parameters)
    {
        _structureKey = null;
        var derived = Derive(width, height, in parameters);
        RecordSilhouetteStage(in context, source, 0, 0, width, height, in derived);
        RecordGrowthStage(in context, in derived, in parameters);
        RecordRenderStage(in context, source, output, new PixelRect(0, 0, width, height), 0, 0, width, height, in derived, in parameters);
    }

    private void RecordSilhouetteStage(
        in ComputeContext context,
        ReadWriteTexture2D<Bgra32, Float4> source,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        in DerivedValues derived)
    {
        context.For(_gridWidth, _gridHeight, new SilhouetteShader(
            source, _mask!, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, _gridWidth, _gridHeight, derived.CellSize, 0.05f));
        context.Barrier(_mask!);
    }

    private void RecordMaskHashStage(in ComputeContext context)
    {
        context.For(1, new MaskHashResetShader(_scratch));
        context.Barrier(_scratch);
        context.For(_gridWidth, _gridHeight, new MaskHashShader(_mask!, _scratch, _gridWidth, _gridHeight));
        context.Barrier(_scratch);
    }

    private void RecordGrowthStage(
        in ComputeContext context,
        in DerivedValues derived,
        in Parameters parameters)
    {
        var gridWidth = _gridWidth;
        var gridHeight = _gridHeight;
        var mask = _mask!;
        var birth = _birth!;
        var boundaryMass = _boundaryMass!;
        var crystalMass = _crystalMass!;
        var reachMask = _reachMask!;

        context.For(1, new InitScratchShader(_scratch));
        context.Barrier(_scratch);
        context.For(gridWidth, gridHeight, new SeedInitShader(
            mask, _stateA!, birth, boundaryMass, crystalMass, _diffusiveA!, _scratch, gridWidth, gridHeight, derived.VaporDensity));
        context.Barrier(_stateA!);
        context.Barrier(birth);
        context.Barrier(boundaryMass);
        context.Barrier(crystalMass);
        context.Barrier(_diffusiveA!);
        context.Barrier(_scratch);

        context.For(gridWidth, gridHeight, new JumpFloodSeedShader(_stateA!, _jumpFloodA!, gridWidth, gridHeight));
        context.Barrier(_jumpFloodA!);
        var reading = _jumpFloodA!;
        var writing = _jumpFloodB!;
        var stepSize = 1;
        var maxSide = Math.Max(gridWidth, gridHeight);
        while (stepSize < maxSide)
            stepSize <<= 1;
        stepSize >>= 1;
        while (stepSize >= 1)
        {
            context.For(gridWidth, gridHeight, new JumpFloodPassShader(reading, writing, gridWidth, gridHeight, stepSize, derived.CellSize));
            context.Barrier(writing);
            (reading, writing) = (writing, reading);
            stepSize >>= 1;
        }
        context.For(gridWidth, gridHeight, new ReachMaskShader(reading, reachMask, gridWidth, gridHeight, derived.CellSize, derived.ReachPixels));
        context.Barrier(reachMask);

        var stateRead = _stateA!;
        var stateWrite = _stateB!;
        for (var step = 0; step < derived.Steps; step++)
        {
            context.For(gridWidth, gridHeight, new DiffusionShader(_diffusiveA!, _diffusiveB!, stateRead, gridWidth, gridHeight));
            context.Barrier(_diffusiveB!);
            context.For(gridWidth, gridHeight, new GrowthUpdateShader(
                _diffusiveB!, _diffusiveA!, boundaryMass, crystalMass, stateRead, stateWrite, birth, reachMask, _scratch,
                gridWidth, gridHeight, step, parameters.Seed,
                CrystallineGrowthSettings.Kappa, derived.Beta, CrystallineGrowthSettings.Alpha, CrystallineGrowthSettings.Theta,
                CrystallineGrowthSettings.Mu, CrystallineGrowthSettings.Gamma, derived.Sigma));
            context.Barrier(_diffusiveA!);
            context.Barrier(stateWrite);
            context.Barrier(boundaryMass);
            context.Barrier(crystalMass);
            (stateRead, stateWrite) = (stateWrite, stateRead);
        }
        context.Barrier(birth);
        context.Barrier(_scratch);
    }

    private void RecordRenderStage(
        in ComputeContext context,
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> output,
        PixelRect rect,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        in DerivedValues derived,
        in Parameters parameters)
    {
        context.For(rect.Width, rect.Height, new RenderShader(
            _birth!, _crystalMass!, _scratch, source, output,
            rect.X, rect.Y, rect.Width, rect.Height, _gridWidth, _gridHeight,
            sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight,
            derived.CellSize, Math.Clamp(parameters.Freeze, 0f, 1f),
            Math.Clamp(parameters.Frost, 0f, 1f), derived.RefractionPixels,
            Math.Clamp(parameters.Specular, 0f, 1f),
            parameters.ColorR, parameters.ColorG, parameters.ColorB));
    }

    private void BuildBoundsPrefix()
    {
        var maxBirth = Math.Clamp(_cachedMaxBirth, -1, CrystallineGrowthSettings.MaximumStepCount);
        _cachedMaxBirth = maxBirth;
        if (maxBirth < 0)
            return;
        for (var step = 0; step <= maxBirth; step++)
        {
            _boundsMinX[step] = int.MaxValue;
            _boundsMinY[step] = int.MaxValue;
            _boundsMaxX[step] = int.MinValue;
            _boundsMaxY[step] = int.MinValue;
        }

        var birth = _cachedBirth!;
        var gridWidth = _gridWidth;
        for (var index = 0; index < birth.Length; index++)
        {
            var step = birth[index];
            if (step < 0 || step > maxBirth)
                continue;
            var x = index % gridWidth;
            var y = index / gridWidth;
            if (x < _boundsMinX[step])
                _boundsMinX[step] = x;
            if (x > _boundsMaxX[step])
                _boundsMaxX[step] = x;
            if (y < _boundsMinY[step])
                _boundsMinY[step] = y;
            if (y > _boundsMaxY[step])
                _boundsMaxY[step] = y;
        }

        for (var step = 1; step <= maxBirth; step++)
        {
            _boundsMinX[step] = Math.Min(_boundsMinX[step], _boundsMinX[step - 1]);
            _boundsMinY[step] = Math.Min(_boundsMinY[step], _boundsMinY[step - 1]);
            _boundsMaxX[step] = Math.Max(_boundsMaxX[step], _boundsMaxX[step - 1]);
            _boundsMaxY[step] = Math.Max(_boundsMaxY[step], _boundsMaxY[step - 1]);
        }
    }

    private DerivedValues Derive(int width, int height, in Parameters parameters)
    {
        var settings = CrystallineGrowthSettings.GetQuality(parameters.Quality);
        var (_, _, cellSize) = CrystallineGrowthSettings.GetGridSize(width, height, settings.GridResolution);
        var reachPixels = Math.Max(parameters.ReachPixels, cellSize);
        var reachCells = reachPixels / cellSize;
        return new DerivedValues(
            cellSize,
            CrystallineGrowthSettings.GetStepCount(reachCells, settings.MaxSteps),
            CrystallineGrowthSettings.GetVaporDensity(parameters.Branching),
            CrystallineGrowthSettings.GetAttachmentThreshold(parameters.Facet),
            CrystallineGrowthSettings.GetNoiseSigma(parameters.Noise),
            reachPixels,
            CrystallineGrowthSettings.KernelRadiusFactor * cellSize,
            Math.Clamp(parameters.Refraction, 0f, 1f) * CrystallineGrowthSettings.MaximumRefractionPixels);
    }

    private void EnsureGridFor(int width, int height, CrystallineGrowthQuality quality)
    {
        var settings = CrystallineGrowthSettings.GetQuality(quality);
        var (gridWidth, gridHeight, _) = CrystallineGrowthSettings.GetGridSize(width, height, settings.GridResolution);
        EnsureGrid(gridWidth, gridHeight);
    }

    private void EnsureGrid(int gridWidth, int gridHeight)
    {
        if (_gridWidth == gridWidth && _gridHeight == gridHeight)
            return;

        DisposeGridBuffers();
        var gridLength = gridWidth * gridHeight;
        _mask = _device.AllocateReadWriteBuffer<int>(gridLength);
        _stateA = _device.AllocateReadWriteBuffer<int>(gridLength);
        _stateB = _device.AllocateReadWriteBuffer<int>(gridLength);
        _birth = _device.AllocateReadWriteBuffer<int>(gridLength);
        _reachMask = _device.AllocateReadWriteBuffer<int>(gridLength);
        _jumpFloodA = _device.AllocateReadWriteBuffer<int>(gridLength);
        _jumpFloodB = _device.AllocateReadWriteBuffer<int>(gridLength);
        _boundaryMass = _device.AllocateReadWriteBuffer<float>(gridLength);
        _crystalMass = _device.AllocateReadWriteBuffer<float>(gridLength);
        _diffusiveA = _device.AllocateReadWriteBuffer<float>(gridLength);
        _diffusiveB = _device.AllocateReadWriteBuffer<float>(gridLength);
        _birthReadBack = _device.AllocateReadBackBuffer<int>(gridLength);
        _cachedBirth = new int[gridLength];
        _cachedMaxBirth = -1;
        _cachedAttached = 0;
        _gridWidth = gridWidth;
        _gridHeight = gridHeight;
    }

    private void EnsurePackedTextures(int width, int height)
    {
        if (_packedWidth == width && _packedHeight == height)
            return;

        _packedSource?.Dispose();
        _packedOutput?.Dispose();
        _packedSource = _device.AllocateReadWriteTexture2D<Bgra32, Float4>(width, height);
        _packedOutput = _device.AllocateReadWriteTexture2D<Bgra32, Float4>(width, height);
        _packedWidth = width;
        _packedHeight = height;
    }

    private void DisposeGridBuffers()
    {
        _mask?.Dispose();
        _stateA?.Dispose();
        _stateB?.Dispose();
        _birth?.Dispose();
        _reachMask?.Dispose();
        _jumpFloodA?.Dispose();
        _jumpFloodB?.Dispose();
        _boundaryMass?.Dispose();
        _crystalMass?.Dispose();
        _diffusiveA?.Dispose();
        _diffusiveB?.Dispose();
        _birthReadBack?.Dispose();
        _mask = null;
        _stateA = null;
        _stateB = null;
        _birth = null;
        _reachMask = null;
        _jumpFloodA = null;
        _jumpFloodB = null;
        _boundaryMass = null;
        _crystalMass = null;
        _diffusiveA = null;
        _diffusiveB = null;
        _birthReadBack = null;
        _cachedBirth = null;
        _cachedMaxBirth = -1;
        _cachedAttached = 0;
        _structureKey = null;
        _gridWidth = 0;
        _gridHeight = 0;
    }

    public void Dispose()
    {
        DisposeGridBuffers();
        _packedSource?.Dispose();
        _packedOutput?.Dispose();
        _packedSource = null;
        _packedOutput = null;
        _packedWidth = 0;
        _packedHeight = 0;
        _scratchReadBack.Dispose();
        _scratch.Dispose();
    }

    internal readonly record struct PixelRect(int X, int Y, int Width, int Height);

    private readonly record struct StructureKey(
        int MaskHashSum,
        int MaskHashMix,
        int CanvasWidth,
        int CanvasHeight,
        CrystallineGrowthQuality Quality,
        int Seed,
        float ReachPixels,
        float Branching,
        float Facet,
        float Noise);

    private readonly record struct DerivedValues(
        float CellSize,
        int Steps,
        float VaporDensity,
        float Beta,
        float Sigma,
        float ReachPixels,
        float KernelRadius,
        float RefractionPixels);

    internal readonly record struct Parameters(
        CrystallineGrowthQuality Quality,
        float Freeze,
        float Branching,
        float Facet,
        float ReachPixels,
        float Noise,
        float Frost,
        float Refraction,
        float Specular,
        float ColorR,
        float ColorG,
        float ColorB,
        int Seed);
}
