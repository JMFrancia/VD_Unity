// Cheap toon-lit vertex-color shader for WebGL. Two-tone half-Lambert ramp,
// receives the main directional light's shadow (so stations ground themselves),
// reads the mesh's vertex colors so the island can be grass-top / soil-sides
// with zero texture download.
Shader "VoidDay/VertexColorToon"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _ShadeBand ("Shade Band (dark tone)", Range(0,1)) = 0.62
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Main-light shadow receiving.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _ShadeBand;
                float  _AmbientBoost;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 N = normalize(IN.normalWS);
                float ndl = dot(N, mainLight.direction) * 0.5 + 0.5;   // half-Lambert 0..1
                float band = ndl > 0.5 ? 1.0 : _ShadeBand;             // two-tone toon
                half3 baseCol = IN.color.rgb * _Tint.rgb;
                half3 ambient = SampleSH(N) * _AmbientBoost;
                // Cast shadow removes the direct term; ambient still lights the shaded ground.
                half3 lit = baseCol * (mainLight.color.rgb * band * mainLight.shadowAttenuation + ambient);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
