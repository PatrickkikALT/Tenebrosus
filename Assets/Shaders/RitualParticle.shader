Shader "Tenebrosus/RitualParticle"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        _Softness("Softness", Range(0.5, 6)) = 2
        _Wisp("Wisp", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _Softness;
            float _Wisp;
            float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float angle = atan2(centered.y, centered.x);
                float wobble = 1.0 + _Wisp * 0.25 * sin(angle * 5.0 + input.color.a * 20.0 + _Time.y);
                float radius = length(centered) * wobble;
                float shape = pow(saturate(1.0 - radius), _Softness);
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                float alpha = shape * texel.a * input.color.a * _Color.a;
                float3 color = texel.rgb * input.color.rgb * _Color.rgb * alpha * GetCurrentExposureMultiplier();
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
