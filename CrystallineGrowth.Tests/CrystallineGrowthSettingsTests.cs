namespace CrystallineGrowth.Tests;

public sealed class CrystallineGrowthSettingsTests
{
    [Theory]
    [InlineData(CrystallineGrowthQuality.Balanced, 192, 1200)]
    [InlineData(CrystallineGrowthQuality.High, 288, 2048)]
    [InlineData(CrystallineGrowthQuality.Ultra, 384, 3072)]
    public void EveryQualityChoosesItsGridResolutionAndStepLimit(CrystallineGrowthQuality quality, int resolution, int maxSteps)
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
    public void TheGridCoversTheWholeCanvas(int width, int height, int resolution)
    {
        var (gridWidth, gridHeight, cellSize) = CrystallineGrowthSettings.GetGridSize(width, height, resolution);

        Assert.True(gridWidth >= CrystallineGrowthSettings.MinimumGridSize);
        Assert.True(gridHeight >= CrystallineGrowthSettings.MinimumGridSize);
        Assert.True(cellSize > 0f);
        Assert.True(gridWidth * cellSize >= width);
        Assert.True(gridHeight * cellSize * CrystallineGrowthSettings.RowStep >= height);
    }

    [Theory]
    [InlineData(1920, 1080, 288, 1920f / 288)]
    [InlineData(1080, 1920, 288, 1920f / 288)]
    [InlineData(8, 8, 288, 1f)]
    [InlineData(4096, 16, 192, 4096f / 192)]
    [InlineData(100, 100, 384, 1f)]
    [InlineData(2, 2, 288, 0.5f)]
    public void TheLongSideIsSplitIntoTheResolutionButNeverIntoFewerThanTheMinimumCells(int width, int height, int resolution, float cellSize)
    {
        var (_, _, actual) = CrystallineGrowthSettings.GetGridSize(width, height, resolution);

        Assert.Equal(cellSize, actual, 4);
    }

    [Theory]
    [InlineData(0f, 1200, 192)]
    [InlineData(10f, 1200, 368)]
    [InlineData(60f, 2048, 1568)]
    [InlineData(500f, 2048, 2048)]
    [InlineData(500f, 1200, 1200)]
    public void TheStepCountGrowsWithTheReachWithinItsLimits(float reachCells, int maxSteps, int expected)
        => Assert.Equal(expected, CrystallineGrowthSettings.GetStepCount(reachCells, maxSteps));

    [Theory]
    [InlineData(-5f, CrystallineGrowthSettings.MinimumVaporDensity)]
    [InlineData(0f, CrystallineGrowthSettings.MinimumVaporDensity)]
    [InlineData(1f, CrystallineGrowthSettings.MaximumVaporDensity)]
    [InlineData(5f, CrystallineGrowthSettings.MaximumVaporDensity)]
    public void TheVaporDensityStaysWithinItsRange(float branching, float expected)
        => Assert.Equal(expected, CrystallineGrowthSettings.GetVaporDensity(branching), 5);

    [Fact]
    public void MoreBranchingFillsTheAirWithMoreVapor()
        => Assert.True(CrystallineGrowthSettings.GetVaporDensity(0.75f) > CrystallineGrowthSettings.GetVaporDensity(0.25f));

    [Theory]
    [InlineData(0f, CrystallineGrowthSettings.MinimumBeta)]
    [InlineData(1f, CrystallineGrowthSettings.MaximumBeta)]
    public void TheAttachmentThresholdSpansItsRangeOverTheFacet(float facet, float expected)
        => Assert.Equal(expected, CrystallineGrowthSettings.GetAttachmentThreshold(facet), 5);

    [Fact]
    public void AStrongerFacetRaisesTheAttachmentThreshold()
        => Assert.True(CrystallineGrowthSettings.GetAttachmentThreshold(0.75f) > CrystallineGrowthSettings.GetAttachmentThreshold(0.25f));

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(1f, CrystallineGrowthSettings.MaximumSigma)]
    [InlineData(2f, CrystallineGrowthSettings.MaximumSigma)]
    public void TheNoiseStaysWithinItsRange(float noise, float expected)
        => Assert.Equal(expected, CrystallineGrowthSettings.GetNoiseSigma(noise), 7);
}
