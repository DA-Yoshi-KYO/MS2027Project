struct appdata
{
    float4 vertex : POSITION;
    float4 uv : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
    float4 color : COLOR0;
    float3 normal : NORMAL;
    float4 tanget : TANGENT;
};

struct v2f
{
    float4 uv : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
    float4 coord5 : TEXCOORD3;
    float3 normal : TEXCOORD4;
    float4 tangent : TEXCOORD5;
    float3 bitangent : TEXCOORD6;
    float3 view : TEXCOORD7;
    float4 color : COLOR0;	
    float4 vertex : SV_POSITION;
};
