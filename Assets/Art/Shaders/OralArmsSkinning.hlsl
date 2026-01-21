#ifndef ORAL_ARMS_SKINNING_INCLUDED
#define ORAL_ARMS_SKINNING_INCLUDED
#include "MedusaBellFormula.hlsl"

// 四元數旋轉函數
float3 RotateVector(float3 v, float4 q)
{
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

// 從兩個向量計算旋轉四元數 (From -> To)
float4 FromToRotation(float3 from, float3 to)
{
    float3 axis = cross(from, to);
    float dotVal = dot(from, to);
    // 防呆：如果方向完全相反，dotVal = -1，這裡會出錯，加個 max
    float w = sqrt((1.0 + max(dotVal, -0.99)) * 2.0); 
    float invW = 1.0 / w;
    return float4(axis * invW, w * 0.5);
}

void GetSkinnedPosition_float(
    float3 PositionOS,    // 原始靜態位置
    float3 NormalOS,      // 原始靜態法線
    float2 Binding,       // UV1: x=PhysicsID, y=LerpT
    
    out float3 OutPosition,
    out float3 OutNormal
)
{
    #if defined(SHADERGRAPH_PREVIEW)
        OutPosition = PositionOS;
        OutNormal = NormalOS;
    #else
    
    int id = (int)(Binding.x + 0.5);
    float t = Binding.y; // 0.0 ~ 1.0 在這節骨骼內的進度
    
    // ★★★ 核心修正：讀取 3 個點來計算平滑方向 ★★★
    float3 p0 = _PhysicsBuffer[id].position;
    float3 p1 = _PhysicsBuffer[id + 1].position;
    
    // 嘗試讀取下下個點 (p2) 用於預測下一節的方向
    // 注意：如果是手臂末端，id+2 可能會讀到下一條手臂的根部（距離很遠）
    // 所以我們需要一個距離檢測來防呆
    float3 p2 = _PhysicsBuffer[id + 2].position;

    // 計算當前段向量 (v0) 和 下一段向量 (v1)
    float3 v0 = p1 - p0;
    float3 v1 = p2 - p1;

    // --- 防呆檢測 ---
    // 如果 v1 長度異常（例如跨越到下一條手臂），就回退使用 v0
    // 假設正常節段長度差異不會超過 3 倍
    float len0 = length(v0);
    float len1 = length(v1);
    if (len1 > len0 * 3.0 + 0.5 || len1 < 0.001) 
    {
        v1 = v0; // 末端沒有下一節了，保持方向不變
    }

    // ★ 平滑插值方向 ★
    // 我們不直接用 normalize(v0)，而是根據 t 在 v0 和 v1 之間混合
    // 這樣在 t=1 (交界處) 的方向會完美銜接下一節的 t=0
    float3 smoothDir = normalize(lerp(v0, v1, t));

    // 2. 計算旋轉 (Rest Pose 朝下 (0,-1,0) -> Current Smooth Dir)
    float3 restDir = float3(0, -1, 0);
    float4 rotQ = FromToRotation(restDir, smoothDir);

    // 3. 計算位移
    // 將 C# 傳來的 PositionOS 視為相對於骨架的偏移 (Local Offset)
    // 假設 PositionOS.y 在 C# 生成時已經包含了相對於節點的垂直偏移，
    // 但為了簡單，我們這裡把 PositionOS 當作是以 (0, -y, 0) 為中心的偏移
    
    // 修正邏輯：
    // C# 裡的 vertex.y 是負的，模擬垂下。
    // 我們需要把這個 "垂下" 的量 (PositionOS.y) 加到骨架插值位置上嗎？
    // 不，bonePos (lerp(p0, p1, t)) 已經包含了垂直位置。
    // 我們只需要 PositionOS 的 "水平擴張" (XZ)。
    
    // 提取水平偏移 (裙邊半徑)
    float3 offset = float3(PositionOS.x, 0, PositionOS.z);
    
    // 旋轉偏移量
    float3 rotatedOffset = RotateVector(offset, rotQ);
    
    // 4. 計算骨架中心點 (線性插值位置)
    float3 bonePos = lerp(p0, p1, t);
    
    // 5. 組合
    OutPosition = bonePos + rotatedOffset;
    
    // 6. 旋轉法線
    OutNormal = RotateVector(NormalOS, rotQ);
    
    #endif
}

#endif