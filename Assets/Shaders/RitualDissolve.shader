Shader "Tenebrosus/RitualDissolve"
{
    Properties
    {
        _BaseColorMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EdgeColor("Edge Color", Color) = (6, 2.2, 0.4, 1)
        _Dissolve("Dissolve", Range(0, 1)) = 0
        _EdgeWidth("Edge Width", Range(0.01, 0.3)) = 0.08
        _NoiseScale("Noise Scale", Float) = 9
        _Brightness("Brightness", Range(0, 2)) = 0.35
        _Char("Char", Range(0, 1)) = 0.85
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColorMap_ST;
            float4 _BaseColor;
            float4 _EdgeColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            float _Brightness;
            float _Char;
            float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.positionOS = input.positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _BaseColorMap_ST.xy + _BaseColorMap_ST.zw;
                return output;
            }

            float RitualHash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float RitualValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = RitualHash31(i);
                float n100 = RitualHash31(i + float3(1, 0, 0));
                float n010 = RitualHash31(i + float3(0, 1, 0));
                float n110 = RitualHash31(i + float3(1, 1, 0));
                float n001 = RitualHash31(i + float3(0, 0, 1));
                float n101 = RitualHash31(i + float3(1, 0, 1));
                float n011 = RitualHash31(i + float3(0, 1, 1));
                float n111 = RitualHash31(i + float3(1, 1, 1));
                float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(a, b, f.z);
            }

            float RitualFbm(float3 x)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += RitualValueNoise(x) * amplitude;
                    x = x * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return sum / 0.9375;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float noise = RitualFbm(input.positionOS * _NoiseScale);
                float threshold = _Dissolve * (1.0 + _EdgeWidth * 2.0) - _EdgeWidth;
                float margin = noise - threshold;
                clip(margin);

                float4 baseColor = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv) * _BaseColor;
                float3 normal = normalize(input.normalWS);
                float light = 0.35 + 0.65 * saturate(dot(normal, normalize(float3(0.3, 1.0, 0.2))));
                float edge = 1.0 - saturate(margin / _EdgeWidth);
                float charring = saturate(_Dissolve * 1.5 + edge) * _Char;
                float3 color = baseColor.rgb * light * _Brightness * (1.0 - charring);
                color += _EdgeColor.rgb * pow(edge, 3.0);
                return float4(color * GetCurrentExposureMultiplier() * _Opacity, _Opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
