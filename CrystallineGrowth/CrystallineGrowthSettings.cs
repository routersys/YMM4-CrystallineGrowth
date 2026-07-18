namespace CrystallineGrowth;

internal static class CrystallineGrowthSettings
{
    public const float RowStep = 0.8660254f;
    public const float MinimumVaporDensity = 0.45f;
    public const float MaximumVaporDensity = 0.80f;
    public const float MinimumBeta = 1.05f;
    public const float MaximumBeta = 2.60f;
    public const float Kappa = 0.07f;
    public const float Mu = 0.015f;
    public const float Gamma = 0.00005f;
    public const float Alpha = 0.21f;
    public const float Theta = 0.0205f;
    public const float MaximumSigma = 0.002f;
    public const float StepsPerCell = 24f;
    public const float KernelRadiusFactor = 1.35f;
    public const float CoverageLow = 0.24f;
    public const float CoverageHigh = 0.52f;
    public const float MassScale = 0.35f;
    public const float NormalAmplitudeFactor = 1.2f;
    public const float MaximumRefractionPixels = 8f;
    public const float SpecularPower = 32f;
    public const int BirthSentinel = 268435456;
    public const int MinimumGridSize = 4;
    public const int MaximumCanvasSize = 8192;
    public const int MaximumStepCount = 3072;
    public const int MarginPadding = 80;
    public const int ScratchLength = 8;
    public const int ScratchAttachedCount = 0;
    public const int ScratchMaxBirth = 1;
    public const int ScratchMaskHashSum = 6;
    public const int ScratchMaskHashMix = 7;

    public static QualitySettings GetQuality(CrystallineGrowthQuality quality)
        => quality switch
        {
            CrystallineGrowthQuality.Balanced => new QualitySettings(192, 1200),
            CrystallineGrowthQuality.Ultra => new QualitySettings(384, 3072),
            _ => new QualitySettings(288, 2048),
        };

    public static (int Width, int Height, float CellSize) GetGridSize(int width, int height, int resolution)
    {
        var longSide = Math.Max(Math.Max(width, height), 1);
        var cellSize = longSide / (float)Math.Max(Math.Min(resolution, longSide), MinimumGridSize);
        var gridWidth = Math.Max((int)Math.Ceiling(width / cellSize) + 1, MinimumGridSize);
        var gridHeight = Math.Max((int)Math.Ceiling(height / (cellSize * RowStep)) + 1, MinimumGridSize);
        return (gridWidth, gridHeight, cellSize);
    }

    public static int GetStepCount(float reachCells, int maxSteps)
        => Math.Clamp((int)(reachCells * StepsPerCell) + 128, 192, maxSteps);

    public static float GetVaporDensity(float branching)
        => MinimumVaporDensity + Math.Clamp(branching, 0f, 1f) * (MaximumVaporDensity - MinimumVaporDensity);

    public static float GetAttachmentThreshold(float facet)
        => MinimumBeta + Math.Clamp(facet, 0f, 1f) * (MaximumBeta - MinimumBeta);

    public static float GetNoiseSigma(float noise)
        => Math.Clamp(noise, 0f, 1f) * MaximumSigma;

    public static int GetJumpFloodPassCount(int width, int height)
    {
        var maxSide = Math.Max(Math.Max(width, height), 1);
        var count = 0;
        var step = 1;
        while (step < maxSide)
        {
            step <<= 1;
            count++;
        }
        return Math.Max(count, 1);
    }

    internal readonly record struct QualitySettings(int GridResolution, int MaxSteps);
}
