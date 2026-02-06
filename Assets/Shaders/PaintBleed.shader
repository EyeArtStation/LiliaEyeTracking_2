Shader "Custom/PaintBatch_WetBleed_Interesting_FAST"
{
    Properties
    {
        _MainTex("Canvas", 2D) = "black" {}
        _BrushTex("Brush", 2D) = "white" {}

        _PaperTex("Paper (Grayscale)", 2D) = "gray" {}
        _NoiseTex("Noise (Grayscale)", 2D) = "gray" {}

        _PaperScale("Paper Scale", Float) = 3.0
        _PaperStrength("Paper Strength", Range(0,1)) = 0.65

        _NoiseScale("Noise Scale", Float) = 6.0

        _BleedStrength("Bleed Strength", Range(0,2)) = 0.9
        _BleedPull("Bleed Pull (in radii)", Range(0,1)) = 0.25
        _BleedEdgeStart("Bleed Edge Start", Range(0,1)) = 0.55
        _BleedSoftness("Bleed Softness", Range(0.5,8)) = 2.5

        _EdgePool("Edge Pooling", Range(0,2)) = 0.9
        _EdgePoolPower("Edge Pool Power", Range(0.5,8)) = 2.3

        _Granulation("Granulation", Range(0,1)) = 0.45
        _GranPower("Gran Power", Range(0.5,10)) = 3.5

        _Irregularity("Edge Irregularity", Range(0,1)) = 0.55

        _MaxStamps("Max Stamps Per Pass", Range(1,32)) = 16
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            Pass
            {
                CGPROGRAM
                #pragma target 3.0
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex, _BrushTex, _PaperTex, _NoiseTex;

        // Unity auto-provides: x=1/width, y=1/height, z=width, w=height
        float4 _BrushTex_TexelSize;

        float _PaperScale, _PaperStrength;
        float _NoiseScale;

        float _BleedStrength, _BleedPull, _BleedEdgeStart, _BleedSoftness;
        float _EdgePool, _EdgePoolPower;
        float _Granulation, _GranPower;
        float _Irregularity;
        float _MaxStamps;

        // from C#: target.width / target.height
        float _Aspect;

        int _StampCount;
        float4 _StampData[128];    // x=uvx y=uvy z=size w=rotRad
        float4 _StampColors[128];  // rgba

        float _RegionX, _RegionY, _RegionW, _RegionH;
        int _RegionSample;

        struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
        struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }

        float2 ResolveCanvasUV(float2 uv)
        {
            if (_RegionW < 0.999 || _RegionH < 0.999)
            {
                if (_RegionSample == 1) return uv; // already cropped temp
                return float2(_RegionX + uv.x * _RegionW, _RegionY + uv.y * _RegionH);
            }
            return uv;
        }

        // Returns:
        //  - suv: brush UV (0..1)
        //  - p: centered brush-space coordinates (-0.5..0.5) that are aspect-corrected
        void StampUVAndP(float2 uvCanvas, float2 center, float sizeWidthNorm, float rotRad,
                         out float2 suv, out float2 p)
        {
            // sizeWidthNorm is defined relative to canvas WIDTH (matches your C#)
            // Convert canvas UV delta into "width-normalized" metric space:
            float2 d = uvCanvas - center;
            float2 m = float2(d.x, d.y / max(1e-6, _Aspect)); // ✅ this is the critical fix

            // Rotate in metric space
            float s = sin(rotRad);
            float c = cos(rotRad);
            float2 mr = float2(m.x * c - m.y * s, m.x * s + m.y * c);

            // Preserve brush texture aspect (bw/bh):
            float brushAspect = _BrushTex_TexelSize.z / max(1.0, _BrushTex_TexelSize.w);
            // Wide brush (aspect>1) should appear wide => compress metric X
            mr.x /= max(1e-6, brushAspect);

            // Map metric delta to brush UV
            float2 q = mr / max(1e-6, sizeWidthNorm); // q is roughly [-1..1] across stamp
            p = q * 0.5;                               // -0.5..0.5
            suv = p + 0.5;                             // 0..1
        }

        fixed4 frag(v2f i) : SV_Target
        {
            float2 uvCanvas = ResolveCanvasUV(i.uv);
            float4 baseCol = tex2D(_MainTex, uvCanvas);

            // paper/noise sampled once per pixel
            float paper = tex2D(_PaperTex, uvCanvas * _PaperScale).r;
            float noise = tex2D(_NoiseTex, uvCanvas * _NoiseScale).r;

            float tooth = lerp(1.0, paper, _PaperStrength);
            float gran = pow(saturate(paper), _GranPower);
            float granMix = lerp(1.0, gran, _Granulation);

            float4 outCol = baseCol;

            int cap = (int)clamp(_MaxStamps, 1.0, 32.0);
            cap = min(cap, _StampCount);

            [unroll] for (int s = 0; s < 32; s++)
            {
                if (s >= cap) break;

                float2 c = _StampData[s].xy;
                float  sz = _StampData[s].z;
                float  rot = _StampData[s].w;

                float2 suv, p;
                StampUVAndP(uvCanvas, c, sz, rot, suv, p);

                // outside stamp
                if (suv.x < 0 || suv.x > 1 || suv.y < 0 || suv.y > 1) continue;

                // ✅ alpha mask (PNG brushes)
                float4 bt = tex2D(_BrushTex, suv);
                float bmask = max(bt.a, max(bt.r, max(bt.g, bt.b)));
                if (bmask <= 0.0001) continue;

                float r = length(p); // aspect-corrected now

                float wobble = (noise - 0.5) * 2.0 * _Irregularity * 0.10;
                float edgeStart = saturate(_BleedEdgeStart + wobble);

                float edge = smoothstep(edgeStart, 0.5, r);
                edge = pow(saturate(edge), _BleedSoftness);

                float4 ink = _StampColors[s];
                float glazeAmt = edge * bmask * _BleedStrength * tooth * granMix * ink.a;
                if (glazeAmt <= 0.00001) continue;

                // Pull from slightly inward (still 1 extra tap)
                float2 toCenter = normalize(-p + 1e-6);

                // p is in brush-space; convert pull back to canvas UV:
                // size is width-normalized => pullUV in canvasUV:
                float2 pullMetric = toCenter * (_BleedPull * sz);          // width-normalized metric
                float2 pullUV = float2(pullMetric.x, pullMetric.y * _Aspect); // invert the earlier dy/_Aspect
                float3 inner = tex2D(_MainTex, uvCanvas + pullUV).rgb;

                // Pooling ring
                float pool = pow(saturate(r / 0.5), _EdgePoolPower) * _EdgePool * bmask;
                float poolAmt = saturate(pool * 0.35) * ink.a;

                outCol.rgb = lerp(outCol.rgb, inner, glazeAmt * 0.6);

                float3 pooled = outCol.rgb * (1.0 - poolAmt) + ink.rgb * poolAmt;

                outCol.rgb = 1.0 - (1.0 - pooled) * (1.0 - ink.rgb * glazeAmt);
                outCol.a = saturate(outCol.a + glazeAmt * 0.25);
            }

            return outCol;
        }
        ENDCG
    }
        }
}
