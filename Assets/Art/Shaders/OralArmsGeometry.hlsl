// 為了避免重複定義，我們加上防衛宏
#ifndef ORAL_ARMS_HELPER_INCLUDED
#define ORAL_ARMS_HELPER_INCLUDED
#include "MedusaBellFormula.hlsl"


// 宣告 Buffer
// 注意：Shader Graph 產生的 code 有時會把變數藏起來，
// 所以我們直接在這裡宣告，C# 只要用 SetBuffer("_PhysicsBuffer", ...) 就能傳進來
//StructuredBuffer<MedusaPhysicsData> _PhysicsBuffer;

// 這是給 Custom Function Node 用的函數
// 輸入: uv (用來藏 ID)
// 輸出: newPosition (讀取到的物理位置)
void GetPhysicsPosition_float(float2 uv, out float3 newPosition)
{
    #if defined(SHADERGRAPH_PREVIEW)
    // 在 Shader Graph 預覽視窗裡沒有 Buffer，我們回傳 0 避免報錯
    newPosition = float3(0, 0, 0);
    #else
    // 從 UV.x 解析出 ID (加 0.5 是為了避免浮點數誤差)
    int id = (int)(uv.x + 0.5);
        
    // 從 Buffer 讀取位置
    // 這裡讀出來的是 World Space (因為我們在 Compute Shader 裡算的就是 World Pos)
    newPosition = _PhysicsBuffer[id].position;
    #endif
}

float3x3 GetChainRotation(int id)
{
    // 讀取當前點與下一個點，算出 "骨架朝向"
    // 注意：這裡簡化處理，假設 Buffer 足夠大，實際專案要檢查邊界
    float3 pCurrent = _PhysicsBuffer[id].position;
    // 嘗試讀取下一個點，如果太遠就讀上一個點 (簡單防呆)
    float3 pNext    = _PhysicsBuffer[id + 1].position; 
    
    // 計算切線 (Tangent): 骨架的延伸方向
    float3 tangent = normalize(pNext - pCurrent);
    
    // 如果兩點重疊(剛生成時)，給一個預設向下
    if (length(pNext - pCurrent) < 0.0001) tangent = float3(0, -1, 0);

    // 建立旋轉基底 (LookAt Matrix)
    // 我們原本的鰭是沿著 Y 軸向下長的，所以我們要把 (0, -1, 0) 轉到 tangent 方向
    
    float3 up = float3(0, 1, 0);
    float3 forward = tangent; // 這是新的 Y
    
    // 計算 Right (X)
    float3 right = normalize(cross(forward, up));
    // 如果 forward 跟 up 平行 (也就是觸手完全垂直)，cross 會失效
    if (length(cross(forward, up)) < 0.01) right = float3(1, 0, 0);
    
    // 重新計算 Up (Z) - 修正後的
    float3 correctUp = cross(right, forward);
    
    // 構建矩陣: 這是把 "水平的鰭" 轉成 "跟隨骨架的鰭" 的關鍵
    // Row 1: Right
    // Row 2: Forward (Tangent)
    // Row 3: CorrectUp
    return float3x3(right, forward, correctUp);
}

void OralArmsDisplacement_float(
    float3 PositionOS,    // Object Space Position (其實是相對於骨架的 Offset)
    float3 NormalOS,      // Object Space Normal
    float2 UV,            // UV0 (x: 0->1 從骨架到邊緣, y: 進度)
    float Frequency,      // 波浪密度 (建議 3.0 ~ 5.0)
    float Amplitude,      // 波浪幅度 (建議 0.1 ~ 0.2)
    
    out float3 OutPosition, // 輸出的最終位置 (Object Space for Shader Graph)
    out float3 OutNormal    // 修正後的法線
)
{
    // --- 1. 計算波浪方向 (旗幟擺動方向) ---
    float3 upDir = float3(0, 1, 0); // 骨架方向
    
    // ★ 關鍵幾何：垂直於 "生長方向" 和 "骨架方向" 的向量
    // 這就是 "垂直於鰭片表面" 的方向 (Side-to-Side)
    // 例如：鰭往右長(X)，波浪就往前後打(Z)
    float3 waveDir = normalize(cross(NormalOS, upDir));
    
    // 防呆
    if (length(waveDir) < 0.01) waveDir = float3(0, 0, 1);

    // --- 2. 遮罩控制 (消除扭轉感的關鍵) ---
    // 我們必須讓中心 (UV.x=0) 像石頭一樣硬，完全不動
    // 這樣大腦才不會把邊緣的移動解讀為 "整體的旋轉"
    float edgeMask = pow(saturate(UV.x), 2.5); // 指數高一點，只讓最邊緣動
    float dMask_dx = 2.5 * pow(saturate(UV.x), 1.5);

    // --- 3. 波浪計算 ---
    // 沿著長度 (UV.y) 產生正弦波
    float phase = UV.y * Frequency * 6.28; 
    
    // ★ 小技巧：打亂四個鰭的相位
    // 讓它們不要同時往左/往右，避免看起來像螺旋
    // 我們利用 NormalOS 的方向來產生一個隨機偏移
    float randomOffset = dot(NormalOS, float3(1,0,1)) * 3.14; 
    
    float sinWave = sin(phase + randomOffset);
    float cosWave = cos(phase + randomOffset); 

    // 計算位移量 (Height)
    float H = sinWave * edgeMask * Amplitude;

    // --- 4. 輸出位置 ---
    // 頂點沿著 "旗幟方向" 移動
    OutPosition = PositionOS + waveDir * H;

    // --- 5. 法線修正 (Recalculate Normal) ---
    // 當平面像窗簾一樣彎曲時，法線會左右擺動
    
    // dH/dY (縱向斜率): 影響切線 Tangent (Y軸)
    // 當旗幟飄動時，表面會沿著 Y 軸傾斜
    float slopeY = cosWave * edgeMask * Amplitude * Frequency * 6.28;
    
    // dH/dX (橫向斜率): 影響寬度方向?
    // 因為 Mask 的關係，波浪高度從中心到邊緣是變化的，這也會造成傾斜
    float slopeX = sinWave * dMask_dx * Amplitude;

    // 構建新的座標系來計算正確法線
    // 1. 新的長度方向 (Y) = 原本Up + 往波浪方向傾斜
    float3 newUp = normalize(upDir + waveDir * slopeY);
    
    // 2. 新的寬度方向 (X) = 原本NormalOS + 往波浪方向傾斜
    float3 newFinDir = normalize(NormalOS + waveDir * slopeX);

    // 3. 最終法線 = 垂直於這兩個新向量
    OutNormal = normalize(cross(newUp, newFinDir));
}

#endif