// Long thin metal (overhead wires, rail heads): simple diffuse light plus the glint of a
// cylinder along _GlintAxis. A cylinder reflects the sun toward the eye wherever the half
// vector is square to its axis, so at the right angle a whole length of wire lights up.
Shader "SortingStation/Glint"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.2, 0.2, 0.2, 1)
        _GlintAxis ("Glint axis (world)", Vector) = (0, 0, 1, 0)
        _GlintSharpness ("Glint sharpness", Float) = 260
        _GlintStrength ("Glint strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _GlintAxis;
                float _GlintSharpness;
                float _GlintStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                Light sun = GetMainLight();
                half3 n = normalize(input.normalWS);
                half3 diffuse = _BaseColor.rgb * (sun.color * saturate(dot(n, sun.direction)) + SampleSH(n));
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfway = normalize(sun.direction + view);
                float square = 1.0 - abs(dot(normalize(_GlintAxis.xyz), halfway));
                half glint = pow(saturate(square), _GlintSharpness) * _GlintStrength;
                half3 colour = diffuse + sun.color * glint * 6.0;
                colour = MixFog(colour, input.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
