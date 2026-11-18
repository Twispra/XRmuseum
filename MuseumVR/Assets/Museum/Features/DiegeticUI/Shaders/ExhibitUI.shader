Shader "Museum/Exhibit UI"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.04,0.12,0.16,1)
        [HideInInspector] _FrontOnly ("Front-facing display", Float) = 0
        [HideInInspector] _FrontLineNormal ("Line display normal (world)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
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
            float _FrontOnly;
            float4 _FrontLineNormal;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float front:TEXCOORD0; float3 positionWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                // The labels and outlines use local -Z as their readable side.
                o.front = _FrontOnly > .5 ? -TransformWorldToObject(GetCameraPositionWS()).z : 1;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Camera-facing lines may use world-space draw matrices regardless of their panel transform.
                float side = _FrontOnly > 1.5
                    ? dot(_FrontLineNormal.xyz, GetCameraPositionWS() - i.positionWS)
                    : i.front;
                clip(side);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
