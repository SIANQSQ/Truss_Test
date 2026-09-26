Shader "Custom/StressVertexColor"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _StartP1Color ("Start P1 Color", Color) = (0.92,0.92,0.92,1)
        _StartP2Color ("Start P2 Color", Color) = (0.92,0.92,0.92,1)
        _StartP3Color ("Start P3 Color", Color) = (0.92,0.92,0.92,1)
        _StartP4Color ("Start P4 Color", Color) = (0.92,0.92,0.92,1)
        _EndP1Color ("End P1 Color", Color) = (0.92,0.92,0.92,1)
        _EndP2Color ("End P2 Color", Color) = (0.92,0.92,0.92,1)
        _EndP3Color ("End P3 Color", Color) = (0.92,0.92,0.92,1)
        _EndP4Color ("End P4 Color", Color) = (0.92,0.92,0.92,1)
        _SectionWidth ("Section Width", Float) = 0.03
        _SectionHeight ("Section Height", Float) = 0.03
        _ElementLength ("Element Length", Float) = 1
        _UseVertexColors ("Use Vertex Colors", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "StressVertexColor"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _StartP1Color;
                half4 _StartP2Color;
                half4 _StartP3Color;
                half4 _StartP4Color;
                half4 _EndP1Color;
                half4 _EndP2Color;
                half4 _EndP3Color;
                half4 _EndP4Color;
                float _SectionWidth;
                float _SectionHeight;
                float _ElementLength;
                float _UseVertexColors;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS);
                output.positionHCS = positionInputs.positionCS;

                float u = saturate(
                    input.positionOS.x / max(_SectionWidth, 0.000001) + 0.5);
                float v = saturate(
                    input.positionOS.z / max(_SectionHeight, 0.000001) + 0.5);
                float axialT = saturate(
                    input.positionOS.y /
                    max(_ElementLength, 0.000001) + 0.5);

                half4 startBottom =
                    lerp(_StartP4Color, _StartP3Color, u);
                half4 startTop =
                    lerp(_StartP1Color, _StartP2Color, u);
                half4 endBottom =
                    lerp(_EndP4Color, _EndP3Color, u);
                half4 endTop =
                    lerp(_EndP1Color, _EndP2Color, u);
                half4 startSection = lerp(startBottom, startTop, v);
                half4 endSection = lerp(endBottom, endTop, v);
                half4 sectionColor =
                    lerp(startSection, endSection, axialT);
                output.color = lerp(
                    sectionColor,
                    input.color,
                    saturate(_UseVertexColors)) * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Unlit output keeps stress colors exact and visible.
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
