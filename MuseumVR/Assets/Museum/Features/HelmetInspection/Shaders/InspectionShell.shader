Shader "Museum/Inspection Shell"
{
    Properties
    {
        _BaseColor ("Surface", Color) = (0.55,0.28,0.13,1)
        _BaseMap ("Surface texture", 2D) = "white" {}
        _BumpMap ("Surface normal", 2D) = "bump" {}
        _RoughnessMap ("Roughness", 2D) = "white" {}
        _TextureStrength ("Texture strength", Float) = 0
        _PatinaStrength ("Procedural patina", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _SpecularScale ("Specular intensity", Range(0,1)) = 1
        _Roughness ("Roughness scale", Range(0,1)) = .5
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
                float4 _BaseMap_ST;
                float _TextureStrength, _PatinaStrength, _Metallic, _Roughness, _SpecularScale;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 tangentWS:TEXCOORD3; float3 positionOS:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            float PatinaHash(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }
            float PatinaNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                float a = lerp(PatinaHash(cell), PatinaHash(cell + float3(1,0,0)), t.x);
                float b = lerp(PatinaHash(cell + float3(0,1,0)), PatinaHash(cell + float3(1,1,0)), t.x);
                float c = lerp(PatinaHash(cell + float3(0,0,1)), PatinaHash(cell + float3(1,0,1)), t.x);
                float d = lerp(PatinaHash(cell + float3(0,1,1)), PatinaHash(cell + float3(1,1,1)), t.x);
                return lerp(lerp(a,b,t.y), lerp(c,d,t.y), t.z);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                return o;
            }
            half4 frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float distance = dot(float4(i.positionWS,1), _CutPlane);
                if (_CutEnabled > .5) clip(-distance);
                half3 normal = normalize(i.normalWS) * IS_FRONT_VFACE(face,1,-1);
                half3 baseColor = _BaseColor.rgb;
                half roughness = _Roughness;
                if (_TextureStrength > .5)
                {
                    baseColor *= SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                    half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
                    half3 tangent = normalize(i.tangentWS.xyz);
                    half3 bitangent = cross(normal,tangent) * i.tangentWS.w;
                    normal = normalize(tangent*tangentNormal.x+bitangent*tangentNormal.y+normal*tangentNormal.z);
                    roughness = saturate(SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,i.uv).r * _Roughness);
                }
                if (_PatinaStrength > .001)
                {
                    // Object-space mottling stays on the metal when a visitor
                    // picks up or rotates the helmet. It is an interpretive
                    // finish, not a scan or tracing of the museum photograph.
                    float broad = PatinaNoise(i.positionOS * 19.0);
                    float spots = PatinaNoise(i.positionOS * 83.0);
                    float grain = PatinaNoise(i.positionOS * 270.0);
                    half dark = smoothstep(.43, .74, broad * .54 + spots * .38 + grain * .08);
                    half wear = smoothstep(.68, .86, broad * .32 + spots * .53 + grain * .15);
                    half3 aged = baseColor * lerp(half3(.62,.59,.56), half3(1.04,.99,.94), dark);
                    aged = lerp(aged, baseColor * half3(1.10,.89,.77), wear * .28);
                    baseColor = lerp(baseColor, aged, _PatinaStrength);
                    roughness = saturate(roughness * lerp(.82,1.17,spots));
                }
                Light light = GetMainLight();
                half shade = .36 + .64 * saturate(dot(normal, light.direction));
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half exponent = (_TextureStrength > .5 || _PatinaStrength > .001) ? lerp(100,8,roughness) : 42;
                half spec = pow(saturate(dot(normal, normalize(light.direction + view))), exponent) * lerp(.22,.45,_Metallic) * _SpecularScale;
                half3 color = IS_FRONT_VFACE(face, baseColor, _InsideColor.rgb);
                // The original hollow mesh has inward-facing inner normals; distinguish these existing surfaces.
                if(_ShellInterior>.5 && dot(i.normalWS,i.positionWS-TransformObjectToWorld(_ShellCenter.xyz))<-.001)
                    color=lerp(baseColor,half3(.29,.23,.17),.55);
                color = color * shade + spec;
                if (_TextureStrength > .5)
                    color += GlossyEnvironmentReflection(reflect(-view,normal),roughness,1) * lerp(.04,.38,_Metallic);
                else if (_PatinaStrength > .001)
                    color += GlossyEnvironmentReflection(reflect(-view,normal),roughness,1) * lerp(.015,.07,_Metallic);
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
