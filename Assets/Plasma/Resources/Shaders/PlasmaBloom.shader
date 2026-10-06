// Cheap mobile bloom (dual box filter, after Catlike Coding's "Bloom" tutorial). Used by PlasmaBloom.cs
// only in High quality. The camera renders HDR, so only HDR-boosted pixels (tracers, sparks, glows,
// specular hot spots) pass the threshold; white UI/world text (<= 1.0) does not bloom.
Shader "Hidden/Plasma/Bloom"
{
    Properties { _MainTex ("", 2D) = "white" {} _SourceTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _SourceTex;
    float4 _MainTex_TexelSize;
    half4 _Filter;      // x threshold, y x-knee, z 2*knee, w 0.25/(knee+eps)
    half _Intensity;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    half3 Box (float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    half3 Prefilter (half3 c)
    {
        half b = max(c.r, max(c.g, c.b));
        half soft = clamp(b - _Filter.y, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half contrib = max(soft, b - _Filter.x) / max(b, 0.00001);
        return c * contrib;
    }
    ENDCG
    SubShader
    {
        Cull Off ZTest Always ZWrite Off
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            half4 f (v2f i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            half4 f (v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            half4 f (v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            half4 f (v2f i) : SV_Target
            {
                half4 c = tex2D(_SourceTex, i.uv);
                c.rgb += _Intensity * Box(i.uv, 0.5);
                return c;
            }
            ENDCG }
    }
    Fallback Off
}
