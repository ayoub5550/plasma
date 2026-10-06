// Unlit, GPU-instanced effects (tracers, smoke, glows, shadows). Colour = instance _Color * vertex colour.
// Blend set by _SrcBlend/_DstBlend. HDR glow: colour *= 1 + _Glow * _PlasmaGlowBoost (global, set by
// QualityManager in High quality so tracers/sparks exceed 1.0 and feed the bloom; 0 in Low). _Soft (0..1) fades by view angle (spheres look like soft puffs).
Shader "Plasma/Fx"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.8,0.2,1)
        _Soft ("Soft edges", Range(0,1)) = 1
        _Glow ("Glow (HDR boost weight)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull [_Cull]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; half4 col : COLOR; };
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)
            float _Soft; float _Glow; float _PlasmaGlowBoost;
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 vd = normalize(WorldSpaceViewDir(v.vertex));
                float facing = saturate(dot(n, vd));
                fixed4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _Color) * v.color;
                float shade = lerp(1, 0.75 + 0.35 * facing, _Soft);
                float alpha = lerp(1, 0.25 + 0.75 * facing, _Soft);
                o.col = half4(c.rgb * shade * (1 + _Glow * _PlasmaGlowBoost), c.a * alpha);
                return o;
            }
            half4 frag (v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
    Fallback Off
}
