Shader "Hidden/PaintSmudge"
{
    Properties
    {
        _MainTex("Canvas", 2D) = "white" {}
        _BrushTex("Brush", 2D) = "white" {}

        _SmudgeStrength("Smudge Strength", Float) = 1
        _SmudgePull("Smudge Pull", Float) = 0.35
        _SmudgeSoftness("Smudge Softness", Float) = 2

        _Aspect("Aspect", Float) = 1
        _StampCount("Stamp Count", Int) = 0

        _RegionX("RegionX", Float) = 0
        _RegionY("RegionY", Float) = 0
        _RegionW("RegionW", Float) = 1
        _RegionH("RegionH", Float) = 1
        _RegionSample("RegionSample", Int) = 0
    }

        SubShader
        {
            Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
            Cull Off
            ZWrite Off
            ZTest Always

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                sampler2D _BrushTex;

                float _SmudgeStrength;
                float _SmudgePull;
                float _SmudgeSoftness;

                float _Aspect;
                int _StampCount;

                float _RegionX, _RegionY, _RegionW, _RegionH;
                int _RegionSample;

                float4 _StampData[128];   // uvx, uvy, size, rot(unused)
                float4 _StampData2[128];  // dirx, diry, strength, unused

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
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

                // Explicit LOD sampling to avoid gradients/derivatives in loops
                inline fixed4 SampleMainLOD(float2 uv)
                {
                    return tex2Dlod(_MainTex, float4(uv, 0, 0));
                }

                inline fixed4 SampleBrushLOD(float2 uv)
                {
                    return tex2Dlod(_BrushTex, float4(uv, 0, 0));
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    float2 uv = i.uv;

                    // Remap when rendering into a dirty-region temp RT
                    if (_RegionSample == 1)
                    {
                        uv = float2(_RegionX + uv.x * _RegionW,
                                    _RegionY + uv.y * _RegionH);
                    }

                    fixed4 col = SampleMainLOD(uv);

                    // Constant loop count (kills the "varying iteration" warning)
                    [loop]
                    for (int s = 0; s < 128; s++)
                    {
                        if (s >= _StampCount) break;

                        float2 suv = _StampData[s].xy;
                        float  size = _StampData[s].z;

                        float2 duv = uv - suv;

                        // aspect-correct ONLY for falloff distance
                        float2 dpx = float2(duv.x * _Aspect, duv.y);
                        float  r = length(dpx);

                        float t = saturate(r / max(1e-6, size));
                        float falloff = pow(1.0 - t, _SmudgeSoftness);
                        //float falloff = saturate(1.0 - (r / max(1e-6, size)));
                        if (falloff <= 0.0) continue;

                        float2 brushUV = (duv / max(1e-6, size)) * 0.5 + 0.5;
                        if (brushUV.x < 0.0 || brushUV.x > 1.0 || brushUV.y < 0.0 || brushUV.y > 1.0)
                            continue;

                        fixed4 b = SampleBrushLOD(brushUV);

                        // Support brushes that encode opacity in RGB (alpha may be missing)
                        float a = b.a;
                        a = max(a, dot(b.rgb, float3(0.3333, 0.3333, 0.3333)));
                        if (a <= 0.0001) continue;

                        float2 dir = _StampData2[s].xy;
                        float  str = _StampData2[s].z;

                        // normalize direction in pixel-space for consistent feel
                        float2 dirPx = float2(dir.x * _Aspect, dir.y);
                        float  dirLen = max(1e-6, length(dirPx));
                        dirPx /= dirLen;

                        // back to UV space
                        float2 dirUv = float2(dirPx.x / _Aspect, dirPx.y);

                        float2 pullUV = uv - dirUv * (size * _SmudgePull);

                        fixed4 pulled = SampleMainLOD(pullUV);

                        float w = saturate(a * falloff) * saturate(str) * _SmudgeStrength;

                        col = lerp(col, pulled, w);
                    }

                    return col;
                }
                ENDCG
            }
        }
}
