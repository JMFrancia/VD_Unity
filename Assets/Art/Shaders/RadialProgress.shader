// Radial progress ring drawn on a unit quad — the in-world "station is working" indicator (BUG-03).
// Unlit, alpha-blended, double-sided (Cull Off) so a billboarded quad reads from either facing.
// _Fill (0..1) sweeps a bright arc clockwise from the top over a dim track ring. Mesh-based on purpose:
// world-space UGUI does not render in this project's URP camera setup, so the radial is a shader on a quad.
Shader "VoidDay/RadialProgress"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (0.45,0.78,0.30,1)
        _TrackColor ("Track Color", Color) = (0.05,0.08,0.12,0.55)
        _Fill ("Fill", Range(0,1)) = 0.35
        _Inner ("Inner Radius", Range(0,0.95)) = 0.62
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor; float4 _TrackColor; float _Fill; float _Inner;
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
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0;                    // 0 center -> 1 outer edge
                if (r > 1.0 || r < _Inner) return half4(0,0,0,0); // ring band only

                // Angle clockwise from the top (12 o'clock), normalized to 0..1.
                float ang = atan2(p.x, p.y);                  // 0 at top, +pi/2 at right (clockwise)
                float t = ang < 0.0 ? ang + 6.2831853 : ang;
                t /= 6.2831853;

                return t <= _Fill ? _FillColor : _TrackColor;
            }
            ENDHLSL
        }
    }
}
