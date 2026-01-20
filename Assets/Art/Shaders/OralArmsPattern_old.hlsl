// OralArmsPattern.hlsl
#ifndef ORAL_ARMS_PATTERN_INCLUDED
#define ORAL_ARMS_PATTERN_INCLUDED

#include "MedusaBellFormula.hlsl"

// --- FBM 函數 (保持不變) ---
float triNoise3D_Impl_Oral(float3 p, float speed, float timez) {
    return snoise(p + float3(0, 0, timez * speed));
}

float fbm3_Oral(float3 p, float speed, float timez) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    for (int i = 0; i < 3; i++) {
        value += amplitude * triNoise3D_Impl_Oral(p * frequency, speed, timez);
        p *= 2.0;       
        amplitude *= 0.5; 
    }
    return value / 0.875;
}

// ★ 新增：計算 Noise 產生的高度 (用於生成法線)
float GetNoiseHeight(float2 uv, float timez) {
    float3 noisePos = float3(uv * 2.0, 1.34);
    // 回傳 0~1 的高度值
    return fbm3_Oral(noisePos, 0.0, timez) * 0.5 + 0.5; 
}

void OralArmsPattern_float(
    float2 UV,
    float TimeZ,
    float Charge,
    float3 ColorMain,   
    float3 ColorBone,   
    float3 ColorEmit,   
    float3 ViewDir,     
    float3 Normal,      
    
    out float3 OutBaseColor,
    out float3 OutEmission,
    out float OutAlpha,
    out float OutSmoothness, // Smoothness 輸出
    out float3 OutNormalDS   // ★ 新增：Detail Normal (Tangent Space)
)
{
    // 1. 基礎 Noise
    float noiseRaw = fbm3_Oral(float3(UV * 2.0, 1.34), 0.0, TimeZ);

    // 2. 顏色定義 (對比度加強)
    float3 white = ColorBone - noiseRaw * 0.2; 
    float3 orange = ColorMain - noiseRaw * 0.2;

    // 3. 花紋遮罩
    float a = (UV.x * 1.5) + (noiseRaw * 0.4);
    float limit = sin(UV.y * 100.0) * 0.03 + 0.3;
    float value = 1.0 - smoothstep(limit, 0.55, a);

    // 4. 混合顏色
    OutBaseColor = lerp(orange, white, value);

    // 5. 自發光 (Emission)
    float emissiveFactor = (1.0 - value); 
    emissiveFactor = pow(emissiveFactor, 2.0); 
    emissiveFactor += Charge * 0.5;
    OutEmission = ColorEmit * emissiveFactor;

    // 6. 透明度
    float alphaFade = smoothstep(0.05, 0.20, UV.y);
    float patternAlpha = lerp(0.9, 0.5, value); // 肉體實，骨架透
    OutAlpha = saturate(alphaFade * patternAlpha);

    // -----------------------------------------------------------
    // ★ 7. 關鍵優化：Procedural Normal & Smoothness
    // -----------------------------------------------------------

    // A. 計算法線擾動 (Normal Map)
    // 原理：取稍微微偏一點點的 UV，算出高度差，這就是斜率(法線)
    float epsilon = 0.01; 
    float h  = GetNoiseHeight(UV, TimeZ);
    float hx = GetNoiseHeight(UV + float2(epsilon, 0), TimeZ);
    float hy = GetNoiseHeight(UV + float2(0, epsilon), TimeZ);
    
    float dx = (h - hx) * 2.0; // 強度係數，越大越凹凸
    float dy = (h - hy) * 2.0;

    // 這是 Tangent Space Normal (藍紫色貼圖的概念)
    OutNormalDS = normalize(float3(dx, dy, 1.0)); 

    // B. Smoothness (光滑度)
    // 讓 "骨架(Value=1)" 非常光滑(濕潤)，"肉體(Value=0)" 稍微粗糙一點
    // 並且整體數值要拉高，才能形成銳利反光
    float wetness = lerp(0.75, 0.95, value); 
    
    // 把 Noise 也疊加進去，讓光滑度本身也不均勻 (重要細節!)
    OutSmoothness = wetness + noiseRaw * 0.05; 

    //OutMetallic = 0.0;
}
#endif