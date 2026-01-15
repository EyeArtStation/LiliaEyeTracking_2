Shader "Custom/BrushBlend_Fragment"
{
    Properties
    {
        _MainTex("Canvas", 2D) = "white" {}
        _BrushTex("Brush",  2D) = "white" {}
    }

        SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushTex;

            // Uploaded by BasePaintCustom.Flush()
            int    _StampCount;
            float4 _StampData[128];   // (uv.x, uv.y, size, rotationRad)
            float4 _StampColors[128]; // RGBA (as Vector4)

            float  _Aspect;           // canvas aspect (width/height)

            // Dirty-region support (set by DoFastSingleDirtyBlit / DoSafePingPongDirtyBlit)
            float _RegionX;
            float _RegionY;
            float _RegionW;
            float _RegionH;
            int   _RegionSample; // 0 = sample base from full canvas using globalUV
                                // 1 = sample base from cropped temp using local UV

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // UV within the *current blit target* (full RT or small dirty-rect RT)
                float2 localUV = i.uv;

                // Convert localUV -> global canvas UV (because stamps are queued in global UV space)
                float2 globalUV = float2(
                    _RegionX + localUV.x * _RegionW,
                    _RegionY + localUV.y * _RegionH
                );

                // Base color sample:
                // - RegionSample=0: base is the full canvas, so sample using globalUV
                // - RegionSample=1: base is already cropped tempA, so sample using localUV
                float4 baseCol = (_RegionSample == 0)
                    ? tex2D(_MainTex, globalUV)
                    : tex2D(_MainTex, localUV);

                // Stamp over base
                [loop]
                for (int j = 0; j < _StampCount; j++)
                {
                    float4 s = _StampData[j];      // xy center, z size (radius in UV), w rotationRad
                    float4 c = _StampColors[j];    // RGBA

                    // delta from stamp center in GLOBAL UV space
                    float2 d = globalUV - s.xy;

                    // match your compute: aspect correct BEFORE rotation
                    d.x *= _Aspect;

                    // rotate around stamp center
                    float sn, cs;
                    sincos(s.w, sn, cs);
                    d = float2(d.x * cs - d.y * sn,
                               d.x * sn + d.y * cs);

                    // map to brush UV
                    float2 brushUV = d / s.z + 0.5;

                    // outside brush quad -> skip
                    if (brushUV.x < 0.0 || brushUV.x > 1.0 || brushUV.y < 0.0 || brushUV.y > 1.0)
                        continue;

                    float4 brush = tex2D(_BrushTex, brushUV);
                    brush = saturate(brush);

                    // Apply tint
                    float4 stamp = brush * c;
                    stamp.a = saturate(stamp.a);

                    // Premultiply
                    stamp.rgb *= stamp.a;

                    // Alpha-over (premultiplied)
                    baseCol.rgb = baseCol.rgb * (1.0 - stamp.a) + stamp.rgb;
                    baseCol.a = baseCol.a * (1.0 - stamp.a) + stamp.a;
                }

                return baseCol;
            }
            ENDHLSL
        }
    }
}
