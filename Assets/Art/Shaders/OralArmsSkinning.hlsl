#ifndef ORAL_ARMS_SKINNING_INCLUDED
#define ORAL_ARMS_SKINNING_INCLUDED
#include "MedusaBellFormula.hlsl"
// 物理 Buffer
//StructuredBuffer<MedusaPhysicsData> _PhysicsBuffer;

// 四元數旋轉函數 (比矩陣更省效能)
float3 RotateVector(float3 v, float4 q)
{
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

// 從兩個向量計算旋轉四元數 (From -> To)
float4 FromToRotation(float3 from, float3 to)
{
    float3 axis = cross(from, to);
    float dotVal = dot(from, to);
    float w = sqrt((1.0 + dotVal) * 2.0); // 優化版 half-angle
    float invW = 1.0 / w;
    return float4(axis * invW, w * 0.5);
}

void GetSkinnedPosition_float(
    float3 PositionOS,    // 原始靜態位置 (Rest Pose)
    float3 NormalOS,      // 原始靜態法線 (Correct Normal)
    float2 Binding,       // UV1: x=PhysicsID, y=LerpT
    
    out float3 OutPosition,
    out float3 OutNormal
)
{
    #if defined(SHADERGRAPH_PREVIEW)
    OutPosition = PositionOS;
    OutNormal = NormalOS;
    #else
    
    // 1. 讀取物理數據
    int id = (int)(Binding.x + 0.5);
    float t = Binding.y; // 插值權重
    
    // 讀取當前節點 (P0) 和 下一個節點 (P1)
    // 我們需要這兩個點來計算 "當前的骨架方向"
    float3 p0 = _PhysicsBuffer[id].position;
    // 簡單防呆，假設 Buffer夠大
    float3 p1 = _PhysicsBuffer[id + 1].position; 
    
    // 2. 計算骨架方向
    float3 restDir = float3(0, -1, 0); // 我們生成網格時是朝下的
    float3 currDir = normalize(p1 - p0);
    
    // 3. 計算旋轉 (從 "朝下" 轉到 "當前骨架方向")
    float4 rotQ = FromToRotation(restDir, currDir);
    
    // 4. 計算變形
    // 核心邏輯：
    // 我們的 PositionOS 包含了 "相對於骨架的偏移" (也就是皺褶形狀)
    // 我們要先算出這個偏移量，旋轉它，然後加到物理位置上
    
    // 在 Rest Pose 中，這個頂點對應的骨架中心是 (0, PositionOS.y, 0)
    // 但因為我們用了 Binding ID，我們知道這個頂點是對應 p0 的
    // 所以偏移量 Offset = PositionOS - (RestPose_Bone_Pos)
    // 這裡有個小技巧：我們生成網格時，是把每一層的中心放在 (0, -y, 0)
    // 而 Binding ID 對應的物理點也是 (0, -y, 0)
    // 所以 Offset 其實就是 Vertex 的 "水平分量" + "垂直局部偏移"
    
    // 為了簡化，我們假設 PositionOS 的 Y 軸是相對於 p0 的
    // 這需要 C# 生成時配合，或者我們直接用 PositionOS - RestBonePos
    // 讓我們用最簡單的方法：
    // 把 PositionOS 當作純粹的 "Vector from Anchor"，直接旋轉
    
    // 修正：C# 生成時是 World 座標 (0, -y, 0)，我們需要把它轉成 Local
    // 假設 Binding ID 對應的 Rest Pose 是 (0, -p*spacing, 0)
    // 這樣太麻煩，我們換個思路：
    // 把 PositionOS 的 Y 軸歸零，只保留 XZ (半徑方向)，這是 "Ruffle Offset"
    // 把 Y 軸轉換為 "沿著骨架的長度"
    
    // ★ 簡單暴力法：
    // 直接把 PositionOS 視為一個 "相對於骨架的向量"
    // 但 PositionOS.y 是負的 (向下)。
    // 我們只旋轉 XZ 平面 (裙邊擴張)，保留 Y 軸的線性插值？
    // 不，要全轉。
    
    // 重新定義 Offset：
    // 我們的 Visual Mesh 每一層都有一個中心點 centerPos (在 C# 裡)
    // 真正的 Offset = PositionOS - centerPos;
    // 但 Shader 不知道 centerPos 在哪。
    
    // ★ 改進方案：C# 傳進來的 PositionOS 直接改成 "Local Offset"！
    // 也就是說，C# 裡 vertices.Add(offset) 而不是 centerPos + offset。
    // 然後 Shader 裡 OutPosition = p_interpolated + Rotate(offset)
    
    // 讓我們修改一下思路，假設 C# 傳的是 Local Offset (相對位置)
    // 這需要在 C# 裡稍微改一行
    // 如果不改 C#，我們可以用 Vertex Color 存 Offset? 
    // 或者，因為我們知道 mesh 是朝下的，我們假設 center 就在 (0, PositionOS.y, 0)
    
    float3 centerPosRest = float3(0, PositionOS.y, 0);
    float3 offset = PositionOS - centerPosRest; // 這是裙邊的水平擴張向量
    
    // 應用旋轉
    float3 rotatedOffset = RotateVector(offset, rotQ);
    
    // 5. 混合物理位置 (平滑蒙皮)
    // 使用 LerpT 在 p0 和 p1 之間插值，讓連接口平滑
    float3 bonePos = lerp(p0, p1, t);
    
    OutPosition = bonePos + rotatedOffset;
    
    // 6. 旋轉法線
    OutNormal = RotateVector(NormalOS, rotQ);
    
    #endif
}

#endif