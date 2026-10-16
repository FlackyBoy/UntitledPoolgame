// Ghost of a prefab in the Scene view (Level Maker, PrefabGhost.cs): a
// translucent tint with simple shading from a fixed light and a rim, so the
// shape reads without the real materials. Drawn immediately
// (Graphics.DrawMeshNow), so it works whatever the render pipeline.
// _ZTest: 4 = LessEqual (the visible part), 5 = Greater (behind walls).
Shader "Hidden/UntitledPool/PrefabGhost"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 1, 1, 0.4)
        _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float light = 0.55 + 0.45 * saturate(dot(n, normalize(float3(0.35, 1.0, 0.25))));
                float rim = pow(1.0 - saturate(dot(n, normalize(i.view))), 2.0);
                return fixed4(_Color.rgb * light + rim * 0.35, saturate(_Color.a + rim * 0.3));
            }
            ENDCG
        }
    }
}
