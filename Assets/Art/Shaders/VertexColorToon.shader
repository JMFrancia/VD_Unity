// Cheap toon-lit vertex-color shader for WebGL. Two-tone half-Lambert ramp,
// receives the main directional light's shadow (so stations ground themselves),
// reads the mesh's vertex colors so the island can be grass-top / soil-sides
// with zero texture download.
//
// The island's top face is flat, so both the lighting band and the vertex colors
// are constant across it — it reads as one painted slab. The patch term below
// breaks that up procedurally, still with zero texture download.
Shader "VoidDay/VertexColorToon"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _ShadeBand ("Shade Band (dark tone)", Range(0,1)) = 0.62
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 0.9

        _PatchColor ("Patch Tint (multiplied in)", Color) = (0.85,0.95,0.72,1)
        _PatchStrength ("Patch Strength", Range(0,1)) = 0
        _PatchScale ("Patch Scale (cells per world unit)", Float) = 1.5
        _PatchDetailMul ("Patch Detail Scale Multiplier", Float) = 4
        _PatchDetailBlend ("Patch Detail Blend", Range(0,1)) = 0.35
        _PatchSteps ("Patch Steps (posterise)", Range(1,8)) = 3

        _FlattenEnable ("Flatten Near Stations", Range(0,1)) = 0
        _FlattenGroundY ("Flatten Ground Y", Float) = 0
        _FlattenSink ("Flatten Sink Depth", Float) = 0.12
        _FlattenFeather ("Flatten Feather", Float) = 0.14
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
                float4 _PatchColor;
                float  _PatchStrength;
                float  _PatchScale;
                float  _PatchDetailMul;
                float  _PatchDetailBlend;
                float  _PatchSteps;
                float  _FlattenEnable;
                float  _FlattenGroundY;
                float  _FlattenSink;
                float  _FlattenFeather;
            CBUFFER_END

            // Station footprints, pushed by StationFlattenMask. Globals, so they stay OUT of
            // UnityPerMaterial or the SRP Batcher rejects the shader. xyz = world position, w = radius;
            // unused slots are left at radius 0 so they contribute nothing and the loop needs no branch.
            #define MAX_FLATTEN_STATIONS 32
            float4 _StationFlatten[MAX_FLATTEN_STATIONS];

            /// 0 out in the open, 1 well inside a station footprint.
            float FlattenAmount (float2 posXZ)
            {
                float f = 0;
                for (int i = 0; i < MAX_FLATTEN_STATIONS; i++)
                {
                    float r = _StationFlatten[i].w;
                    float d = distance(posXZ, _StationFlatten[i].xz);
                    f = max(f, 1.0 - smoothstep(r - _FlattenFeather, r, d));
                }
                return saturate(f);
            }

            // Hash-based value noise. Driven off world-space XZ because the island
            // mesh carries no UV set at all — there is nothing else to sample against.
            float Hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);           // smoothstep-interpolated lattice
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                float3 positionWS = pos.positionWS;

                if (_FlattenEnable > 0.5)
                {
                    // Sink below the ground plane rather than scaling the blade to zero height: a tuft
                    // collapsed flat AT ground level would z-fight with the island's top face.
                    float f = FlattenAmount(positionWS.xz);
                    positionWS.y = lerp(positionWS.y, _FlattenGroundY - _FlattenSink, f);
                }

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
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

                // Broad patches plus a finer speckle, so the grass reads as organic
                // rather than as one even wash.
                float2 p = IN.positionWS.xz;
                float patch = lerp(ValueNoise(p * _PatchScale),
                                   ValueNoise(p * _PatchScale * _PatchDetailMul),
                                   _PatchDetailBlend);
                // Posterise into flat steps — smooth noise airbrushes the ground, which
                // fights the two-tone toon lighting this shader is built around.
                patch = floor(patch * _PatchSteps) / _PatchSteps;
                // Up-facing surfaces only: the noise is constant along Y, so letting it
                // reach the island's vertical soil sides would smear it into stripes.
                float upness = saturate(N.y);
                baseCol = lerp(baseCol, baseCol * _PatchColor.rgb, patch * _PatchStrength * upness);
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
