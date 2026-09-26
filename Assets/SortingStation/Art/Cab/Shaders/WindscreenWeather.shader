// Rain drops and snowflakes on the outside of the windscreen, cleared by the wipers.
// Stateless: every cell of the glass holds one drop that appears at a random moment and
// lives for a while. A drop shows only if it landed after the wiper blade last passed over
// its spot; the blade position is the same function of time the cab uses to move the arms
// (0.5 - 0.5 cos(2.75 t) of a 78 degree sweep). UV0 is metres on the glass: x across, y up.
Shader "SortingStation/WindscreenWeather"
{
    Properties
    {
        _Rain ("Rain", Range(0, 1)) = 0
        _Snow ("Snow", Range(0, 1)) = 0
        _WipersOn ("Wipers on", Float) = 0
        _WiperTime ("Wiper clock", Float) = 0
        _PivotLeft ("Left pivot (m)", Vector) = (-0.46, 0.03, 0, 0)
        _PivotRight ("Right pivot (m)", Vector) = (0.46, 0.03, 0, 0)
        _Light ("Light", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Rain;
                float _Snow;
                float _WipersOn;
                float _WiperTime;
                float4 _PivotLeft;
                float4 _PivotRight;
                half4 _Light;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            // Seconds since a wiper blade last passed over the point (large if never).
            float SinceWipe(float2 p, float2 pivot, float restDeg, float sweepSign)
            {
                float2 d = p - pivot;
                float r = length(d);
                if (r < 0.08 || r > 0.7) return 1000.0;
                float angle = degrees(atan2(-d.x, d.y));
                float u = (angle - restDeg) * sweepSign / 78.0;
                if (u < 0.0 || u > 1.0) return 1000.0;
                const float omega = 2.75;
                float a = acos(clamp(1.0 - 2.0 * u, -1.0, 1.0));
                float phase = fmod(_WiperTime * omega, 6.2831853);
                float first = fmod(phase - a + 12.5663706, 6.2831853);
                float second = fmod(phase - (6.2831853 - a) + 12.5663706, 6.2831853);
                return min(first, second) / omega;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv;
                float since = 1000.0;
                if (_WipersOn > 0.5)
                    since = min(SinceWipe(p, _PivotLeft.xy, -86.0, 1.0), SinceWipe(p, _PivotRight.xy, 86.0, -1.0));

                half4 result = half4(0, 0, 0, 0);
                // Rain: one drop per 3.5 cm cell, some of them heavy enough to run down the glass.
                if (_Rain > 0.01)
                {
                    const float cell = 0.035;
                    float2 grid = p / cell;
                    float2 id = floor(grid);
                    float h = Hash(id);
                    float period = lerp(6.0, 2.0, _Rain) * (0.6 + h);
                    float age = fmod(_WiperTime + h * 37.0, period);
                    // Fresh glass behind the blade: drops come back one by one over a few seconds.
                    float present = step(Hash(id + 5.3), _Rain * 0.85 * saturate(since / 2.2)) * step(age, since);
                    float heavy = step(0.8, Hash(id + 9.1));
                    float2 centre = (id + 0.2 + 0.6 * float2(Hash(id + 1.7), Hash(id + 2.9))) * cell;
                    centre.y -= heavy * age * 0.05;
                    float radius = cell * lerp(0.12, 0.3, Hash(id + 4.4)) * saturate(age * 3.0 + 0.3);
                    float2 offset = (p - centre) / float2(radius, radius * (1.0 + heavy * 1.5));
                    float drop = saturate(1.0 - dot(offset, offset));
                    float rim = saturate(drop * 4.0) * (1.0 - saturate(drop * 1.6));
                    float spark = pow(saturate(1.0 - length(offset - float2(-0.35, 0.35)) * 2.2), 3.0);
                    half3 colour = _Light.rgb * (0.25 + spark * 1.4);
                    float alpha = (rim * 0.55 + drop * 0.12 + spark * 0.6) * present;
                    result = half4(colour, alpha);
                }
                // Snow: soft white flakes that settle and stay until the blade clears them.
                if (_Snow > 0.01)
                {
                    const float cell = 0.05;
                    float2 id = floor(p / cell);
                    float h = Hash(id + 11.0);
                    float period = lerp(14.0, 5.0, _Snow) * (0.7 + h);
                    float age = fmod(_WiperTime + h * 53.0, period);
                    float present = step(Hash(id + 3.1), _Snow * 0.7) * step(age, since);
                    float2 centre = (id + 0.2 + 0.6 * float2(Hash(id + 7.7), Hash(id + 8.8))) * cell;
                    float radius = cell * lerp(0.15, 0.4, Hash(id + 6.6));
                    float flake = saturate(1.0 - length(p - centre) / radius);
                    float alpha = smoothstep(0.0, 0.6, flake) * 0.85 * present;
                    result = lerp(result, half4(_Light.rgb * 0.95, alpha), alpha);
                }
                // The blade leaves a thin wet film that shines for a moment.
                if (_WipersOn > 0.5 && _Rain + _Snow > 0.01)
                {
                    float film = saturate(1.0 - since / 0.25) * saturate(since / 0.03);
                    result = lerp(result, half4(_Light.rgb * 1.2, 0.22), film * 0.8);
                }
                return result;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
