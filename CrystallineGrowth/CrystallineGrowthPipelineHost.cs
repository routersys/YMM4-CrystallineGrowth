using ComputeWeave;

namespace CrystallineGrowth;

[ComputeResourceGroup]
internal sealed partial class CrystallineGrowthGridResources
{
    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> Mask { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> ReachMask { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> JumpFloodA { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<int> JumpFloodB { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> BoundaryMass { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> CrystalMass { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> DiffusiveA { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> DiffusiveB { get; }
}

[ComputePipelineHost("_device", 1)]
internal sealed partial class CrystallineGrowthPipelineHost
{
    private readonly GraphicsDevice _device;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceGroupSlot<CrystallineGrowthGridResources> _grid = new();

    [ComputePipeline]
    private void RecordFullPipeline(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> output,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> birth,
        int width,
        int height,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        _ = _device;

        RecordSilhouetteStage(in context, grid, source, 0, 0, width, height, gridWidth, gridHeight, in derived);
        RecordGrowthStage(in context, grid, scratch, birth, gridWidth, gridHeight, in derived, in parameters);
        RecordRenderStage(in context, grid, source, output, scratch, birth, new CrystallineGrowthPipeline.PixelRect(0, 0, width, height), 0, 0, width, height, gridWidth, gridHeight, in derived, in parameters);
    }

    [ComputePipeline]
    private void RecordSilhouetteAndMaskHash(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived)
    {
        _ = _device;

        RecordSilhouetteStage(in context, grid, source, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, gridWidth, gridHeight, in derived);
        RecordMaskHashStage(in context, grid, scratch, gridWidth, gridHeight);
    }

    [ComputePipeline]
    [ComputeInterop]
    private void RecordSharedSilhouetteAndMaskHash(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived)
    {
        _ = _device;

        RecordSilhouetteStage(in context, grid, source, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, gridWidth, gridHeight, in derived);
        RecordMaskHashStage(in context, grid, scratch, gridWidth, gridHeight);
    }

    [ComputePipeline]
    [ComputeInterop]
    private void RecordSharedRender(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> output,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> birth,
        in CrystallineGrowthPipeline.PixelRect rect,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        _ = _device;

        RecordRenderStage(in context, grid, source, output, scratch, birth, rect, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, gridWidth, gridHeight, in derived, in parameters);
    }

    [ComputePipeline]
    private void RecordGrowth(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> birth,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        _ = _device;

        RecordGrowthStage(in context, grid, scratch, birth, gridWidth, gridHeight, in derived, in parameters);
    }

    [ComputePipeline]
    private void RecordRender(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] CrystallineGrowthGridResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> output,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> birth,
        in CrystallineGrowthPipeline.PixelRect rect,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        _ = _device;

        RecordRenderStage(in context, grid, source, output, scratch, birth, rect, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, gridWidth, gridHeight, in derived, in parameters);
    }

    private static void RecordSilhouetteStage(
        in ComputeContext context,
        CrystallineGrowthGridResources grid,
        ReadWriteTexture2D<Bgra32, Float4> source,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived)
    {
        context.For(gridWidth, gridHeight, new SilhouetteShader(
            source, grid.Mask, sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight, gridWidth, gridHeight, derived.CellSize, 0.05f));
        context.Barrier(grid.Mask);
    }

    private static void RecordMaskHashStage(
        in ComputeContext context,
        CrystallineGrowthGridResources grid,
        ReadWriteBuffer<int> scratch,
        int gridWidth,
        int gridHeight)
    {
        context.For(1, new MaskHashResetShader(scratch));
        context.Barrier(scratch);
        context.For(gridWidth, gridHeight, new MaskHashShader(grid.Mask, scratch, gridWidth, gridHeight));
        context.Barrier(scratch);
    }

    private static void RecordGrowthStage(
        in ComputeContext context,
        CrystallineGrowthGridResources grid,
        ReadWriteBuffer<int> scratch,
        ReadWriteBuffer<int> birth,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        context.For(1, new InitScratchShader(scratch));
        context.Barrier(scratch);
        context.For(gridWidth, gridHeight, new SeedInitShader(
            grid.Mask, birth, grid.BoundaryMass, grid.CrystalMass, grid.DiffusiveA, scratch, gridWidth, gridHeight, derived.VaporDensity));
        context.Barrier(birth);
        context.Barrier(grid.BoundaryMass);
        context.Barrier(grid.CrystalMass);
        context.Barrier(grid.DiffusiveA);
        context.Barrier(scratch);

        context.For(gridWidth, gridHeight, new JumpFloodSeedShader(birth, grid.JumpFloodA, gridWidth, gridHeight));
        context.Barrier(grid.JumpFloodA);
        var reading = grid.JumpFloodA;
        var writing = grid.JumpFloodB;
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
        context.For(gridWidth, gridHeight, new ReachMaskShader(reading, grid.ReachMask, gridWidth, gridHeight, derived.CellSize, derived.ReachPixels));
        context.Barrier(grid.ReachMask);

        for (var step = 0; step < derived.Steps; step++)
        {
            context.For(gridWidth, gridHeight, new DiffusionShader(grid.DiffusiveA, grid.DiffusiveB, gridWidth, gridHeight));
            context.Barrier(grid.DiffusiveB);
            context.For(gridWidth, gridHeight, new GrowthUpdateShader(
                grid.DiffusiveB, grid.DiffusiveA, grid.BoundaryMass, grid.CrystalMass, birth, grid.ReachMask, scratch,
                gridWidth, gridHeight, step, parameters.Seed,
                CrystallineGrowthSettings.Kappa, derived.Beta, CrystallineGrowthSettings.Alpha, CrystallineGrowthSettings.Theta,
                CrystallineGrowthSettings.Mu, CrystallineGrowthSettings.Gamma, derived.Sigma));
            context.Barrier(grid.DiffusiveA);
            context.Barrier(grid.BoundaryMass);
            context.Barrier(grid.CrystalMass);
        }
        context.Barrier(birth);
        context.Barrier(scratch);
    }

    private static void RecordRenderStage(
        in ComputeContext context,
        CrystallineGrowthGridResources grid,
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> output,
        ReadWriteBuffer<int> scratch,
        ReadWriteBuffer<int> birth,
        in CrystallineGrowthPipeline.PixelRect rect,
        int sourceOffsetX,
        int sourceOffsetY,
        int sourceWidth,
        int sourceHeight,
        int gridWidth,
        int gridHeight,
        in CrystallineGrowthPipeline.DerivedValues derived,
        in CrystallineGrowthPipeline.Parameters parameters)
    {
        context.For(rect.Width, rect.Height, new RenderShader(
            birth, grid.CrystalMass, scratch, source, output,
            rect.X, rect.Y, rect.Width, rect.Height, gridWidth, gridHeight,
            sourceOffsetX, sourceOffsetY, sourceWidth, sourceHeight,
            derived.CellSize, Math.Clamp(parameters.Freeze, 0f, 1f),
            Math.Clamp(parameters.Frost, 0f, 1f), derived.RefractionPixels,
            Math.Clamp(parameters.Specular, 0f, 1f),
            parameters.ColorR, parameters.ColorG, parameters.ColorB));
    }
}
