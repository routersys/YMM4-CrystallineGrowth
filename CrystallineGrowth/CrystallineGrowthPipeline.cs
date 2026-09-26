using System.Runtime.InteropServices;
using ComputeWeave;

namespace CrystallineGrowth;

internal sealed class CrystallineGrowthPipeline : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly CrystallineGrowthPipelineHost _host;
    private readonly ReadWriteBuffer<int> _scratch;
    private readonly ReadBackBuffer<int> _scratchReadBack;
    private readonly int[] _boundsMinX;
    private readonly int[] _boundsMinY;
    private readonly int[] _boundsMaxX;
    private readonly int[] _boundsMaxY;
    private ReadWriteBuffer<int>? _birth;
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

    private CrystallineGrowthPipeline(GraphicsDevice device, CrystallineGrowthPipelineHost host)
    {
        _device = device;
        _host = host;
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
            return TryCreate(GraphicsDevice.GetDefault());
        }
        catch
        {
            return null;
        }
    }

    public static CrystallineGrowthPipeline? TryCreate(GraphicsDevice device)
    {
        CrystallineGrowthPipelineHost? host = null;
        try
        {
            host = CrystallineGrowthPipelineHost.Create(device, CrystallineGrowthSettings.MaximumPendingSubmissions);
            return new CrystallineGrowthPipeline(device, host);
        }
        catch
        {
            host?.Dispose();
            host?.WaitForDisposal();
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
        SubmitFullPipeline(sourceTexture, outputTexture, width, height, in parameters).Wait();
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
        _ = SubmitFullPipeline(source, destination, width, height, in parameters);
    }

    internal void ProcessSharedAndWait(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> destination,
        int width,
        int height,
        in Parameters parameters)
    {
        EnsureGridFor(width, height, parameters.Quality);
        SubmitFullPipeline(source, destination, width, height, in parameters).Wait();
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
        var derived = BeginSimulate(canvasWidth, canvasHeight, in parameters);
        _host.RecordSilhouetteAndMaskHash(
            source, _scratch, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, _gridWidth, _gridHeight, in derived).Wait();
        return CompleteSimulate(canvasWidth, canvasHeight, in parameters, in derived);
    }

    internal bool Simulate(
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> source,
        int canvasWidth,
        int canvasHeight,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        in Parameters parameters)
    {
        var derived = BeginSimulate(canvasWidth, canvasHeight, in parameters);
        _host.RecordSharedSilhouetteAndMaskHash(
            source, _scratch, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, _gridWidth, _gridHeight, in derived).Wait();
        return CompleteSimulate(canvasWidth, canvasHeight, in parameters, in derived);
    }

    private DerivedValues BeginSimulate(int canvasWidth, int canvasHeight, in Parameters parameters)
    {
        EnsureGridFor(canvasWidth, canvasHeight, parameters.Quality);
        return Derive(canvasWidth, canvasHeight, in parameters);
    }

    private bool CompleteSimulate(int canvasWidth, int canvasHeight, in Parameters parameters, in DerivedValues derived)
    {
        _scratchReadBack.CopyFrom(_scratch);
        var hashed = _scratchReadBack.Span;
        var key = new StructureKey(
            hashed[CrystallineGrowthSettings.ScratchMaskHashSum],
            hashed[CrystallineGrowthSettings.ScratchMaskHashMix],
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

        _host.RecordGrowth(_scratch, _birth!, _gridWidth, _gridHeight, in derived, in parameters).Wait();
        _scratchReadBack.CopyFrom(_scratch);
        var birthReadBack = _birthReadBack!;
        birthReadBack.CopyFrom(_birth!);
        var scratch = _scratchReadBack.Span;
        _cachedAttached = scratch[CrystallineGrowthSettings.ScratchAttachedCount];
        _cachedMaxBirth = scratch[CrystallineGrowthSettings.ScratchMaxBirth];
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
        _host.RecordRender(
            source, output, _scratch, _birth!, in rect, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, _gridWidth, _gridHeight, in derived, in parameters).Wait();
    }

    internal void RenderVisible(
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> source,
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> output,
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
        _host.RecordSharedRender(
            source, output, _scratch, _birth!, in rect, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, _gridWidth, _gridHeight, in derived, in parameters).Wait();
    }

    private ComputeSubmission SubmitFullPipeline(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in Parameters parameters)
    {
        _structureKey = null;
        var derived = Derive(width, height, in parameters);
        return _host.RecordFullPipeline(source, output, _scratch, _birth!, width, height, _gridWidth, _gridHeight, in derived, in parameters);
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
        if (!_host.TryEnsureGrid(
                new CrystallineGrowthGridResources.Plan(
                    boundaryMassLength: gridLength,
                    crystalMassLength: gridLength,
                    diffusiveALength: gridLength,
                    diffusiveBLength: gridLength,
                    jumpFloodALength: gridLength,
                    jumpFloodBLength: gridLength,
                    maskLength: gridLength,
                    reachMaskLength: gridLength),
                out _))
            throw new InvalidOperationException();
        _birth = _device.AllocateReadWriteBuffer<int>(gridLength);
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
        _birth?.Dispose();
        _birthReadBack?.Dispose();
        _birth = null;
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
        _host.Dispose();
        _host.WaitForDisposal();
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

    internal readonly record struct DerivedValues(
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
