Shader "Hidden/Locked/HoyoToon/Genshin/Character/f6cce4769b52e1545958a2fee34c4c5c"
{
    Properties 
    { 
        [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
		[HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/gilogo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/gil", Float) = 0
        [HideInInspector] CharacterRight ("UI/gir", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
		[HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
		[HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        [HoyoToonShaderOptimizerLockButton] _ShaderOptimizerEnabled ("Lock Material", Float) = 1
        [HoyoToonWideEnum(Base, 0, Face, 1, Weapon, 2, Glass, 3, Bangs, 4)]variant_selector("Material Type--{on_value_actions:[
            {value:0,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=1.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=1.0}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=1.0}]},
            {value:4,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]}
            ]}", Int) = 0
        [HideInInspector] [HoyoToonWideEnum(Pre Natlan, 0, Post Natlan, 1)] _gameVersion ("", Float) = 0
        [HideInInspector] [Toggle] _IsDevMode ("Dev Mode", Float) = 0
        [HideInInspector] start_main ("Main", Float) = 0
            [SmallTexture]_MainTex("Diffuse Texture",2D)= "white" { }
            [SmallTexture]_LightMapTex("Light Map Texture", 2D) = "grey" {}
            [Toggle] _DrawBackFace ("Turn On Back Face", Float) = 0 // need to make this turn off backface culling
            [Enum(UV0, 0, UV1, 1)] _UseBackFaceUV2("Backface UV", int) = 1.0
            [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1 // because VRC world creators are fucking awful at lighting you need to do shit like this to not blow your models the fuck up
            [Toggle] _MainTexColoring("Enable Material Tinting", Float) = 0
            [Toggle] _DisableColors("Disable Material Colors", Float) = 0    
            [HideInInspector] start_facingvector ("Facing Vectors", Float) = 0
                _headUpVector ("Up Vector | XYZ", Vector) = (0, 1, 0, 0)
                _headForwardVector ("Forward Vector | XYZ", Vector) = (0, 0, 1, 0)
                _headRightVector ("Right Vector | XYZ", Vector) = (-1, 0, 0, 0)
            [HideInInspector] end_facingvector("", Float) = 0
            [HideInInspector] start_maincolor ("Color Options", Float) = 0
                _Color ("Tint Color 1", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color2 ("Tint Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color3 ("Tint Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color4 ("Tint Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color5 ("Tint Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
            [HideInInspector] end_maincolor ("", Float) = 0
            [HideInInspector] start_mainalpha ("Alpha Options", Float) = 0
                    [Helpbox] _MainTexAlphaUseHelp("Be careful: Changing these values will reset your render queue value as well both the Source and Destination Blend values.", float) = 0
                    [HoyoToonWideEnum(Off, 0, AlphaTest, 1, Glow, 2, FaceBlush, 3, Transparency, 4)] _MainTexAlphaUse("Diffuse Alpha Channel--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:render_queue=2225}]}]}", Int) = 0
                _MainTexAlphaCutoff("Alpha Cuttoff--{condition_show:{type:PROPERTY_BOOL,data:_MainTexAlphaUse==1.0}}", Range(0, 1.0)) = 0.5
                [Toggle] _EnableDithering ("Alpha Dithering", Float) = 0
                [HideInInspector] end_mainalpha ("", Float) = 0
            [HideInInspector] start_matid ("Material IDs", Float) = 0
                [Toggle] _UseMaterial2 ("Enable Material 2", Float) = 1.0
                [Toggle] _UseMaterial3 ("Enable Material 3", Float) = 1.0
                [Toggle] _UseMaterial4 ("Enable Material 4", Float) = 1.0
                [Toggle] _UseMaterial5 ("Enable Material 5", Float) = 1.0
            [HideInInspector] end_matid ("", Float) = 0
        [HideInInspector] end_main ("", Float) = 0
        [HideInInspector] start_lighting("Lighting Options", Float) = 0
            [HideInInspector] start_lightandshadow("Shadow--{reference_property:_EnableShadow}", Float) = 0
                [Toggle] _EnableShadow ("Enable Shadow", Float) = 1
                [Toggle] _EnableSelfShadow ("Enable Self Shadow", Float) = 1
                [Toggle] _AutomaticNight ("Enable Auto Night/Day", Float) = 1
                [Toggle] _DayOrNight ("Enable Nighttime", Range(0.0, 1.0)) = 0.0 // _ES_ColorTone       
                [SmallTexture]_PackedShadowRampTex("Shadow Ramp",2D)= "white"{ }
                [Toggle] _UseLightMapColorAO ("Enable Lightmap Ambient Occlusion", Float) = 1.0
                [Toggle] _UseShadowRamp ("Enable Shadow Ramp Texture", Float) = 1.0
                [Toggle] _UseVertexColorAO ("Enable Vertex Color Ambient Occlusion", Float) = 1.0
                [Toggle] _UseVertexRampWidth ("Use Vertex Shadow Ramp Width", Float) = 0
                [Toggle] _MultiLight ("Enable Multi Light Source Mode", float) = 1.0
                _LightArea ("Shadow Position", Range(0.0, 2.0)) = 0.55
                _ShadowRampWidth ("Ramp Width", Range(0.2, 3.0)) = 1.0
                [Toggle] _CustomAOEnable ("Enable Custom AO", Float) = 0	
                [SmallTexture]_CustomAO ("Custom AO Texture--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}",2D)= "white"{ }
                [Enum(Repeat, 0, Clamp, 1)] _AOSamplerType ("Custom AO Sampler Type--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}", Int) = 0
                [Enum(UV0, 0, UV1, 1, ScreenSpaceUVs, 2)] _CustomAOUV ("Custom AO UV--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}", Int) = 0
                [HideInInspector] start_shadowtransitions("Shadow Transitions--{reference_property:_UseShadowTransition}", Float) = 0
                    [Toggle] _UseShadowTransition ("Use Shadow Transition (only work when shadow ramp is off)", Float) = 0
                    _ShadowTransitionRange ("Shadow Transition Range 1", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange2 ("Shadow Transition Range 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange3 ("Shadow Transition Range 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange4 ("Shadow Transition Range 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange5 ("Shadow Transition Range 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionSoftness ("Shadow Transition Softness 1", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness2 ("Shadow Transition Softness 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness3 ("Shadow Transition Softness 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness4 ("Shadow Transition Softness 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness5 ("Shadow Transition Softness 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0.5
                [HideInInspector] end_shadowtransitions ("", Float) = 0
                [HideInInspector] start_shadowcolorsday("DayTime Colors", Float) = 0
                    _FirstShadowMultColor ("Daytime Shadow Color 1", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor2 ("Daytime Shadow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor3 ("Daytime Shadow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor4 ("Daytime Shadow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor5 ("Daytime Shadow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                [HideInInspector] end_shadowcolorsday ("", Float) = 0
                [HideInInspector] start_shadowcolorsnight("NightTime Colors", Float) = 0
                    _CoolShadowMultColor ("Nighttime Shadow Color 1", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor2 ("Nighttime Shadow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor3 ("Nighttime Shadow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor4 ("Nighttime Shadow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor5 ("Nighttime Shadow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                [HideInInspector] end_shadowcolorsnight ("", Float) = 0
            [HideInInspector] end_lightandshadow ("", Float) = 0
            [HideInInspector] start_rimlight("Rim Light--{reference_property:_UseRimLight}", Float) = 0
                [Enum(Off, 0, Legacy, 1, New, 2)] _RimLightType ("Rim Light Type--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_UseRimLight=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_UseRimLight=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_UseRimLight=1}]}
                    ]}", Int) = 2
                [HideInInspector][Toggle] _UseRimLight ("Enable Rim Light--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_RimLightType=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_RimLightType=2}]}
                    ]}", Float) = 1
                _ES_AvatarRimWidth  ("Rim Light Width--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Range(0.0, 10.0)) = 1.5
                _ES_AvatarRimWidthScale ("Rim Light Width Scale--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Range(0.0, 10.0)) = 1
                _RimThreshold ("Rim Threshold--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Range(0.0, 1.0)) = 0.5
                _RimLightIntensity ("Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Float) = 0.25
                _RimLightThickness ("Rim Light Thickness--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Range(0.0, 10.0)) = 1.0
                [HideInInspector] start_rimfront ("Front Parameters--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 0
                    [HDR]_ES_AvatarFrontRimColor ("Front Rim Light Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Color) = (1, 1, 1, 1)
                    _ES_AvatarFrontRimIntensity ("Front Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 1
                [HideInInspector] end_rimfront ("", Float) = 0
                [HideInInspector] start_rimback ("Back Parameters--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 0
                    [HDR]_ES_AvatarBackRimColor ("Back Rim Light Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Color) = (1, 1, 1, 1)
                    _ES_AvatarBackRimIntensity ("Back Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 1
                [HideInInspector] end_rimback ("", Float) = 0
                [HideInInspector] start_lightingrimcolor("Rimlight Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType>=1.0}}", Float) = 0
                    _RimColor (" Rim Light Color", Color)   = (1, 1, 1, 1)
                    _RimColor0 (" Rim Light Color 1 | (RGB ID = 0)", Color)   = (1, 1, 1, 1)
                    _RimColor1 (" Rim Light Color 2 | (RGB ID = 31)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor2 (" Rim Light Color 3 | (RGB ID = 63)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor3 (" Rim Light Color 4 | (RGB ID = 95)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor4 (" Rim Light Color 5 | (RGB ID = 127)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1, 1, 1, 1)
                [HideInInspector] end_lightingrimcolor("", Float) = 0
            [HideInInspector] end_rimlight ("", Float) = 0
        [HideInInspector] end_lightning ("", Float) = 0
        [HideInInspector] start_reflections("Reflections", Float) = 0
                [HideInInspector] start_metallics("Metallics--{reference_property:_MetalMaterial}", Int) = 0
                    [Toggle] _MetalMaterial ("Enable Metallic", Float) = 1.0
                    [SmallTexture]_MTMap("Metallic Matcap--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}",2D)= "white"{ }
                    [Toggle] _MTUseSpecularRamp ("Enable Metal Specular Ramp--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 0.0
                    [SmallTexture] _MTSpecularRamp("Specular Ramp--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_MetalMaterial==1},{type:PROPERTY_BOOL,data:_MTUseSpecularRamp==1}]}}", 2D) = "white" { }
                    _MTMapBrightness ("Metallic Matcap Brightness--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 3.0
                    _MTShininess ("Metallic Specular Shininess--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 90.0
                    _MTSpecularScale ("Metallic Specular Scale--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 15.0 
                    _MTMapTileScale ("Metallic Matcap Tile Scale--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.0, 2.0)) = 1.0
                    _MTSpecularAttenInShadow ("Metallic Specular Power in Shadow--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.0, 1.0)) = 0.2
                    _MTSharpLayerOffset ("Metallic Sharp Layer Offset--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.001, 1.0)) = 1.0
                    [HideInInspector] start_metallicscolor("Metallic Colors--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Int) = 0
                        [HDR]_MTMapDarkColor ("Metallic Matcap Dark Color", Color) = (0.51, 0.3, 0.19, 1.0)
                        [HDR]_MTMapLightColor ("Metallic Matcap Light Color", Color) = (1.0, 1.0, 1.0, 1.0)
                        _MTShadowMultiColor ("Metallic Matcap Shadow Multiply Color", Color) = (0.78, 0.77, 0.82, 1.0)
                        [HDR]_MTSpecularColor ("Metallic Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
                        [HDR]_MTSharpLayerColor ("Metallic Sharp Layer Color", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HideInInspector] end_metallicscolor ("", Int) = 0
                [HideInInspector] end_metallics("", Int) = 0
        [HideInInspector] end_reflections ("", Float) = 0
        [HideInInspector] start_outlines("Outlines--{reference_property:_OutlineEnabled}", Float) = 0
            [HideInInspector] [Toggle] _OutlineEnabled ("Hidden Outline Bool--{on_value_actions:[{value:0,actions:[{type:SET_PROPERTY,data:_OutlineType=0}]}, {value:1,actions:[{type:SET_PROPERTY,data:_OutlineType=2}]}]}", Float) = 1
            [Enum(None, 0, Normal, 1,  Tangent, 2)] _OutlineType ("Outline Type--{on_value_actions:[{value:0,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=0},{type:SET_PROPERTY,data:_saveoutlinevalue=0}]}, {value:1,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=1},{type:SET_PROPERTY,data:_saveoutlinevalue=1}]}, {value:2,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=1},{type:SET_PROPERTY,data:_saveoutlinevalue=2}]}]}", Float) = 1.0
            [Toggle] _UseOutlineTex ("Use Outline Width Texture", Float) = 0
            [Toggle] _FallbackOutlines ("Enable Static Outlines", Float) = 0
            [Toggle] _EviroAffectOutline ("Outlines Affected by Lighting", Float) = 0
            [Toggle] _DisableFOVScalingOL("Disable FOV Scaling On Outline", Float) = 0
            [Toggle] _DisableZShift ("Disable Z Shift", Float) = 0
            _OutlineWidth ("Outline Width", Float) = 0.03
            _Scale ("Outline Scale", Float) = 0.01
            _OutlineOffsetBlockBChannel ("Use Vertex Color B Channel", Float) = 0
            [Toggle] [HideInInspector] _UseClipPlane ("Use Clip Plane?", Float) = 0.0
            [HideInInspector] start_outlinestex("Outline Texture--{condition_show:{type:PROPERTY_BOOL,data:_UseOutlineTex==1.0}}", Float) = 0
                [SmallTexture] _OutlineTex ("Outline Width Texture", 2D) = "black"{}
                [Enum(From Red, 0, From Green, 1, From Blue, 2, From Alpha, 3)] _OutlineWidthChannel ("Outline Width Channel", Float) = 0
                [Enum(From Texture, 0, From Vertex Color, 1, Combination, 2)] _OutlineWidthSource ("Outline Width Source", Float) = 0
            [HideInInspector] end_outlinestex ("", Float) = 0
            [HideInInspector] _ClipPlane ("Clip Plane", Vector) = (0.0, 0.0, 0.0, 0.0)
            [HideInInspector] start_outlinescolor("Outline Colors", Float) = 0
                _OutlineColor ("Outline Color 1", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor2 ("Outline Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor3 ("Outline Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor4 ("Outline Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor5 ("Outline Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
            [HideInInspector] end_outlinescolor ("", Float) = 0
            [HideInInspector] start_outlineint ("Diffuse Intensity", Float) = 0
                _OutLineIntensity ("Intensity 1", Range(0, 1)) = 0
                _OutLineIntensity2 ("Intensity 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity3 ("Intensity 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity4 ("Intensity 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity5 ("Intensity 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0, 1)) = 0
            [HideInInspector] end_outlineint ("", Float) =0
            [HideInInspector] start_outlinesoffset("Outline Offset & Adjustments", Float) = 0
                [Toggle] _DisableDepthScaling ("Disable Width Depth Scaling", Float) = 0
                [Helpbox] _OutlineHelp("Each component (X Y Z) refers is a the Near, Middle, and Far distance at which the scales below will be applied. Max Z-Offset is the furthest the outlines can travel on the Z axis away. Setting it to zero will disable Z offsets.", float) = 0
                [Vector3] _OutlineWidthAdjustZs ("Width Adjustment At Distance", Vector) = (0.001, 2.0, 6.0, 0.0)
                [Vector3]  _OutlineWidthAdjustScales ("Scale at Distances", Vector) = (0.01, 0.245, 0.6, 0.0)
                _MaxOutlineZOffset ("Max Z-Offset", Float) = 1.0
            [HideInInspector] end_outlinesoffset ("", Float) = 0
            [HideInInspector] start_outlinestencil("Outline Stencil", Float) = 0
                _StencilRefO ("Ref", Int) = 244
                _StencilCompO ("Comparison", Int) = 8 // Always
                _StencilPassO ("Pass", Int) = 0 // Keep
            [HideInInspector] end_outlinestencil ("", Float) = 0
        [HideInInspector] end_outlines ("", Float) = 0
        [HideInInspector] start_specialeffects("Special Effects", Float) = 0
            [HideInInspector] start_emissionglow("Emission / Archon Glow", Float) = 0
                [Enum(From Diffuse Alpha, 0, From Custom Mask, 1)]  _EmissionType ("Emission Mask Source", Float) = 0
                _CustomEmissionTex ("Custom Emission Texture--{condition_show:{type:PROPERTY_BOOL,data:_EmissionType==1}}", 2D) = "black"{}
                [HideInInspector] start_glowscale("Emission Intensity", Float) = 0
                    _EmissionScaler ("Emission Intensity", Range(0, 100)) = 1
                    _EmissionScaler1 ("Emission Intensity For Material 1", Range(0, 100)) = 1
                    _EmissionScaler2 ("Emission Intensity For Material 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler3 ("Emission Intensity For Material 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler4 ("Emission Intensity For Material 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler5 ("Emission Intensity For Material 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0, 100)) = 1
                [HideInInspector] end_glowscale("", Float) = 0
                [HideInInspector] start_glowcolor("Emission Color", Float) = 0
                    [HDR]_EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
                    [HDR]_EmissionColor1_MHY ("Emission Color For Material 1", Color) = (1,1,1,1)
                    [HDR]_EmissionColor2_MHY ("Emission Color For Material 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor3_MHY ("Emission Color For Material 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor4_MHY ("Emission Color For Material 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor5_MHY ("Emission Color For Material 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColorEye ("Emission Color For Eye--{condition_show:{type:PROPERTY_BOOL,data:_ToggleEyeGlow==1.0}}", Color) = (1,1,1,1)
                [HideInInspector] end_glowcolor("", Float) = 0
                [HideInInspector] start_eyeemission("Eye Emission--{reference_property:_ToggleEyeGlow}", Float) = 0
                    [Toggle] _ToggleEyeGlow ("Enable Eye Glow", Float) = 0.0
                    _EyeGlowStrength ("Eye Glow Strength", Float) = 0.5
                    _EyeTimeOffset ("Eye Glow Timing Offset", Range(0.0, 1.0)) = 0.1
                [HideInInspector] end_eyeemission("", Float) = 0
                [HideInInspector] start_emissionpulse("Pulsing Emission--{reference_property:_TogglePulse}", Float) = 0
                    [Toggle] _TogglePulse ("Enable Pulse", Float) = 0.0 
                    [Toggle] _EyePulse ("Enable Pulse for Eyes", Float) = 0
                    _PulseSpeed ("Pulse Speed", Float) = 1.3
                    _PulseMinStrength ("Minimum Pulse Strength", Range(0.0, 1.0)) = 0.0
                    _PulseMaxStrength ("Maximum Pulse Strength", Range(0.0, 1.0)) = 1.0
                [HideInInspector] end_emissionpulse ("", Float) = 0
            [HideInInspector] end_emissionglow ("", Float) = 0
            [HideInInspector] start_starcock("Star Cloak--{reference_property:_StarCloakEnable}", Float) = 0 //tribute to the starcock 
            [HideInInspector] end_starcock ("", Float) = 0   
            [HideInInspector] start_nyx("NightSoul", Float) = 0
                [Enum(Premade, 0, Custom, 1)] _NyxStateRampType ("Ramp Type", Float) = 0
                [NoScaleOffset] _NyxStateOutlineColorRamp ("Color Ramp", 2D) = "gray" { }
                [HideInInspector] start_customramp("Custom Ramp Settings", Float) = 0
                    _RampPoint0 ("Color Ramp Point 0", Color) = (0.00,0.00,0.00,0)
                    _RampPoint1 ("Color Ramp Point 1", Color) = (0.25,0.25,0.25,1)
                    _RampPoint2 ("Color Ramp Point 2", Color) = (0.50,0.50,0.50,1)
                [HideInInspector] end_customramp ("", Float) = 0
                [NoScaleOffset] _NyxStateOutlineNoise ("Noise(RG)", 2D) = "gray" { }
                [Vector2] _NyxStateOutlineColorNoiseScale ("Noise Scale", Vector) = (2,2,0,0)
                _NyxStateOutlineColorNoiseAnim ("Noise Speed", Vector) = (0.05,0.05,0,0)
                _NyxStateOutlineColorNoiseTurbulence ("Noise Turbulence", Range(0, 1)) = 0.25
                [HideInInspector] start_bodygroup ("Body Markings", Float) = 0
                    [Toggle] _EnableNyxBody ("Enable Body Markings", Float) = 0
                    [Toggle] _BodyAffected ("Affected by Light", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _TempNyxStatePaintMaskChannel("Mask Channel", Float) = 1
                    [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _NyxBodyUVCoord ("UV Coord for Mask", Float) = 0
                    _TempNyxStatePaintMaskTex ("Body Mask Texture", 2D) = "black" {}
                    _NyxStateOutlineColorOnBodyMultiplier ("Color Multiplier", Color) = (1,1,1,1)
                    _NyxStateOutlineColorOnBodyOpacity ("Blend Rate", Float) = 0
                [HideInInspector] end_bodygroup ("", Float) = 0
                [HideInInspector] start_nyxoutline ("Outline", Float) = 0
                    [Helpbox] _NyxOutlineHelpBox("This effect is incompatible with the Eye Stencils as it introduces conflicting Stencil States.", Float) = 0
                    [Toggle(ENABLE_NYX)] _EnableNyxOutline ("Enable Outline--{on_value_actions:[
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=2},{type:SET_PROPERTY,data:_StencilPassB=2},{type:SET_PROPERTY,data:_sdwPass=2}, {type:SET_PROPERTY,data:_StencilPassNyx=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_StencilCompA=8},{type:SET_PROPERTY,data:_StencilCompB=8}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompNyx=6}, {type:SET_PROPERTY,data:_StencilRefA=10}, {type:SET_PROPERTY,data:_sdwRef=10}, {type:SET_PROPERTY,data:_StencilRefB=10}, {type:SET_PROPERTY,data:_StencilRefNyx=10}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Float) = 0
                    [Toggle] _LineAffected ("Affected by Light", Float) = 0
                    _NyxStateOutlineColor ("Color", Color) =  (1,1,1,1)
                    _NyxStateOutlineColorScale ("Color Intensity", Float) = 1
                    _NyxStateOutlineWidthScale ("Width Scale", Float) = 5
                    [Toggle] _NyxStateEnableOutlineWidthScaleHeightLerp ("Enable Height Blending", Float) = 0
                    [Vector2] _NyxStateOutlineWidthScaleRange ("Width Scale Lerp Range", Vector) = (1,1,0,0)
                    _NyxStateOutlineWidthVarietyWithResolution ("Variety with Resolution", Vector) = (1080,0,0,0)
                    _NyxStateOutlineWidthScaleLerpHeightRange ("Lerp Height Range", Vector) = (0,1,1,0)
                    [HideInInspector] start_nyxvert ("Vertex Animation", Float) = 0
                        [Vector2] _NyxStateOutlineVertAnimNoiseScale ("Vertex Noise Scale", Vector) = (2,2,0,0)
                        [Vector2] _NyxStateOutlineVertAnimNoiseAnim ("Vertex Noise Speed", Vector) = (0.05,0.05,0,0)
                        _NyxStateOutlineVertAnimScale ("Vertex Scale", Float) = 30
                        [Toggle] _NyxStateEnableOutlineVertAnimScaleHeightLerp ("Enable Vertex Scale Height Lerp", Float) = 0
                        [Vector2] _NyxStateOutlineVertAnimScaleRange ("Vertex Scale Lerp Range", Vector) = (1,1,0,0)
                        _NyxStateOutlineVertAnimScaleLerpHeightRange ("Vertex Lerp Height Range", Vector) = (0,1,1,0)
                    [HideInInspector] end_nyxvert ("", Float) = 0
                    [HideInInspector] start_nyxstencilsetting ("Stencil Settings", Float) = 0
                        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassNyx ("Stencil Pass Op A", Float) = 0
                        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompNyx ("Stencil Compare Function A", Float) = 8
                        [IntRange] _StencilRefNyx ("Stencil Reference Value", Range(0, 255)) = 0
                    [HideInInspector] end_nyxstencilsetting ("", Float) = 0
                [HideInInspector] end_nyxoutline ("", Float) = 0
            [HideInInspector] end_nyx("NightSoul", Float) = 0
            [HideInInspector] start_fakelight("Fake Point Light", float) = 0
                [HideInInspector] start_firstlight("Fake Light One", Float) = 1
                    [Toggle] _UseFakePoint ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange ("Light Range", Float) = 1
                    _FakePointIntensity ("Light Intensity", Float) = 1
                    _FakePointPosition ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection ("Light Reflection", Float) = 1
                    _FakePointFrequency ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_firstlight("", float) = 0
                [HideInInspector] start_secondlight("Fake Light Two", Float) = 1
                    [Toggle] _UseFakePoint2 ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex2 ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor2 ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange2 ("Light Range", Float) = 1
                    _FakePointIntensity2 ("Light Intensity", Float) = 1
                    _FakePointPosition2 ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection2 ("Light Reflection", Float) = 1
                    _FakePointFrequency2 ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin2 ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity2 ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate2 ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_secondlight("", float) = 0
                [HideInInspector] start_thirdlight("Fake Light Three", Float) = 1
                    [Toggle] _UseFakePoint3 ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex3 ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor3 ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange3 ("Light Range", Float) = 1
                    _FakePointIntensity3 ("Light Intensity", Float) = 1
                    _FakePointPosition3 ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection3 ("Light Reflection", Float) = 1
                    _FakePointFrequency3 ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin3 ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity3 ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate3 ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_thirdlight("", float) = 0
            [HideInInspector] end_fakelight("", float) = 0  
            [HideInInspector] start_eyestencil ("Eye Stencil", Float) = 0
                [Helpbox] _StencilHelp("Warning: This feature requires you to seperate the eyes from the hair material and make it it's own material. Depending on future game updates it may break.", float) = 0
                [Helpbox] _StencilHelp2("This effect is incompatible with the NightSoul outline as it introduces conflicting Stencil States--{condition_show:{type:PROPERTY_BOOL,data:_EnableNyxOutline==1}}", float) = 0
                [Toggle] _UseEyeStencil ("Use Stencil", Float) = 0
                [Enum(Face, 0, Eye, 1, Hair, 2, Off, 3)] _StencilType ("Stencil Type--{on_value_actions:[
                    {value:3,actions:[{type:SET_PROPERTY,data:_CullMode=0}, {type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2040}, {type:SET_PROPERTY,data:render_type=Opaque}]},
                    {value:0,actions:[{type:SET_PROPERTY,data:_CullMode=2}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:0,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:0,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2010}, {type:SET_PROPERTY,data:render_type=Opaque}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_CullMode=2}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2010}, {type:SET_PROPERTY,data:render_type=Opaque}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_CullMode=0}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2020}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Float) = 3
                [Enum(Off, 0, Left, 1, Right, 2)] _StencilFilter ("Filter Stencil Side", Float) = 0
                [Enum(None, 3, Light Map, 1, Eye Mask, 0, Custom Mask, 2)] _StencilMaskSource ("Stencil Mask Source", Float) = 0
                [Helpbox] _StencilHelp3("Setting your Mask Source to none will make the entire material act as a stencil", float) = 0
                _EyeMask ("Eye Mask Stencil--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource==0}}", 2D) = "black" {}
                _EyeMaskCustom ("Custom Mask--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource==2}}", 2D) = "black" {}
                [Toggle] _InvertMask("Invert Mask", Float) = 0
                [Enum(One Channel, 0, Two Channel, 1, Three Channel, 2, All Channels, 3)] _StencilChannelCount ("Stencil Mask Channel Usage", Float) = 0
                [HideInInspector] start_stencil_mask_layer("Stencil Mask Creation--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource!=3}}", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer0 ("Channel 1", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer1 ("Channel 2--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>0}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer1Op ("Channel 2 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>0}}", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer2 ("Channel 3--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>1}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer2Op ("Channel 3 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>1}}", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer3 ("Channel 4--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>2}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer3Op ("Channel 4 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>2}}", float) = 0 
                [HideInInspector] end_stencil_mask_layer("Stencil Mask Creation", Float) = 0
                [HideInInspector] start_stencil_condition("Stencil Test Conditions", Float) = 0
                    [Helpbox] _StencilHelp4("This is the condition that the eye mask is checked as and the threshold is the value used in the condition.", Float) = 0
                    [Helpbox] _StencilHelp5("Example being: Stencil mask is greater than or equal to 1", Float) = 0
                    [Enum(less than, 0, greater than, 1, equal to, 2, less than or equal, 3, greater than or equal, 4)] _StencilConditional ("Conditional", Float) = 1
                    _StencilConditionThresh ("Threshold", Float) = 0
                    [HideInInspector] end_stencil_condition("Stencil Test Conditions", Float) = 0
                    [HideInInspector] start_stencil_fade("Fade Settings", Float) = 0
                    [Toggle] _HairBlendUse("View Angle Fade", Float) = 0
                    _HairTransparentValue ("Stencil Blend", Float) = 0.5
                    _HairZOffset ("Z Mask Offset", Float) = 0
                    [Helpbox] _StencilHelp6("These two values below control the steepness of the view angle fade.--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", Float) = 0
                    _AlphaYZ ("Alpha Up Control--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", float) = 0.658 
                    _AlphaXZ ("Alpha Side Control--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", float) = 0.293
                [HideInInspector] end_stencil_fade ("", Float) = 0
                [HideInInspector] start_stencilsetting ("Stencil Settings", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassA ("Stencil Pass Op A", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassB ("Stencil Pass Op B", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
                    [IntRange] _StencilRefA ("Stencil Reference Value", Range(0, 255)) = 0
                    [IntRange] _StencilRefB ("Stencil Reference Value", Range(0, 255)) = 0
                [HideInInspector] end_stencilsetting ("", Float) = 0
            [HideInInspector] end_eyestencil ("", Float) = 0
            [HideInInspector] start_dissolve("Avatar Dissolve", Float) = 0
                [Helpbox] _AvatarDeathHelp ("This is used in game for Avatar Death dissolves", Float) = 0
                [Toggle] _EnableAvatarDie ("Enable Avatar Death", Float) = 0
                [Toggle] _ApplyOnlyNyx ("Apply Only to NightSoul Outline", float) = 0
                [NoScaleOffset] _DissolveNoise ("Dissolve Noise", 2D) = "white" { }
                _DissolveNoiseST ("Dissolve Noise Scale Tiling", Vector) = (1,1,0,0)
                _DissolveValue ("Dissolve Value", Range(0, 1)) = 0
                _DissolveEdgeWidth ("Dissolve Edge Width Value", Float) = 1.1
                _DissolveColorScaler ("Dissolve Color Scaler", Float) = 1
                [HDR] _DissolveColor ("Dissolve Color", Color) = (0.4338235,1,0.9297161,1)
                [HDR] _DeathTintColor ("Death Tint Color", Color) = (1,1,1,1)
            [HideInInspector] end_dissolve ("", Float) = 0
            [HideInInspector] start_tonemapping ("Built in Tonemapping", Float) = 0
                [Toggle] _EnableTonemapping ("Enable Tonemapping", float) = 0
            [HideInInspector] end_tonemapping ("", Float) = 0
        [HideInInspector] end_specialeffects ("", Float) = 0
        [HideInInspector] start_renderingOptions("Rendering Options", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
            [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Int) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Int) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Int) = 0
            [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Source Blend--{on_value_actions:[{value:any,actions:[{type:LINK_PROPERTY,data:_SrcBlendMode==_SrcBlend}]}]}", Int) = 1
            [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Destination Blend--{on_value_actions:[{value:any,actions:[{type:LINK_PROPERTY,data:_DstBlendMode==_DstBlend}]}]}", Int) = 1
        [HideInInspector] end_renderingOptions("Rendering Options", Float) = 0
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
            #define use_shadow
            #define has_sramp
            #define use_rimlight
            #define use_metal
            #define use_outline
        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityShaderVariables.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "UnityInstancing.cginc"
        #include "/HoyoToonGenshin-declarations.hlsl"
        #include "/HoyoToonGenshin-inputs.hlsl"
        #include "/HoyoToonGenshin-common.hlsl"
        ENDHLSL
        Pass // Character Pass, the only REQUIRED pass
        {
            Name "HairShadow Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Back
            Blend [_sdwSrc] [_sdwDst]
            ColorMask [_sdwColorMask]
            Stencil
            {
				Ref [_sdwRef]
				Comp [_sdwComp]
                Pass [_sdwPass]  
			}
            ZWrite [_sdwZWrite]
            ZTest [_sdwZTest]
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _is_shadow
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      
        Pass // Character Pass, the only REQUIRED pass
        {
            Name "Character Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
				Ref [_StencilRefA]
				Comp [_StencilCompA]
                Pass [_StencilPassA]  
			}
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      
        Pass // Eye Stencil Pass
        {
            Name "Character Stencil Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRefB]
                Comp [_StencilCompB]
        		Pass [_StencilPassB]  
            }
            HLSLPROGRAM            
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #define is_stencil
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      
        Pass // Character Light Pass
        {
            Name "Character Light Pass"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_Cull]
            ZWrite Off
            Blend One One     
            HLSLPROGRAM
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT
            #pragma vertex vs_model
            #pragma fragment ps_model 
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }    
        Pass // Outline Pass
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Stencil
            {
				ref [_StencilRefO]
                Comp [_StencilCompO]
                Pass [_StencilPassO]
            }
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex vs_edge
            #pragma fragment ps_edge
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }
        Pass // Shadow Pass, this ensures the model shows up in CameraDepthTexture
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex vs_shadow
            #pragma fragment ps_shadow
            #include "/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }   
    }
    CustomEditor "HoyoToon.ShaderEditor"
}
