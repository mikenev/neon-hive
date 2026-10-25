// Neon Hive: unlit additive glow for white line art (ring meshes and sprites). Colour = texture * _Color (HDR) * vertex colour
// (if _UseVC), faded out with camera distance between _FadeStart and _FadeEnd. Bloom does the rest.
Shader "NeonHive/Additive" {
  Properties {
    _MainTex ("Texture", 2D) = "white" {}
    [HDR] _Color ("Color", Color) = (1,1,1,1)
    _UseVC ("Use vertex colour", Float) = 1
    _FadeStart ("Fade start", Float) = 1000
    _FadeEnd ("Fade end", Float) = 2000
  }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Blend One One
      ZWrite Off
      Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; float dist : TEXCOORD1; };
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST; float4 _Color; float _UseVC; float _FadeStart; float _FadeEnd;
      CBUFFER_END
      V vert (A i) {
        V o; float3 w = TransformObjectToWorld(i.pos.xyz);
        o.pos = TransformWorldToHClip(w); o.uv = TRANSFORM_TEX(i.uv, _MainTex);
        o.col = lerp(float4(1,1,1,1), i.col, _UseVC); o.dist = distance(w, _WorldSpaceCameraPos);
        return o;
      }
      half4 frag (V i) : SV_Target {
        half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
        float f = saturate(1 - (i.dist - _FadeStart) / max(0.001, _FadeEnd - _FadeStart));
        half4 c = t * _Color * i.col;
        return half4(c.rgb * c.a * f, 0);
      }
      ENDHLSL
    }
  }
}
