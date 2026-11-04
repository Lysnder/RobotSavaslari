Shader "RobotSavaslari/ImpactParticles"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.color=v.color; o.uv=v.uv; return o; }
            half4 Frag(Varyings i):SV_Target { float alpha=saturate(1-length(i.uv*2-1)); return half4(i.color.rgb,i.color.a*alpha*alpha); }
            ENDHLSL
        }
    }
}
