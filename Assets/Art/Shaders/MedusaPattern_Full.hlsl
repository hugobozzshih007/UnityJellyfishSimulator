// MedusaPattern_Full.hlsl
#ifndef MEDUSA_BELL_PATTERN_FULL_INCLUDED
#define MEDUSA_BELL_PATTERN_FULL_INCLUDED

#include "MedusaBellFormula.hlsl"

float glsl_mod(float x, float y) {
    return x - y * floor(x / y);
}

// 基礎單層 Noise
float triNoise3D_Impl(float3 p, float speed, float timez) {
    return snoise(p + float3(0, 0, timez * speed));
}

// ★ 新增：FBM (Fractal Brownian Motion) - 模擬 Three.js 的 triNoise3D
// 這會疊加 3 層雜訊，同時提供"形狀"與"細節"
float fbm3(float3 p, float speed, float timez) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    // 疊加 3 層 (Octaves)
    for (int i = 0; i < 3; i++) {
        value += amplitude * triNoise3D_Impl(p * frequency, speed, timez);
        p *= 2.0;       // 頻率加倍 (Lacunarity)
        amplitude *= 0.5; // 振幅減半 (Gain)
    }
    
    // Remap 到 [-1, 1] 區間 (因為疊加後數值會變大)
    return value / 0.875; // 0.5 + 0.25 + 0.125 = 0.875
}

void MedusaPatternFull_float(
    float2 MeshUV, 
    float TimeZ,
    float Phase,
    float3 PositionLocal,
    float3 DarkStripColor,
    float3 EmitColor, 
    float3 BellColor,
    out float3 OutBaseColor,
    out float3 OutEmission,
    out float OutMetallic,
    out float OutAlpha
)
{
    // --- 1. UV 還原 ---
    float2 vUv = MeshUV * 0.8;
    float d = length(vUv); 
    float azimuth = atan2(vUv.x, vUv.y);

    // --- 2. Main Noise 計算 (使用 FBM) ---
    
    // ★ 關鍵修正：使用 FBM3 來模擬 triNoise3D
    // 輸入頻率設為 6.0 (對應原作 JS 的註解 vUv.mul(6))
    // 這樣 FBM 內部會自動產生 6.0, 12.0, 24.0 的細節
    float3 noisePos = float3(vUv * 6.0, 1.34); 
    
    float rawNoise = fbm3(noisePos, 0.5, TimeZ);

    // Contrast & Remap
    // FBM 輸出通常比較集中在 0 附近，我們稍微增強對比
    float noiseRemapped = rawNoise * 0.5 + 0.5;
    float noise = noiseRemapped * 3.0 - 1.0; 

    // --- 3. 線條邏輯 ---
    float azVal = (azimuth / 3.14159265359) * 4.0;
    float a = glsl_mod(azVal, 0.5) - 0.25;

    // 因為用了 FBM，現在 noise 裡同時有"大波浪"和"小雜點"
    // 線條會扭曲得很自然，而且邊緣會有毛躁感，不會斷裂
    float lineDistortion = noise * 0.1 * (1.0 - smoothstep(0.23, 0.25, abs(a)));
    a += lineDistortion;

    // Fades
    float fade0 = smoothstep(0.2, 0.25, d);
    a *= fade0;
    float fade1 = 1.0 - smoothstep(0.6, 0.85, d);
    a *= fade1;

    // Masks - 保持我們加粗的設定
    float lineShape = smoothstep(0.04, 0.12, abs(a));
    float lineRed = smoothstep(0.0, 0.04, abs(a));

    // Circles
    float dNoisy = d + noise * 0.03;
    float fade2 = smoothstep(0.80, 0.96, dNoisy);
    float fade2red = smoothstep(0.65, 0.85, dNoisy);
    float innerCircle = 1.0 - smoothstep(0.15, 0.2, d);

    // Combine
    float linePattern = max(max(lineShape, fade2), innerCircle);
    float linePattern2 = max(max(lineRed, fade2red), innerCircle);

    float resX = linePattern; 
    float resY = linePattern2;

    // Seam Circles Fade
    float circlesFade = 1.0 - smoothstep(0.90, 1.0, d + noise * 0.05);
    resX = min(resX, circlesFade);

    // --- 4. 斑點邏輯 ---
    // 斑點也可以用 FBM，或者維持單層高頻
    float3 specklePos = float3(vUv * 12.0, 12.34);
    float specklesNoiseRaw = triNoise3D_Impl(specklePos, 0.0, 0.0);
    float specklesNoise = smoothstep(0.0, 0.3, specklesNoiseRaw * 0.5 + 0.5);
    float specklesFade = smoothstep(0.7, 0.9, d);
    float specklesFade2 = 1.0 - smoothstep(0.0, 0.2, d);
    
    float speckles = max(max(specklesNoise, specklesFade), specklesFade2);
    resX = min(resX, speckles);

    // --- 5. 顏色計算 ---
    // 使用 FBM Noise 來做顏色，細節會非常豐富
    float colorNoise = noise * 0.2; 
    
    float3 colWhite = BellColor - colorNoise;
    float3 colOrange = DarkStripColor - colorNoise;
    float3 colRed = EmitColor - colorNoise;

    OutMetallic = 1.0 - resX; 

    float3 color = lerp(colOrange, colWhite, resX);
    color = lerp(colRed, color, resY);
    OutBaseColor = color;

    // --- 6. 發光計算 ---
    float pulse = pow(sin(Phase*1.5 + PositionLocal.y) * 0.5 + 0.6, 10.0) * 2.0;
    
    float3 emissive = colRed * (1.0 - resY) * pulse;
    emissive += colRed * resY * 0.105;

    // Inner Glow (原作 JS 120行: emissiveness.addAssign(orange * vEmissive))
    // 雖然我們沒有 vEmissive，但可以簡單模擬一下邊緣光
    // (這行是選用的，如果不想太亮可以拿掉)
    // emissive += colOrange * 0.2; 

    OutEmission = emissive;

    // ★ Alpha 修正 ★
    // resX: 0 = 花紋(實心), 1 = 背景(透明)
    // 
    // 花紋 (0.0) -> Alpha 0.95 (幾乎不透明，展現紮實的肉質感)
    // 背景 (1.0) -> Alpha 0.25 (非常透明，展現果凍感)
    //
    OutAlpha = lerp(0.95, 0.25, resX);
}

#endif