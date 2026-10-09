// common utility functions first 

float get_channel(float4 input, float value, float override)
{
    float channel = 1.0f;
    if(override ==  1.0f)
    {
        channel = (value == 3) ? input.z : 1.0f;
        channel = (value == 2) ? input.y : channel;
        channel = (value == 1) ? input.x : channel;
        channel = (value == 0) ? input.w : channel;
    }
    else
    {
        channel = (value == 4) ? 1.0f : 0.0;
        channel = (value == 3) ? input.w : channel;
        channel = (value == 2) ? input.z : channel;
        channel = (value == 1) ? input.y : channel;
        channel = (value == 0) ? input.x : channel;
    }
    return channel;
}

void soft_particles(v2f i, inout float alpha)
{
    float2 ss = i.coord5.xy / i.coord5.ww;

    float depth = LinearEyeDepth(_CameraDepthTexture.Sample(linear_repeat_sampler, ss));


    depth = depth + (-i.coord5.w);
    float depth_area =  saturate(depth.x / _DepthThresh);
    depth = saturate(depth * _DepthFade);

    depth = lerp(depth, 1.0f, depth_area);
    alpha = alpha * depth;
}

// ----------------- particle functions -----------------
void one_channel(v2f i,  inout float4 output)
{    
    float channel = (_BaseTexColorChannelSwitch == 4) ? 1.0f : 0.0;
    float4 baseTex =  _BaseTex.Sample(linear_repeat_sampler, i.uv.xy);
    float chosen_channel = get_channel(baseTex, _BaseTexColorChannelSwitch, 0);
    float3 special = lerp(i.uv1.xyz, i.uv2.xyz, chosen_channel);
    float3 color = lerp(_MainColor.xyz, special.xyz, _UseCustom2ColorToggle) * _ColorBrightness;
    color.xyz = color.xyz * i.color.xyz;
    color.xyz = color.xyz * _DayColor.xyz;
    color.xyz = color.xyz + color.xyz;
    float alpha_channel = get_channel(baseTex, _BaseTexAlphaChannelSwitch, 0);
    float alpha = i.color.w * _MainColor.w;
    alpha = alpha *  alpha_channel;
    alpha = saturate(dot(alpha.xx, _AlphaBrightness.xx)) * _DayColor.w;
    if(_AlphaClipping) clip(alpha - 0.5);
    output.w = alpha;
    output.rgb = color.xyz;
}

void uv_move(v2f i, inout float4 output)
{

    float2 base_uv =  i.uv.zw;
    if(_NoiseTexToggle)
    {
        float4 noiseTex = _NoiseTex.Sample(linear_repeat_sampler, i.uv.xy);
        float noise = get_channel(noiseTex, _NoiseTexChannelSwitch, 0);

        float noise_control = _Noise_Brightness * _Noise_Offset;
        noise = _Noise_Brightness * noise + noise_control;

        base_uv = base_uv + noise;
    }

    float4 baseTex =  _BaseTex.Sample(linear_repeat_sampler, i.uv.zw);
    float channel = get_channel(baseTex, _BaseTexAlphaChannelSwitch, 1);
    float alpha = ((((channel * _BaseTexAlphaBrightness) * _AlphaBrightness) * _MainColor.w) * ( i.color.w * _DayColor.w)) * _Alpha ;

    channel = get_channel(baseTex, _BaseTexColorChannelSwitch, 0);
    float3 color = (((channel * _ColorBrightness) * _MainColor.xyz) * i.color.xyz) * _DayColor.xyz;
    output.w = alpha;
    output.xyz = color;
}

void liquid(v2f i, inout float4 output)
{
    // check the vertex color alpha first
    float vAlpha =  (-i.color.w) + 1.0f;

    // normal mapping shit
    float2 normal_uv = (float2)(vAlpha * _NormalIntensity) * i.uv1.xy + i.uv1.xy;
    float4 normal_map = _Normalmap.Sample(linear_repeat_sampler, normal_uv);
    normal_map.xyz = normal_map * (float3)2.0f - (float3)1.0f;

    float3 normal = i.normal;
    normal = mul(float3x3(normalize(i.tangent.xyz), normalize(i.bitangent.xyz), normalize(i.normal)), normal_map).xyz;
    normal = normalize(normal);

    // sample matcap now that normals are finished
    float2 sphere_uv = (mul((float3x3)UNITY_MATRIX_I_V, normal.xyz).xy * (float2)_MatcapSize) * (float2)0.5f + (float2)0.5f;
    float4 matcap = _Matcap.Sample(linear_repeat_sampler, sphere_uv);

    float2 liquid_tex = _LiquidTex.Sample(linear_repeat_sampler, i.uv.xy).xy;
    float liquid_check = vAlpha <= liquid_tex.x;
    
    float matcap_alpha =  (liquid_tex.y * liquid_check);
    matcap_alpha = (_MatcapAlphaToggle) ? matcap_alpha * matcap.w : matcap_alpha;

    float alpha = matcap_alpha * _AlphaBrightness;
    alpha = (_VertexRForLiquidOpacityToggle) ? alpha : alpha;
    alpha = (alpha * _Alpha) * _DayColor.w;
    alpha = saturate(alpha);

    float3 color = _VertexColorForLiquidColorToggle ?  i.color.xyz : _Color.xyz;
    color = color * (float3)_ColorBrightness;
    color = color * matcap;
    color = min(max(color, (float3)0.0f), (float3)_ColorBrightnessMax) * _DayColor.xyz;

    output.xyz = color;
    output.w = alpha;
}

