namespace CrystallineGrowth;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/CrystallineGrowth;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
