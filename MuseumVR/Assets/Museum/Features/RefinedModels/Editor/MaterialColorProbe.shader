// Acceptance-only probe: the same ordinary Color property as Inspection Shell.
// A linear floating-point target exposes Unity's uploaded shader value without
// lighting, tone mapping, texture sampling, display transfer or quantization.
Shader "Hidden/Museum/QA/Material Color Probe"
{
    Properties { _BaseColor ("Surface", Color) = (1,1,1,1) }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _BaseColor;
            float4 frag(v2f_img input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
