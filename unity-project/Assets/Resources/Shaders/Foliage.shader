// Two-sided foliage with a gentle wind sway. Leaves are real geometry on the
// Poly Haven trees, so no alpha test is needed; back faces get flipped normals.
Shader "DockToDrop/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
        _Wind ("Wind strength", Range(0,0.2)) = 0.045
        _Translucency ("Back light", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Glossiness;
        half _Wind;
        half _Translucency;
        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };
        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = saturate(v.vertex.y * 0.18);
            float t = _Time.y;
            float sway = sin(t * 1.3 + wp.x * 0.21 + wp.z * 0.17) * 0.6 + sin(t * 2.7 + wp.x * 0.9) * 0.25;
            float flutter = sin(t * 7.0 + dot(wp, float3(3.1, 2.3, 2.7))) * 0.25;
            float3 offs = float3(sway, 0, sway * 0.6) * h + float3(flutter, flutter * 0.5, flutter) * h * 0.4;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offs * _Wind);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            if (IN.facing < 0) n.z = -n.z;
            o.Normal = n;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Emission = c.rgb * _Translucency * 0.12 * unity_AmbientSky.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
