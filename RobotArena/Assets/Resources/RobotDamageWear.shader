Shader "RobotSavaslari/DamageWear"
{
    Properties
    {
        _Wear ("Wear", Range(0,1)) = 0
        _Hit0 ("Hit 0", Vector) = (0,0,0,0)
        _Hit1 ("Hit 1", Vector) = (0,0,0,0)
        _Hit2 ("Hit 2", Vector) = (0,0,0,0)
        _Hit3 ("Hit 3", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="TransparentCutout" }
        Pass
        {
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Wear;
                float4 _Hit0, _Hit1, _Hit2, _Hit3;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xyz;
                return o;
            }
            float Hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float Mark(float3 p, float4 hit) { return saturate(1-length(p-hit.xyz)/0.42)*hit.w; }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionOS;
                float marks = max(max(Mark(p,_Hit0),Mark(p,_Hit1)),max(Mark(p,_Hit2),Mark(p,_Hit3)));
                float wear = saturate(_Wear * 0.35 + marks * 0.8);
                float noise = Hash(floor(p * 95));
                float scratch = step(abs(frac(dot(p,float3(23,7,41)) + Hash(floor(p*12))*0.18)-0.5), 0.028);
                float chipped = step(noise, wear * 0.26) * step(0.5, Hash(floor(p*28)));
                float scorch = step(noise, marks * _Wear * 0.45);
                clip(max(scratch * step(noise, wear), max(chipped, scorch)) - 0.5);
                float3 metal = lerp(float3(0.28,0.30,0.32),float3(0.55,0.57,0.58),scratch);
                return half4(lerp(metal,float3(0.035,0.025,0.018),scorch * 0.9),1);
            }
            ENDHLSL
        }
    }
}
