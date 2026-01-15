Shader "Custom/BrushBlend"
{
    Properties
    {
        _BrushTex("Brush Texture", 2D) = "white" {}
        _MainTex("Canvas (input)", 2D) = "white" {}
        _Aspect("Aspect Ratio", Float) = 1.0
        _RegionX("Region X", Float) = 0.0
        _RegionY("Region Y", Float) = 0.0
        _RegionW("Region Width", Float) = 1.0
        _RegionH("Region Height", Float) = 1.0
    }

        SubShader
        {
            Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                sampler2D _BrushTex;
                float _Aspect;
                float _RegionX, _RegionY, _RegionW, _RegionH;
                int   _RegionSample;   // 0 = full-canvas source, 1 = cropped source

                #define MAX_STAMPS 128
                int     _StampCount;
                float4  _StampData[MAX_STAMPS];   // (center.x, center.y, size, unused)
                float4  _StampColors[MAX_STAMPS];

                struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

                v2f vert(appdata_full v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.uv = v.texcoord;
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    // Global UV of this pixel (where it sits on the full canvas)
                    float2 globalUV = float2(
                        _RegionX + i.uv.x * _RegionW,
                        _RegionY + i.uv.y * _RegionH
                    );

                // Choose sampling UV for the base canvas:
                // - full-canvas source: sample with GLOBAL uv
                // - cropped source: sample with LOCAL 0..1 i.uv
                float2 sampleUV = (_RegionSample != 0) ? i.uv : globalUV;

                fixed4 baseCol = tex2D(_MainTex, sampleUV);

                // Paint all queued stamps (positions are always in GLOBAL UVs)
                [loop]
                for (int j = 0; j < _StampCount; j++)
                {
                    float2 center = _StampData[j].xy;
                    float  size = _StampData[j].z;

                    float2 brushUV = (globalUV - center);
                    brushUV.x *= _Aspect;
                    brushUV = brushUV / size + 0.5;

                    if (brushUV.x < 0 || brushUV.x > 1 || brushUV.y < 0 || brushUV.y > 1)
                        continue;

                    fixed4 brush = tex2D(_BrushTex, brushUV);
                    fixed4 stamp = brush * _StampColors[j];
                    baseCol = lerp(baseCol, stamp, stamp.a);
                }

                return baseCol;
            }
            ENDCG
        }
        }
}
