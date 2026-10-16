float4 sample_texture(SamplerState smp, Texture2D tex, float2 uv)
{
    return tex.Sample(smp, uv);
}

float4 sample_bloom_texture(Texture2D tex, SamplerState smp, float2 uv, float4 texelsize)
{
    float4 tmp = sample_texture(smp, tex, uv);
    return tmp;
}  

float4 prefilter(float4 color)
{   
    if(_GameType == 1.0f || _GameType == 3.0f)
    {
        color = max(color - _MHYBloomThreshold, 0.0f) * _MHYBloomScaler;
    }
    else if(_GameType >= 2.0f)
    {
        float3 bloomColor = -float3(_BloomR, _BloomG, _BloomB) + 1.0f;
        bloomColor = bloomColor * _BloomThreshold;
        color.xyz = max(-bloomColor + color.xyz, 0.0f) * _MHYBloomScaler;
    }
    else
    {
        color.xyz = (float3)0.0f;
    }
    return color;
}

float4 bloom_blur_h(Texture2D tex, SamplerState smp, float2 uv, float2 texelsize)
{  
    float2 vs_TEXCOORD0;
    float4 vs_TEXCOORD1;
    float4 u_xlat0;
    float4 SV_Target0;
    

    // Transform UV coordinates
    vs_TEXCOORD0.xy = uv + _UVTransformSource.zw;
    
    // Calculate UV boundaries
    float4 uvBoundaries = (_UVTransformSource.xxyy * float4(0.0, 1.0, 0.0, 1.0)) + _UVTransformSource.zzww;
    vs_TEXCOORD1 = (texelsize.xxyy * float4(1.0, -1.0, 1.0, -1.0)) + uvBoundaries;

    // For horizontal blur, we only need x component
    texelsize.y = 0.0f;
    
    // Sample with Gaussian weights at different offsets
    // First far negative offsets (-6.13, -4.22)
    float4 farLeftUVs = (texelsize.xyxy * float4(-6.1384072, -6.1384072, -4.2199531, -4.2199531)) + vs_TEXCOORD0.xyxy;
    farLeftUVs = max(farLeftUVs, vs_TEXCOORD1.xzxz); // Clamp to boundaries
    farLeftUVs = min(farLeftUVs, vs_TEXCOORD1.ywyw);
    
    float4 sample1 = sample_texture(smp, tex, farLeftUVs.zw);
    float4 sample0 = sample_texture(smp, tex, farLeftUVs.xy);
    
    // Apply weights for Gaussian blur
    float4 weightedResult = sample1 * float4(0.028576409, 0.028576409, 0.028576409, 0.028576409);
    weightedResult += sample0 * float4(0.0015526291, 0.0015526291, 0.0015526291, 0.0015526291);
    
    // Medium negative offsets (-2.33, -0.46)
    float4 midLeftUVs = (texelsize.xyxy * float4(-2.3310809, -2.3310809, -0.4648928, -0.4648928)) + vs_TEXCOORD0.xyxy;
    midLeftUVs = max(midLeftUVs, vs_TEXCOORD1.xzxz);
    midLeftUVs = min(midLeftUVs, vs_TEXCOORD1.ywyw);
    
    float4 sample2 = sample_texture(smp, tex, midLeftUVs.xy);
    float4 sample3 = sample_texture(smp, tex, midLeftUVs.zw);
    
    weightedResult += sample2 * float4(0.1802229, 0.1802229, 0.1802229, 0.1802229);
    weightedResult += sample3 * float4(0.3954528, 0.3954528, 0.3954528, 0.3954528);
    
    // Positive offsets (1.39, 3.27)
    float4 rightUVs = (texelsize.xyxy * float4(1.3960429, 1.3960429, 3.271976, 3.271976)) + vs_TEXCOORD0.xyxy;
    rightUVs = max(rightUVs, vs_TEXCOORD1.xzxz);
    rightUVs = min(rightUVs, vs_TEXCOORD1.ywyw);
    
    float4 sample4 = sample_texture(smp, tex, rightUVs.xy);
    float4 sample5 = sample_texture(smp, tex, rightUVs.zw);
    
    weightedResult += sample4 * float4(0.3043977, 0.3043977, 0.3043977, 0.3043977);
    weightedResult += sample5 * float4(0.081959292, 0.081959292, 0.081959292, 0.081959292);
    
    // Far positive offsets (5.17, 7.0)
    float4 farRightUVs = (texelsize.xyxy * float4(5.1754818, 5.1754818, 7.0, 7.0)) + vs_TEXCOORD0.xyxy;
    farRightUVs = max(farRightUVs, vs_TEXCOORD1.xzxz);
    farRightUVs = min(farRightUVs, vs_TEXCOORD1.ywyw);
    
    float4 sample6 = sample_texture(smp, tex, farRightUVs.xy);
    float4 sample7 = sample_texture(smp, tex, farRightUVs.zw);
    
    weightedResult += sample6 * float4(0.0076231952, 0.0076231952, 0.0076231952, 0.0076231952);
    SV_Target0 = weightedResult + sample7 * float4(0.00021489531, 0.00021489531, 0.00021489531, 0.00021489531);
    
    return SV_Target0;
}

