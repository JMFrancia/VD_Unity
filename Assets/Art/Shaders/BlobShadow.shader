// Cheap procedural soft blob shadow — a round alpha falloff, no texture download.
// Unlit, alpha-blended, drawn just above the ground under stations/pets to ground them.
Shader "VoidDay/BlobShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _Strength ("Strength", Range(0,1)) = 0.45
        _Softness ("Softness", Range(0.01,1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Strength; float _Softness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float d = length(IN.uv - 0.5) * 2.0;          // 0 center -> 1 edge
                float a = saturate(1.0 - d);
                a = pow(a, 1.0 / max(_Softness, 0.01)) * _Strength;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
