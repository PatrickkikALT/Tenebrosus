Shader "GoodDog/VHS"
{
    Properties
    {
        _PixelRows("Pixel Rows", Float) = 270
        _Aberration("Aberration", Float) = 1.2
        _Noise("Noise", Float) = 0.05
        _Scanline("Scanline", Float) = 0.22
        _Wobble("Wobble", Float) = 0.0012
        _Tear("Tear", Float) = 0.25
        _Distortion("Distortion", Float) = 0
        _Ghost("Ghost", Float) = 0
        _Vignette("Vignette", Float) = 0.55
        _Saturation("Saturation", Float) = 0.82
        _Invert("Invert", Float) = 0
        _Darken("Darken", Float) = 0
        _Flash("Flash", Float) = 0
        _NoiseLine("Noise Line", Float) = 0
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _PixelRows;
    float _Aberration;
    float _Noise;
    float _Scanline;
    float _Wobble;
    float _Tear;
    float _Distortion;
    float _Ghost;
    float _Vignette;
    float _Saturation;
    float _Invert;
    float _Darken;
    float _Flash;
    float _NoiseLine;

    float Hash12(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    float3 Fetch(float2 uv)
    {
        uv = clamp(uv, 0.0, 0.999);
        return CustomPassSampleCustomColor(uv).rgb;
    }

    float4 CopyPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        return float4(CustomPassLoadCameraColor(varyings.positionCS.xy, 0), 1.0);
    }

    float4 VhsPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float t = _Time.y;

        float2 res = float2(_PixelRows * _ScreenSize.x / _ScreenSize.y, _PixelRows);
        float row = floor(uv.y * res.y);

        float offset = sin(uv.y * 38.0 + t * 3.1) * _Wobble + sin(uv.y * 6.0 - t * 1.3) * _Wobble * 0.7;
        offset += sin(uv.y * 11.0 + t * 2.3) * _Distortion * 0.008;
        float burst = step(0.93, Hash12(float2(floor(t * 9.0), 7.0)));
        offset += (Hash12(float2(row, floor(t * 20.0))) - 0.5) * _Distortion * 0.03 * burst;

        float band = frac(t * 0.11);
        float bandMask = smoothstep(0.0, 0.015, uv.y - band) * smoothstep(0.07, 0.03, uv.y - band);
        offset += bandMask * _Tear * 0.02 * (Hash12(float2(row, floor(t * 30.0))) - 0.2);

        float2 warped = float2(uv.x + offset, uv.y);
        warped.y += sin(uv.x * 9.0 + t) * _Distortion * 0.004;

        float2 puv = (floor(warped * res) + 0.5) / res;
        float2 texel = 1.0 / res;
        float ab = _Aberration * texel.x;

        float3 color;
        color.r = Fetch(puv + float2(ab, 0.0)).r;
        color.g = Fetch(puv).g;
        color.b = Fetch(puv - float2(ab, 0.0)).b;

        float3 bleed = (Fetch(puv + float2(texel.x, 0.0)) + Fetch(puv - float2(texel.x, 0.0)) + Fetch(puv - float2(texel.x * 2.0, 0.0))) / 3.0;
        color = lerp(color, bleed, 0.35);

        float3 ghost = Fetch(puv + float2(0.025 * sin(t * 0.7), 0.012 * cos(t * 0.5)));
        color = lerp(color, max(color, ghost), _Ghost * 0.6);
        color += Fetch(puv - float2(texel.x * 6.0, 0.0)) * _Ghost * 0.15;

        float luma = dot(color, float3(0.299, 0.587, 0.114));
        color = lerp(luma.xxx, color, _Saturation);
        color *= float3(1.03, 1.0, 0.94);

        float scan = 0.5 + 0.5 * cos((warped.y * res.y) * 6.28318);
        color *= 1.0 - _Scanline * scan;

        float grain = Hash12(varyings.positionCS.xy * 0.73 + frac(t * 13.7) * 317.0) - 0.5;
        color += grain * _Noise;

        float noiseLine = step(0.996, Hash12(float2(row, floor(t * 24.0))));
        color += noiseLine * Hash12(float2(floor(uv.x * res.x * 0.25), row + t)) * _Noise * 5.0 * _NoiseLine;
        color += bandMask * _Tear * 0.06 * Hash12(float2(floor(uv.x * res.x), row + floor(t * 30.0))) * _NoiseLine;

        float2 centered = uv - 0.5;
        centered.x *= _ScreenSize.x / _ScreenSize.y * 0.75;
        color *= saturate(1.0 - dot(centered, centered) * _Vignette * 2.2);

        color = lerp(color, 1.0 - saturate(color), _Invert);
        color *= 1.0 - _Darken;
        color += _Flash;
        return float4(max(color, 0.0), 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Copy"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CopyPass
            ENDHLSL
        }

        Pass
        {
            Name "VHS"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment VhsPass
            ENDHLSL
        }
    }
    Fallback Off
}
