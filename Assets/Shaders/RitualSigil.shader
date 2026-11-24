Shader "Tenebrosus/RitualSigil"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (4, 1.6, 0.4, 1)
        _Reveal("Reveal", Range(0, 1)) = 0
        _Pulse("Pulse", Range(0, 3)) = 0
        _Intensity("Intensity", Range(0, 1)) = 0
        _Flash("Flash", Range(0, 1)) = 0
        _LineWidth("Line Width", Float) = 0.006
        _Glow("Glow", Float) = 0.35
        _RuneCount("Rune Count", Float) = 24
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Reveal;
            float _Pulse;
            float _Intensity;
            float _Flash;
            float _LineWidth;
            float _Glow;
            float _RuneCount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.uv = input.uv;
                return output;
            }

            float RitualHash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            float RitualStroke(float d, float width)
            {
                float aa = max(fwidth(d), 1e-5);
                return 1.0 - smoothstep(width, width + aa * 1.5, d) + exp(-d / (width * 5.0)) * _Glow;
            }

            float RitualSegment(float2 p, float2 a, float2 b, out float t)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                t = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * t);
            }

            float RitualStage(float start, float end)
            {
                return saturate((_Reveal - start) / (end - start));
            }

            float RitualRing(float2 p, float radius, float angle01, float progress, float width)
            {
                float d = abs(length(p) - radius);
                return RitualStroke(d, width) * step(angle01, progress - 1e-3);
            }

            float RitualGlyph(float2 q, float2 halfSize, float seed)
            {
                float t;
                float stemX = (frac(seed * 3.1) - 0.5) * halfSize.x;
                float2 top = float2(stemX, halfSize.y);
                float2 bottom = float2(stemX, -halfSize.y);
                float d = RitualSegment(q, bottom, top, t);
                float branch = RitualSegment(q, top, float2(halfSize.x, 0.0), t);
                d = min(d, branch + step(frac(seed * 13.7), 0.4) * 10.0);
                float tail = RitualSegment(q, bottom, float2(-halfSize.x, halfSize.y * 0.3), t);
                d = min(d, tail + step(frac(seed * 29.3), 0.5) * 10.0);
                float bar = RitualSegment(q, float2(-halfSize.x, 0.0), float2(halfSize.x, 0.0), t);
                d = min(d, bar + step(frac(seed * 41.9), 0.6) * 10.0);
                return d;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float angle01 = atan2(p.x, p.y) / TWO_PI + 0.5;
                float w = _LineWidth;
                float time = _Time.y;

                float ringProgress = RitualStage(0.0, 0.45);
                float shape = RitualRing(p, 0.95, angle01, ringProgress, w * 1.4);
                shape += RitualRing(p, 0.88, 1.0 - angle01, ringProgress, w);
                shape += RitualRing(p, 0.70, angle01, RitualStage(0.1, 0.5), w);
                shape += RitualRing(p, 0.62, 1.0 - angle01, RitualStage(0.15, 0.55), w * 0.8);

                float starProgress = RitualStage(0.35, 0.8) * 5.0;
                [unroll]
                for (int k = 0; k < 5; k++)
                {
                    float a0 = TWO_PI * k / 5.0;
                    float a1 = TWO_PI * (k + 2) / 5.0;
                    float2 v0 = 0.62 * float2(sin(a0), cos(a0));
                    float2 v1 = 0.62 * float2(sin(a1), cos(a1));
                    float s;
                    float d = RitualSegment(p, v0, v1, s);
                    shape += RitualStroke(d, w) * step(s, saturate(starProgress - k) - 1e-3);
                    float node = abs(length(p - v0) - 0.04);
                    shape += RitualStroke(node, w * 0.8) * step(k + 0.5, starProgress);
                }

                float inner = 0.73;
                float outer = 0.85;
                float band = outer - inner;
                float cell = floor(angle01 * _RuneCount);
                float u = frac(angle01 * _RuneCount);
                float v = (r - inner) / band;
                float cellWidth = TWO_PI * (inner + outer) * 0.5 / _RuneCount;
                float2 q = float2((u - 0.5) * cellWidth, (v - 0.5) * band);
                float glyph = RitualGlyph(q, float2(cellWidth * 0.28, band * 0.36), RitualHash11(cell + 1.0));
                float inBand = step(abs(v - 0.5), 0.6);
                shape += RitualStroke(glyph, w * 0.8) * inBand * step(cell / _RuneCount, RitualStage(0.6, 1.0) - 1e-3);

                float edge = saturate((1.02 - r) * 20.0);
                float wave = pow(saturate(sin(r * 18.0 - time * 3.0)), 8.0) * 0.25 * _Intensity * step(r, 0.95);
                float disk = pow(saturate(1.0 - r), 2.0) * 0.12 * (0.5 + _Intensity);
                float pulse = 1.0 + _Pulse * 1.5 + sin(time * 2.0) * 0.1;
                float energy = (shape * (0.6 + _Intensity * 1.4) * pulse + (wave + disk) * _Reveal) * edge;

                float3 color = lerp(_Color.rgb, float3(6.0, 0.3, 0.2), _Flash);
                return float4(color * energy * GetCurrentExposureMultiplier(), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
