Shader "Custom/DeepSeaBall"
{
    Properties
    {
        [Header(Colors)]
        _TopColor ("Top Color (Surface)", Color) = (0.2, 0.6, 1, 1)
        _MidColor ("Mid Color (Deep)", Color) = (0.1, 0.3, 0.6, 1)
        _BotColor ("Bot Color (Abyss)", Color) = (0.0, 0.05, 0.2, 1)
        
        [Header(Settings)]
        _MidPoint ("Middle Point (Y Axis)", Range(-1, 1)) = 0.0
        _Contrast ("Gradient Smoothness", Range(0.1, 5)) = 1.0
    }
    SubShader
    {
        // ★ 修改 1: 改為不透明物體 (Opaque) 的設定
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // ★ 修改 2: 開啟深度寫入，剔除背面 (這是標準物體設定)
        ZWrite On
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL; // 如果需要光照可以加，這裡暫時不用
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            fixed4 _TopColor;
            fixed4 _MidColor;
            fixed4 _BotColor;
            float _MidPoint;
            float _Contrast;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // ★ 修改 3: 取得「正規化」的模型座標
                // normalize(v.vertex.xyz) 會把座標變成一個「從中心向外的方向向量」
                // 這樣不管球多大、多小，漸層都會完美貼合球面 (-1 到 1)
                o.localPos = normalize(v.vertex.xyz); 
                
                // 如果您希望漸層是「垂直線性」的 (像水位)，而不是球面的，可以用這行取代上面：
                // o.localPos = v.vertex.xyz * 2.0; // 假設球半徑是 0.5，乘 2 變 1
                
                return o;
            }

            // 輔助函式 (保持不變)
            float invLerp(float from, float to, float value) {
                return saturate((value - from) / (to - from));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 因為在 Vertex 階段已經 Normalize 過了，這裡的 y 直接就是 -1(底) 到 1(頂)
                float y = i.localPos.y;

                fixed4 col = float4(0,0,0,1);

                // 下面的混合邏輯完全不用動
                if (y < _MidPoint)
                {
                    float t = invLerp(-1.0, _MidPoint, y);
                    t = pow(t, _Contrast); 
                    col = lerp(_BotColor, _MidColor, t);
                }
                else
                {
                    float t = invLerp(_MidPoint, 1.0, y);
                    t = pow(t, _Contrast);
                    col = lerp(_MidColor, _TopColor, t);
                }

                return col;
            }
            ENDCG
        }
    }
}