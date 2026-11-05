// The glowing wall on the edge of a Play Anywhere course
// (PlayAnywhereCourseWall): a see-through veil with vertical beams of light
// that flicker. Seen from both sides. UV: x = metres along the edge, y = 0 at
// the ground to 1 at the top. The colour is HDR so that bloom catches it. While
// the player whose camera is drawn aims, it dithers away near that camera.
Shader "UntitledPool/PlayAnywhereCourseWall"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 0.12, 0.08, 1)
        _VeilAlpha ("Veil Alpha", Range(0, 1)) = 0.18
        _Falloff ("Veil Falloff", Range(0.2, 6)) = 1.6
        _BeamAlpha ("Beam Alpha", Range(0, 1)) = 0.65
        _BeamSpacing ("Beam Spacing (m)", Range(0.05, 3)) = 0.35
        _BeamSpeed ("Beam Flicker Speed", Range(0, 20)) = 3
        _BaseAlpha ("Ground Line Alpha", Range(0, 1)) = 0.7
        _DitherNear ("Aiming: Gone Within (m)", Range(0, 5)) = 0.8
        _DitherFar ("Aiming: Whole Beyond (m)", Range(0.1, 10)) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _VeilAlpha;
                float _Falloff;
                float _BeamAlpha;
                float _BeamSpacing;
                float _BeamSpeed;
                float _BaseAlpha;
                float _DitherNear;
                float _DitherFar;
            CBUFFER_END

            // Set per camera by PlayAnywhereCourseWall: 1 while its player aims.
            float _PlayAnywhereWallAiming;

            static const float Bayer[16] =
            {
                 0.5 / 16.0,  8.5 / 16.0,  2.5 / 16.0, 10.5 / 16.0,
                12.5 / 16.0,  4.5 / 16.0, 14.5 / 16.0,  6.5 / 16.0,
                 3.5 / 16.0, 11.5 / 16.0,  1.5 / 16.0,  9.5 / 16.0,
                15.5 / 16.0,  7.5 / 16.0, 13.5 / 16.0,  5.5 / 16.0
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                if (_PlayAnywhereWallAiming > 0.5)
                {
                    float kept = saturate((distance(input.positionWS, _WorldSpaceCameraPos) - _DitherNear) / max(0.01, _DitherFar - _DitherNear));
                    uint2 pixel = uint2(input.positionCS.xy) % 4u;
                    clip(kept - Bayer[pixel.y * 4u + pixel.x]);
                }

                float height = input.uv.y;

                // Each beam has its own width, reach and rhythm.
                float x = input.uv.x / _BeamSpacing;
                float cell = floor(x);
                float random = frac(sin(cell * 12.9898) * 43758.5453);
                float across = abs(frac(x) - 0.5) / lerp(0.15, 0.45, random);
                float beam = saturate(1.0 - across);
                beam *= beam;
                beam *= saturate(1.0 - height / lerp(0.55, 1.0, random));
                beam *= 0.6 + 0.4 * sin(_Time.y * _BeamSpeed * (0.5 + random) + random * 6.283);

                float below = 1.0 - height;
                float veil = pow(below, _Falloff) * _VeilAlpha;
                float groundLine = pow(below, 24.0) * _BaseAlpha;

                float alpha = saturate(veil + beam * _BeamAlpha + groundLine);
                return float4(_Color.rgb * (0.5 + alpha), alpha);
            }
            ENDHLSL
        }
    }
}
