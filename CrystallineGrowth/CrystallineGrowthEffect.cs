using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace CrystallineGrowth;

[VideoEffect(nameof(Texts.CrystallineGrowth), [VideoEffectCategories.Decoration, VideoEffectCategories.Animation], [nameof(Texts.TagIce), nameof(Texts.TagFrost), nameof(Texts.TagFreeze)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class CrystallineGrowthEffect : VideoEffectBase
{
    public override string Label => Texts.CrystallineGrowth;

    public CrystallineGrowthEffect()
    {
        CrystallineGrowthTelemetry.EnsureStartedOnce();
        CrystallineGrowthUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Amount { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Freeze), Description = nameof(Texts.FreezeDescription), Order = 1, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Freeze { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Quality), Description = nameof(Texts.QualityDescription), Order = 2, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public CrystallineGrowthQuality Quality { get => _quality; set => Set(ref _quality, value); }
    private CrystallineGrowthQuality _quality = CrystallineGrowthQuality.High;

    [Display(GroupName = nameof(Texts.GrowthGroup), Name = nameof(Texts.Branching), Description = nameof(Texts.BranchingDescription), Order = 10, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Branching { get; } = new Animation(60, 0, 100);

    [Display(GroupName = nameof(Texts.GrowthGroup), Name = nameof(Texts.Facet), Description = nameof(Texts.FacetDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Facet { get; } = new Animation(40, 0, 100);

    [Display(GroupName = nameof(Texts.GrowthGroup), Name = nameof(Texts.Reach), Description = nameof(Texts.ReachDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 5, 100)]
    public Animation Reach { get; } = new Animation(25, 1, 400);

    [Display(GroupName = nameof(Texts.GrowthGroup), Name = nameof(Texts.Noise), Description = nameof(Texts.NoiseDescription), Order = 13, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Noise { get; } = new Animation(25, 0, 100);

    [Display(GroupName = nameof(Texts.GrowthGroup), Name = nameof(Texts.Seed), Description = nameof(Texts.SeedDescription), Order = 14, ResourceType = typeof(Texts))]
    [Range(0, int.MaxValue)]
    [DefaultValue(0)]
    [TextBoxSlider("F0", "", 0, 10000)]
    public int Seed
    {
        get => _seed;
        set => Set(ref _seed, Math.Max(value, 0));
    }
    private int _seed;

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Frost), Description = nameof(Texts.FrostDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Frost { get; } = new Animation(70, 0, 100);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Refraction), Description = nameof(Texts.RefractionDescription), Order = 21, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Refraction { get; } = new Animation(40, 0, 100);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Specular), Description = nameof(Texts.SpecularDescription), Order = 22, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Specular { get; } = new Animation(50, 0, 100);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.IceColor), Description = nameof(Texts.IceColorDescription), Order = 23, ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color IceColor
    {
        get => _iceColor;
        set => Set(ref _iceColor, value);
    }
    private Color _iceColor = Color.FromArgb(255, 205, 228, 255);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new CrystallineGrowthEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            CrystallineGrowthTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Amount, Freeze, Branching, Facet, Reach, Noise, Frost, Refraction, Specular];
}
