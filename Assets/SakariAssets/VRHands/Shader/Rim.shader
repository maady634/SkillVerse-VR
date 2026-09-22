// URP port of the original built-in surface shader (Lambert + additive rim emission).
// The shader name and properties are unchanged so existing materials keep working.
Shader "Sakari/VRHands"
{
    Properties
    {
        _InnerColor("Inner Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _MainTex("Particle Texture", 2D) = "white" {}
        _RimColor("Rim Color", Color) = (0.26,0.19,0.16,0.0)
        _RimPower("Rim Power", Range(0.5,8.0)) = 3.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _InnerColor;
            half4 _RimColor;
            half _RimPower;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS   : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        // Depth-only prepass (the original "ColorMask 0" pass) so the additive pass only shows the front surface.
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            half4 fragDepth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardRim"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            Blend One One
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Lambert albedo (main light + ambient), as the surface shader did
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(dot(normalWS, mainLight.direction)) + SampleSH(normalWS);
                half3 albedo = _InnerColor.rgb * lighting;

                half rim = 1.0h - saturate(dot(viewDirWS, normalWS));
                half3 emission = _RimColor.rgb * pow(rim, _RimPower);
                return half4(albedo + emission, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
