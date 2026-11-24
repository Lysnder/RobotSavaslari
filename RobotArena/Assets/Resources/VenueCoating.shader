Shader "RobotSavaslari/VenueCoating"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 0.12)
        _Grazing ("Grazing reflection", Range(0,1)) = 0
        _Falloff ("Light falloff", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 viewWS : TEXCOORD1; half3 normalWS : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Grazing, _Falloff;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half grazing = 1 - saturate(abs(dot(normalize(i.viewWS), normalize(i.normalWS))));
                half alpha = _Tint.a + _Grazing * grazing * grazing;
                half fade = 1 - saturate(i.uv.y);
                return half4(_Tint.rgb, alpha * lerp(1, fade * fade, _Falloff));
            }
            ENDHLSL
        }
    }
}
