#ifndef TENTACLE_LIBRARY_INCLUDED
#define TENTACLE_LIBRARY_INCLUDED

// 定義與 C# 和 Compute Shader 一致的結構
struct VertexData {
    float3 position;
    float3 normal;   // ★ 必須補上這行 (即使下面函數沒用到它)
    float isFixed;
    float uvY;
};

// 宣告 Buffer
// 注意：Shader Graph 無法在屬性面板顯示這個，但 C# SetBuffer 仍然有效
StructuredBuffer<VertexData> _VertexData;

// 這是在 Custom Function Node 中呼叫的函數
void GetTentaclePosition_float(
    float Angle, 
    float Width, 
    float IndexA, 
    float IndexB, 
    out float3 OutPosition, 
    out float3 OutNormal) 
{
    // 預覽防呆：因為 Shader Graph 編輯器裡沒有 Buffer，會報錯，所以要加這段
    #if defined(SHADERGRAPH_PREVIEW)
    OutPosition = float3(0, 0, 0);
    OutNormal = float3(0, 1, 0);
    #else
    uint idA = (uint)IndexA;
    uint idB = (uint)IndexB;

    // 讀取物理位置
    float3 p0 = _VertexData[idA].position;
    float3 p1 = _VertexData[idB].position;

    // 這裡需要轉成 Object Space 嗎？
    // 通常 Compute Shader 算出的是 World Space。
    // Shader Graph 的 Vertex Position 預設期望 Object Space。
    // 如果你的 Renderer 是設為 World Space，就不用轉。
    // 為了通用，我們先假設是在 World Space 計算，Shader Graph 裡再處理。

    // --- 核心數學邏輯 (與原案一致) ---
    float3 tangent = p1 - p0;
        
    // 防止退化的 Up 向量
    float3 up = abs(tangent.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 bitangent = normalize(cross(tangent, up));
    float3 bitangent2 = normalize(cross(tangent, bitangent));

    // 計算環狀法線
    float3 normalDir = sin(Angle) * bitangent + cos(Angle) * bitangent2;
    normalDir = normalize(normalDir);

    // 最終位置
    float3 finalPos = (p0 + p1) * 0.5 + normalDir * Width;

    OutPosition = finalPos;
    OutNormal = normalDir;
    #endif
}

#endif