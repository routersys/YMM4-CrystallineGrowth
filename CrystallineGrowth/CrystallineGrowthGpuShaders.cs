using ComputeSharp;

namespace CrystallineGrowth;

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct FillIntShader(
    ReadWriteBuffer<int> values,
    int length,
    int value) : IComputeShader
{
    private readonly ReadWriteBuffer<int> values = values;
    private readonly int length = length;
    private readonly int value = value;

    public void Execute()
    {
        var index = ThreadIds.X;
        if (index >= length)
            return;
        values[index] = value;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct InitScratchShader(
    ReadWriteBuffer<int> scratch) : IComputeShader
{
    private readonly ReadWriteBuffer<int> scratch = scratch;

    public void Execute()
    {
        if (ThreadIds.X != 0)
            return;
        scratch[0] = 0;
        scratch[1] = -1;
        scratch[2] = 0;
        scratch[3] = 0;
        scratch[4] = 0;
        scratch[5] = 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SilhouetteShader(
    ReadWriteTexture2D<Bgra32, Float4> source,
    ReadWriteBuffer<int> mask,
    int sourceOffsetX,
    int sourceOffsetY,
    int sourceWidth,
    int sourceHeight,
    int gridWidth,
    int gridHeight,
    float cellSize,
    float alphaThreshold) : IComputeShader
{
    private readonly ReadWriteTexture2D<Bgra32, Float4> source = source;
    private readonly ReadWriteBuffer<int> mask = mask;
    private readonly int sourceOffsetX = sourceOffsetX;
    private readonly int sourceOffsetY = sourceOffsetY;
    private readonly int sourceWidth = sourceWidth;
    private readonly int sourceHeight = sourceHeight;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly float cellSize = cellSize;
    private readonly float alphaThreshold = alphaThreshold;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var center = CrystallineGrowthShaderMath.CellCenter(gx, gy, cellSize);
        var x0 = Hlsl.Max((int)(center.X - cellSize * 0.5f), sourceOffsetX);
        var x1 = Hlsl.Min((int)Hlsl.Ceil(center.X + cellSize * 0.5f), sourceOffsetX + sourceWidth);
        var y0 = Hlsl.Max((int)(center.Y - cellSize * CrystallineGrowthSettings.RowStep * 0.5f), sourceOffsetY);
        var y1 = Hlsl.Min((int)Hlsl.Ceil(center.Y + cellSize * CrystallineGrowthSettings.RowStep * 0.5f), sourceOffsetY + sourceHeight);

        var found = 0;
        for (var y = y0; y < y1 && found == 0; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (source[new Int2(x - sourceOffsetX, y - sourceOffsetY)].W > alphaThreshold)
                {
                    found = 1;
                    break;
                }
            }
        }
        mask[gy * gridWidth + gx] = found;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskHashResetShader(
    ReadWriteBuffer<int> scratch) : IComputeShader
{
    private readonly ReadWriteBuffer<int> scratch = scratch;

    public void Execute()
    {
        if (ThreadIds.X != 0)
            return;
        scratch[6] = 0;
        scratch[7] = 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskHashShader(
    ReadWriteBuffer<int> mask,
    ReadWriteBuffer<int> scratch,
    int gridWidth,
    int gridHeight) : IComputeShader
{
    private readonly ReadWriteBuffer<int> mask = mask;
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var index = gy * gridWidth + gx;
        if (mask[index] != 1)
            return;

        var mixed = (uint)index * 0x9E3779B9u;
        mixed ^= mixed >> 16;
        mixed *= 0x85EBCA6Bu;
        mixed ^= mixed >> 13;
        Hlsl.InterlockedAdd(ref scratch[6], (int)mixed);
        Hlsl.InterlockedXor(ref scratch[7], (int)(mixed * 0xC2B2AE35u));
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SeedInitShader(
    ReadWriteBuffer<int> mask,
    ReadWriteBuffer<int> state,
    ReadWriteBuffer<int> birth,
    ReadWriteBuffer<float> boundaryMass,
    ReadWriteBuffer<float> crystalMass,
    ReadWriteBuffer<float> diffusiveMass,
    ReadWriteBuffer<int> scratch,
    int gridWidth,
    int gridHeight,
    float vaporDensity) : IComputeShader
{
    private readonly ReadWriteBuffer<int> mask = mask;
    private readonly ReadWriteBuffer<int> state = state;
    private readonly ReadWriteBuffer<int> birth = birth;
    private readonly ReadWriteBuffer<float> boundaryMass = boundaryMass;
    private readonly ReadWriteBuffer<float> crystalMass = crystalMass;
    private readonly ReadWriteBuffer<float> diffusiveMass = diffusiveMass;
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly float vaporDensity = vaporDensity;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var index = gy * gridWidth + gx;
        var isSeed = false;
        if (mask[index] == 1)
        {
            var parity = gy & 1;
            for (var neighbor = 0; neighbor < 6 && !isSeed; neighbor++)
            {
                var nx = gx + CrystallineGrowthShaderMath.NeighborDx(neighbor, parity);
                var ny = gy + CrystallineGrowthShaderMath.NeighborDy(neighbor);
                if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight || mask[ny * gridWidth + nx] == 0)
                    isSeed = true;
            }
        }

        if (isSeed)
        {
            state[index] = 1;
            birth[index] = 0;
            boundaryMass[index] = 0f;
            crystalMass[index] = 1f;
            diffusiveMass[index] = 0f;
            Hlsl.InterlockedAdd(ref scratch[0], 1);
            Hlsl.InterlockedMax(ref scratch[1], 0);
        }
        else
        {
            state[index] = 0;
            birth[index] = CrystallineGrowthSettings.BirthSentinel;
            boundaryMass[index] = 0f;
            crystalMass[index] = 0f;
            diffusiveMass[index] = vaporDensity;
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct JumpFloodSeedShader(
    ReadWriteBuffer<int> state,
    ReadWriteBuffer<int> jumpFlood,
    int gridWidth,
    int gridHeight) : IComputeShader
{
    private readonly ReadWriteBuffer<int> state = state;
    private readonly ReadWriteBuffer<int> jumpFlood = jumpFlood;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var index = gy * gridWidth + gx;
        jumpFlood[index] = state[index] == 1 ? index : -1;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct JumpFloodPassShader(
    ReadWriteBuffer<int> input,
    ReadWriteBuffer<int> output,
    int gridWidth,
    int gridHeight,
    int stepSize,
    float cellSize) : IComputeShader
{
    private readonly ReadWriteBuffer<int> input = input;
    private readonly ReadWriteBuffer<int> output = output;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly int stepSize = stepSize;
    private readonly float cellSize = cellSize;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= gridWidth || y >= gridHeight)
            return;

        var position = CrystallineGrowthShaderMath.CellCenter(x, y, cellSize);
        var best = -1;
        var bestDistance = 3.402823e+38f;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var sx = x + dx * stepSize;
                var sy = y + dy * stepSize;
                if (sx < 0 || sx >= gridWidth || sy < 0 || sy >= gridHeight)
                    continue;
                var candidate = input[sy * gridWidth + sx];
                if (candidate < 0)
                    continue;
                var candidateCenter = CrystallineGrowthShaderMath.CellCenter(candidate % gridWidth, candidate / gridWidth, cellSize);
                var delta = position - candidateCenter;
                var distance = delta.X * delta.X + delta.Y * delta.Y;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }
        output[y * gridWidth + x] = best;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ReachMaskShader(
    ReadWriteBuffer<int> jumpFlood,
    ReadWriteBuffer<int> reachMask,
    int gridWidth,
    int gridHeight,
    float cellSize,
    float reachPixels) : IComputeShader
{
    private readonly ReadWriteBuffer<int> jumpFlood = jumpFlood;
    private readonly ReadWriteBuffer<int> reachMask = reachMask;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly float cellSize = cellSize;
    private readonly float reachPixels = reachPixels;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= gridWidth || y >= gridHeight)
            return;

        var index = y * gridWidth + x;
        var seed = jumpFlood[index];
        if (seed < 0)
        {
            reachMask[index] = 0;
            return;
        }
        var position = CrystallineGrowthShaderMath.CellCenter(x, y, cellSize);
        var seedCenter = CrystallineGrowthShaderMath.CellCenter(seed % gridWidth, seed / gridWidth, cellSize);
        var delta = position - seedCenter;
        reachMask[index] = delta.X * delta.X + delta.Y * delta.Y <= reachPixels * reachPixels ? 1 : 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DiffusionShader(
    ReadWriteBuffer<float> diffusiveIn,
    ReadWriteBuffer<float> diffusiveOut,
    ReadWriteBuffer<int> state,
    int gridWidth,
    int gridHeight) : IComputeShader
{
    private readonly ReadWriteBuffer<float> diffusiveIn = diffusiveIn;
    private readonly ReadWriteBuffer<float> diffusiveOut = diffusiveOut;
    private readonly ReadWriteBuffer<int> state = state;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var index = gy * gridWidth + gx;
        if (state[index] == 1)
        {
            diffusiveOut[index] = 0f;
            return;
        }

        var self = diffusiveIn[index];
        var sum = self;
        var parity = gy & 1;
        for (var neighbor = 0; neighbor < 6; neighbor++)
        {
            var nx = gx + CrystallineGrowthShaderMath.NeighborDx(neighbor, parity);
            var ny = gy + CrystallineGrowthShaderMath.NeighborDy(neighbor);
            if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight || state[ny * gridWidth + nx] == 1)
                sum += self;
            else
                sum += diffusiveIn[ny * gridWidth + nx];
        }
        diffusiveOut[index] = sum * (1f / 7f);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct GrowthUpdateShader(
    ReadWriteBuffer<float> diffusiveMid,
    ReadWriteBuffer<float> diffusiveOut,
    ReadWriteBuffer<float> boundaryMass,
    ReadWriteBuffer<float> crystalMass,
    ReadWriteBuffer<int> stateIn,
    ReadWriteBuffer<int> stateOut,
    ReadWriteBuffer<int> birth,
    ReadWriteBuffer<int> reachMask,
    ReadWriteBuffer<int> scratch,
    int gridWidth,
    int gridHeight,
    int step,
    int seed,
    float kappa,
    float beta,
    float alpha,
    float theta,
    float mu,
    float gamma,
    float sigma) : IComputeShader
{
    private readonly ReadWriteBuffer<float> diffusiveMid = diffusiveMid;
    private readonly ReadWriteBuffer<float> diffusiveOut = diffusiveOut;
    private readonly ReadWriteBuffer<float> boundaryMass = boundaryMass;
    private readonly ReadWriteBuffer<float> crystalMass = crystalMass;
    private readonly ReadWriteBuffer<int> stateIn = stateIn;
    private readonly ReadWriteBuffer<int> stateOut = stateOut;
    private readonly ReadWriteBuffer<int> birth = birth;
    private readonly ReadWriteBuffer<int> reachMask = reachMask;
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly int step = step;
    private readonly int seed = seed;
    private readonly float kappa = kappa;
    private readonly float beta = beta;
    private readonly float alpha = alpha;
    private readonly float theta = theta;
    private readonly float mu = mu;
    private readonly float gamma = gamma;
    private readonly float sigma = sigma;

    public void Execute()
    {
        var gx = ThreadIds.X;
        var gy = ThreadIds.Y;
        if (gx >= gridWidth || gy >= gridHeight)
            return;

        var index = gy * gridWidth + gx;
        if (stateIn[index] == 1)
        {
            stateOut[index] = 1;
            diffusiveOut[index] = 0f;
            return;
        }

        var parity = gy & 1;
        var attachedNeighbors = 0;
        var neighborDiffusive = 0f;
        for (var neighbor = 0; neighbor < 6; neighbor++)
        {
            var nx = gx + CrystallineGrowthShaderMath.NeighborDx(neighbor, parity);
            var ny = gy + CrystallineGrowthShaderMath.NeighborDy(neighbor);
            if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight)
                continue;
            var neighborIndex = ny * gridWidth + nx;
            if (stateIn[neighborIndex] == 1)
                attachedNeighbors++;
            else
                neighborDiffusive += diffusiveMid[neighborIndex];
        }

        var diffusive = diffusiveMid[index];
        var boundary = boundaryMass[index];
        var crystal = crystalMass[index];
        if (attachedNeighbors > 0)
        {
            boundary += (1f - kappa) * diffusive;
            crystal += kappa * diffusive;
            diffusive = 0f;

            var attach = false;
            if (attachedNeighbors >= 4)
                attach = true;
            else if (attachedNeighbors == 3)
                attach = boundary >= 1f || (neighborDiffusive < theta && boundary >= alpha);
            else
                attach = boundary >= beta;

            if (attach && reachMask[index] == 1)
            {
                stateOut[index] = 1;
                birth[index] = step + 1;
                crystalMass[index] = crystal + boundary;
                boundaryMass[index] = 0f;
                diffusiveOut[index] = 0f;
                Hlsl.InterlockedAdd(ref scratch[0], 1);
                Hlsl.InterlockedMax(ref scratch[1], step + 1);
                return;
            }

            diffusive += mu * boundary + gamma * crystal;
            boundary *= 1f - mu;
            crystal *= 1f - gamma;
        }

        if (sigma > 0f)
        {
            var noise = CrystallineGrowthShaderMath.Hash01((uint)index * 0x9E3779B9u ^ (uint)step * 0x85EBCA6Bu ^ (uint)seed * 0xC2B2AE35u);
            diffusive *= noise < 0.5f ? 1f - sigma : 1f + sigma;
        }

        stateOut[index] = 0;
        boundaryMass[index] = boundary;
        crystalMass[index] = crystal;
        diffusiveOut[index] = diffusive;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct RenderShader(
    ReadWriteBuffer<int> birth,
    ReadWriteBuffer<float> crystalMass,
    ReadWriteBuffer<int> scratch,
    ReadWriteTexture2D<Bgra32, Float4> source,
    ReadWriteTexture2D<Bgra32, Float4> output,
    int rectOffsetX,
    int rectOffsetY,
    int rectWidth,
    int rectHeight,
    int gridWidth,
    int gridHeight,
    int sourceOffsetX,
    int sourceOffsetY,
    int sourceWidth,
    int sourceHeight,
    float cellSize,
    float freeze,
    float frost,
    float refractionPixels,
    float specular,
    float colorR,
    float colorG,
    float colorB) : IComputeShader
{
    private readonly ReadWriteBuffer<int> birth = birth;
    private readonly ReadWriteBuffer<float> crystalMass = crystalMass;
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly ReadWriteTexture2D<Bgra32, Float4> source = source;
    private readonly ReadWriteTexture2D<Bgra32, Float4> output = output;
    private readonly int rectOffsetX = rectOffsetX;
    private readonly int rectOffsetY = rectOffsetY;
    private readonly int rectWidth = rectWidth;
    private readonly int rectHeight = rectHeight;
    private readonly int gridWidth = gridWidth;
    private readonly int gridHeight = gridHeight;
    private readonly int sourceOffsetX = sourceOffsetX;
    private readonly int sourceOffsetY = sourceOffsetY;
    private readonly int sourceWidth = sourceWidth;
    private readonly int sourceHeight = sourceHeight;
    private readonly float cellSize = cellSize;
    private readonly float freeze = freeze;
    private readonly float frost = frost;
    private readonly float refractionPixels = refractionPixels;
    private readonly float specular = specular;
    private readonly float colorR = colorR;
    private readonly float colorG = colorG;
    private readonly float colorB = colorB;

    public void Execute()
    {
        if (ThreadIds.X >= rectWidth || ThreadIds.Y >= rectHeight)
            return;
        var px = ThreadIds.X + rectOffsetX + 0.5f;
        var py = ThreadIds.Y + rectOffsetY + 0.5f;
        var visible = freeze * (scratch[1] + 1);

        var kernelRadius = CrystallineGrowthSettings.KernelRadiusFactor * cellSize;
        var inverseRadiusSquared = 1f / (kernelRadius * kernelRadius);
        var rowStep = cellSize * CrystallineGrowthSettings.RowStep;
        var j0 = Hlsl.Max((int)Hlsl.Floor((py - kernelRadius) / rowStep - 0.5f), 0);
        var j1 = Hlsl.Min((int)Hlsl.Ceil((py + kernelRadius) / rowStep - 0.5f), gridHeight - 1);

        var num = 0f;
        var den = 0f;
        var numX = 0f;
        var denX = 0f;
        var numY = 0f;
        var denY = 0f;
        for (var j = j0; j <= j1; j++)
        {
            var shift = 0.5f * (j & 1) + 0.5f;
            var i0 = Hlsl.Max((int)Hlsl.Floor((px - kernelRadius) / cellSize - shift), 0);
            var i1 = Hlsl.Min((int)Hlsl.Ceil((px + kernelRadius) / cellSize - shift), gridWidth - 1);
            var cy = (j + 0.5f) * rowStep;
            var dy = py - cy;
            for (var i = i0; i <= i1; i++)
            {
                var cx = (i + shift) * cellSize;
                var dx = px - cx;
                var distanceSquared = dx * dx + dy * dy;
                var u = distanceSquared * inverseRadiusSquared;
                if (u >= 1f)
                    continue;
                var oneMinusU = 1f - u;
                var weight = oneMinusU * oneMinusU;
                var weightGradientScale = -4f * oneMinusU * inverseRadiusSquared;
                var index = j * gridWidth + i;
                var cellBirth = birth[index];
                var mass = 0f;
                if (cellBirth < CrystallineGrowthSettings.BirthSentinel)
                {
                    var fade = Hlsl.Saturate(visible - cellBirth);
                    mass = fade * (0.55f + 0.45f * Hlsl.Saturate(crystalMass[index] * CrystallineGrowthSettings.MassScale));
                }
                num += weight * mass;
                den += weight;
                numX += weightGradientScale * dx * mass;
                denX += weightGradientScale * dx;
                numY += weightGradientScale * dy * mass;
                denY += weightGradientScale * dy;
            }
        }

        if (num <= 0f)
        {
            output[ThreadIds.XY] = new Float4(0f, 0f, 0f, 0f);
            return;
        }

        var safeDen = Hlsl.Max(den, 1e-4f);
        var field = num / safeDen;
        var coverage = Hlsl.SmoothStep(CrystallineGrowthSettings.CoverageLow, CrystallineGrowthSettings.CoverageHigh, field);
        if (coverage <= 0f)
        {
            output[ThreadIds.XY] = new Float4(0f, 0f, 0f, 0f);
            return;
        }

        var inverseDenSquared = 1f / (safeDen * safeDen);
        var amplitude = CrystallineGrowthSettings.NormalAmplitudeFactor * cellSize;
        var slopeX = (numX * safeDen - num * denX) * inverseDenSquared * amplitude;
        var slopeY = (numY * safeDen - num * denY) * inverseDenSquared * amplitude;
        var normal = Hlsl.Normalize(new Float3(-slopeX, -slopeY, 1f));

        var offsetScale = refractionPixels / Hlsl.Max(normal.Z, 0.5f);
        var refracted = SampleSource(
            px - normal.X * offsetScale - sourceOffsetX,
            py - normal.Y * offsetScale - sourceOffsetY);

        var frostAmount = frost * Hlsl.Saturate(field * 1.25f);
        var baseAlpha = frostAmount + refracted.W * (1f - frostAmount);
        var baseR = colorR * frostAmount + refracted.X * (1f - frostAmount);
        var baseG = colorG * frostAmount + refracted.Y * (1f - frostAmount);
        var baseB = colorB * frostAmount + refracted.Z * (1f - frostAmount);

        var highlight = specular * Hlsl.Pow(Hlsl.Saturate(normal.X * -0.2490f + normal.Y * -0.3598f + normal.Z * 0.8992f), CrystallineGrowthSettings.SpecularPower);

        var alpha = Hlsl.Saturate(coverage * baseAlpha);
        var r = Hlsl.Min(coverage * baseR + highlight * alpha, alpha);
        var g = Hlsl.Min(coverage * baseG + highlight * alpha, alpha);
        var b = Hlsl.Min(coverage * baseB + highlight * alpha, alpha);
        output[ThreadIds.XY] = new Float4(r, g, b, alpha);
    }

    private Float4 SampleSource(float x, float y)
    {
        var fx = x - 0.5f;
        var fy = y - 0.5f;
        var ix0 = (int)Hlsl.Floor(fx);
        var iy0 = (int)Hlsl.Floor(fy);
        var wx = fx - ix0;
        var wy = fy - iy0;
        var c00 = SampleTexel(ix0, iy0);
        var c10 = SampleTexel(ix0 + 1, iy0);
        var c01 = SampleTexel(ix0, iy0 + 1);
        var c11 = SampleTexel(ix0 + 1, iy0 + 1);
        return Hlsl.Lerp(Hlsl.Lerp(c00, c10, wx), Hlsl.Lerp(c01, c11, wx), wy);
    }

    private Float4 SampleTexel(int x, int y)
    {
        if (x < 0 || x >= sourceWidth || y < 0 || y >= sourceHeight)
            return new Float4(0f, 0f, 0f, 0f);
        return source[new Int2(x, y)];
    }
}

internal static class CrystallineGrowthShaderMath
{
    public static Float2 CellCenter(int i, int j, float cellSize)
        => new((i + 0.5f * (j & 1) + 0.5f) * cellSize, (j + 0.5f) * cellSize * CrystallineGrowthSettings.RowStep);

    public static int NeighborDx(int neighbor, int parity)
    {
        if (neighbor == 0)
            return -1;
        if (neighbor == 1)
            return 1;
        return neighbor == 2 || neighbor == 4 ? parity - 1 : parity;
    }

    public static int NeighborDy(int neighbor)
    {
        if (neighbor < 2)
            return 0;
        return neighbor < 4 ? -1 : 1;
    }

    public static float Hash01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value * 2.3283064e-10f;
    }
}
