using System.Windows;
using Telemetry;

namespace CrystallineGrowth.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        CrystallineGrowthTelemetry.EnsureStartedOnce();
        CrystallineGrowthTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new CrystallineGrowthEffect();

        Assert.Equal(Texts.CrystallineGrowth, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