float4 bloom_blur_v(Texture2D tex, SamplerState smp, float2 uv, float2 texelsize)
{
    // Define input/output variables with clear names
    float2 texCoord;
    float4 texBoundaries;
    float4 tempSample;
    float4 finalColor;
    float4 weightedColor;
    float4 sampleResult;

    // Transform UV coordinates
    texCoord.xy = uv + _UVTransformSource.zw;
    
    // Calculate texture boundaries
    float4 boundaries = (_UVTransformSource.xxyy * float4(0.0, 1.0, 0.0, 1.0)) + _UVTransformSource.zzww;
    texBoundaries = ((texelsize.xxyy * float4(1.0, -1.0, 1.0, -1.0)) + boundaries);

    // Disable horizontal sampling by setting x component to 0
    texelsize.x = 0.0f;
    
    // Sample with far negative offsets (-6.13, -4.22)
    float4 farNegativeUVs = ((texelsize.xyxy * float4(-6.1384072, -6.1384072, -4.2199531, -4.2199531)) + texCoord.xyxy);
    farNegativeUVs = max(farNegativeUVs, texBoundaries.xzxz); // Clamp to boundaries
    farNegativeUVs = min(farNegativeUVs, texBoundaries.ywyw);
    
    float4 sampleFarNeg2 = sample_texture(smp, tex, farNegativeUVs.zw);
    float4 sampleFarNeg1 = sample_texture(smp, tex, farNegativeUVs.xy);
    
    // Apply Gaussian weights to samples
    weightedColor = sampleFarNeg2 * float4(0.028576409, 0.028576409, 0.028576409, 0.028576409);
    weightedColor += sampleFarNeg1 * float4(0.0015526291, 0.0015526291, 0.0015526291, 0.0015526291);
    
    // Sample with medium negative offsets (-2.33, -0.46)
    float4 medNegativeUVs = ((texelsize.xyxy * float4(-2.3310809, -2.3310809, -0.4648928, -0.4648928)) + texCoord.xyxy);
    medNegativeUVs = max(medNegativeUVs, texBoundaries.xzxz);
    medNegativeUVs = min(medNegativeUVs, texBoundaries.ywyw);
    
    float4 sampleMedNeg1 = sample_texture(smp, tex, medNegativeUVs.xy);
    float4 sampleMedNeg2 = sample_texture(smp, tex, medNegativeUVs.zw);
    
    weightedColor += sampleMedNeg1 * float4(0.1802229, 0.1802229, 0.1802229, 0.1802229);
    weightedColor += sampleMedNeg2 * float4(0.3954528, 0.3954528, 0.3954528, 0.3954528);
    
    // Sample with positive offsets (1.39, 3.27)
    float4 positiveUVs = ((texelsize.xyxy * float4(1.3960429, 1.3960429, 3.271976, 3.271976)) + texCoord.xyxy);
    positiveUVs = max(positiveUVs, texBoundaries.xzxz);
    positiveUVs = min(positiveUVs, texBoundaries.ywyw);
    
    float4 samplePos1 = sample_texture(smp, tex, positiveUVs.xy);
    float4 samplePos2 = sample_texture(smp, tex, positiveUVs.zw);
    
    weightedColor += samplePos1 * float4(0.3043977, 0.3043977, 0.3043977, 0.3043977);
    weightedColor += samplePos2 * float4(0.081959292, 0.081959292, 0.081959292, 0.081959292);
    
    // Sample with far positive offsets (5.17, 7.0)
    float4 farPositiveUVs = ((texelsize.xyxy * float4(5.1754818, 5.1754818, 7.0, 7.0)) + texCoord.xyxy);
    farPositiveUVs = max(farPositiveUVs, texBoundaries.xzxz);
    farPositiveUVs = min(farPositiveUVs, texBoundaries.ywyw);
    
    float4 sampleFarPos1 = sample_texture(smp, tex, farPositiveUVs.xy);
    float4 sampleFarPos2 = sample_texture(smp, tex, farPositiveUVs.zw);
    
    weightedColor += sampleFarPos1 * float4(0.0076231952, 0.0076231952, 0.0076231952, 0.0076231952);
    finalColor = weightedColor + sampleFarPos2 * float4(0.00021489531, 0.00021489531, 0.00021489531, 0.00021489531);
    
    return finalColor;
}

