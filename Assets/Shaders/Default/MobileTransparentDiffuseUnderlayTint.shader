// Simplified Diffuse shader. Differences from regular Diffuse one:
// - no Main Color
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.

Shader "KT/Transparent/Mobile Diffuse Underlay200 Tintable" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_Color ("Tint", Color) = (1,1,1,1)
}
SubShader {
	Tags {"Queue"="Transparent-200" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 150

CGPROGRAM
#pragma surface surf Lambert alpha

sampler2D _MainTex;
fixed4 _Color;

struct Input {
	float2 uv_MainTex;
};

void surf (Input IN, inout SurfaceOutput o) {
	fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
	o.Albedo = c.rgb * _Color.rgb;
	o.Alpha = c.a * _Color.a;
}
ENDCG
}

Fallback "Mobile/VertexLit"
}
