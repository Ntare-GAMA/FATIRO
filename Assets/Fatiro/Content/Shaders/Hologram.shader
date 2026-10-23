// Fatiro hologram: a glowing, see-through rendering for blueprints, the terrain grid and skins.
// Additive and unlit, so it reads as light floating over the real table in AR.
Shader "Fatiro/Hologram"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.25, 0.88, 1, 1)
        _Fill ("Body fill", Range(0, 1)) = 0.12
        _RimPower ("Rim sharpness", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim strength", Range(0, 4)) = 1.6
        _GridScale ("Grid lines per metre", Float) = 1.0
        _GridWidth ("Grid line width", Range(0.001, 0.2)) = 0.04
        _GridStrength ("Grid strength", Range(0, 3)) = 0.9
        _ScanSpacing ("Scan line spacing (m)", Float) = 0.6
        _ScanSpeed ("Scan speed", Float) = 0.35
        _ScanStrength ("Scan strength", Range(0, 2)) = 0.35
        _Flicker ("Flicker", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Fill;
                half _RimPower;
                half _RimStrength;
                float _GridScale;
                half _GridWidth;
                half _GridStrength;
                float _ScanSpacing;
                float _ScanSpeed;
                half _ScanStrength;
                half _Flicker;
            CBUFFER_END

            // Set by the site: model metres per world metre (the scale denominator), so grid and scan
            // lines are measured in real building metres whatever the tabletop scale.
            float _FatiroWorldToModel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // Thin lines where a coordinate crosses a whole number of grid cells.
            half GridLine(float coordinate, half width)
            {
                float f = frac(coordinate);
                float distanceToLine = min(f, 1.0 - f);
                float pixel = fwidth(coordinate) * 1.5;
                return 1.0 - smoothstep(width, width + pixel, distanceToLine);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(abs(dot(normal, view))), _RimPower) * _RimStrength;

                float toModel = _FatiroWorldToModel > 0.0 ? _FatiroWorldToModel : 1.0;
                float3 p = input.positionWS * toModel * _GridScale;
                half grid = max(max(GridLine(p.x, _GridWidth), GridLine(p.y, _GridWidth)), GridLine(p.z, _GridWidth)) * _GridStrength;

                float scan = frac(input.positionWS.y * toModel / max(_ScanSpacing, 0.01) - _Time.y * _ScanSpeed);
                half scanLine = smoothstep(0.85, 1.0, scan) * _ScanStrength;

                half flicker = 1.0 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 37.0 + input.positionWS.y * 3.0));
                half intensity = (_Fill + rim + grid + scanLine) * flicker;
                return half4(_BaseColor.rgb * intensity, saturate(intensity) * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
