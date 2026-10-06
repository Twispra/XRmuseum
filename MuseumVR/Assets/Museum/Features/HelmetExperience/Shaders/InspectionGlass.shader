Shader "Museum/Inspection Glass"
{
    Properties
    {
        _BaseColor ("Glass tint", Color) = (.16,.30,.32,.16)
        _CutEnabled ("Cut enabled", Float) = 0
        _CutPlane ("World cut plane", Vector) = (1,0,0,0)
        _Hover ("Hover", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "GlassForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _CutPlane;
                float _CutEnabled, _Hover;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            half4 frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(_CutEnabled>.5)clip(-dot(float4(i.positionWS,1),_CutPlane));
                half3 normal=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel=pow(1-saturate(abs(dot(normal,view))),3);
                Light light=GetMainLight();
                half spec=pow(saturate(dot(normal,normalize(light.direction+view))),96);
                half3 reflected=GlossyEnvironmentReflection(reflect(-view,normal),.12,1);
                half3 color=_BaseColor.rgb+reflected*.35+light.color*spec*.9+fresnel*.22;
                color=lerp(color,half3(.12,.65,.68),saturate(_Hover)*.15);
                return half4(color,saturate(_BaseColor.a+fresnel*.32+spec*.32));
            }
            ENDHLSL
        }
    }
}
