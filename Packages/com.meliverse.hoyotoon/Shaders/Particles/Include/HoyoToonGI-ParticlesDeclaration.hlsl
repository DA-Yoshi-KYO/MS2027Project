// textures
Texture2D _BaseTex;
Texture2D _Normalmap;
Texture2D _LiquidTex;
Texture2D _TextureMask;
Texture2D _MaskTex;
Texture2D _Noise_Tex;
Texture2D _NoiseTex;
Texture2D _Matcap;
Texture2D _Mask2;
Texture2D _HighlightTex;
Texture2D _FireNoiseTex;
Texture2D _FireTex;
Texture2D _Fire02_Tex;
Texture2D _RampTexWeak;
Texture2D _RampTexStrong;
Texture2D _DissolveTex;

Texture2D _CameraDepthTexture;

// scale and transform
float4 _BaseTex_ST;
float4 _Normalmap_ST;
float4 _LiquidTex_ST;
float4 _TextureMask_ST;
float4 _NoiseTex_ST;
float4 _Noise_Tex_ST;
float4 _MaskTex_ST;
float4 _Matcap_ST;
float4 _Fire02_Tex_ST;
float4 _RampTexWeak_ST;
float4 _FireNoiseTex_ST;
float4 _FireTex_ST;
float4 _HighlightTex_ST;
float4 _DissolveTex_ST;
float4 _Mask2_ST;


// samplers 
SamplerState linear_repeat_sampler;
SamplerState linear_clamp_sampler;

// particle types
float _ParticleType;
float _AlphaClipping;

// SOFT PARTICLES 
float _SOFTPARTICLES;
float _DepthThresh;
float _DepthFade;

// one channel
float _BaseTexAlphaChannelSwitch;
float _BaseTexColorChannelSwitch;
float _UseCustom2ColorToggle;
float4 _MainColor;
float4 _DayColor;
float _ColorBrightness;
float _AlphaBrightness;

// liquid
float4 _Color;
float _ColorBrightnessMax;
float _Alpha;
float4 _LiquidColor;
float _LiquidColorBrightness;
float _UspeedToggle;
float _Uspeed;
float _NormalIntensity;
float _MatcapAlphaToggle;
float _MatcapSize;
float _MaskTexToggle;
float _NoiseToggle;
float _NoiseRandomToggle;
float _Noise_Uspeed;
float _Noise_Vspeed;
float _Noise_Offset;
float _Noise_Brightness;
float _VertexColorForLiquidColorToggle;
float _VertexRForLiquidOpacityToggle;

// uvmove
float _AlphaSoftedgeToggle;
float _AlphaSoftedgeTwoSideToggle;
float _AlphaSoftedgeScale;
float _AlphaSoftedgePower;
float _AlphaFadeByDistanceToggle;
float _AlphaFadeDistance;
float _AlphaFadeOffset;
float _AlphaFadeDistanceInvertToggle;
float _AlphaFadeDistanceTwoWayToggle;
float _AlphaFadeDistanceTwoWay;
float _AlphaFadeOffsetTwoWay;
float _BaseTexAlphaBrightness;
float _BaseTexURandomToggle;
float _BaseTexVRandomToggle;
float _BaseTex_Uspeed;
float _BaseTex_Vspeed;
float _NoiseIntensityOnMask;
float _MaskTexBlendModeToggle;
float _MaskTexChannelSwitch;
float _MaskTex_Uspeed;
float _MaskTex_Vspeed;
float _MaskTexBrightness;
float _NoiseTexChannelSwitch;
float _NoiseTexToggle;
float _NoiseTexUVRandomToggle;
float _NoiseTex_Uspeed;
float _NoiseTex_Vspeed;

// mavuika hair line renderer shit
float4 _AllColorBrightness;
float _MaskTex1TillingAdd2Offset;
float _MaskTexSwitch;
float _NoiseTexSwitch;
float _NoiseInt;
float _NoiseOffset;
float _NoiseTex1TillingAdd2Offset;
float2 _Mask2Speed;
float _Mask2Switch;
float _Mask2NoiseInt;
float _HighlightTex1TillingAdd2Offset;
float _HighlightTex_Switch;
float _HighlightSoft;
float _HighlightRange;
float _HighlightTex_Uspeed;
float _HighlightTex_Vspeed;
float _HighlightTex_LightColor;
float _HighlightTex_DarkColor;
float2 _HighlightTex_Mask;
float _FireNoiseTex_Switch;
float2 _FireNoiseTex_UVspeed;
float _FireNoiseInt;
float4 _FireColor;
float _FireTex_Switch;
float2 _Fire02Tex_UVspeed;
float4 _Fire02_Color;
float _Fire02Tex_Switch;
float4 _Fire02_TexMask;
float _FireSpeedTime;
float _FireSpeedInt;
float _EdgeBlurPower;
float _EdgeBlur;
float _Switch;
float _RampTexWeakStrongLerp;
float _RampTexScale;
float _RampTexBrightness;
float _RampTexNoiseBrightness;
float _DissovleTex1TillingAdd2Offset;
float _DissolveTex_Switch;
float _DissolveSoft1;
float _DissolveRange1;
float4 _OutlineColor;
float _OutlineWidth;
float _DissolveTex_Uspeed;
float _DissolveTex_Vspeed;
float _DissolveTex_Uoffset;
float _DissolveTex_Voffset;



// stuff specifically for flipbooks/spritesheets
float _UseFlipbook;
float _SpritesPerRow;
float _SpritesPerColumn;
float _SpriteFrameCount;
float _SpriteFrameRate;

