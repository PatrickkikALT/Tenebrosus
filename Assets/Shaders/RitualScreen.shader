Shader "Tenebrosus/RitualScreen"
{
    Properties
    {
        _HazeScale("Haze Scale", Float) = 0.006
        _ShockWidth("Shock Width", Float) = 0.08
        _ShockScale("Shock Scale", Float) = 0.04
        _ShockAberration("Shock Aberration", Float) = 0.012
    }

    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _HazeScale;
    float _ShockWidth;
    float _ShockScale;
    float _ShockAberration;

    float _RitualHaze;
    float4 _RitualShock;
    float _RitualFlash;
    float4 _RitualFlashColor;
    float _RitualBurst;
    float _RitualSanityHit;

    float3 RitualFetch(float2 uv)
    {
        return CustomPassSampleCameraColor(clamp(uv, 0.001, 0.999), 0);
    }

    float4 RitualScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float time = _Time.y;
        float aspect = _ScreenSize.x / _ScreenSize.y;

        float2 haze = float2(
            sin(uv.y * 42.0 + time * 4.1) + sin(uv.y * 17.0 - time * 2.3 + uv.x * 9.0),
            cos(uv.x * 37.0 + time * 3.3) + sin(uv.x * 13.0 + time * 1.7 - uv.y * 7.0)) * 0.5;
        float edgeWeight = 0.4 + 0.6 * saturate(length(uv - 0.5) * 2.0);
        float2 offset = haze * _HazeScale * _RitualHaze * edgeWeight;

        float2 toCenter = (uv - _RitualShock.xy) * float2(aspect, 1.0);
        float distance01 = length(toCenter);
        float ring = 1.0 - saturate(abs(distance01 - _RitualShock.z) / _ShockWidth);
        ring = ring * ring * (3.0 - 2.0 * ring) * _RitualShock.w;
        float2 direction = distance01 > 1e-4 ? toCenter / distance01 : float2(0.0, 0.0);
        direction.x /= aspect;
        offset -= direction * ring * _ShockScale;

        float2 split = direction * ring * _ShockAberration + offset * 0.5;
        float2 sampleUv = uv + offset;
        float3 color;
        color.r = RitualFetch(sampleUv + split).r;
        color.g = RitualFetch(sampleUv).g;
        color.b = RitualFetch(sampleUv - split).b;

        float luma = dot(color, float3(0.299, 0.587, 0.114));
        float2 centered = uv - 0.5;
        centered.x *= aspect;
        float vignette = saturate(dot(centered, centered) * 1.6);
        float hit = _RitualSanityHit * (0.75 + 0.25 * sin(time * 30.0));
        color = lerp(color, float3(luma * 1.2, luma * 0.35, luma * 0.3), hit * 0.7);
        color *= 1.0 - vignette * hit * 0.9;

        color = color * (1.0 - _RitualFlash * 0.35) + _RitualFlashColor.rgb * _RitualFlash * (0.6 + luma);

        float burstFalloff = saturate(1.0 - length(centered) * 0.9);
        color += float3(1.0, 0.95, 0.85) * _RitualBurst * (0.6 + burstFalloff * 2.5);

        return float4(max(color, 0.0), 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "RitualScreen"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment RitualScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
