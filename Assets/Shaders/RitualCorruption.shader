Shader "Tenebrosus/RitualCorruption"
{
    Properties
    {
        _Corruption("Corruption", Range(0, 1)) = 0
        _Flare("Flare", Range(0, 1)) = 0
        _Darkness("Darkness", Range(0, 1)) = 0.8
        [HDR] _VeinColor("Vein Color", Color) = (5, 0.15, 0.1, 1)
        _VeinScale("Vein Scale", Float) = 6
        _VeinWidth("Vein Width", Range(0.01, 0.3)) = 0.08
        _SpreadScale("Spread Scale", Float) = 2.5
        _PulseSpeed("Pulse Speed", Float) = 2.2
        _Inflate("Inflate", Float) = 0.002
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Corruption;
            float _Flare;
            float _Darkness;
            float4 _VeinColor;
            float _VeinScale;
            float _VeinWidth;
            float _SpreadScale;
            float _PulseSpeed;
            float _Inflate;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                positionRWS += TransformObjectToWorldNormal(input.normalOS) * _Inflate;
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.positionWS = GetAbsolutePositionWS(positionRWS);
                return output;
            }

            float3 RitualHash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float RitualValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = RitualHash33(i).x;
                float n100 = RitualHash33(i + float3(1, 0, 0)).x;
                float n010 = RitualHash33(i + float3(0, 1, 0)).x;
                float n110 = RitualHash33(i + float3(1, 1, 0)).x;
                float n001 = RitualHash33(i + float3(0, 0, 1)).x;
                float n101 = RitualHash33(i + float3(1, 0, 1)).x;
                float n011 = RitualHash33(i + float3(0, 1, 1)).x;
                float n111 = RitualHash33(i + float3(1, 1, 1)).x;
                float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(a, b, f.z);
            }

            float RitualVeins(float3 x)
            {
                float3 cell = floor(x);
                float3 local = frac(x);
                float first = 8.0;
                float second = 8.0;
                [unroll]
                for (int k = -1; k <= 1; k++)
                {
                    [unroll]
                    for (int j = -1; j <= 1; j++)
                    {
                        [unroll]
                        for (int i = -1; i <= 1; i++)
                        {
                            float3 offset = float3(i, j, k);
                            float3 delta = offset - local + RitualHash33(cell + offset);
                            float dist = dot(delta, delta);
                            if (dist < first)
                            {
                                second = first;
                                first = dist;
                            }
                            else if (dist < second)
                            {
                                second = dist;
                            }
                        }
                    }
                }
                return sqrt(second) - sqrt(first);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 position = input.positionWS;
                float spread = RitualValueNoise(position * _SpreadScale) * 0.7 + RitualValueNoise(position * _SpreadScale * 3.1) * 0.3;
                float coverage = saturate((_Corruption * 1.25 - spread) * 4.0);
                float warp = RitualValueNoise(position * _VeinScale * 0.5) * 0.6;
                float veinEdge = RitualVeins(position * _VeinScale + warp);
                float vein = 1.0 - smoothstep(0.0, _VeinWidth, veinEdge);
                float time = _Time.y * _PulseSpeed;
                float beat = pow(saturate(sin(time + spread * 6.0)), 6.0) + pow(saturate(sin(time + 0.6 + spread * 6.0)), 10.0);
                float glow = vein * coverage * (0.35 + beat + _Flare * 2.0);
                float alpha = saturate(coverage * _Darkness * (1.0 - vein * 0.5) + _Flare * 0.2 * coverage);
                float3 color = _VeinColor.rgb * glow * GetCurrentExposureMultiplier();
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
