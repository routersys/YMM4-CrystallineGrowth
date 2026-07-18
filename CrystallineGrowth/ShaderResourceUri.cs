namespace CrystallineGrowth;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/CrystallineGrowth;component/Shaders/{shaderName}.cso", UriKind.Absolute);
}
