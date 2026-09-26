// Ground of the endless immersive world. Vertex colours carry the layer weights set by the
// chunk builder (R = ploughed soil, G = forest floor, B = gravel / paving, A = ripe crops over
// grass); steep slopes turn to rock by their normal. Every layer is sampled at two scales and
// the larger one takes over with distance, so the tiling never shows toward the horizon, and a
// broad macro texture breaks up the colour. UV0 holds world metres (wrapped by the builder),
// UV1.x the height, because the world itself moves under the camera and world-space
// coordinates would make the textures swim.
Shader "SortingStation/TerrainBlend"
{
    Properties
    {
        _GrassTex ("Grass", 2D) = "white" {}
        _SoilTex ("Soil", 2D) = "white" {}
        _ForestTex ("Forest floor", 2D) = "white" {}
        _GravelTex ("Gravel", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _MacroTex ("Macro variation", 2D) = "gray" {}
        _GrassTint ("Grass tint", Color) = (1, 1, 1, 1)
        _CropTint ("Crop tint", Color) = (0.86, 0.72, 0.36, 1)
        _SoilTint ("Soil tint", Color) = (1, 1, 1, 1)
        _ForestTint ("Forest tint", Color) = (1, 1, 1, 1)
        _GravelTint ("Gravel tint", Color) = (1, 1, 1, 1)
        _RockTint ("Rock tint", Color) = (1, 1, 1, 1)
        _Tile ("Tile (m)", Float) = 6
        _FarTile ("Far tile (m)", Float) = 30
        _FarStart ("Far blend start (m)", Float) = 30
        _FarEnd ("Far blend end (m)", Float) = 160
        _MacroTile ("Macro tile (m)", Float) = 250
        _RockSlope ("Rock below normal.y", Float) = 0.72
        _Wetness ("Wetness", Range(0, 1)) = 0
        _SnowCover ("Fresh snow", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_SoilTex);
            TEXTURE2D(_ForestTex);
            TEXTURE2D(_GravelTex);
            TEXTURE2D(_RockTex);
            TEXTURE2D(_MacroTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassTex_ST;
                half4 _GrassTint;
                half4 _CropTint;
                half4 _SoilTint;
                half4 _ForestTint;
                half4 _GravelTint;
                half4 _RockTint;
                float _Tile;
                float _FarTile;
                float _FarStart;
                float _FarEnd;
                float _MacroTile;
                half _RockSlope;
                half _Wetness;
                half _SnowCover;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uvh : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 normalOS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = input.normalOS;
                output.uvh = float3(input.uv, input.uv1.x);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half3 SampleLayer(TEXTURE2D_PARAM(tex, samp), float2 metres, half farBlend)
            {
                half3 nearColor = SAMPLE_TEXTURE2D(tex, samp, metres / _Tile).rgb;
                half3 farColor = SAMPLE_TEXTURE2D(tex, samp, metres / _FarTile).rgb;
                return lerp(nearColor, farColor, farBlend);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float distanceToEye = distance(GetCameraPositionWS(), input.positionWS);
                half farBlend = saturate((distanceToEye - _FarStart) / max(1.0, _FarEnd - _FarStart));
                float2 metres = input.uvh.xy;
                half4 weights = input.color;

                half3 grass = SampleLayer(TEXTURE2D_ARGS(_GrassTex, sampler_GrassTex), metres, farBlend) * _GrassTint.rgb;
                half grassLuma = dot(grass, half3(0.3, 0.59, 0.11));
                half3 crop = _CropTint.rgb * (0.55 + grassLuma * 1.6);
                half3 soil = SampleLayer(TEXTURE2D_ARGS(_SoilTex, sampler_GrassTex), metres, farBlend) * _SoilTint.rgb;
                half3 forest = SampleLayer(TEXTURE2D_ARGS(_ForestTex, sampler_GrassTex), metres, farBlend) * _ForestTint.rgb;
                half3 gravel = SampleLayer(TEXTURE2D_ARGS(_GravelTex, sampler_GrassTex), metres, farBlend) * _GravelTint.rgb;

                // Rock is projected on the two vertical planes, weighted by the static mesh normal.
                half3 n = normalize(input.normalOS);
                half3 rockX = SAMPLE_TEXTURE2D(_RockTex, sampler_GrassTex, float2(metres.y, input.uvh.z) / (_Tile * 1.5)).rgb;
                half3 rockZ = SAMPLE_TEXTURE2D(_RockTex, sampler_GrassTex, float2(metres.x, input.uvh.z) / (_Tile * 1.5)).rgb;
                half3 rock = lerp(rockZ, rockX, saturate(abs(n.x) / max(0.001, abs(n.x) + abs(n.z)))) * _RockTint.rgb;
                half rockWeight = saturate((_RockSlope - n.y) * 5.0);

                half3 albedo = grass;
                albedo = lerp(albedo, crop, weights.a);
                albedo = lerp(albedo, soil, weights.r);
                albedo = lerp(albedo, forest, weights.g);
                albedo = lerp(albedo, gravel, weights.b);
                albedo = lerp(albedo, rock, rockWeight);
                half macro = SAMPLE_TEXTURE2D(_MacroTex, sampler_GrassTex, metres / _MacroTile).r;
                albedo *= lerp(0.78, 1.18, macro);
                // Rain darkens the ground; falling snow slowly whitens it (less on steep rock).
                albedo *= lerp(1.0, 0.68, _Wetness);
                half settle = saturate(_SnowCover * (1.4 - rockWeight) - (1.0 - macro) * 0.25);
                albedo = lerp(albedo, half3(0.86, 0.89, 0.93) * lerp(0.9, 1.05, macro), settle);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(input.positionWS));
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentBlinnPhong(inputData, albedo, half4(0, 0, 0, 0), 0.1, half3(0, 0, 0), 1.0, half3(0, 0, 1));
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
