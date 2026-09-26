using ComputeWeave;

namespace CrystallineGrowth.Tests;

public sealed class CrystallineGrowthShaderMathTests
{
    const float CellSize = 3f;
    const int Column = 7;

    static Float4 Texel(int[] bytes) => new(bytes[0] / 255f, bytes[1] / 255f, bytes[2] / 255f, bytes[3] / 255f);

    public static readonly TheoryData<int, int> Neighbors = new()
    {
        { 4, 0 }, { 4, 1 }, { 4, 2 }, { 4, 3 }, { 4, 4 }, { 4, 5 },
        { 5, 0 }, { 5, 1 }, { 5, 2 }, { 5, 3 }, { 5, 4 }, { 5, 5 },
    };

    [Theory]
    [MemberData(nameof(Neighbors))]
    public void EveryNeighborLiesOneCellAway(int row, int neighbor)
    {
        var center = CrystallineGrowthShaderMath.CellCenter(Column, row, CellSize);

        var other = CrystallineGrowthShaderMath.CellCenter(
            Column + CrystallineGrowthShaderMath.NeighborDx(neighbor, row & 1),
            row + CrystallineGrowthShaderMath.NeighborDy(neighbor),
            CellSize);

        Assert.Equal(CellSize, MathF.Sqrt((other.X - center.X) * (other.X - center.X) + (other.Y - center.Y) * (other.Y - center.Y)), 4);
    }

    [Theory]
    [InlineData(4, 0, 1)]
    [InlineData(4, 1, 0)]
    [InlineData(4, 2, 5)]
    [InlineData(4, 5, 2)]
    [InlineData(4, 3, 4)]
    [InlineData(4, 4, 3)]
    [InlineData(5, 0, 1)]
    [InlineData(5, 1, 0)]
    [InlineData(5, 2, 5)]
    [InlineData(5, 5, 2)]
    [InlineData(5, 3, 4)]
    [InlineData(5, 4, 3)]
    public void OppositeNeighborsPointBackToEachOther(int row, int neighbor, int opposite)
    {
        var x = Column + CrystallineGrowthShaderMath.NeighborDx(neighbor, row & 1);
        var y = row + CrystallineGrowthShaderMath.NeighborDy(neighbor);

        var back = (x + CrystallineGrowthShaderMath.NeighborDx(opposite, y & 1), y + CrystallineGrowthShaderMath.NeighborDy(opposite));

        Assert.Equal((Column, row), back);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ChangingAnyChannelOfATexelChangesItsMix(int channel)
    {
        int[] original = [32, 96, 160, 255];
        var changed = original.ToArray();
        changed[channel] += channel == 3 ? -1 : 1;

        var before = CrystallineGrowthShaderMath.MixTexel(1234, Texel(original));
        var after = CrystallineGrowthShaderMath.MixTexel(1234, Texel(changed));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void TheSameTexelMixesDifferentlyAtAnotherPosition()
    {
        var texel = Texel([32, 96, 160, 255]);

        var here = CrystallineGrowthShaderMath.MixTexel(1234, texel);
        var there = CrystallineGrowthShaderMath.MixTexel(1235, texel);

        Assert.NotEqual(here, there);
    }
}
