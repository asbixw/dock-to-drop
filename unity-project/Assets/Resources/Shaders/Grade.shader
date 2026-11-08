Shader "Hidden/DockToDrop/Grade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c = saturate(max(c, 0.0));
                // gentle S-curve, then filmic contrast and a touch of saturation
                float3 s = c * c * (3.0 - 2.0 * c);
                c = lerp(c, s, 0.12);
                c = (c - 0.5) * 1.055 + 0.5;
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(l.xxx, c, 1.07);
                // cool the shadows, warm the highlights
                c += saturate(1.0 - l * 2.1) * float3(-0.010, 0.001, 0.018);
                c += saturate((l - 0.52) * 2.2) * float3(0.018, 0.007, -0.006);
                // a whisper of warm haze in the deepest blacks
                c = lerp(c, float3(0.040, 0.044, 0.046), 0.035);
                // soft lens vignette
                float2 p = i.uv - 0.5;
                float v = saturate(1.0 - dot(p, p) * 0.48);
                c *= lerp(0.875, 1.0, v);
                return fixed4(saturate(c), 1.0);
            }
            ENDCG
        }
    }
}
