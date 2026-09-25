// Sky of the immersive world: a colour gradient with a warm sunset band, the sun with its
// glow, drifting clouds lit by the sun, and at night stars (twinkling), the Milky Way and a
// moon with its phase. Sun and moon directions are in the scene frame (as lit); clouds and
// stars use the world frame through _SkyRotation, because the world turns under the camera.
Shader "SortingStation/Sky"
{
    Properties
    {
        _CloudTex ("Cloud noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            float4x4 _SkyRotation;
            float3 _SunDirection;
            float3 _MoonDirection;
            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _SunsetColor;
            float4 _SunColor;
            float4 _CloudLight;
            float4 _CloudShadow;
            float _SunsetAmount;
            float _SunVisible;
            float _CloudCover;
            float _StarAmount;
            float _MoonAmount;
            float _MoonPhase;
            float _SkyTime;
            float _Brightness;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Clouds(float3 d, float time)
            {
                float2 uv = d.xz / (d.y + 0.12) * 0.22 + float2(time * 0.004, time * 0.0015);
                float n = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r * 0.55
                        + SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 2.3 + 0.37).r * 0.3
                        + SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 5.1 + 0.71).r * 0.15;
                return n;
            }

            float Stars(float3 d, float time)
            {
                // Stars sit on a grid in latitude/longitude; one cell in ~70 holds a star.
                float2 sphere = float2(atan2(d.x, d.z), asin(clamp(d.y, -1.0, 1.0)));
                float2 grid = sphere * float2(95.0, 95.0);
                float2 cell = floor(grid);
                float h = Hash(cell);
                if (h < 0.986) return 0.0;
                float2 centre = cell + 0.25 + 0.5 * float2(Hash(cell + 7.1), Hash(cell + 3.3));
                float dist = length(grid - centre);
                float size = 0.12 + 0.2 * Hash(cell + 11.7);
                float twinkle = 0.65 + 0.35 * sin(time * (2.0 + 4.0 * Hash(cell + 5.5)) + h * 60.0);
                return saturate(1.0 - dist / size) * twinkle * (0.4 + 1.2 * (h - 0.986) / 0.014);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 v = normalize(input.direction);
                float3 d = normalize(mul((float3x3)_SkyRotation, v));
                float h = v.y;

                // Gradient: zenith to horizon, darkening below the horizon.
                float toHorizon = pow(1.0 - saturate(h), 3.0);
                float3 colour = lerp(_ZenithColor.rgb, _HorizonColor.rgb, toHorizon);
                if (h < 0.0) colour = lerp(_HorizonColor.rgb, _HorizonColor.rgb * 0.55, saturate(-h * 6.0));

                // Sunrise and sunset: a warm band along the horizon, strongest toward the sun.
                float cs = dot(v, _SunDirection);
                float2 flatV = normalize(v.xz + 1e-4);
                float2 flatS = normalize(_SunDirection.xz + 1e-4);
                float towardSun = saturate(dot(flatV, flatS) * 0.5 + 0.5);
                float band = (1.0 - saturate(abs(h) * 2.6)) * (0.55 + 0.45 * pow(towardSun, 2.0));
                colour = lerp(colour, _SunsetColor.rgb, band * _SunsetAmount);

                // Sun glow (wide and tight) and its disc.
                float glow = pow(saturate(cs), 10.0) * 0.45 + pow(saturate(cs), 90.0) * 1.2;
                colour += _SunColor.rgb * glow * _SunVisible * (1.0 + _SunsetAmount);
                float disc = smoothstep(0.99955, 0.99975, cs);
                colour = lerp(colour, _SunColor.rgb * 3.0, disc * _SunVisible);

                // Night: stars, the Milky Way and the moon.
                float night = _StarAmount * saturate(h * 8.0);
                float stars = Stars(d, _SkyTime) * night;
                float3 galaxyNormal = normalize(float3(0.35, 0.55, 0.76));
                float galaxy = exp(-pow(dot(d, galaxyNormal), 2.0) * 45.0) * night * 0.12
                             * SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, d.xz * 1.7 + d.y).r;
                float cm = dot(v, _MoonDirection);
                float moonDisc = smoothstep(0.99925, 0.9995, cm);
                float3 moonLocal = v - _MoonDirection * cm;
                float lit = saturate(dot(normalize(moonLocal + 1e-5), float3(_MoonPhase, 0.0, 0.0)) * 3.0 + 0.5 + _MoonPhase * 0.2);
                float3 moon = (moonDisc * lit * 1.4 + pow(saturate(cm), 400.0) * 0.25) * float3(0.92, 0.94, 1.0) * _MoonAmount;

                // Clouds hide the stars and moon and catch the sun on their edges.
                float cloud = 0.0;
                if (d.y > 0.0)
                {
                    float n = Clouds(d, _SkyTime);
                    cloud = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.22, n) * saturate(d.y * 7.0);
                    float3 cloudColour = lerp(_CloudShadow.rgb, _CloudLight.rgb, saturate(n * 1.4 - 0.2));
                    cloudColour += _SunColor.rgb * pow(saturate(cs), 6.0) * 0.8 * _SunVisible * (1.0 - cloud * 0.5);
                    cloudColour = lerp(cloudColour, _SunsetColor.rgb * 0.9, _SunsetAmount * 0.45 * (1.0 - saturate(h * 1.5)));
                    colour = lerp(colour, cloudColour, cloud * 0.92);
                }
                colour += (stars + galaxy) * (1.0 - cloud) + moon * (1.0 - cloud * 0.85);
                return half4(colour * _Brightness, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
