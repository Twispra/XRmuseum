Shader "Museum/Exhibit UI"
{
    Properties { _BaseColor ("Color", Color) = (0.04,0.12,0.16,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS=TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _BaseColor; }
            ENDHLSL
        }
    }
}
