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
        _GlintStrength2 ("Headlight sheen", Float) = 1
        _Polish ("Polish (sky reflection and sun streak)", Float) = 0
        _MinPixel ("Keep at least a pixel wide (wires)", Float) = 0
        _WireRadius ("Half thickness (m)", Float) = 0.01
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

            // The locomotive's headlight (set globally by the renderer): position, direction, on.
            float4 _CabHeadlightPos;
            float4 _CabHeadlightDir;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _GlintAxis;
                float _GlintSharpness;
                float _GlintStrength;
                float _Polish;
                float _MinPixel;
                float _WireRadius;
                float _GlintStrength2;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; half coverage : TEXCOORD3; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Far away a wire is thinner than a pixel and breaks into dots; keep it one pixel
                // wide and let it fade into the air instead, as a real wire does.
                float pixel = 2.0 * length(GetCameraPositionWS() - positionWS) / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
                float grow = max(0.0, 0.5 * pixel - _WireRadius) * _MinPixel;
                positionWS += normalWS * grow;
                output.coverage = _WireRadius / (_WireRadius + grow);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.fog = ComputeFogFactor(output.positionCS.z);
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
                // A sharp line of light plus a soft glow around it, so the flash reads from afar.
                half glint = (pow(saturate(square), _GlintSharpness) + pow(saturate(square), _GlintSharpness * 0.45) * 0.2) * _GlintStrength;
                half thin = sqrt(input.coverage);
                half3 colour = lerp(unity_FogColor.rgb, diffuse, max(input.coverage, 0.35h)) + sun.color * glint * 9.0 * thin;

                // Polished rail tops: a mirror of the sky at grazing angles and a blinding streak
                // where the low sun ahead reflects off them.
                half fresnel = pow(1.0 - saturate(dot(n, view)), 4.0);
                // The probe is HDR (the sun disc can be far above 1): keep the mirror image within white.
                half3 sky = min(GlossyEnvironmentReflection(reflect(-view, n), 0.22h, 1.0h), half3(1.0h, 1.0h, 1.0h));
                colour += sky * (0.25 + fresnel) * _Polish;
                half streak = pow(saturate(dot(n, halfway)), 90.0) * 6.0 + pow(saturate(dot(n, halfway)), 12.0) * 0.4;
                colour += sun.color * streak * _Polish * _GlintStrength;

                // Headlight: polished metal shines in the beam, two bright lines of rail ahead.
                if (_CabHeadlightPos.w > 0.5)
                {
                float3 toLamp = _CabHeadlightPos.xyz - input.positionWS;
                float lampDistance = length(toLamp);
                float3 lampDir = toLamp / max(0.001, lampDistance);
                float cone = smoothstep(0.93, 0.985, dot(-lampDir, normalize(_CabHeadlightDir.xyz)));
                float reach = _CabHeadlightPos.w * cone * 900.0 / (lampDistance * lampDistance + 25.0);
                float3 lampHalf = normalize(lampDir + view);
                float sheen = pow(saturate(dot(n, lampHalf)), 6.0) * 1.6 + saturate(dot(n, lampDir)) * 0.35;
                colour += half3(1.0, 0.95, 0.85) * sheen * reach * _GlintStrength2;
                }
                colour = MixFog(colour, input.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
