using System.Windows.Media;

namespace CrystallineGrowth.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, CrystallineGrowthEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("default-frames-0-8", Create(), Enumerable.Range(0, 9).ToArray());
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0), [0]);
        yield return ("freeze-35", Create(effect => effect.Freeze.Values[0].Value = 35), [0]);
        yield return ("quality-balanced", Create(effect => effect.Quality = CrystallineGrowthQuality.Balanced), [0]);
        yield return ("quality-ultra", Create(effect => effect.Quality = CrystallineGrowthQuality.Ultra), [0]);
        yield return ("branching-0", Create(effect => effect.Branching.Values[0].Value = 0), [0]);
        yield return ("branching-100", Create(effect => effect.Branching.Values[0].Value = 100), [0]);
        yield return ("facet-0", Create(effect => effect.Facet.Values[0].Value = 0), [0]);
        yield return ("facet-100", Create(effect => effect.Facet.Values[0].Value = 100), [0]);
        yield return ("reach-5", Create(effect => effect.Reach.Values[0].Value = 5), [0]);
        yield return ("reach-100", Create(effect => effect.Reach.Values[0].Value = 100), [0]);
        yield return ("noise-0", Create(effect => effect.Noise.Values[0].Value = 0), [0]);
        yield return ("noise-100", Create(effect => effect.Noise.Values[0].Value = 100), [0]);
        yield return ("frost-0", Create(effect => effect.Frost.Values[0].Value = 0), [0]);
        yield return ("frost-100", Create(effect => effect.Frost.Values[0].Value = 100), [0]);
        yield return ("refraction-0", Create(effect => effect.Refraction.Values[0].Value = 0), [0]);
        yield return ("refraction-100", Create(effect => effect.Refraction.Values[0].Value = 100), [0]);
        yield return ("specular-0", Create(effect => effect.Specular.Values[0].Value = 0), [0]);
        yield return ("specular-100", Create(effect => effect.Specular.Values[0].Value = 100), [0]);
        yield return ("seed-42", Create(effect => effect.Seed = 42), [0]);
        yield return ("ice-color", Create(effect => effect.IceColor = Color.FromArgb(255, 200, 20, 20)), [0]);
    }

    public static IEnumerable<(string Name, Func<CrystallineGrowthEffect> Create, Action<CrystallineGrowthEffect> Change, int Frame)> Transitions()
    {
        yield return ("freeze-100-to-35", () => Create(), effect => effect.Freeze.Values[0].Value = 35, 0);
        yield return ("quality-high-to-ultra", () => Create(), effect => effect.Quality = CrystallineGrowthQuality.Ultra, 0);
        yield return ("branching-60-to-100", () => Create(), effect => effect.Branching.Values[0].Value = 100, 0);
        yield return ("facet-40-to-100", () => Create(), effect => effect.Facet.Values[0].Value = 100, 0);
        yield return ("reach-25-to-100", () => Create(), effect => effect.Reach.Values[0].Value = 100, 0);
        yield return ("noise-25-to-100", () => Create(), effect => effect.Noise.Values[0].Value = 100, 0);
        yield return ("frost-70-to-100", () => Create(), effect => effect.Frost.Values[0].Value = 100, 0);
        yield return ("refraction-40-to-100", () => Create(), effect => effect.Refraction.Values[0].Value = 100, 0);
        yield return ("specular-50-to-100", () => Create(), effect => effect.Specular.Values[0].Value = 100, 0);
        yield return ("seed-0-to-42", () => Create(), effect => effect.Seed = 42, 0);
        yield return ("ice-color-change", () => Create(), effect => effect.IceColor = Color.FromArgb(255, 200, 20, 20), 0);
        yield return ("amount-100-to-0", () => Create(), effect => effect.Amount.Values[0].Value = 0, 0);
    }

    public static IEnumerable<(string Name, CrystallineGrowthEffect Effect)> Benchmarks()
    {
        yield return ("quality-balanced", Create(effect => effect.Quality = CrystallineGrowthQuality.Balanced));
        yield return ("default", Create());
        yield return ("quality-ultra", Create(effect => effect.Quality = CrystallineGrowthQuality.Ultra));
        yield return ("reach-100", Create(effect => effect.Reach.Values[0].Value = 100));
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0));
    }

    public static CrystallineGrowthEffect Create(Action<CrystallineGrowthEffect>? configure = null)
    {
        var effect = new CrystallineGrowthEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
