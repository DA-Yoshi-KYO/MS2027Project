vs_out vs_model(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    UNITY_SETUP_INSTANCE_ID(v); 
    UNITY_INITIALIZE_OUTPUT(vs_out, o); 
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); 
    #if defined(use_vat)
        if((0.0 /*_VertexAnimType*/) == 1)
        {
            mavuika_vat_vs(v.vertex, v.uv_1.xy, v.normal, v.v_col);
        }
        else if ((0.0 /*_VertexAnimType*/) == 2)
        {
            o.pos = v.vertex;
            float4 worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
            float4 clipSpacePosition = mul(UNITY_MATRIX_VP, worldSpacePosition);
            o.pos = clipSpacePosition;
            bool shouldHideVertex = v.v_col.w == 0 || v.v_col.w == 1;
            shouldHideVertex = ((0.0 /*_DisappearByVerColAlpha*/)) ? shouldHideVertex : false;
            if(shouldHideVertex)
            {
                o = (vs_out)0.0f;
                return o;
            }
            float4 vertexColor;
            float3 texCoord0;
            float3 texCoord1;
            float3 texCoord2;
            float4 texCoord3;
            float4 texCoord4;
            float4 tempVec0;
            uint4 tempUintVec0;
            bool2 tempBoolVec0;
            float4 tempVec1;
            float4 tempVec2;
            float3 normalizedTangent;
            float3 normalizedBitangent;
            float3 normalizedNormal;
            float3 tempVec4;
            uint3 tempUintVec4;
            float3 boundMin;
            float3 boundSize;
            float3 positionA;
            float3 positionB;
            bool isAutoPlayback;
            float2 frameUV;
            float currentFrameNormalized;
            bool isPositiveFrame;
            float frameTime;
            bool isNextFrameZero;
            float normalLength;
            float tangentLength;
            float verticalUV = (-v.uv_1.y) + 1.0;
            isAutoPlayback = 9.99999975e-06 < (1.0 /*_IfAutoPlayback*/);
            frameTime = _Time.y * (1.0 /*_Speed*/);
            frameTime = frameTime * (30.0 /*_HoudiniFPS*/);
            frameTime = frameTime / (100.0 /*_FrameCount*/);
            frameTime = frac(frameTime);
            float frameIndex = frameTime * (100.0 /*_FrameCount*/);
            float frameFraction = frac(frameIndex);
            frameIndex = floor(frameIndex);
            frameIndex = frameIndex / (100.0 /*_FrameCount*/);
            isPositiveFrame = frameIndex >= (-frameIndex);
            frameIndex = frac(abs(frameIndex));
            frameUV.y = (isPositiveFrame) ? frameIndex : (-frameIndex);
            float nextFrameIndex = frameTime * (100.0 /*_FrameCount*/) + 1.0;
            nextFrameIndex = floor(nextFrameIndex);
            nextFrameIndex = nextFrameIndex / (100.0 /*_FrameCount*/);
            isPositiveFrame = nextFrameIndex >= (-nextFrameIndex);
            nextFrameIndex = frac(abs(nextFrameIndex));
            frameUV.x = (isPositiveFrame) ? nextFrameIndex : (-nextFrameIndex);
            frameUV.xy = frameUV.xy * float2((100.0 /*_FrameCount*/), (100.0 /*_FrameCount*/));
            isNextFrameZero = abs(frameUV.x) < 9.99999975e-06;
            tempVec1.z = (isNextFrameZero) ? 0.0 : frameFraction;
            frameUV.xy = frameUV.xy / float2((100.0 /*_FrameCount*/), (100.0 /*_FrameCount*/));
            frameUV.xy = verticalUV + frameUV.xy;
            tempVec1.xy = (-frameUV.yx) + float2(1.0, 1.0);
            float manualFrameIndex = floor((0.0 /*_CurrentFrame*/));
            float prevManualFrameIndex = manualFrameIndex - 1.0;
            frameUV.xy = float2(manualFrameIndex, prevManualFrameIndex) / float2((100.0 /*_FrameCount*/), (100.0 /*_FrameCount*/));
            isPositiveFrame = frameUV.y >= (-frameUV.y);
            frameIndex = frac(abs(frameUV.y));
            frameUV.y = (isPositiveFrame) ? frameIndex : (-frameIndex);
            isPositiveFrame = frameUV.x >= (-frameUV.x);
            frameIndex = frac(abs(frameUV.x));
            frameUV.x = (isPositiveFrame) ? frameIndex : (-frameIndex);
            frameUV.xy = frameUV.xy * float2((100.0 /*_FrameCount*/), (100.0 /*_FrameCount*/));
            frameUV.xy = frameUV.xy / float2((100.0 /*_FrameCount*/), (100.0 /*_FrameCount*/));
            tempVec0.w = verticalUV + frameUV.y;
            tempVec0.x = verticalUV + frameUV.x;
            tempVec2.xy = (-tempVec0.wx) + float2(1.0, 1.0);
            tempVec2.z = frac((0.0 /*_CurrentFrame*/));
            tempVec0.xyz = (isAutoPlayback) ? tempVec1.xyz : tempVec2.xyz;
            normalLength = dot(v.normal.xyz, v.normal.xyz);
            normalLength = rsqrt(normalLength);
            normalizedNormal = normalLength * v.normal.xyz;
            tangentLength = dot(v.tangent.xyz, v.tangent.xyz);
            tangentLength = rsqrt(tangentLength);
            normalizedTangent = tangentLength * v.tangent.xyz;
            normalizedBitangent = cross(normalizedNormal, normalizedTangent);
            tempVec0.w = v.uv_1.x;
            float3 sampledPosition = _PosTex_A.SampleLevel(sampler_linear_repeat, tempVec0.wx, 0).xyz;
            float3 decodedPosition = sampledPosition * float3(255.0, 255.0, 255.0);
            tempUintVec4.xyz = uint3(decodedPosition);
            tempUintVec4.xyz = (uint3)tempUintVec4.xyz << int3(8, 8, 8);
            tempVec4.xyz = uint3(tempUintVec4.xyz);
            tempVec4.xyz = tempVec4.xyz * float3(1.52590219e-05, 1.52590219e-05, 1.52590219e-05);
            boundMin.x = (0.0 /*_BoundMinX*/);
            boundMin.yz = float2((0.0 /*_BoundMinY*/), (0.0 /*_BoundMinZ*/));
            boundSize.xyz = float3((0.0 /*_BoundMaxX*/), (0.0 /*_BoundMaxY*/), (0.0 /*_BoundMaxZ*/)) - boundMin.xyz;
            positionA.xyz = tempVec4.xyz * boundSize.xyz + boundMin.xyz;
            if((1.0 /*_IfInterframeInterp*/)) {
                float3 nextFramePosition = _PosTex_A.SampleLevel(sampler_linear_repeat, tempVec0.wy, 0).xyz;
                float3 decodedNextPosition = nextFramePosition * float3(255.0, 255.0, 255.0);
                tempUintVec0.xyw = uint3(decodedNextPosition);
                tempUintVec0.xyw = (uint3)tempUintVec0.xyw << int3(8, 8, 8);
                tempVec0.xyw = uint3(tempUintVec0.xyw);
                tempVec0.xyw = tempVec0.xyw * float3(1.52590219e-05, 1.52590219e-05, 1.52590219e-05);
                positionB.xyz = tempVec0.xyw * boundSize.xyz + boundMin.xyz;
                float3 positionDelta = positionB.xyz - positionA.xyz;
                tempVec0.xyz = positionDelta * tempVec0.zzz + positionA.xyz;
            } else {
                tempVec0.xyz = positionA.xyz;
            }
            float3 tangentOffset = normalizedBitangent * tempVec0.yyy;
            float3 combinedOffset = tempVec0.xxx * normalizedTangent + tangentOffset;
            float3 finalOffset = tempVec0.zzz * normalizedNormal + combinedOffset;
            float3 finalPosition = finalOffset + v.vertex.xyz;
            o.pos = UnityObjectToClipPos(finalPosition);
            float3 worldNormal;
            worldNormal.x = dot(v.normal.xyz, unity_WorldToObject[0].xyz);
            worldNormal.y = dot(v.normal.xyz, unity_WorldToObject[1].xyz);
            worldNormal.z = dot(v.normal.xyz, unity_WorldToObject[2].xyz);
            o.normal.xyz = normalize(worldNormal);
            float3 worldPosition = unity_ObjectToWorld[3].xyz * v.vertex.www + finalPosition;
            float3 viewDirection = _WorldSpaceCameraPos.xyz - worldPosition;
            o.view = normalize(viewDirection);
            float2 screenPos = o.pos.xw * float2(0.5, 0.5);
            float projectionY = o.pos.y * _ProjectionParams.x;
            float2 projectionPos = float2(screenPos.x, projectionY * 0.5);
            texCoord4.xy = o.pos.zz + projectionPos;
            o.v_col = v.v_col;
            o.uv_a = v.uv_0.xyxy;
            o.ss_pos = ComputeScreenPos(o.pos);
            return o;
        }
    #endif
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    #if defined(_is_shadow)
    if((0.0 /*_UseHairShadow*/) && ((0.0 /*variant_selector*/) == 4)) // if material type is bangs
    {
        float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
        float3 vl = mul(_WorldSpaceLightPos0.xyz, UNITY_MATRIX_V) * (1.f / ws_pos.w);
        float3 offset_pos = ((vl * .001f) * float3(7,-1,5)) + v.vertex.xyz;
        v.vertex.xyz = offset_pos;
        o.pos = UnityObjectToClipPos(v.vertex);
    }
    else if((0.0 /*_UseHairShadow*/) && ((0.0 /*variant_selector*/) == 1)) // if material type is face
    {
        o.pos = pos_wvp;
    }
    else
    {
        o.pos = float4(-90,-90,-90,1.0f); // if shadow pass, set position to a negative value so it doesn't render
    }
    #endif
    o.uv_a.xy = v.uv_0.xy;
    o.uv_a.zw = v.uv_1.xy;
    o.uv_b.xy = v.uv_2.xy;
    o.uv_b.zw = v.uv_3.xy;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    o.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent);
    o.tangent.w = v.tangent.w * unity_WorldTransformParams.w;
    o.view = camera_position() - mul(unity_ObjectToWorld, v.vertex).xyz;
    o.n_view.xyz = -mul(unity_ObjectToWorld, v.vertex).xyz + unity_ObjectToWorld[3].xyz;
    o.n_view = float4((0.0 /*_DummyFixedForNormal*/) ? o.n_view : o.view, 1.0f);
    o.ws_pos =  v.vertex;
    o.ppos = o.pos;
    o.ss_pos = ComputeScreenPos(o.pos);
    o.v_col = v.v_col;
    o.light_pos = mul(_LightMatrix0, o.ws_pos);
    float3 bitangent = cross(o.normal.xyz, o.tangent.xyz) * o.tangent.w;
    if(!(0.0 /*_UseGlassSpecularToggle*/))
    {
        float3 five;
        float3 six;
        float3 parallax;
        float3 view = normalize(o.view);
        five.x = o.tangent.z;
        five.y = bitangent.x;
        five.z = o.normal.x;
        six.x = o.tangent.x;
        six.y = bitangent.z;
        six.z = o.normal.y;
        parallax = view.yyy * six;
        parallax = five * view.xxx + parallax;
        bitangent.x = o.tangent.y;
        bitangent.z = o.normal.z;
        parallax = bitangent * view.zzz + parallax;
        o.parallax = parallax; 
    }
    TRANSFER_SHADOW(o)
    return o; // output to pixel shader
}
vs_out vs_edge(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    if((1.0 /*_OutlineType*/) ==  0.0f)
    {
        vs_out o = (vs_out)0.0f;
    }
    else
    {   
        float outlineWidth = v.v_col.w;
        float outline_tex = 1.0f;
        #if defined(use_outline)
            outline_tex = packed_channel_picker(sampler_linear_repeat, _OutlineTex, v.uv_0.xy, (0.0 /*_OutlineWidthChannel*/)) * (0.0 /*_UseOutlineTex*/);
        #endif
        switch((0.0 /*_OutlineWidthSource*/) * (0.0 /*_UseOutlineTex*/))
        {
            case 0:
                outlineWidth = v.v_col.w;
                break;
            case 1:
                outlineWidth = outline_tex;
                break;
            case 2:
                outlineWidth = outline_tex * v.v_col.w;
                break;
        }
        float3 outline_normal = ((1.0 /*_OutlineType*/) == 1.0) ? v.normal : v.tangent.xyz;
        #if defined(use_vat)
            if((0.0 /*_VertexAnimType*/) != 0)
            {
                mavuika_vat_vs(v.vertex, v.uv_1.xy, outline_normal, v.v_col.xyzw);
            }
        #endif
        float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
        float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
        o.view = normalize(view);
        float3 ws_normal = mul(outline_normal, (float3x3)unity_ObjectToWorld);
        outline_normal = mul((float3x3)UNITY_MATRIX_MV, outline_normal);
        if(!(0.0 /*_DisableZShift*/))outline_normal.z = 0.01f;
        outline_normal.xy = normalize(outline_normal.xyz).xy;
        if(!(0.0 /*_FallbackOutlines*/))
        {
            float fov_matrix = unity_CameraProjection[1].y;
            float fov = (0.0 /*_DisableFOVScalingOL*/) ? 1.0f : 2.414f / fov_matrix; // may need to come back in and change this back to 1.0f
            float depth = -wv_pos.z * fov;
            float2 adjs = (depth <  float4(0.001,2,6,0).y) ? float4(0.001,2,6,0).xy : float4(0.001,2,6,0).yz; 
            float2 scales = (depth <  float4(0.001,2,6,0).y) ? float4(0.01,0.245,0.6,0).xy : float4(0.01,0.245,0.6,0).yz; 
            float z_scale = depth + -(adjs.x);
            float2 z_something = float2((-adjs.x) + adjs.y, (-scales.x) + scales.y);
            z_something.x = max(z_something.x, 0.001f);
            z_scale = z_scale / z_something.x;
            z_scale = saturate(z_scale);
            z_scale = z_scale * z_something.y + scales.x;
            z_scale = (0.0 /*_DisableDepthScaling*/) ? 0.1f : z_scale;
            float outline_scale = ((0.11 /*_OutlineWidth*/) * 1.5f) * z_scale;
            outline_scale = outline_scale * 100.0f;
            outline_scale = outline_scale * (0.01 /*_Scale*/);
            outline_scale = outline_scale * 0.414f;
            outline_scale = outline_scale * outlineWidth;
            #if defined(faceishadow)
                if((0.0 /*_UseFaceMapNew*/)) outline_scale = outline_scale * _FaceMapTex.SampleLevel(sampler_linear_repeat, v.uv_0.xy, 0.0f).z;
            #endif
            float offset_depth = saturate(1.0f - depth);
            float max_offset = lerp((1.0 /*_MaxOutlineZOffset*/) * 0.1, (1.0 /*_MaxOutlineZOffset*/), offset_depth);
            float3 z_offset = (wv_pos.xyz) * (float3)max_offset * (float3)_Scale;
            float blue = v.v_col.z + -0.5f; // never trust this fucking line
            o.pos = wv_pos;
            o.pos.xyz = (o.pos.xyz + (z_offset)) + (outline_normal.xyz * outline_scale);
        }
        else
        {
            o.pos = wv_pos;
            o.pos.xyz = o.pos.xyz + (outline_normal.xyz * ((0.11 /*_OutlineWidth*/) * 100.0f * (0.01 /*_Scale*/) * 0.414f * outlineWidth));
        }
        o.ws_pos = o.pos;
        o.pos = mul(UNITY_MATRIX_P, o.pos);
        o.ss_pos = ComputeScreenPos(o.pos);
        o.normal = normalize(ws_normal);
        o.uv_a.xy = v.uv_0;
        o.uv_a.zw = v.uv_1;
        o.v_col = v.v_col;
    }
    return o;
}
vs_out vs_nyx(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    #if defined(nyx_outline)
    if((1.0 /*_OutlineType*/) ==  0.0f)
    {
        vs_out o = (vs_out)0.0f;
    }
    else
    {
        float3 outline_normal = ((1.0 /*_OutlineType*/) == 1.0) ? v.normal : v.tangent.xyz;
        float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
        float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
        o.view = normalize(view);
        float3 ws_normal = mul(outline_normal, (float3x3)unity_ObjectToWorld);
        outline_normal = mul((float3x3)UNITY_MATRIX_MV, outline_normal);
        outline_normal.z = 0.01f;
        outline_normal.xy = normalize(outline_normal.xyz).xy;
        float outline_width = v.v_col.w;
        float something = wv_pos.z + wv_pos.y;
        something = something * float4(2,2,0,0).x;
        float2 screen_pos;
        screen_pos.x = something * 0.5;
        screen_pos.y = wv_pos.x * float4(2,2,0,0).y;
        float2 noise_uv = _Time.yy * float4(0.05,0.05,0,0);
        noise_uv = frac(noise_uv);
        noise_uv = noise_uv + screen_pos.xy;
        float noise_a = _NyxStateOutlineNoise.SampleLevel(sampler_linear_repeat, noise_uv, 0).y;
        float widthScale = (5.0 /*_NyxStateOutlineWidthScale*/) * float4(1,1,0,0).x;
        float nyx_outline_width = saturate((-wv_pos.y) * float4(0,1,1,0).z + (-float4(0,1,1,0).w));
        nyx_outline_width = nyx_outline_width * (float4(1,1,0,0).y * (5.0 /*_NyxStateOutlineWidthScale*/) + (-widthScale)) + widthScale;
        nyx_outline_width = ((0.0 /*_NyxStateEnableOutlineWidthScaleHeightLerp*/)) ? nyx_outline_width : (5.0 /*_NyxStateOutlineWidthScale*/);
        widthScale = (30.0 /*_NyxStateOutlineVertAnimScale*/) * float4(1,1,0,0).x;
        float height_width = saturate((-wv_pos.y) * float4(0,1,1,0).z + (-float4(0,1,1,0).w));
        height_width = height_width * (float4(1,1,0,0).y * (30.0 /*_NyxStateOutlineVertAnimScale*/) + (-widthScale)) + widthScale;
        height_width = ((0.0 /*_NyxStateEnableOutlineVertAnimScaleHeightLerp*/)) ? height_width : (30.0 /*_NyxStateOutlineVertAnimScale*/);
        float outline_height = noise_a * height_width + nyx_outline_width;
        float res_width = max((-_ScreenParams.y) + float4(1080,0,0,0).x, 0.0f);
        res_width = min(res_width * float4(1080,0,0,0).y,  float4(1080,0,0,0).z) + 1.0f;
        outline_width = (outline_width * 0.005f) * (res_width * outline_height) * 0.5f;
        float3 normalized_pos = (normalize(wv_pos.xyz) * (float3)(1.0 /*_MaxOutlineZOffset*/)) * (float3)_Scale;
        o.normal = ((1.0 /*_OutlineType*/) == 1.0) ? v.normal : v.tangent.xyz;
        o.normal = mul((float3x3)unity_ObjectToWorld, o.normal.xyz);
        o.pos = wv_pos;
        o.pos.xyz = normalized_pos.xyz * (v.v_col.z + -0.5f) + o.pos.xyz;
        o.pos.xyz = outline_normal * outline_width + o.pos.xyz;
        o.ss_pos = ComputeScreenPos(o.pos);
        o.uv_a.xy = v.uv_0;
    }
    o.pos = mul(UNITY_MATRIX_P, o.pos);
    #endif
    return o;
}
shadow_out vs_shadow(shadow_in v)
{
    shadow_out o = (shadow_out)0.0f; // initialize so no funny compile errors
    float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
    o.view = normalize(view);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = pos_ws;
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.uv_a = float4(v.uv_0.xy, v.uv_1.xy);
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    return o;
}
float4 ps_model(vs_out i,  bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float4 ws_pos = mul(unity_ObjectToWorld, i.ws_pos);
    float cockType = (0 /*_StarCockType*/);
    float cockEnabled = (0.0 /*_StarCloakEnable*/);
    if(!cockEnabled) 
    {
        cockType = 10;
    }
    float specular_enabled = 0;
    if(((0.0 /*_SpecularHighlights*/) == 1) || ((0.0 /*_UseToonSpecular*/) == 1))
    { // cringe ass code
        specular_enabled = 1;
    }
    UNITY_LIGHT_ATTENUATION(atten, i, ws_pos.xyz);
    float4 out_color = (float4)1.0f; 
    float3 normal = normalize(i.normal);
    float3 tangent = normal;
    float3 bitangent = normal;
    normal = (vface) ? normal : -normal; // check if back facing and invert the normals 
    float3 view   = normalize(i.view);
    float2 uv_a = (!vface && (1.0 /*_UseBackFaceUV2*/)) ? i.uv_a.zw : i.uv_a.xy; 
    float4 uv_b = i.uv_b;
    float3 light = _WorldSpaceLightPos0.xyz;
    float night_shift = auto_night_shift();
    float unity_shadow = SHADOW_ATTENUATION(i);
    unity_shadow = smoothstep(0.5f, 1.f, unity_shadow);
    if(!(1.0 /*_EnableShadow*/) || !(1.0 /*_EnableSelfShadow*/)) 
    {
        unity_shadow = 1.f;
    }
    float4 diffuse = _MainTex.Sample(sampler_MainTex, uv_a);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv_a);
    float material_id = materialID(lightmap.w);
    #if defined(use_shadow)
        float2 ao_uv[3] =
        {
            uv_a, i.uv_a.zw, i.ss_pos.xy/i.ss_pos.ww
        };
        float2 final_uv =  ao_uv[(0.0 /*_CustomAOUV*/) % 3] * float4(1,1,0,0).xy + float4(1,1,0,0).zw;
        float customao  = (0.0 /*_AOSamplerType*/) == 0 ? _CustomAO.Sample(sampler_linear_repeat, final_uv).x : _CustomAO.Sample(sampler_linear_clamp, final_uv).x ; // loop through array if number is greater than 3 or less than 0
    #endif
    #if defined(use_bump)
        float4 normalmap = _BumpMap.Sample(sampler_linear_repeat, uv_a);
    #endif
    #if defined(faceishadow)
        float4 facemap = _FaceMapTex.Sample(sampler_linear_repeat, uv_a);
    #endif
    #if defined(_is_shadow)
        if(!(0.0 /*_EnableNyxOutline*/))
        {
            if((0.0 /*_UseHairShadow*/) && (0.0 /*variant_selector*/) == 4) // if material type is bangs
            {  
                float4 hair_color = lerp(float4(0.8661482,0.3230331,0.137999,1), float4(0,0,0,0.07843138), night_shift);
                hair_color.w = 0.07f;
                diffuse = hair_color;
                return diffuse;
            }
            else if((0.0 /*_UseHairShadow*/) && (0.0 /*variant_selector*/) == 1) // if material type is face
            {
                float shadow_mask = shadow_area_face(uv_a, light).x;
                clip(shadow_mask - 0.1f);
                return shadow_mask;
            }
            else
            {
                clip(-1);
            }
        }
        else
        {return diffuse;} // if shadow pass, clip everything so it doesn't render
    #endif
    #ifdef _IS_PASS_BASE // Basic character shading pass, should only include the basic enviro light stuff + debug rendering shit
        #if defined(use_vat)
        if((0.0 /*_VertexAnimType*/) == 1)
        {
            if((0.0 /*_MainTexAlphaUse*/) == 1) clip(diffuse.w - (0.5 /*_MainTexAlphaCutoff*/));
            mavuika_vat_ps(diffuse, i.uv_a, normal, view, i.v_col);
            return diffuse;
        }
        else if((0.0 /*_VertexAnimType*/)==2)
        {
            float4 u_xlat0;
            float3 u_xlat10_0;
            bool u_xlatb0;
            float4 u_xlat1;
            float3 u_xlat10_1;
            float3 u_xlat16_2;
            float3 u_xlat16_3;
            float2 u_xlat4;
            float u_xlat16_4;
            float u_xlat12;
            float u_xlat16_14;
            float vertical_fade = (i.uv_a.y + (-(0.5 /*_VerticalFadeOffset*/))) / (0.5 /*_VerticalFadeRange*/);
            vertical_fade = saturate(vertical_fade);
            vertical_fade = -vertical_fade + 1.0f;
            if(((0.0 /*_VerticalFadeToggle*/) && (0.0 /*_EnableDithering*/)) && (vertical_fade < 0.95f))
            {
                ditherClip(i.ss_pos.xy/i.ss_pos.z, vertical_fade);
            }
            u_xlat0.x = i.uv_a.y;
            u_xlat0.y = 0.5;
            u_xlat10_1.xyz = _VerticalRampTex.Sample(sampler_linear_clamp, u_xlat0.xy).xyz;
            u_xlat10_0.xyz = _VerticalRampTex2.Sample(sampler_linear_clamp, u_xlat0.xy).xyz;
            u_xlat16_2.xyz = (-u_xlat10_1.xyz) + u_xlat10_0.xyz;
            u_xlat16_2.xyz = (0.0 /*_VerticalRampLerp*/) * u_xlat16_2.xyz + u_xlat10_1.xyz;
            u_xlat16_3.xyz = u_xlat16_2.xyz * float4(1,1,1,1).xyz;
            u_xlat0.xy = i.uv_a.yx * float4(1,1,0,0).yx + float4(1,1,0,0).wz;
            u_xlat0 = _HighlightMaskTex.Sample(sampler_linear_repeat, u_xlat0.xy);
            u_xlat16_14 = ((0.0 /*_HighlightMaskTexChannelSwitch*/) == 3) ? u_xlat0.w : 0.0;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTexChannelSwitch*/) == 2) ? u_xlat0.z : u_xlat16_14;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTexChannelSwitch*/) == 1) ? u_xlat0.y : u_xlat16_14;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTexChannelSwitch*/) == 0) ? u_xlat0.x : u_xlat16_14;
            u_xlat16_2.xyz = (-u_xlat16_2.xyz) * float4(1,1,1,1).xyz + float4(1,1,1,0).xyz;
            u_xlat16_2.xyz = u_xlat16_14 * u_xlat16_2.xyz + u_xlat16_3.xyz;
            u_xlat0.xy = i.uv_a.xy * float4(1,1,0,0).xy + float4(1,1,0,0).zw;
            u_xlat12 = (0.0 /*_HighlightMaskTex2VOffsetByVerColA*/) ? i.v_col.w : float(0.0);
            u_xlat0.z = u_xlat12 + u_xlat0.x;
            u_xlat0 = _HighlightMaskTex2.Sample(sampler_linear_repeat, u_xlat0.yz);
            u_xlat16_14 = ((0.0 /*_HighlightMaskTex2ChannelSwitch*/) == 3) ? u_xlat0.w : 0.0;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTex2ChannelSwitch*/) == 2) ? u_xlat0.z : u_xlat16_14;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTex2ChannelSwitch*/) == 1) ? u_xlat0.y : u_xlat16_14;
            u_xlat16_14 = ((0.0 /*_HighlightMaskTex2ChannelSwitch*/) == 0) ? u_xlat0.x : u_xlat16_14;
            u_xlat16_3.xyz = (-u_xlat16_2.xyz) + float4(1,1,1,0).xyz;
            u_xlat16_2.xyz = u_xlat16_14 * u_xlat16_3.xyz + u_xlat16_2.xyz;
            u_xlat16_2.xyz = u_xlat16_2.xyz * float4(1,1,1,1).xyz;
            u_xlat16_2.xyz = u_xlat16_2.xyz * float4(1,1,1,1).xyz;
            u_xlat16_14 = max(u_xlat16_2.z, u_xlat16_2.y);
            u_xlat16_14 = max(u_xlat16_14, u_xlat16_2.x);
            u_xlatb0 = 1.0<u_xlat16_14;
            u_xlat16_3.x = float(1.0) / float(u_xlat16_14);
            u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_3.xxx;
            u_xlat16_4 = u_xlat16_14 * 0.0500000007;
            u_xlat16_4 = clamp(u_xlat16_4, 0.0, 1.0);
            u_xlat4.x = sqrt(u_xlat16_4);
            float3 color = (bool(u_xlatb0)) ? u_xlat16_3.xyz : u_xlat16_2.xyz;
            return float4(color, (_VerticalFadeToggle?vertical_fade:1));
        }
        #endif
        float2 metalspec;
        metalspec.x = lightmap.x < 0.50f;
        metalspec.y = lightmap.x < 0.90f; // if metal area
        half bump_enable = (0.0 /*_UseBumpMap*/);
        half isMainNormal = (0.0 /*_isNativeMainNormal*/);
        half isLine = (0.0 /*_TextureLineUse*/);
        float fixNormal = (0.0 /*_DummyFixedForNormal*/);
        #if defined(use_bump)
            if(bump_enable || isMainNormal) normal_mapping(normalmap, (0.0 /*_DummyFixedForNormal*/) ? ws_pos.xyz : i.view, (0.0 /*_BumpScale*/), uv_a, normal, tangent, bitangent);
        #if defined(sdf_line)
            if((bump_enable || isMainNormal) && (0.0 /*_TextureLineUse*/) && ((float4(0.6,0.6,0.6,1).x + float4(0.6,0.6,0.6,1).y + float4(0.6,0.6,0.6,1).z) > 0)) detail_line(i.ss_pos.zw, normalmap.z, diffuse.xyz);
        #endif
        #endif
        float3 normal_stock = normalize(i.normal);
        #if defined(use_stockings)
            if((0.0 /*_UseCharacterStockings*/) && (material_id  == 4)) 
            {
                float2 stocking_uv = uv_b.xy * (1.0 /*_StockingsDetailTilingNear*/); 
                float3 stocking_normal;
                stocking_normal.z = 1.0f;
                stocking_normal.xy = _StockingsDetailTex.Sample(sampler_linear_repeat, stocking_uv).xy * 2 - 1;
                stocking_normal.xy *= (1.0 /*_StockingsDetailScale*/);
                float3 normal_stock = normalize(i.normal); 
                float3 tangent_stock; 
                float3 bitangent_stock;
                normal_mapping(stocking_normal, normalize(i.view), (1.0 /*_StockingsDetailScale*/), uv_b.xy, normal_stock, tangent, bitangent);
            }
        #endif
        float3 half_vector = normalize(light + view);
        float ndotl = dot(normal, light);
        float ndotv = dot(normal, view);
        float ndoth = dot(normal, half_vector);
        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);
        float3 GI_color = DecodeLightProbe(normal);
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;
        #if defined(use_texTint)
        if((0.0 /*_MainTexColoring*/)) diffuse = maintint(diffuse);
        #endif
        #if defined (has_mask)
        float4 material_mask = _MaterialMasksTex.Sample(sampler_linear_repeat, uv_a);
        #endif
        #if defined(can_shift)
            float diffuse_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, (0.0 /*_DiffuseMaskSource*/));
            float rim_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, (0.0 /*_RimMaskSource*/));
            float emission_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, (0.0 /*_EmissionMaskSource*/));
            if(!(0.0 /*_UseHueMask*/))
            {
                diffuse_mask = 1.0f;
                rim_mask = 1.0f;
                emission_mask = 1.0f;
            }
        #endif
        if((0.0 /*_MainTexAlphaUse*/) == 0) diffuse.w = 1.0f;
        if((0.0 /*_MainTexAlphaUse*/) == 3) diffuse.xyz = lerp(diffuse, float4(1,0.6038274,0.4479884,1), diffuse.w * (0.0 /*_FaceBlushStrength*/));
        if((0.0 /*_MainTexAlphaUse*/) == 1) clip(diffuse.w - (0.5 /*_MainTexAlphaCutoff*/));
        float emis_check = ((0.0 /*_MainTexAlphaUse*/) == 2 || (0.0 /*_EmissionType*/) == 1);
        float emis_check_eye = (0.0 /*_EyePulse*/) ? (0.0 /*_ToggleEyeGlow*/) * pulsate((1.3 /*_PulseSpeed*/), (0.0 /*_PulseMinStrength*/), (1.0 /*_PulseMaxStrength*/), (0.1 /*_EyeTimeOffset*/)) : (0.0 /*_ToggleEyeGlow*/);
        float mask = diffuse.w; 
        float eye_mask = 0.0f;
        if((0.0 /*_EmissionType*/) == 1) mask = _CustomEmissionTex.Sample(sampler_linear_repeat, uv_a).x;
        if((0.0 /*_ToggleEyeGlow*/) == 1) eye_mask = lightmap.y > 0.95f;
        mask = saturate(mask - 0.02f); // removing any artifacts
        if((0.0 /*_StarCloakEnable*/) && (0.0 /*_StarCockEmis*/)) mask = 1.0f;
        if(((0.0 /*_StarCloakEnable*/) && (0.0 /*_StarCockEmis*/)) && ((0 /*_StarCockType*/) == 2) && (!emis_check)) mask = diffuse.w;
        emis_check = (0.0 /*_TogglePulse*/) ? emis_check * pulsate((1.3 /*_PulseSpeed*/), (0.0 /*_PulseMinStrength*/), (1.0 /*_PulseMaxStrength*/), 0.0f) : emis_check; 
        if((0.0 /*_UseFaceMapNew*/))
        {
            mask = 0.0f;
            eye_mask = 0.0f;
        }
        float3 shadow = (float3)1.0f;
        float3 metalshadow = (float3)0.0f;
        float3 s_color = (float3)1.0f;
        float3 leather = (float3)0;
        #if defined(use_shadow)
            if((1.0 /*_EnableShadow*/)) shadow_color(lightmap.y, i.v_col.x, customao, unity_shadow, i.v_col.y, ndotl, material_id, i.uv_a.xy, shadow, metalshadow, s_color, light);
        #endif
        float3 specular = (float3)0.0f;
        float3 holographic = (float3)1.0f;
        #if defined(use_specular)
            if(specular_enabled) specular_color(ndoth, shadow, lightmap.x, lightmap.z, material_id, specular);
            if(lightmap.x > 0.90f) specular = 0.0f; // making sure the specular doesnt bleed into the metal area
        #endif
        #if defined(use_metal)
            if((1.0 /*_MetalMaterial*/) && !((0.0 /*_UseCharacterStockings*/) * (material_id == 4))) metalics(metalshadow, normal, ndoth, lightmap.x, vface, diffuse.xyz);
            if((0.0 /*_DebugMode*/) && ((0.0 /*_DebugMetal*/) == 1)) return float4(diffuse.xyz, 1.0f);
        #endif
        if(!(0.0 /*_DisableColors*/) && !(0.0 /*_UseMaterialMasksTex*/)) diffuse = diffuse * coloring(material_id);
        #if defined(has_mask)
            if((0.0 /*_UseMaterialMasksTex*/))
            {
                material_mask = material_mask * float4((1.0 /*_UseMaterial3*/), (1.0 /*_UseMaterial4*/), (1.0 /*_UseMaterial5*/), (1.0 /*_UseMaterial5*/));
                float3 color = lerp(float4(1,1,1,1), float4(1,1,1,1), material_mask.w);
                color = lerp(color, float4(1,1,1,1), material_mask.x);
                color = lerp(color, float4(1,1,1,1), material_mask.y);
                color = lerp(color, float4(1,1,1,1), material_mask.z);
                diffuse.xyz = diffuse.xyz * color;
            }
        #endif
        #if defined(can_shift)
            if((1.0 /*_EnableColorHue*/)) diffuse.xyz = hue_shift(diffuse.xyz, material_id, (0.0 /*_ColorHue*/), (0.0 /*_ColorHue2*/), (0.0 /*_ColorHue3*/), (0.0 /*_ColorHue4*/), (0.0 /*_ColorHue5*/), (0.0 /*_GlobalColorHue*/), (0.0 /*_AutomaticColorShift*/), (0.0 /*_ShiftColorSpeed*/), diffuse_mask);
        #endif
        out_color = diffuse;
        out_color.w = 1.0f;
        #if defined(use_stockings)
            if((0.0 /*_UseCharacterStockings*/) && (material_id  == 4)) 
            {
                float2 stocking_uv[4] = 
                {
                    i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw
                };
                character_stocking(normal_stock, view, light, stocking_uv[(0.0 /*_StockingUV*/) % 4], lightmap, out_color);
            }
        #endif
        #if defined(use_nbrbase)
        if((0.0 /*_UseCharacterNbrBase*/))
        {
            if(lightmap.x < 0.85f && lightmap.x > 0.5f)
            {
                nbr(normal, view, light, lightmap, out_color);  
            }
        }
        #endif
        #if defined(asmogay_arm)
            if((0.0 /*_HandEffectEnable*/))
            {
                out_color = diffuse;
                arm_effect(out_color, i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, view, normal, ndotl);
                #if defined(can_shift)
                    if((1.0 /*_EnableColorHue*/)) out_color.xyz = hue_shift(out_color.xyz, material_id, (0.0 /*_ColorHue*/), (0.0 /*_ColorHue2*/), (0.0 /*_ColorHue3*/), (0.0 /*_ColorHue4*/), (0.0 /*_ColorHue5*/), (0.0 /*_GlobalColorHue*/), (0.0 /*_AutomaticColorShift*/), (0.0 /*_ShiftColorSpeed*/), diffuse_mask);
                #endif
                out_color.xyz = out_color.xyz * light_color;
                out_color.xyz = out_color.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
                return out_color;
            }
        #endif
        #if defined(has_fresnel)
            if((0.0 /*_EnableFresnel*/))fresnel_hit(ndotv, out_color.xyz);
        #endif
        #if defined(is_cock)
            if((0.0 /*_StarCloakEnable*/)) star_cocks(float4(out_color.xyz, diffuse.w), i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, (0.0 /*_ScreenIsWorld*/) ? i.ppos : i.ss_pos, ndotv, light, i.parallax);
        #endif
        float3 spec_color = 0.0f;
        #if defined(use_specular)
            spec_color = out_color.xyz + (float3)-1.0f;
            spec_color.xyz = ((0.1 /*_SpecOpacity*/)) * spec_color.xyz + (float3)1.0f;
            spec_color.xyz = spec_color.xyz * specular;
        #endif
        #if defined(use_leather)
            if((0.0 /*_UseCharacterLeather*/) ) leather_color(ndoth, normal, light, lightmap.z, leather, holographic, out_color.xyz);
        #endif
        out_color.xyz = out_color.xyz * s_color + (spec_color);
        #if defined(parallax_glass)
            if((0.0 /*_UseGlassSpecularToggle*/)) glass_color(out_color, i.uv_a, view, normal);
        #endif
            if((0.0 /*_EnableNyxBody*/) && (0.0 /*_BodyAffected*/)) nyx_state_marking(out_color.xyz, uv_a.xy, i.uv_a.zw, uv_b.xy, uv_b.zw, normal, view, i.ss_pos);
        CalcLighting(normal, out_color.xyz);
            if((0.0 /*_EnableNyxBody*/) && !(0.0 /*_BodyAffected*/)) nyx_state_marking(out_color.xyz, uv_a.xy, i.uv_a.zw, uv_b.xy, uv_b.zw, normal, view, i.ss_pos);
        if((0.0 /*_UseFakePoint*/)) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, (1.0 /*_FakePointReflection*/), (0.0 /*_FakePointFrequency*/), (0.0 /*_FakePointFrequencyMin*/), float4(0,0,0,0), float4(1,1,1,1), (1.0 /*_FakePointRange*/), (1.0 /*_FakePointSkinIntensity*/), (1.0 /*_FakePointIntensity*/), (0.0 /*_FakePointSkinSaturate*/));
        if((0.0 /*_UseFakePoint2*/)) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, (1.0 /*_FakePointReflection2*/), (0.0 /*_FakePointFrequency2*/), (0.0 /*_FakePointFrequencyMin2*/), float4(0,0,0,0), float4(1,1,1,1), (1.0 /*_FakePointRange2*/), (1.0 /*_FakePointSkinIntensity2*/), (1.0 /*_FakePointIntensity2*/), (0.0 /*_FakePointSkinSaturate2*/));
        if((0.0 /*_UseFakePoint3*/)) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, (1.0 /*_FakePointReflection3*/), (0.0 /*_FakePointFrequency3*/), (0.0 /*_FakePointFrequencyMin3*/), float4(0,0,0,0), float4(1,1,1,1), (1.0 /*_FakePointRange3*/), (1.0 /*_FakePointSkinIntensity3*/), (1.0 /*_FakePointIntensity3*/), (0.0 /*_FakePointSkinSaturate3*/));
        float3 rim_light = (float3)0.0f;
        #if defined(use_rimlight)
            if((1.0 /*_UseRimLight*/)) 
            {
                rim_light = rimlighting(i.ss_pos, normal, ws_pos, light, material_id, out_color.xyz, view);
                #if defined(can_shift)
                    if((1.0 /*_EnableRimHue*/)) rim_light.xyz = hue_shift(rim_light.xyz, material_id, (0.0 /*_RimHue*/), (0.0 /*_RimHue2*/), (0.0 /*_RimHue3*/), (0.0 /*_RimHue4*/), (0.0 /*_RimHue5*/), (0.0 /*_GlobalRimHue*/), (0.0 /*_AutomaticRimShift*/), (0.0 /*_ShiftRimSpeed*/), rim_mask);
                #endif
                out_color.xyz = out_color.xyz + rim_light;
            }
        #endif
        if((0.0 /*_MainTexAlphaUse*/) == 4)
        {
            out_color.w = diffuse.w;      
            if((0.0 /*_EnableDithering*/)) ditherClip(ws_pos.xy, out_color.w);
        }
        if((0.0 /*_EnableAvatarDie*/) && !(0.0 /*_ApplyOnlyNyx*/))
        {
            avatar_death(i.uv_a.xy, diffuse.a, vface, out_color);
        }
        #if defined(is_stencil)
            if((0.0 /*_UseEyeStencil*/))
            {
                stencil_mask(i.ws_pos, out_color, lightmap, view, i.uv_a.xy);
            }
            else
            {
                discard;
            }
        #endif
        if((0.0 /*_EnableTonemapping*/)) out_color = tonemapping(out_color);
        float4 emis_color = float4(diffuse.xyz, 1.0f);
        float4 emis_color_eye = float4(diffuse.xyz, 1.0f);
        emis_color = emission_color(emis_color, material_id);
        emis_color_eye = emission_color_eyes(emis_color, material_id);
        #if defined(can_shift)
            if((1.0 /*_EnableEmissionHue*/)) emis_color.xyz = hue_shift(emis_color.xyz, material_id, (0.0 /*_EmissionHue*/), (0.0 /*_EmissionHue2*/), (0.0 /*_EmissionHue3*/), (0.0 /*_EmissionHue4*/), (0.0 /*_EmissionHue5*/), (0.0 /*_GlobalEmissionHue*/), (0.0 /*_AutomaticEmissionShift*/), (0.0 /*_ShiftEmissionSpeed*/), emission_mask);
            if((1.0 /*_EnableEmissionHue*/)) emis_color_eye.xyz = hue_shift(emis_color_eye.xyz, material_id, (0.0 /*_EmissionHue*/), (0.0 /*_EmissionHue2*/), (0.0 /*_EmissionHue3*/), (0.0 /*_EmissionHue4*/), (0.0 /*_EmissionHue5*/), (0.0 /*_GlobalEmissionHue*/), (0.0 /*_AutomaticEmissionShift*/), (0.0 /*_ShiftEmissionSpeed*/), emission_mask);
        #endif
        #if defined(is_cock)
            if((0.0 /*_StarCloakEnable*/) && (0.0 /*_StarCockEmis*/))
            {
                emis_color.xyz = emis_color.xyz;
                emis_color.w = (1.0 /*_EmissionScaler*/);
                mask = (0 /*_StarCockType*/) == 2 ? diffuse.w : 1.0f;
                emis_check = 1.0f;
            }
        #endif
        out_color.xyz = lerp(out_color.xyz, emis_color, ((mask * emis_color.w) * emis_check));
        out_color.xyz = lerp(out_color.xyz, emis_color_eye, (eye_mask * emis_check_eye) * emis_color_eye.w);
        #if defined(can_debug)  
            if((0.0 /*_DebugMode*/)) // debuuuuuug
            {
                if((0.0 /*_DebugLighting*/) == 1) return float4(ndotl.xxx, 1.0f);
                if((0.0 /*_DebugLighting*/) == 2) return float4(ndotv.xxx, 1.0f);
                if((0.0 /*_DebugLighting*/) == 3) return float4(ndoth.xxx, 1.0f);
                if((0.0 /*_DebugLighting*/) == 4) return float4(shadow.xxx, 1.0f);
                if((0.0 /*_DebugDiffuse*/) == 1) return float4(diffuse.xyz, 1.0f);
                if((0.0 /*_DebugDiffuse*/) == 2) return float4(diffuse.www, 1.0f);
                if((0.0 /*_DebugLightMap*/) == 1) return float4(lightmap.xxx, 1.0f);
                if((0.0 /*_DebugLightMap*/) == 2) return float4(lightmap.yyy, 1.0f);
                if((0.0 /*_DebugLightMap*/) == 3) return float4(lightmap.zzz, 1.0f);
                if((0.0 /*_DebugLightMap*/) == 4) return float4(lightmap.www, 1.0f);
                #if defined(faceishadow)
                    if((0.0 /*_DebugFaceMap*/) == 1) return float4(facemap.xxx, 1.0f);
                    if((0.0 /*_DebugFaceMap*/) == 2) return float4(facemap.yyy, 1.0f);
                    if((0.0 /*_DebugFaceMap*/) == 3) return float4(facemap.zzz, 1.0f);
                    if((0.0 /*_DebugFaceMap*/) == 4) return float4(facemap.www, 1.0f);
                #endif
                #if defined(use_bump)
                    if((0.0 /*_DebugNormalMap*/) == 1) return float4(normalmap.xy, 1.0f, 1.0f);
                    if((0.0 /*_DebugNormalMap*/) == 2) return float4(normalmap.zzz, 1.0f);
                #endif
                if((0.0 /*_DebugVertexColor*/) == 1) return float4(i.v_col.xxx, 1.0f);
                if((0.0 /*_DebugVertexColor*/) == 2) return float4(i.v_col.yyy, 1.0f);
                if((0.0 /*_DebugVertexColor*/) == 3) return float4(i.v_col.zzz, 1.0f);
                if((0.0 /*_DebugVertexColor*/) == 4) return float4(i.v_col.www, 1.0f);
                if((0.0 /*_DebugRimLight*/) == 1) return float4(rim_light.xyz, 1.0f);
                if((0.0 /*_DebugNormalVector*/) == 1) return float4(i.normal.xyz * 0.5f + 0.5f, 1.0f);
                if((0.0 /*_DebugNormalVector*/) == 2) return float4(i.normal.xyz, 1.0f);
                if((0.0 /*_DebugNormalVector*/) == 3) return float4(normal.xyz * 0.5f + 0.5f, 1.0f);
                if((0.0 /*_DebugNormalVector*/) == 4) return float4(normal.xyz, 1.0f);
                if((0.0 /*_DebugTangent*/) == 1) return float4(i.tangent.xyz, 1.0f);
                if((0.0 /*_DebugSpecular*/) == 1) return float4(specular.xyz, 1.0f);
                if((0.0 /*_DebugEmission*/) == 1) return float4((mask * emis_check).xxx, 1.0f);
                if((0.0 /*_DebugEmission*/) == 2) return float4(emis_color.xyz, 1.0f);
                if((0.0 /*_DebugEmission*/) == 3) return float4(emis_color.xyz * (mask * emis_check).xxx, 1.0f);
                if((0.0 /*_DebugFaceVector*/) == 1) return float4(normalize(UnityObjectToWorldDir(float4(0,0,1,0).xyz)).xyz, 1.0f);
                if((0.0 /*_DebugFaceVector*/) == 2) return float4(normalize(UnityObjectToWorldDir(float4(-1,0,0,0).xyz)).xyz, 1.0f);
                if(((0.0 /*_DebugMaterialIDs*/) > 0) && ((0.0 /*_DebugMaterialIDs*/) != 6))
                {
                    if((0.0 /*_DebugMaterialIDs*/) == material_id)
                    {
                        return (float4)1.0f;
                    }
                    else 
                    {
                        return float4((float3)0.0f, 1.0f);
                    }
                }
                if((0.0 /*_DebugMaterialIDs*/) == 6)
                {
                    float4 debug_color = float4(0.0f, 0.0f, 0.0f, 1.0f);
                    if(material_id == 1)
                    {
                        debug_color.xyz = float3(1.0f, 0.0f, 0.0f);
                    }
                    else if(material_id == 2)
                    {
                        debug_color.xyz = float3(0.0f, 1.0f, 0.0f);
                    }
                    else if(material_id == 3)
                    {
                        debug_color.xyz = float3(0.0f, 0.0f, 1.0f);
                    }
                    else if(material_id == 4)
                    {
                        debug_color.xyz = float3(1.0f, 0.0f, 1.0f);
                    }
                    else if(material_id == 5)
                    {
                        debug_color.xyz = float3(0.0f, 1.0f, 1.0f);
                    }
                    return debug_color;
                }
                if((0.0 /*_DebugLights*/) == 1) return float4((float3)0.0f, 1.0f);
            }
        #endif
    #endif
    #ifdef _IS_PASS_LIGHT // Lighting shading pass, should only include the necessary lighting things needed
        if((0.0 /*_UseFaceMapNew*/)) normal = float3(0.5f, 0.5f, 1.0f);
        #if defined(POINT) || defined(SPOT)
        light = normalize(_WorldSpaceLightPos0.xyz - ws_pos.xyz);
        #endif
        float ndotl = dot(normal, light) * unity_shadow;
        float3 shadow_area = (float3)1.0f;
        shadow_area = shadow_area_transition(lightmap.y, i.v_col.x, ndotl, material_id);
        if((0.0 /*_UseFaceMapNew*/)) shadow_area = saturate(ndotl);
        float bright = lightmap.y > 0.9f && !(0.0 /*_UseFaceMapNew*/);
        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        float3 light_pass_color = ((diffuse.xyz * 5.0f) * _LightColor0.xyz) * atten * shadow_area * 0.5f;
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), (1.0 /*_FilterLight*/)); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
        out_color.xyz = (light_color * saturate(1.0f - bright)) * 0.5f;
        #elif defined(DIRECTIONAL)
        out_color.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
    #endif
    if((0.0 /*_UseWeapon*/)) weapon_shit(out_color.xyz, diffuse.w, i.uv_a.zw, normal, view, ws_pos);
    return out_color; 
}
float4 ps_edge(vs_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    float4 out_color = (float4)1.0f;
    #if defined(can_shift)
        float outline_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, i.uv_a.xy, (0.0 /*_OutlineMaskSource*/));
        outline_mask = (0.0 /*_UseHueMask*/) ? outline_mask : 1.0f;
    #endif
    float4 diffuse = _MainTex.Sample(sampler_MainTex, i.uv_a.xy);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, i.uv_a.xy);
    float material_ID = materialID(lightmap.w);
    float4 outline_color[5] =
    {
        float4(float4(0.5461946,0.5461946,0.5461946,1).xyz, (0.0 /*_OutLineIntensity*/).x), float4(float4(0,0,0,1).xyz, (0.0 /*_OutLineIntensity2*/).x), float4(float4(0,0,0,1).xyz, (0.0 /*_OutLineIntensity3*/).x), float4(float4(0,0,0,1).xyz, (0.0 /*_OutLineIntensity4*/).x), float4(float4(0,0,0,1).xyz, (0.0 /*_OutLineIntensity5*/).x)
    };
    float3 emission;
    out_color.xyz = outline_color[material_ID - 1].w * (diffuse.xyz * (float3)0.203f + (-outline_color[material_ID - 1].xyz)) + outline_color[material_ID - 1].xyz;
    CalcLighting(normalize(i.normal), out_color.xyz);
    if((0.0 /*_EnableOutlineGlow*/))
    {
        emission = outline_emission(out_color.xyz, material_ID);
        out_color.xyz = emission;
    }
    if((0.0 /*_MainTexAlphaUse*/) == 1) clip(diffuse.w - (0.5 /*_MainTexAlphaCutoff*/));
    if((0.0 /*_MainTexAlphaUse*/) == 4) out_color.w = diffuse.w;
    #if defined(weapon_mode)
        if((0.0 /*_UseWeapon*/)) weapon_shit(out_color.xyz, diffuse.w, i.uv_a.zw, i.normal, i.view, i.ws_pos);
    #endif
    #if defined(can_shift)
        if((1.0 /*_EnableOutlineHue*/)) out_color.xyz = hue_shift(out_color.xyz, material_ID, (0.0 /*_OutlineHue*/), (0.0 /*_OutlineHue2*/), (0.0 /*_OutlineHue3*/), (0.0 /*_OutlineHue4*/), (0.0 /*_OutlineHue5*/), (0.0 /*_GlobalOutlineHue*/), (0.0 /*_AutomaticOutlineShift*/), (0.0 /*_ShiftOutlineSpeed*/), 1.0f);
    #endif
    if((1.0 /*_MultiLight*/) && !(0.0 /*_EnableOutlineGlow*/)) out_color.xyz = out_color.xyz  * (UNITY_LIGHTMODEL_AMBIENT.xyz + _LightColor0.xyz);
    if((0.0 /*_EnableAvatarDie*/) && !(0.0 /*_ApplyOnlyNyx*/))
    {
        avatar_death(i.uv_a.xy, diffuse.w, !vface, out_color);
    }
    return out_color;
}
float4 ps_nyx(vs_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    #if defined(nyx_outline)
        if((0.0 /*_EnableNyxOutline*/))
        {
            float3 normal = normalize(i.normal);
            float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
            float3 light_color = max(ambient_color, _LightColor0.rgb);
            float3 GI_color = DecodeLightProbe(normal);
            GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
            float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
            GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;
            float3 gi = (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
            float4 color = (float4)1.0f;
            float alpha = _MainTex.Sample(sampler_MainTex, i.uv_a.xy).w;
            if((0.0 /*_MainTexAlphaUse*/) == 0) alpha = 1.0f;
            if((0.0 /*_MainTexAlphaUse*/) == 1) clip(alpha - (0.5 /*_MainTexAlphaCutoff*/));
            if((0.0 /*_MainTexAlphaUse*/) == 4) color.w = alpha;
            float4 screen_uv;
            screen_uv = ((i.ss_pos.xyxy / i.ss_pos.wwww) * _ScreenParams.xyxy) / _ScreenParams.xxxx;
            screen_uv.yw = 1.0f - screen_uv.yw;
            float4 noise_uv = _Time.yyyy * (float4(0.05,0.05,0,0).zwxy);
            noise_uv = frac(noise_uv);
            screen_uv = screen_uv * float4(2,2,0,0).xyxy + noise_uv;
            float noise_a = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.xy).x;
            screen_uv.xy = noise_a.xx * (float2)(0.25 /*_NyxStateOutlineColorNoiseTurbulence*/) + screen_uv.zw;
            float2 ramp_uv;
            float2 time_uv;
            ramp_uv.x = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.xy).x;
            ramp_uv.y = float(0.75);
            time_uv.y = float(0.25);
            float3 nyx_ramp = _NyxStateOutlineColorRamp.Sample(sampler_linear_repeat, ramp_uv.xy, 0.0).xyz;
            time_uv.x = ((0.0 /*_DayOrNight*/)) ? 0 : 1;
            float3 time_ramp = _NyxStateOutlineColorRamp.Sample(sampler_linear_repeat, time_uv.xy, 0.0).xyz;
            nyx_ramp.xyz = time_ramp.xyz * nyx_ramp.xyz;
            nyx_ramp.xyz = nyx_ramp.xyz * (float3)(1.0 /*_NyxStateOutlineColorScale*/) * float4(1,1,1,1);
            if((0.0 /*_NyxStateRampType*/) == 1) 
            {
                nyx_ramp = custom_ramp_color(ramp_uv.x);
            }
            float nyx_brightness = max(nyx_ramp.z, nyx_ramp.y);
            nyx_brightness = max(nyx_ramp.x, nyx_brightness);
            float bright_check = 1.0f < nyx_brightness;
            color.xyz = bright_check ? (nyx_ramp * (1.0f / nyx_brightness)) : nyx_ramp;
            if((0.0 /*_LineAffected*/)) color.xyz = color.xyz * light_color + gi;
            if(!(0.0 /*_EnableNyxOutline*/)) clip(-1);
            if((0.0 /*_EnableAvatarDie*/))
            {
                avatar_death(i.uv_a.xy, alpha, !vface, color);
            }
            return color;
        }
        else
        {
            clip(-1);
        }
    #else // if nyx mode is disabled, all pixels from this pass should be discarded
        clip(-1);
    #endif
    return (float4)1.0f;
}
float4 ps_shadow(shadow_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    float2 uv = (!vface && (1.0 /*_UseBackFaceUV2*/)) ? i.uv_a.zw : i.uv_a.xy;
    float alpha = _MainTex.Sample(sampler_MainTex, uv).w;
    float4 out_color = (float4)0.0f;
    if((0.0 /*_MainTexAlphaUse*/) == 1) clip(alpha - (0.5 /*_MainTexAlphaCutoff*/));
    if((0.0 /*_MainTexAlphaUse*/) == 4) out_color.w = alpha;
    if((0.0 /*_UseWeapon*/)) weapon_shit(out_color.xyz, alpha, i.uv_a.zw, i.normal, i.view, i.ws_pos);
    return 0.0f;
}
