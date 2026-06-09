Shader "Hidden/DepthOnlyURP_NoColor_NoShadow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Name "DepthOnlyColorMask0"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0
        }
    }
}