float4 bloom_blur_a(Texture2D tex, SamplerState smp, float2 uv, float2 texelsize, float2 scaler)
{
    float4 vs_TEXCOORD1;
    float4 vs_TEXCOORD0;

    vs_TEXCOORD0.xy = uv;
    vs_TEXCOORD1 = ((texelsize.xxyy * float4(1.0, -1.0, 1.0, -1.0)) + ((_UVTransformSource.xxyy * float4(0.0, 1.0, 0.0, 1.0)) + _UVTransformSource.zzww));

    scaler = scaler * texelsize;
    float4 output = float4(0, 0, 0, 0);
    static const float2 offsets[9] = {
        float2(-7.1588202, -7.1588202), float2(-5.2274981, -5.2274981),
        float2(-3.3147621, -3.3147621), float2(-1.417412, -1.417412),
        float2(0.47224459, 0.47224459), float2(2.364548, 2.364548),
        float2(4.268898, 4.268898), float2(6.1908078, 6.1908078),
        float2(8.0, 8.0)
    };
    static const float weights[9] = {
        0.00096486782, 0.01512981, 0.1009583, 0.28889999,
        0.35640359, 0.1897708, 0.043465629, 0.0042536259,
        0.0001532399
    };
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        float4 coord = ((scaler.xyxy * float4(offsets[i] * _bloomRadius, offsets[i] * _bloomRadius)) + vs_TEXCOORD0.xyxy);
        coord = max(coord, vs_TEXCOORD1.xzxz);
        coord = min(coord, vs_TEXCOORD1.ywyw);
        float4 tmp = sample_texture(smp, tex, coord.xy);
        output += tmp * weights[i];
    }

    return output;
}

float4 bloom_blur_b(Texture2D tex, SamplerState smp, float2 uv, float2 texelsize, float2 scaler)
{
    float4 vs_TEXCOORD1;
    float4 vs_TEXCOORD0;

    vs_TEXCOORD0.xy = uv;
    vs_TEXCOORD1 = ((texelsize.xxyy * float4(1.0, -1.0, 1.0, -1.0)) + ((_UVTransformSource.xxyy * float4(0.0, 1.0, 0.0, 1.0)) + _UVTransformSource.zzww));

    scaler = scaler * texelsize;

    float4 output = float4(0, 0, 0, 0);
    float2 offsets[16] = {
        float2(-14.26509, -14.26509), float2(-12.29338, -12.29338),
        float2(-10.32336, -10.32336), float2(-8.3548632, -8.3548632),
        float2(-6.3876772, -6.3876772), float2(-4.4215422, -4.4215422),
        float2(-2.456162, -2.456162), float2(-0.49121079, -0.49121079),
        float2(1.473654, 1.473654), float2(3.4387779, 3.4387779),
        float2(5.4044962, 5.4044962), float2(7.3711209, 7.3711209),
        float2(9.338933, 9.338933), float2(11.30817, 11.30817),
        float2(13.27902, 13.27902), float2(15.0, 15.0)
    };
    float weights[16] = {
        0.00014632, 0.00094709668, 0.0046462719, 0.01727958,
        0.048726629, 0.1042022, 0.1690129, 0.207937,
        0.1940565, 0.13737381, 0.073762059, 0.03003788,
        0.0092757363, 0.002171654, 0.00038539231, 3.8788519e-05
    };
    [unroll]
    for (int i = 0; i < 16; i++)
    {
        float4 coord = ((scaler.xyxy * float4(offsets[i] * _bloomRadius, offsets[i] * _bloomRadius)) + vs_TEXCOORD0.xyxy);
        coord = max(coord, vs_TEXCOORD1.xzxz);
        coord = min(coord, vs_TEXCOORD1.ywyw);
        float4 tmp = sample_texture(smp, tex, coord.xy);
        output += tmp * weights[i];
    }
    return output;
}

