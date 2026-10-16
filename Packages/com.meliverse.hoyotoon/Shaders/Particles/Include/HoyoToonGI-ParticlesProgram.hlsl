v2f vert (appdata v)
{
    v2f o = (v2f)0; // to prevent intiialization warnings
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv.xy = TRANSFORM_TEX(v.uv, _BaseTex);
    if(_ParticleType == 0)
    {
        o.uv.zw = v.uv.zw;
        o.uv1 = v.uv1;
        o.uv2 = v.uv2;
    }
    else if(_ParticleType == 1)
    {
        float2 scroll;
        scroll.x = _Time.y * _BaseTex_Uspeed;
        scroll.y = _Time.y * _BaseTex_Vspeed;
        scroll = frac(scroll);

        o.uv.xy = (v.uv.xy * _BaseTex_ST.xy + _BaseTex_ST.zw) + scroll;
        float2 random = v.uv1.xy * float2(_BaseTexURandomToggle, _BaseTexVRandomToggle);
        o.uv.zw = o.uv.xy + random;
        o.uv.xy = (v.uv.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw) + frac(_Time.yy * float2(_NoiseTex_Uspeed, _NoiseTex_Vspeed));
        
        float4 ws_pos =  mul(unity_ObjectToWorld, v.vertex);

        o.uv1.w = ws_pos.y * unity_MatrixV[1].z;
        ws_pos.x = unity_MatrixV[0].z * ws_pos.x + o.uv1.w;
        ws_pos.x = unity_MatrixV[2].z * ws_pos.z + ws_pos.x;
        ws_pos.x = unity_MatrixV[3].z * ws_pos.w + ws_pos.x;
        o.uv1.w = (-ws_pos.x); 

        o.uv2.xy = (v.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw) + frac(_Time.yy * float2(_MaskTex_Uspeed, _MaskTex_Uspeed));
        o.uv2.zw = (float2)1.f;
        o.uv1.xyz = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
    }      
    else if(_ParticleType == 2)
    {
        float2 scroll;
        scroll.y = 0.0f;
        scroll.x = frac(_Time.y * _Uspeed);
        scroll.x = scroll.x + v.uv.x;
        scroll.x = scroll.x * _UspeedToggle;

        // since they do it here in the vertex shader in the original shader, i will also it like this
        o.uv.xy = (v.uv.xy * _LiquidTex_ST.xy + _LiquidTex_ST.zw) + scroll.xy;
        o.uv.zw = (float2)0.0f;
        o.uv1.xy = (v.uv.xy * _Normalmap_ST.xy + _Normalmap_ST.zw) + scroll.xy;
        o.uv1.zw = (v.uv.xy * _TextureMask_ST.xy + _TextureMask_ST.zw) + scroll.xy;
    }
    float4 ws_vertex = mul(unity_ObjectToWorld, v.vertex);
    float4 wvp_vertex = mul(unity_MatrixVP, ws_vertex);
    // o.coord5.zw = ws_vertex.zw;
    // o.coord5.xy = wvp_vertex.zz + wvp_vertex.xw;
    o.coord5 = ComputeScreenPos(wvp_vertex);
    o.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tanget.xyz);
    o.tangent.w = v.tanget.w * unity_WorldTransformParams.w;
    o.normal.xyz = mul((float3x3)unity_WorldToObject, v.normal.xyz);
    o.bitangent = cross(o.normal, o.tangent.xyz) * v.tanget.w;
    o.view = _WorldSpaceCameraPos - mul(v.vertex, (float3x3)unity_ObjectToWorld);
    o.color = v.color;
    return o;
}

float4 frag (v2f i, bool frontface : SV_IsFrontFace) : SV_Target
{
    float4 output = (float4)1.0f;
    if(_ParticleType == 0) one_channel(i, output);
    if(_ParticleType == 2) liquid(i, output);
    if(_ParticleType == 1) uv_move(i, output);
    if(_ParticleType == 3) bolt(i, output);
    if(_ParticleType == 4) line_renderer(i, output, frontface);
    if(_SOFTPARTICLES)
    {
        soft_particles(i, output.w);
        output.w = saturate(output.w);
    }
    return output;
}