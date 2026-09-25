// Draws a tree into the distant-tree atlas (TreeImpostors): unlit albedo with a soft fixed
// shading from the normals, alpha-cut foliage, and alpha 1 wherever the tree is, so the
// atlas background stays transparent. Used with CommandBuffer.DrawMesh, never by a camera.
Shader "SortingStation/ImpostorBake"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                float3 n = normalize(i.normal);
                float shade = 0.72 + 0.28 * saturate(dot(n, normalize(float3(0.35, 0.8, -0.5))));
                return fixed4(c.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
