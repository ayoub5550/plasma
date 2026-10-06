// Unlit, GPU-instanced effects (bullets, smoke puffs, glows). _Color alpha = opacity. Blend set by _SrcBlend/_DstBlend.
Shader "Plasma/Fx"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.8,0.2,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 vd = normalize(WorldSpaceViewDir(v.vertex));
                float facing = saturate(dot(n, vd));
                fixed4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                o.col = fixed4(c.rgb * (0.75 + 0.35 * facing), c.a * (0.55 + 0.45 * facing));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
    Fallback Off
}
