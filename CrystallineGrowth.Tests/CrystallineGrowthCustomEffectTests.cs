using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;

namespace CrystallineGrowth.Tests;

[Collection("Direct2D")]
public sealed class CrystallineGrowthCustomEffectTests
{
    const int AmountIndex = 0;
    const int Width = 40;
    const int Height = 24;

    static readonly Bgra Blue = Bgra.Opaque(255, 0, 0);
    static readonly Bgra HalfIce = new(224, 192, 160, 128);

    static Rendering Render(IGraphicsDevicesAndContext devices, ID2D1Image source, ID2D1Image ice, float amount)
    {
        using var effect = new CrystallineGrowthCustomEffect(devices);
        effect.SetInput(0, source, true);
        effect.SetInput(1, ice, true);
        effect.Amount = amount;
        using var output = effect.Output;
        return Rendering.Capture(devices, output);
    }

    static AffineTransform2D Translate(IGraphicsDevicesAndContext devices, ID2D1Image image, float dx, float dy)
    {
        var transform = new AffineTransform2D(devices.DeviceContext)
        {
            InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
            BorderMode = BorderMode.Hard,
            TransformMatrix = Matrix3x2.CreateTranslation(dx, dy),
        };
        transform.SetInput(0, image, true);
        return transform;
    }

    static bool WithinRounding(Bgra expected, Bgra actual)
        => Math.Abs(expected.Blue - actual.Blue) <= 1 && Math.Abs(expected.Green - actual.Green) <= 1 && Math.Abs(expected.Red - actual.Red) <= 1 && Math.Abs(expected.Alpha - actual.Alpha) <= 1;

    static Bgra Over(Bgra ice, Bgra source, float amount)
    {
        var iceAlpha = ice.Alpha * amount;
        var keep = 1d - iceAlpha / byte.MaxValue;
        byte Channel(byte iceValue, byte sourceValue) => (byte)Math.Round(iceValue * amount + sourceValue * keep, MidpointRounding.AwayFromZero);
        return new Bgra(Channel(ice.Blue, source.Blue), Channel(ice.Green, source.Green), Channel(ice.Red, source.Red), Channel(ice.Alpha, source.Alpha));
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new CrystallineGrowthCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void TheAmountStartsFromZero()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new CrystallineGrowthCustomEffect(context);

        Assert.Equal(0f, effect.GetFloatValue(AmountIndex));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.4f, 0.4f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.MaxValue, 1f)]
    public void TheAmountIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new CrystallineGrowthCustomEffect(context);

        effect.Amount = value;

        Assert.Equal(expected, effect.GetFloatValue(AmountIndex));
    }

    [Fact]
    public void TheOutputBoundsCoverTheSourceAndTheIce()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var ice = SourceImage.Solid(context, 8, 8, HalfIce);
        using var moved = Translate(context, ice.Bitmap, 50f, -6f);
        using var movedOutput = moved.Output;
        using var effect = new CrystallineGrowthCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, movedOutput, true);
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal(0f, bounds.Left);
        Assert.Equal(-6f, bounds.Top);
        Assert.Equal(58f, bounds.Right);
        Assert.Equal((float)Height, bounds.Bottom);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void WithoutAmountTheSourcePassesThroughUntouched(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var ice = SourceImage.Solid(context, Width, Height, HalfIce);

        var rendering = Render(context, source.Bitmap, ice.Bitmap, amount);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.25f)]
    public void TheIceIsLaidOverTheSourceScaledByTheAmount(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var ice = SourceImage.Solid(context, Width, Height, HalfIce);
        var expected = Over(HalfIce.Premultiplied(), Blue.Premultiplied(), amount);

        var rendering = Render(context, source.Bitmap, ice.Bitmap, amount);

        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(expected, rendering[point.X, point.Y]), $"({point.X}, {point.Y}) {rendering[point.X, point.Y]}"));
    }

    [Fact]
    public void IceOutsideTheSourceIsDrawnOnItsOwn()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var ice = SourceImage.Solid(context, 8, 8, HalfIce);
        using var moved = Translate(context, ice.Bitmap, 50f, 4f);
        using var movedOutput = moved.Output;
        var expected = Over(HalfIce.Premultiplied(), Bgra.Transparent, 1f);

        var rendering = Render(context, source.Bitmap, movedOutput, 1f);

        Assert.True(WithinRounding(expected, rendering[53, 7]), $"{rendering[53, 7]}");
        Assert.Equal(Blue.Premultiplied(), rendering[10, 10]);
        Assert.Equal(Bgra.Transparent, rendering[45, 10]);
    }
}
