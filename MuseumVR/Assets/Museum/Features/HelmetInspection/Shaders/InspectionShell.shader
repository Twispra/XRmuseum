Shader "Museum/Inspection Shell"
{
    Properties
    {
        _BaseColor ("Surface", Color) = (0.55,0.28,0.13,1)
        _InsideColor ("Inside", Color) = (0.18,0.28,0.30,1)
        _CutColor ("Section edge", Color) = (1,0.65,0.18,1)
        _CutEnabled ("Cut enabled", Float) = 0
        _CutPlane ("World cut plane", Vector) = (1,0,0,0)
        _Hover ("Hover highlight", Float) = 0
        _Focus ("Interpretive attention", Float) = 0
        _ShellCenter ("Hollow shell center (object space)", Vector) = (0,0,0,0)
        _ShellInterior ("Distinguish actual inner surfaces", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "InspectionForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _InsideColor, _CutColor;
                float4 _CutPlane, _ShellCenter;
                float _CutEnabled, _Hover, _Focus, _ShellInterior;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float distance = dot(float4(i.positionWS,1), _CutPlane);
                if (_CutEnabled > .5) clip(-distance);
                half3 normal = normalize(i.normalWS) * IS_FRONT_VFACE(face,1,-1);
                Light light = GetMainLight();
                half shade = .36 + .64 * saturate(dot(normal, light.direction));
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half spec = pow(saturate(dot(normal, normalize(light.direction + view))), 42) * .22;
                half3 color = IS_FRONT_VFACE(face, _BaseColor.rgb, _InsideColor.rgb);
                // The original hollow mesh has inward-facing inner normals; distinguish these existing surfaces.
                if(_ShellInterior>.5 && dot(i.normalWS,i.positionWS-TransformObjectToWorld(_ShellCenter.xyz))<-.001)
                    color=lerp(_BaseColor.rgb,half3(.29,.23,.17),.55);
                color = color * shade + spec;
                // A narrow coloured rim marks the removed half. It is a visual aid, not measured wall thickness.
                if (_CutEnabled > .5 && abs(distance) < .0018) color = _CutColor.rgb;
                color = lerp(color, half3(.12,.9,.9), saturate(_Hover)*.5);
                color += half3(.26,.22,.12) * saturate(_Focus);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
