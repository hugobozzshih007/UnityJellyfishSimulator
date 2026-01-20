#ifndef ORAL_ARMS_PATTERN_INCLUDED
#define ORAL_ARMS_PATTERN_INCLUDED

#include "MedusaBellFormula.hlsl"

// -----------------------------------------------------------------------------
// 1. 輔助雜訊函數 (Noise Helpers)
// -----------------------------------------------------------------------------
float triNoise3D_Impl_Oral(float3 p, float speed, float timez) {
    return snoise(p + float3(0, 0, timez * speed));
}

float fbm3_Oral(float3 p, float speed, float timez) {
    float value = 0.0;
    float amplitude = 0.5;
    // 降低頻率：解決摩爾紋/橫紋雜訊的關鍵
    float frequency = 0.8; 
    
    for (int i = 0; i < 3; i++) {
        value += amplitude * triNoise3D_Impl_Oral(p * frequency, speed, timez);
        p *= 2.0;       
        amplitude *= 0.5; 
    }
    return value / 0.875;
}

// -----------------------------------------------------------------------------
// 2. 頂點位移函數 (Vertex Displacement) - 讓邊緣產生荷葉邊
// -----------------------------------------------------------------------------
// 請在 Shader Graph 的 Vertex Stage 使用此函數
void OralArmsDisplacement_float(
    float3 PositionOS,    // Object Space Position
    float3 NormalOS,      // Object Space Normal
    float3 TangentOS,
    float2 UV,            // UV0
    float Frequency,      // 建議 15 ~ 20 (波浪密度)
    float Amplitude,      // 建議 0.1 ~ 0.3 (波浪幅度)
    
    out float3 OutPosition, // 輸出新的頂點位置
    out float3 OutNormal
)
{
    // --- 1. 準備數據 ---
    float3 bitangentOS = normalize(cross(NormalOS, TangentOS));
    
    // --- 2. 波浪數學 (Static Wave) ---
    
    // A. 邊緣遮罩 (只讓邊緣捲曲)
    float xCentered = (UV.x - 0.5) * 2.0; 
    float edgeMask = pow(abs(xCentered), 3.0);
    float dMask_dx = 3.0 * pow(xCentered, 2.0) * 2.0 * sign(xCentered);

    // B. 正弦波 (純空間函數，不隨時間變化)
    // 這樣摺痕就會「固定」在觸手上，隨觸手擺動而擺動
    float phase = UV.y * Frequency; 
    float sinWave = sin(phase);
    float cosWave = cos(phase); 
    
    // C. 位移量
    float disp = sinWave * edgeMask * Amplitude;

    // --- 3. 計算新位置 ---
    OutPosition = PositionOS + NormalOS * disp;

    // --- 4. 計算新法線 ---
    float slopeX = sinWave * dMask_dx * Amplitude;
    float slopeY = cosWave * edgeMask * Amplitude * Frequency;

    float3 newBitangent = normalize(bitangentOS + NormalOS * slopeX);
    float3 newTangent   = normalize(TangentOS   + NormalOS * slopeY);

    OutNormal = normalize(cross(newTangent, newBitangent));
}

// -----------------------------------------------------------------------------
// 3. 表面材質函數 (Fragment Pattern) - 海刺水母質感
// -----------------------------------------------------------------------------
void OralArmsPattern_float(
    float2 UV,
    float TimeZ,
    float Charge,
    float3 ColorMain,   // 建議輸入：淺粉白色 (Hex: #FFEFEF)
    float3 ColorBone,   // 建議輸入：深赭色/橘褐色 (Hex: #8B4513)
    float3 ColorEmit,   // 發光色
    float3 ViewDir,     
    float3 Normal,      
    
    out float3 OutBaseColor,
    out float3 OutEmission,
    out float OutAlpha,
    out float OutSmoothness, 
    out float3 OutNormalDS  
)
{
    // A. 雜訊計算
    // 降低 Y 軸縮放 (1.34 -> 0.5) 讓紋理看起來更像垂直生長的纖維
    float noiseRaw = fbm3_Oral(float3(UV * 2.0, 0.5), 0.2, TimeZ);

    // B. 幾何遮罩 (Geometry Mask)
    // centerDist: 0(中心) -> 1(邊緣)
    float centerDist = abs(UV.x - 0.5) * 2.0;
    
    // C. 顏色混合 (Pacific Sea Nettle Style)
    // 邏輯：中心是深色的骨架(ColorBone)，邊緣是淺色的薄膜(ColorMain)
    // 混合雜訊讓過渡自然
    float mixFactor = centerDist + noiseRaw * 0.2;
    mixFactor = saturate(mixFactor); // 限制在 0~1

    // 這裡我們反轉一下思路：
    // mixFactor 越小(中心) -> 越接近 ColorBone
    // mixFactor 越大(邊緣) -> 越接近 ColorMain
    float3 baseCol = lerp(ColorBone, ColorMain, smoothstep(0.2, 0.8, mixFactor));
    
    OutBaseColor = baseCol;

    // D. 透明度 (Alpha)
    // 邊緣(荷葉邊)要很透，中心要稍微實一點
    // 使用 pow 讓邊緣快速變透
    float edgeAlpha = 1.0 - pow(centerDist, 2.0); 
    float noiseAlpha = 0.3 + noiseRaw * 0.4; // 基礎透明度變化
    
    // 垂直漸層 (上實下虛)
    float verticalFade = smoothstep(0.0, 0.1, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
    
    OutAlpha = saturate(edgeAlpha * noiseAlpha * verticalFade + 0.1); 
    // +0.1 是保底不完全消失，保留薄紗感

    // E. 自發光 (Emission)
    // 只在充電(Charge)時發光，且集中在骨架(中心)
    float emitMask = (1.0 - centerDist); // 中心亮
    emitMask = pow(emitMask, 3.0); // 聚焦中心
    OutEmission = ColorEmit * Charge * emitMask * 2.0;

    // F. 法線與光滑度 (Details)
    // 計算 Procedural Normal
    float epsilon = 0.01;
    // 使用 noiseRaw 的變體來做法線，避免重複感
    float h  = noiseRaw;
    float hx = fbm3_Oral(float3((UV + float2(epsilon, 0)) * 2.0, 0.5), 0.2, TimeZ);
    float hy = fbm3_Oral(float3((UV + float2(0, epsilon)) * 2.0, 0.5), 0.2, TimeZ);
    
    float dx = (h - hx) * 5.0; // 增強法線凹凸感
    float dy = (h - hy) * 5.0;
    
    OutNormalDS = normalize(float3(dx, dy, 1.0));

    // 光滑度：非常濕潤
    OutSmoothness = 0.92 + noiseRaw * 0.05;
}

#endif