Shader "Custom/UnderwaterEnvironment_Adjustable"
{
    Properties
    {
        [Header(Environment)]
        _CubeMap ("Environment Cube Map", Cube) = "" {}
        _NormalMap ("Wave Normal Map", 2D) = "bump" {}
        _RefractPower ("Refraction Power", Range(0, 1.0)) = 0.5
        _BumpScale ("Bump Scale", Range(0, 5)) = 2.0
        
        [Header(Surface Range)]
        _SurfaceLevel ("Surface Level (Height)", Range(-1.0, 1.0)) = 0.5
        _SurfaceSmooth ("Surface Smoothness", Range(0.01, 1.0)) = 0.1

        [Header(Flow Direction A)]
        _FlowSpeed1 ("Flow Speed A", Vector) = (0.05, 0.0, 0, 0)
        _NormalTiling1 ("Tiling A", Range(0.1, 50)) = 1.0

        [Header(Flow Direction B)]
        _FlowSpeed2 ("Flow Speed B", Vector) = (0.0, 0.05, 0, 0)
        _NormalTiling2 ("Tiling B", Range(0.1, 50)) = 1.2

        [Header(Sun)]
        _SunDir ("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Core Size", Range(500, 2000)) = 1500.0
        _SunHaloSize ("Halo Size", Range(5, 100)) = 25.0
        _SunHaloIntensity ("Halo Intensity", Range(0, 5)) = 1.0

        [Header(Sea Colors)]
        _TopColor ("Top Color", Color) = (0.1, 0.4, 0.6, 1)
        _MidColor ("Mid Color", Color) = (0.0, 0.1, 0.3, 1)
        _BottomColor ("Bottom Color", Color) = (0.0, 0.0, 0.1, 1)
        _MidPoint ("Mid Point", Range(-1, 1)) = 0.0
        _Contrast ("Contrast", Range(0.1, 10)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Cull Front ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _CubeMap; sampler2D _NormalMap;
            float4 _SunDir, _SunColor, _TopColor, _MidColor, _BottomColor, _FlowSpeed1, _FlowSpeed2;
            float _RefractPower, _BumpScale, _MidPoint, _Contrast, _NormalTiling1, _NormalTiling2;
            float _SunSize, _SunHaloSize, _SunHaloIntensity, _SurfaceLevel, _SurfaceSmooth;

            struct v2f { float4 pos : SV_POSITION; float3 viewDir : TEXCOORD0; float3 worldPos : TEXCOORD1; };

            v2f vert (appdata_base v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.viewDir = v.vertex.xyz; o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float3 viewDir = normalize(i.viewDir);
                
                // ★ 修正：使用自定義變數控制水面高度 ★
                float surfaceMask = smoothstep(_SurfaceLevel - _SurfaceSmooth, _SurfaceLevel, viewDir.y);

                // 雙向交叉流動運算
                float2 uv1 = i.worldPos.xz * _NormalTiling1 + _Time.y * _FlowSpeed1.xy;
                float2 uv2 = i.worldPos.xz * _NormalTiling2 + _Time.y * _FlowSpeed2.xy;
                float3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                float3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                float3 worldNormal = normalize(n1 + n2);
                worldNormal.xy *= _BumpScale;

                // 物理折射
                float3 refractedDir = normalize(refract(viewDir, normalize(worldNormal), 0.75));
                float3 finalDir = lerp(viewDir, refractedDir, _RefractPower * surfaceMask);

                // 漸層混色 (承接 Shader Graph 邏輯)
                float t1 = smoothstep(-1.0, _MidPoint, viewDir.y);
                float3 g1 = lerp(_BottomColor.rgb, _MidColor.rgb, pow(max(0, t1), _Contrast));
                float t2 = smoothstep(_MidPoint, 1.0, viewDir.y);
                float3 grad = lerp(g1, _TopColor.rgb, pow(max(0, t2), _Contrast));

                // 太陽與光暈
                float sunDot = max(0, dot(finalDir, normalize(_SunDir.xyz)));
                float3 sun = (pow(sunDot, _SunSize) * 12.0 + pow(sunDot, _SunHaloSize) * _SunHaloIntensity) * _SunColor.rgb * surfaceMask;

                fixed4 env = texCUBE(_CubeMap, finalDir);
                // 最終顏色合成
                float3 finalColor = lerp(grad, (env.rgb * grad) + sun, surfaceMask);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}