float4 bloom_blur_c(Texture2D tex, SamplerState smp, float2 uv, float2 texelsize, float2 scaler)
{
    float4 u_xlat0;

    float4 vs_TEXCOORD1;
    float4 vs_TEXCOORD0;

    vs_TEXCOORD0.xy = uv;
    (vs_TEXCOORD1 = ((texelsize.xxyy * float4(1.0, -1.0, 1.0, -1.0)) + ((_UVTransformSource.xxyy * float4(0.0, 1.0, 0.0, 1.0)) + _UVTransformSource.zzww)));

    scaler = scaler * texelsize;

    float4 output = float4(0, 0, 0, 0);
    
    float2 offsets[20] = {
        float2(-18.303, -18.303), float2(-16.322, -16.322),
        float2(-14.342, -14.342), float2(-12.363, -12.363),
        float2(-10.384, -10.384), float2(-8.406, -8.406),
        float2(-6.427, -6.427), float2(-4.450, -4.450),
        float2(-2.472, -2.472), float2(-0.494, -0.494),
        float2(1.483, 1.483), float2(3.461, 3.461),
        float2(5.438, 5.438), float2(7.416, 7.416),
        float2(9.395, 9.395), float2(11.373, 11.373),
        float2(13.353, 13.353), float2(15.332, 15.332),
        float2(17.313, 17.313), float2(19.000, 19.000)
    };
    float weights[20] = {
        0.000083, 0.000393, 0.001564, 0.005202,
        0.014478, 0.033726, 0.065747, 0.107267,
        0.146470, 0.167388, 0.160103, 0.128165,
        0.085869, 0.048149, 0.022595, 0.008873,
        0.002916, 0.000802, 0.000185, 0.000025
    };
    [unroll]
    for (int i = 0; i < 20; i++)
    {
        float4 coord = ((scaler.xyxy * float4(offsets[i] * _bloomRadius, offsets[i] * _bloomRadius)) + vs_TEXCOORD0.xyxy);
        coord = max(coord, vs_TEXCOORD1.xzxz);
        coord = min(coord, vs_TEXCOORD1.ywyw);
        float4 tmp = sample_texture(smp, tex, coord.xy);
        output += tmp * weights[i];
    }

    return output;
}

float4 sharpening(Texture2D tex, SamplerState smp, float2 uv, float rate)
{
    float2 texelSize = float2(ddx(uv.x), ddy(uv.y));

    float neighbour = rate * -1;
    float center = rate * 4 + 1;

    float4 color = sample_texture(smp, tex, uv);
    float4 color1 = sample_texture(smp, tex, uv + float2(texelSize.x, 0.0f));
    float4 color2 = sample_texture(smp, tex, uv + float2(-texelSize.x, 0.0f));
    float4 color3 = sample_texture(smp, tex, uv + float2(0.0f, texelSize.y));
    float4 color4 = sample_texture(smp, tex, uv + float2(0.0f, -texelSize.y));
    float4 final = (color * center) + (color1 * neighbour) + (color2 * neighbour) + (color3 * neighbour) + (color4 * neighbour);
    final.a = color.a;
    return saturate(final);
}