void bolt(v2f i, inout float4 output)
{

}

void line_renderer(v2f i, inout float4 output, bool frontFacing)
{
    // fire02 tex uv creation and sampling
    float2 fire02_uv = i.uv.xy * _Fire02_Tex_ST.xy + _Fire02_Tex_ST.zw;
    // fire02_uv.y = frontFacing ? fire02_uv.y : (-fire02_uv.y);
    fire02_uv.xy = frac(_Time.yy * _Fire02Tex_UVspeed.xy) + fire02_uv.xy;
    float4 fire02_tex = _Fire02_Tex.Sample(linear_repeat_sampler, fire02_uv.xy);
    float fire02 = get_channel(fire02_tex, _Fire02Tex_Switch, 0);

    float uv_mask = -i.uv.x + 1.0f;
    float mask_a = smoothstep(_Fire02_TexMask.x, _Fire02_TexMask.y, uv_mask);
    float mask_b = smoothstep(_Fire02_TexMask.z, _Fire02_TexMask.w, uv_mask);
    float mask_comp = (mask_b * mask_a) * fire02;

    // ramp uv creation
    float2 ramp_uv = i.uv.xy * _RampTexWeak_ST.xy + _RampTexWeak_ST.zw;
    ramp_uv.y = ((-ramp_uv.y) + 1.0f) * ramp_uv.y;

    // noise uv and sampling
    float2 noise_uv = i.uv.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
    // noise_uv.y = frontFacing ? noise_uv.y : (-noise_uv.y) + _NoiseTex1TillingAdd2Offset;
    noise_uv.xy = frac(_Time.yy * float2(_NoiseTex_Uspeed, _NoiseTex_Vspeed)) + noise_uv.xy;

    float4 noise_tex = _NoiseTex.Sample(linear_repeat_sampler, noise_uv.xy);
    float noise = get_channel(noise_tex, _NoiseTexSwitch, 0);
    noise.x = noise.x * _NoiseInt + _NoiseOffset;

    // back to ramp bullshit
    ramp_uv = max(ramp_uv, (float2)0.0001f);
    ramp_uv = pow(ramp_uv, _RampTexScale);
    ramp_uv = _RampTexNoiseBrightness * noise.x + ramp_uv;

    // sample both ramps
    float3 weak = _RampTexWeak.Sample(linear_repeat_sampler, ramp_uv.xy);
    float3 strong = _RampTexStrong.Sample(linear_repeat_sampler, ramp_uv.xy);

    float3 ramp = lerp(weak, strong, _RampTexWeakStrongLerp);

    // fire 01 uv and sampling
    float2 fire_uv = i.uv.xy * _FireNoiseTex_ST.xy + _FireNoiseTex_ST.zw;
    // fire_uv.y = frontFacing ? fire_uv.y : (-fire_uv.y) + -0.25f;
    fire_uv.xy = frac(_Time.yy * _FireNoiseTex_UVspeed.xy) + fire_uv.xy;

    // sample fire noise 
    float4 firenoise_tex = _FireNoiseTex.Sample(linear_repeat_sampler, fire_uv.xy);
    float fire_noise = get_channel(firenoise_tex, _FireNoiseTex_Switch, 0);

    fire_uv.xy = i.uv.xy * _FireTex_ST.xy + _FireTex_ST.zw;
    fire_uv.x = sin(_Time.y * _FireSpeedTime) * _FireSpeedInt + fire_uv.y;
    fire_uv.xy = fire_noise.xx * (_FireNoiseInt) + fire_uv.xy;
    float4 fire_tex = _FireTex.Sample(linear_repeat_sampler, fire_uv.xy);
    float fire = get_channel(fire_tex, _FireTex_Switch, 0);

    ramp = lerp(ramp * _RampTexBrightness, _FireColor, fire);

    // hightlight mask, uv, and sampling
    float high_mask = smoothstep(_HighlightTex_Mask.x, _HighlightTex_Mask.y, i.uv.x);

    float2 high_uv = i.uv.xy * _HighlightTex_ST.xy + _HighlightTex_ST.zw;
    // high_uv.y = frontFacing ? high_uv.y :  (-high_uv.y) + _HighlightTex1TillingAdd2Offset;
    high_uv.xy = frac(_Time.yy * float2(_HighlightTex_Uspeed, _HighlightTex_Vspeed)) + high_uv.xy;
    float4 high_tex = _HighlightTex.Sample(linear_repeat_sampler, high_uv.xy);
    float high = get_channel(high_tex, _HighlightTex_Switch, 0);

    float3 high_color = ramp * _HighlightTex_LightColor;
    fire = min(fire, 1.0f);

    float highrange = saturate(high * _HighlightSoft + -(-_HighlightRange) + float(1.0) * (-_HighlightSoft) + float(-1.0) + _HighlightSoft);
    fire = fire * highrange;

    ramp.xyz = ramp.xyz * _HighlightTex_DarkColor + (-high_color.xyz);
    ramp.xyz = fire.xxx * ramp.xyz + high_color.xyz;

    // dissolve uv and sampling
    float2 dissolve_uv = i.uv.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
    // dissolve_uv.y = frontFacing ? dissolve_uv.y : (-dissolve_uv.y) + _DissovleTex1TillingAdd2Offset;
    dissolve_uv.xy = (frac(_Time.yy * float2(_DissolveTex_Uspeed, _DissolveTex_Vspeed)) + float2(_DissolveTex_Uoffset, _DissolveTex_Voffset)) + dissolve_uv.xy;
    float4 diss_tex = _DissolveTex.Sample(linear_repeat_sampler, dissolve_uv.xy);
    float dissolve = get_channel(diss_tex, _DissolveTex_Switch, 0);

    // mask uv and sampling
    float2 mask_uv = i.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
    // mask_uv.y = frontFacing ? mask_uv.y : (-mask_uv.y) + _MaskTex1TillingAdd2Offset;
    mask_uv.xy = frac(_Time.y * float2(_MaskTex_Uspeed, _MaskTex_Vspeed)) + mask_uv.xy;
    float4 mask_tex = _MaskTex.Sample(linear_repeat_sampler, mask_uv.xy);
    float mask = get_channel(mask_tex, _MaskTexSwitch, 0);

    float state_switch = _Switch + -0.25;
    state_switch = (-state_switch) + i.uv.x;
    state_switch = state_switch + state_switch;

    state_switch = clamp(state_switch, 0.0, 1.0);
    state_switch = state_switch * state_switch;
    state_switch = (-(state_switch * -2.0 + 3.0)) * state_switch + 1.0;
    state_switch = max(state_switch, 0.0);

    float top = mask + _DissolveRange1;
    top.x = state_switch + top.x;
    top.x = (-top.x) + 1.0;
    top = saturate(top);

    float bot = (-_DissolveSoft1) + -1.0;
    top.x = top.x * bot.x + _DissolveSoft1;
    top.x = dissolve.x * _DissolveSoft1 + (-top.x);

    top.x = clamp(top.x, 0.0, 1.0);

    float outline = _DissolveRange1 + _OutlineWidth;
    float outline_mask = mask + outline;
    outline_mask = state_switch + outline_mask;
    outline_mask = (-outline_mask) + 1.0;

    outline_mask = clamp(outline_mask, 0.0, 1.0);

    outline_mask = outline_mask * bot.x + _DissolveSoft1;
    dissolve.x = dissolve.x * _DissolveSoft1 + (-outline_mask);

    dissolve.x = clamp(dissolve.x, 0.0, 1.0);

    dissolve.x = (-dissolve.x) + top.x;
    ramp.xyz = mask_comp.xxx * _Fire02_Color.xyz + ramp.xyz;

    float another_mask = smoothstep(0.6, 0.4f, i.uv.x);

    another_mask.x = another_mask.x * dissolve.x;
    ramp = lerp(ramp, _OutlineColor, another_mask.x);
    
    ramp.xyz = ramp.xyz * _AllColorBrightness.xyz;
    ramp.xyz = ramp.xyz * _DayColor.xyz;


    // final mask2 
    float2 mask2_uv = i.uv.xy * _Mask2_ST.xy + _Mask2_ST.zw;
    // mask2_uv.y = frontFacing ? mask2_uv.y : (-mask2_uv.y) + 1.0;
    mask2_uv.xy = frac(_Time.yy * _Mask2Speed.xy) + mask2_uv.xy;
    mask2_uv.xy = noise.xx * _Mask2NoiseInt + mask2_uv.xy;
    float4 mask2_tex = _Mask2.Sample(linear_repeat_sampler, mask2_uv.xy);
    float mask2 =  get_channel(mask2_tex, _Mask2Switch, 0);


    float3 view = normalize(i.view);
    float rim = dot(normalize(i.normal.xyz), view.xyz);
    rim.x = max(abs(rim.x), 0.0001f);
    rim.x = pow(rim.x, _EdgeBlurPower);
    rim.x = rim.x / _EdgeBlur;

    rim = saturate(rim);
    float alpha = saturate(rim.x * (mask2.x * top.x));

    output.xyz = ramp;
    output.w = alpha;
    if(_AlphaClipping)clip(alpha - 0.5);
}