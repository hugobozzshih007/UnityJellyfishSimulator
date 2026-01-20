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

// --- 函數 2: 讀取法線 (新增) ---
void GetPhysicsNormal_float(float2 uv, out float3 outNormal)
{
    #if defined(SHADERGRAPH_PREVIEW)
    outNormal = float3(0, 1, 0);
    #else
    int id = (int)(uv.x + 0.5);
    // 這裡就能讀到 Compute Shader 算好存進去的圓潤法線了
    outNormal = _PhysicsBuffer[id].normal; 
    #endif
}

#endif