float4 vignette(float4 color, float2 uv)
{
    float4 finalColor = color;
    float2 offsetUV = uv - _Vignette_Params2.xy;
    float2 absOffsetUV = abs(offsetUV) * _Vignette_Params2.zz;
    
    float vignetteStrength = dot(absOffsetUV, absOffsetUV);
    vignetteStrength = max(1.0 - vignetteStrength, 0.0);
    vignetteStrength = pow(vignetteStrength, _Vignette_Params2.w);
    
    float3 vignetteColor = lerp(_Vignette_Params1.xyz, float3(1.0, 1.0, 1.0), vignetteStrength);
    
    // Apply dithering
    float2 noiseCoord = uv.xy;
    float dither;
    dither.x = ((uv.y * 543.31f) + uv.x);
    dither.x = sin(dither.x);
    dither.x = (dither.x * 493013.0f);
    dither.x = frac(dither.x);
    dither = (dither.x + -0.5f);
    dither = (dither * 0.004f) + vignetteColor;
    
    finalColor.xyz *= dither;
    return saturate(finalColor);
}

float4 tone_mapping(float4 color, float4 bloom, float2 uv, float mask)
{
    float4 final = color;
    final.xyz = bloom * _MHYBloomIntensity + color;
    final.xyz = final * _MHYBloomExposure;

    float3 tmp = final.xyz;
    float3 f0 = (1.36 * final + 0.047) * final;
    float3 f1 = (0.93 * final + 0.56) * final + 0.14;
    final.xyz = saturate(f0 / f1);
    final.xyz = ((_MHYBloomTonemapping) ? (final.xyz) : (tmp.xyz));

    float dither;
    dither.x = ((uv.y * 543.31f) + uv.x);
    dither.x = sin(dither.x);
    dither.x = (dither.x * 493013.0f);
    dither.x = frac(dither.x);
    dither = (dither.x + -0.5f);
    final.xyz = dither * 0.004f + final.xyz;

    return final;
}

float3 LUT_2D(float3 color, float4 lutParams)
{   
    // Apply initial transformations to the color
    float3 adjustedColor = color.zxy * 5.55555582f + 0.0479959995f;
    adjustedColor = log2(adjustedColor);
    adjustedColor = adjustedColor * 0.0734997839f + 0.386036009f;
    adjustedColor = clamp(adjustedColor, 0.0, 1.0);

    // Calculate LUT coordinates
    float3 lutCoord = adjustedColor * lutParams.z;
    float xCoord = floor(lutCoord.x);
    
    // Calculate base and next LUT sample positions
    float2 lutSize = lutParams.xy * 0.5;
    float2 lutPos = lutCoord.yz * lutParams.xy + lutSize;
    float2 lutPos1 = float2(xCoord * lutParams.y + lutPos.x, lutPos.y);
    float2 lutPos2 = lutPos1 + float2(lutParams.y, 0);
    
    // invert the lutPos1 and lutPos2 y components but only if the game type is wuthering waves
    // this is only because the lut that i ripped from the game is flipped... thanks unreal...
    if(_GameType == 3.0f)
    {
        lutPos1.y = 1.0f - lutPos1.y;
        lutPos2.y = 1.0f - lutPos2.y;
    }

    // Sample the LUT
    float3 sample1 = sample_texture(sampler_linear_clamp, _Lut2DTex, lutPos1).rgb;
    float3 sample2 = sample_texture(sampler_linear_clamp, _Lut2DTex, lutPos2).rgb;

    // Interpolate between the two 
    float lerpFactor = lutCoord.x - xCoord;
    float3 lutColor = lerp(sample1, sample2, lerpFactor);

    // Clamp the final color
    lutColor = saturate(lutColor);

    if(_GameType == 3.0f) // wuthering waves is fucking weird
    {
        lutColor = pow(lutColor, 2.0f);
    }

    return lutColor;
}

float4 tone_mapping_star_rail(float4 color, float4 bloom, float2 uv)
{
    float4 final = max(color, 0.0f);
    final.xyz = bloom * _MHYBloomIntensity + color;
    final.xyz = LUT_2D(final, _Lut2DTexParam);
    return final;   
}
