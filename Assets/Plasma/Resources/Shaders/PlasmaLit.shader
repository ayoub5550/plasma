// Opaque, GPU-instanced, mobile-cheap lit shader.
// Vertex colour = base colour; vertex alpha = how much the per-instance _Color tints it (1 = team colour, 0 = keep).
Shader "Plasma/Lit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Ambient ("Ambient", Color) = (0.42,0.44,0.52,1)
        _LightDir ("Light dir (world)", Vector) = (-0.35, 0.85, -0.4, 0)
        _Rim ("Rim strength", Range(0,1)) = 0.25
        _Flash ("Hit flash", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wpos : TEXCOORD1; fixed4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
            UNITY_INSTANCING_BUFFER_END(Props)
            fixed4 _Ambient; float4 _LightDir; float _Rim;

            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                o.col = fixed4(lerp(v.color.rgb, v.color.rgb * tint.rgb, v.color.a), 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.n);
                float3 l = normalize(_LightDir.xyz);
                float ndl = dot(n, l) * 0.5 + 0.5;             // half-lambert: soft toy look
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float rim = pow(1 - saturate(dot(n, v)), 3) * _Rim;
                float3 c = i.col.rgb * (_Ambient.rgb + ndl * 0.62) + rim;
                c = lerp(c, float3(1,1,1), UNITY_ACCESS_INSTANCED_PROP(Props, _Flash));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
