namespace CrystallineGrowth.Tests;

public sealed class CrystallineGrowthEffectTests
{
    private static double ValueAt(YukkuriMovieMaker.Commons.Animation animation) => animation.GetValue(0, 1, 30);

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
}
