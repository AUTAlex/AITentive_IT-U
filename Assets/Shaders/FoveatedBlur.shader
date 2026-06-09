Shader "Hidden/FoveatedVision"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _Gaze ("Gaze UV", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Foveal Radius", Float) = 0.1
        _BlurSize ("Blur Size", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // Needed for blur sampling
            float2 _Gaze;
            float _Radius;
            float _BlurSize;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Distance from gaze
                float dist = distance(uv, _Gaze);

                // Blend between sharp and blurred regions
                float blurStrength = saturate((dist - _Radius) / _Radius);

                float3 color = tex2D(_MainTex, uv).rgb;

                // Apply a very simple blur (box blur)
                float3 blurred = 0;
                int samples = 5;
                for (int x = -samples; x <= samples; ++x)
                {
                    for (int y = -samples; y <= samples; ++y)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy * _BlurSize;
                        blurred += tex2D(_MainTex, uv + offset).rgb;
                    }
                }
                blurred /= pow(2 * samples + 1, 2);

                // Mix sharp and blurred
                float3 finalColor = lerp(color, blurred, blurStrength);
                return float4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
