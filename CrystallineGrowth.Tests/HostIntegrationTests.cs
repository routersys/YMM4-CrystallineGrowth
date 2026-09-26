using System.Windows;

namespace CrystallineGrowth.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new CrystallineGrowthEffect();

        Assert.Equal(Texts.CrystallineGrowth, effect.Label);
    }
}
