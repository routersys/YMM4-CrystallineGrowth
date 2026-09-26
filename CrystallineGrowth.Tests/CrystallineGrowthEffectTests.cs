using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace CrystallineGrowth.Tests;

public sealed class CrystallineGrowthEffectTests
{
    static readonly Color DefaultIceColor = Color.FromArgb(255, 205, 228, 255);

    static PropertyInfo Property(string name) => typeof(CrystallineGrowthEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(CrystallineGrowthEffect effect)
        => [effect.Amount, effect.Freeze, effect.Branching, effect.Facet, effect.Reach, effect.Noise, effect.Frost, effect.Refraction, effect.Specular];

    [Theory]
    [InlineData(nameof(CrystallineGrowthEffect.Amount), 100d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Freeze), 100d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Branching), 60d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Facet), 40d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Reach), 25d, 1d, 400d)]
    [InlineData(nameof(CrystallineGrowthEffect.Noise), 25d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Frost), 70d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Refraction), 40d, 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Specular), 50d, 0d, 100d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new CrystallineGrowthEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void QualitySeedAndIceColorStartFromTheirDefaults()
    {
        var effect = new CrystallineGrowthEffect();

        Assert.Equal(CrystallineGrowthQuality.High, effect.Quality);
        Assert.Equal(0, effect.Seed);
        Assert.Equal(DefaultIceColor, effect.IceColor);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1234, 1234)]
    [InlineData(10000, 10000)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void SeedNeverDropsBelowZero(int value, int expected)
    {
        var effect = new CrystallineGrowthEffect { Seed = 5 };

        effect.Seed = value;

        Assert.Equal(expected, effect.Seed);
        Assert.False(effect.HasErrors);
    }

    [Fact]
    public void ChangingQualitySeedOrIceColorNotifiesTheEditor()
    {
        var effect = new CrystallineGrowthEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Quality = CrystallineGrowthQuality.Ultra;
        effect.Seed = 7;
        effect.IceColor = Colors.Crimson;

        Assert.Equal([nameof(CrystallineGrowthEffect.Quality), nameof(CrystallineGrowthEffect.Seed), nameof(CrystallineGrowthEffect.IceColor)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedOrClampedValueDoesNotNotify()
    {
        var effect = new CrystallineGrowthEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.Quality = CrystallineGrowthQuality.High;
        effect.Seed = 0;
        effect.Seed = -1;
        effect.IceColor = DefaultIceColor;

        Assert.Empty(changed);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        var effect = new CrystallineGrowthEffect();

        Assert.Equal(Texts.CrystallineGrowth, effect.Label);
    }

    [Fact]
    public void TheNineNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new CrystallineGrowthEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new CrystallineGrowthEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheEffectIsRegisteredForDecorationAndAnimationWithoutAviUtlSupport()
    {
        var attribute = typeof(CrystallineGrowthEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.CrystallineGrowth), attribute.Name);
        Assert.Equal([VideoEffectCategories.Decoration, VideoEffectCategories.Animation], attribute.Categories);
        Assert.Equal([nameof(Texts.TagIce), nameof(Texts.TagFrost), nameof(Texts.TagFreeze)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.CrystallineGrowth, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(CrystallineGrowthEffect.Amount), nameof(Texts.BasicGroup), nameof(Texts.Amount), nameof(Texts.AmountDescription), 0)]
    [InlineData(nameof(CrystallineGrowthEffect.Freeze), nameof(Texts.BasicGroup), nameof(Texts.Freeze), nameof(Texts.FreezeDescription), 1)]
    [InlineData(nameof(CrystallineGrowthEffect.Quality), nameof(Texts.BasicGroup), nameof(Texts.Quality), nameof(Texts.QualityDescription), 2)]
    [InlineData(nameof(CrystallineGrowthEffect.Branching), nameof(Texts.GrowthGroup), nameof(Texts.Branching), nameof(Texts.BranchingDescription), 10)]
    [InlineData(nameof(CrystallineGrowthEffect.Facet), nameof(Texts.GrowthGroup), nameof(Texts.Facet), nameof(Texts.FacetDescription), 11)]
    [InlineData(nameof(CrystallineGrowthEffect.Reach), nameof(Texts.GrowthGroup), nameof(Texts.Reach), nameof(Texts.ReachDescription), 12)]
    [InlineData(nameof(CrystallineGrowthEffect.Noise), nameof(Texts.GrowthGroup), nameof(Texts.Noise), nameof(Texts.NoiseDescription), 13)]
    [InlineData(nameof(CrystallineGrowthEffect.Seed), nameof(Texts.GrowthGroup), nameof(Texts.Seed), nameof(Texts.SeedDescription), 14)]
    [InlineData(nameof(CrystallineGrowthEffect.Frost), nameof(Texts.AppearanceGroup), nameof(Texts.Frost), nameof(Texts.FrostDescription), 20)]
    [InlineData(nameof(CrystallineGrowthEffect.Refraction), nameof(Texts.AppearanceGroup), nameof(Texts.Refraction), nameof(Texts.RefractionDescription), 21)]
    [InlineData(nameof(CrystallineGrowthEffect.Specular), nameof(Texts.AppearanceGroup), nameof(Texts.Specular), nameof(Texts.SpecularDescription), 22)]
    [InlineData(nameof(CrystallineGrowthEffect.IceColor), nameof(Texts.AppearanceGroup), nameof(Texts.IceColor), nameof(Texts.IceColorDescription), 23)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(CrystallineGrowthEffect.Amount), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Freeze), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Branching), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Facet), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Reach), 5d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Noise), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Frost), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Refraction), 0d, 100d)]
    [InlineData(nameof(CrystallineGrowthEffect.Specular), 0d, 100d)]
    public void AnimatedParametersAreEditedAsPercentagesWithAnimationSliders(string property, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal("F1", slider.StringFormat);
        Assert.Equal("%", slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void TheQualityIsChosenFromACombo()
    {
        Assert.NotNull(Attribute<EnumComboBoxAttribute>(nameof(CrystallineGrowthEffect.Quality)));
        Assert.Equal([CrystallineGrowthQuality.Balanced, CrystallineGrowthQuality.High, CrystallineGrowthQuality.Ultra], Enum.GetValues<CrystallineGrowthQuality>());
    }

    [Theory]
    [InlineData(CrystallineGrowthQuality.Balanced, nameof(Texts.QualityBalanced), nameof(Texts.QualityBalancedDescription))]
    [InlineData(CrystallineGrowthQuality.High, nameof(Texts.QualityHigh), nameof(Texts.QualityHighDescription))]
    [InlineData(CrystallineGrowthQuality.Ultra, nameof(Texts.QualityUltra), nameof(Texts.QualityUltraDescription))]
    public void EveryQualityIsDisplayedWithItsLocalizedName(CrystallineGrowthQuality quality, string name, string description)
    {
        var display = typeof(CrystallineGrowthQuality).GetField(quality.ToString())!.GetCustomAttribute<DisplayAttribute>()!;

        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Fact]
    public void TheSeedIsEditedWithoutAUnitFromZero()
    {
        var slider = Attribute<TextBoxSliderAttribute>(nameof(CrystallineGrowthEffect.Seed));
        var range = Attribute<RangeAttribute>(nameof(CrystallineGrowthEffect.Seed));

        Assert.Equal("F0", slider.StringFormat);
        Assert.Equal(string.Empty, slider.UnitText);
        Assert.Equal(0d, slider.DefaultMin);
        Assert.Equal(10000d, slider.DefaultMax);
        Assert.Equal(0, range.Minimum);
        Assert.Equal(int.MaxValue, range.Maximum);
        Assert.Equal(0, Attribute<DefaultValueAttribute>(nameof(CrystallineGrowthEffect.Seed)).Value);
    }

    [Fact]
    public void TheIceColorIsPickedWithAColorPicker()
    {
        Assert.NotNull(Attribute<ColorPickerAttribute>(nameof(CrystallineGrowthEffect.IceColor)));
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new CrystallineGrowthEffect { Quality = CrystallineGrowthQuality.Ultra, Seed = 42, IceColor = Colors.Crimson };
        var values = new[] { 55d, 45d, 70d, 20d, 120d, 65d, 15d, 85d, 35d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(CrystallineGrowthQuality.Ultra, clone.Quality);
        Assert.Equal(42, clone.Seed);
        Assert.Equal(Colors.Crimson, clone.IceColor);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
    }
}
