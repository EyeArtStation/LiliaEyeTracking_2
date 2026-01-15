Shader "XD Paint/Paint World"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _IslandMap ("Island Texture", 2D) = "white" {}
        _BrushTex ("Brush Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _UsePlanarProjection ("Use Planar Projection", Float) = 1
        _UseNormalDirection ("Use Normal Direction", Float) = 1
        _NormalCutout ("Normal Cutout", Range(-1.0, 1.0)) = 0.0
        _MaxAngle ("Max Angle", Range(-180, 180.0)) = 90.0
        _AngleAttenuationMode ("Angle Attenuation Mode", Float) = 0.0
        _BrushDepth ("Brush Depth", Range(0.0, 1.0)) = 0.0
        _DepthFadeRange ("Depth Fade Range", Float) = 0.0
        _EdgeSoftness ("Edge Softness", Range(0.0, 10.0)) = 0.0
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane"}
        Cull Off
        Lighting Off 
        ZWrite Off
        ZTest Off
        Fog { Color (0, 0, 0, 0) }
        Blend Off

        Pass
        {
            Name "Unwrap [0]"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 verex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata i)
            {
                v2f o;
                o.vertex = float4(float2(1, _ProjectionParams.x) * (i.uv.xy * float2(2, 2) - float2(1, 1)), 0, 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.vertex;
            }
            ENDCG
        }
        Pass
        {
            Name "Draw [1]"
            CGPROGRAM
            #include "BlendingModes.cginc"
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest

            sampler2D _MainTex;
            sampler2D _BrushTex;
            float3 _PaintPositions[32];
            float3 _Normals[32];
            float _RotationAngles[32];
            int _PaintPositionsCount;
            float _BrushSizes[2];
            float3 _PointerPosition;
            float4 _Color;
            float _UsePlanarProjection;
            float _UseNormalDirection;
            float _NormalCutout;
            float _MaxAngle;
            float _AngleAttenuationMode;
            float _BrushDepth;
            float _DepthFadeRange;
            float _EdgeAttenuationScale;
            float _EdgeSoftness;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            v2f vert (appdata i)
            {
                v2f o;
                o.vertex = float4(float2(1, _ProjectionParams.x) * (i.uv.xy * float2(2, 2) - float2(1, 1)), 0, 1);
                o.worldPos = mul(unity_ObjectToWorld, i.vertex).xyz;
                o.uv = i.uv;
                o.normal = UnityObjectToWorldNormal(i.normal);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 color = 0;
                float3 direction = _PointerPosition.xyz - i.worldPos;
                float attenuation = dot(i.normal, normalize(direction));
                if (attenuation < _NormalCutout)
                    discard;

                attenuation = 1.0f;

                [unroll]
                for (int j = 0; j < 32; j++)
                {
                    if (j >= _PaintPositionsCount)
                        break;

                    float brushSize = _BrushSizes[0];
                    if (_PaintPositionsCount > 1)
                    {
                        float t = (float)j / (_PaintPositionsCount - 1);
                        brushSize += (_BrushSizes[1] - _BrushSizes[0]) * t;
                    }

                    float3 paintPosition = _PaintPositions[j];
                    float relativeDistance = distance(paintPosition, i.worldPos);
                    if (relativeDistance > brushSize)
                        continue;

                    float3 relativePosition = i.worldPos - paintPosition;
                    float3 brushNormal;
                    if (_UseNormalDirection > 0)
                    {
                        brushNormal = _Normals[j];
                    }
                    else
                    {
                        brushNormal = normalize(_PointerPosition - paintPosition);
                    }

                    float angleAttenuation = 1.0;
                    if (_MaxAngle > 0)
                    {
                        float angleCosine = dot(brushNormal, i.normal);
                        float angleThreshold = cos(radians(_MaxAngle));
                        angleAttenuation = smoothstep(angleThreshold, 1.0, angleCosine);
                        // angleAttenuation = saturate((angleCosine - angleThreshold) / (1.0 - angleThreshold));
                        if (angleAttenuation <= 0.0)
                            continue;

                        if (_AngleAttenuationMode > 0)
                        {
                            angleAttenuation = 1.0f;
                        }
                    }

                    float depthAttenuation = 1.0;
                    if (_BrushDepth > 0)
                    {
                        float depth = _BrushDepth * brushSize;
                        float depthDistance = dot(relativePosition, brushNormal);
                        float absDepthDistance = abs(depthDistance);
                        float innerDepth = max(0.0, depth - _DepthFadeRange);
                        depthAttenuation = 1.0 - smoothstep(innerDepth, depth, absDepthDistance);
                        if (depthAttenuation <= 0.0)
                            continue;
                    }

                    float4 brushColor = 0;
                    if (_UsePlanarProjection > 0)
                    {
                        // Select a world up vector that's not parallel to the brush normal
                        float3 worldUp = abs(brushNormal.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                        float3 tangent = normalize(cross(worldUp, brushNormal));
                        float3 bitangent = cross(brushNormal, tangent);
                        float2 uv = float2(dot(relativePosition, tangent), dot(relativePosition, bitangent))  / brushSize;
                        float rotationAngle = radians(_RotationAngles[j]);
                        float cosAngle = cos(rotationAngle);
                        float sinAngle = sin(rotationAngle);
                        float2x2 rotationMatrix = float2x2(cosAngle, -sinAngle, sinAngle, cosAngle);
                        uv = mul(rotationMatrix, uv);
                        if (uv.x * uv.x + uv.y * uv.y > 1.0)
                            continue;

                        float2 brushUV = uv * 0.5f + 0.5f;
                        if (brushUV.x < 0.0 || brushUV.x > 1.0 || brushUV.y < 0.0 || brushUV.y > 1.0)
                            continue;

                        brushColor = tex2D(_BrushTex, brushUV);
                    }
                    else
                    {
                        float _BlendingExponent = 100.0f;
                        float3 blendingWeights = pow(abs(i.normal), _BlendingExponent);
                        // blendingWeights *= angleAttenuation;
                        float sumWeights = blendingWeights.x + blendingWeights.y + blendingWeights.z;
                        if (sumWeights > 0.0)
                        {
                            blendingWeights /= sumWeights;
                        }
                        else
                        {
                            continue;
                        }
                        
                        float2 uvX = relativePosition.yz / brushSize;
                        float2 uvY = relativePosition.zx / brushSize;
                        float2 uvZ = relativePosition.xy / brushSize;
                        uvX = uvX * 0.5f + 0.5f;
                        uvY = uvY * 0.5f + 0.5f;
                        uvZ = uvZ * 0.5f + 0.5f;
                        float rotationAngle = radians(_RotationAngles[j]);
                        float cosAngle = cos(rotationAngle);
                        float sinAngle = sin(rotationAngle);
                        float2x2 rotationMatrix = float2x2(
                            cosAngle, -sinAngle,
                            sinAngle, cosAngle
                        );
                        uvX -= 0.5;
                        uvY -= 0.5;
                        uvZ -= 0.5;
                        uvX = mul(rotationMatrix, uvX);
                        uvY = mul(rotationMatrix, uvY);
                        uvZ = mul(rotationMatrix, uvZ);
                        uvX += 0.5;
                        uvY += 0.5;
                        uvZ += 0.5;
                        uvX = frac(uvX);
                        uvY = frac(uvY);
                        uvZ = frac(uvZ);
                        float4 brushColorX = tex2D(_BrushTex, uvX);
                        float4 brushColorY = tex2D(_BrushTex, uvY);
                        float4 brushColorZ = tex2D(_BrushTex, uvZ);
                        brushColor = brushColorX * blendingWeights.x + brushColorY * blendingWeights.y + brushColorZ * blendingWeights.z;
                    }

                    brushColor.a *= angleAttenuation * depthAttenuation;
                    if (_EdgeSoftness > 0)
                    {
                        float radialDistance = length(relativePosition) / brushSize;
                        float edgeAttenuation = exp(-radialDistance * radialDistance * _EdgeSoftness);
                        brushColor.a *= edgeAttenuation;
                    }

                    color = BlendColors(color, brushColor);
                }

                color.a *= attenuation;
                float4 textureColor = tex2D(_MainTex, i.uv);
                color = AlphaComposite(textureColor, textureColor.a, color, color.a);
                return color;
            }
            ENDCG
        }
        Pass
        {
            Name "Island Edges [2]"
            BlendOp Add
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            uniform	float4 _MainTex_TexelSize;
            sampler2D _IslandMap;

            v2f vert (appdata i)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(i.vertex);
                o.uv = i.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 color = tex2D(_MainTex, uv);
                float island = tex2D(_IslandMap, uv).r;
                if (island >= 1.0)
                {
                    return color;
                }
                
                float4 extendedColor = color * island;
                float totalWeight = island;
                float2 offsets[4] = { float2(-1, 0), float2(1, 0), float2(0, -1), float2(0, 1) };

                [unroll]
                for (int j = 0; j < 4; j++)
                {
                    float2 offsetUV = uv + offsets[j] * _MainTex_TexelSize.xy;
                    float neighborIsland = tex2D(_IslandMap, offsetUV).r;
                    if (neighborIsland > 0.0)
                    {
                        float4 neighborColor = tex2D(_MainTex, offsetUV) * neighborIsland;
                        extendedColor += neighborColor;
                        totalWeight += neighborIsland;
                    }
                }

                if (totalWeight > 0.0)
                {
                    color = extendedColor / totalWeight;
                    color.a = 1;
                }
                else
                {
                    color = 0;
                }

                return color;
            }
            ENDCG
        }
    }
}