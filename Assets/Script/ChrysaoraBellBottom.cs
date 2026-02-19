using System.Collections.Generic;
using UnityEngine;

//// <summary>
// /// 標準水母鐘體內殼實作 (繼承自 MedusaBellBottomBase)
// /// </summary>
public class ChrysaoraBellBottom : MedusaBellBottomBase
{

    /// <summary>
    /// 初始化模組，由 Medusa.cs 統一呼叫
    /// </summary>
    public override void Initialize(Medusa medusa)
    {
        this.owner = medusa;
        
        // 內殼與外殼共享相同的細分參數，確保頂點一一對應
        CreateGeometry();
    }

    public void CreateGeometry()
    {
        int subdivisions = owner.config.subdivisions;
        
        // ★ 重點 1: 使用 geometryInside (內殼)
        // ★ 關鍵：使用 owner 提供的內殼幾何容器
        MedusaBellGeometry geometry = owner.geometryInside;

        // --- Icosahedron 數學常數 (與 Top 相同) ---
        float icoCircumradius = 0.951057f;
        float icoRadius = 1f / (2f * Mathf.Sin(36f * Mathf.Deg2Rad));
        float alpha = Mathf.Acos(icoRadius);
        float h = icoCircumradius - Mathf.Sin(alpha);

        Vector3 icoVertexTop = new Vector3(0, icoCircumradius, 0);
        List<Vector3> icoVertexRowTop = new List<Vector3>();
        List<Vector3> icoVertexRowBottom = new List<Vector3>();

        for (int i = 0; i <= 5; i++)
        {
            float topAngle = i * (2.0f * Mathf.PI / 5f);
            float bottomAngle = (0.5f + i) * (2.0f * Mathf.PI / 5f);
            icoVertexRowTop.Add(new Vector3(Mathf.Sin(topAngle) * icoRadius, h, Mathf.Cos(topAngle) * icoRadius));
            icoVertexRowBottom.Add(new Vector3(Mathf.Sin(bottomAngle) * icoRadius, -h, Mathf.Cos(bottomAngle) * icoRadius));
        }

        // --- Helper: 添加頂點 ---
        MedusaBellGeometry.VertexInfo AddVertex(Vector3 position)
        {
            // [關鍵修正] 與 Top 相同的 Zenith 計算，確保內外殼變形同步
            float width = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            float zenith = Mathf.Atan2(width, position.y) / (Mathf.PI * 0.5f);
            float azimuth = Mathf.Atan2(position.x, position.z);
            
            // ★ 重點 2: Side.z = -1 代表內表面 (Inner Side)
            return geometry.AddVertexFromParams(zenith, azimuth, new Vector3(0, 0, -1), 0);
        }

        // 復用結構存儲頂點引用
        List<List<MedusaBellGeometry.VertexInfo>> vertexRows = new List<List<MedusaBellGeometry.VertexInfo>>();
        vertexRows.Add(new List<MedusaBellGeometry.VertexInfo>()); // placeholder for index alignment

        // --- 上半球插值 (Top Hemisphere) ---
        for (int y = 1; y <= subdivisions; y++)
        {
            List<MedusaBellGeometry.VertexInfo> row = new List<MedusaBellGeometry.VertexInfo>();
            for (int f = 0; f < 5; f++)
            {
                Vector3 e0 = Vector3.Lerp(icoVertexTop, icoVertexRowTop[f], (float)y / subdivisions);
                Vector3 e1 = Vector3.Lerp(icoVertexTop, icoVertexRowTop[f + 1], (float)y / subdivisions);
                for (int x = 0; x < y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e0, e1, (float)x / y).normalized;
                    row.Add(AddVertex(pos));
                }
            }
            row.Add(row[0]);
            row.Add(row[1]);
            vertexRows.Add(row);
        }

        // --- 下半球插值 (Bottom Hemisphere) ---
        for (int y = 1; y <= subdivisions / 2; y++)
        {
            List<MedusaBellGeometry.VertexInfo> row = new List<MedusaBellGeometry.VertexInfo>();
            for (int f = 0; f < 5; f++)
            {
                Vector3 e0 = Vector3.Lerp(icoVertexRowTop[f], icoVertexRowBottom[f], (float)y / subdivisions);
                Vector3 e1 = Vector3.Lerp(icoVertexRowTop[f + 1], icoVertexRowBottom[f], (float)y / subdivisions);
                Vector3 e2 = Vector3.Lerp(icoVertexRowTop[f + 1], icoVertexRowBottom[f + 1], (float)y / subdivisions);

                for (int x = 0; x < subdivisions - y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e0, e1, (float)x / (subdivisions - y)).normalized;
                    row.Add(AddVertex(pos));
                }
                for (int x = 0; x < y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e1, e2, (float)x / y).normalized;
                    row.Add(AddVertex(pos));
                }
            }
            row.Add(row[0]);
            row.Add(row[1]);
            vertexRows.Add(row);
        }

        // --- Helper Functions ---
        int GetVertexFromTopFace(int face, int row, int index)
        {
            return vertexRows[row][face * row + index].ptr;
        }
        int GetVertexFromBottomDownlookingFace(int face, int row, int index)
        {
            return vertexRows[subdivisions + row][face * subdivisions + index].ptr;
        }
        int GetVertexFromBottomUplookingFace(int face, int row, int index)
        {
            return vertexRows[subdivisions + row][face * subdivisions + (subdivisions - row) + index].ptr;
        }

        // --- 構建面 (Faces) - 注意順序與 Top 相反 ---
        
        // 1. 極點蓋子
        {
            var r = vertexRows[1];
            // JS: addFace(v0, v2, v1) -> 這裡順序是關鍵，確保法線向內
            geometry.AddFace(r[0].ptr, r[2].ptr, r[1].ptr);
            geometry.AddFace(r[0].ptr, r[3].ptr, r[2].ptr);
            geometry.AddFace(r[0].ptr, r[4].ptr, r[3].ptr);
        }

        // 2. 上半球三角形
        for (int y = 2; y <= subdivisions; y++)
        {
            for (int f = 0; f < 5; f++)
            {
                for (int x = 0; x < y; x++)
                {
                    int v0 = GetVertexFromTopFace(f, y, x);
                    int v1 = GetVertexFromTopFace(f, y - 1, x);
                    int v2 = GetVertexFromTopFace(f, y, x + 1);
                    
                    // JS: addFace(v0,v1,v2) -> 對比 Top 的 (v2,v1,v0)，這裡是反的
                    geometry.AddFace(v0, v1, v2);
                    
                    if (x < y - 1)
                    {
                        int v3 = GetVertexFromTopFace(f, y - 1, x + 1);
                        geometry.AddFace(v3, v2, v1);
                    }
                }
            }
        }

        // 3. 下半球三角形
        for (int y = 1; y <= subdivisions / 2; y++)
        {
            for (int f = 0; f < 5; f++)
            {
                for (int x = 0; x < subdivisions - y; x++)
                {
                    int v0 = GetVertexFromBottomDownlookingFace(f, y, x);
                    int v1 = GetVertexFromBottomDownlookingFace(f, y - 1, x + 1);
                    int v2 = GetVertexFromBottomDownlookingFace(f, y, x + 1);
                    int v3 = GetVertexFromBottomDownlookingFace(f, y - 1, x + 2);
                    
                    geometry.AddFace(v0, v1, v2);
                    geometry.AddFace(v3, v2, v1);
                }
                for (int x = 0; x < y; x++)
                {
                    int v0 = GetVertexFromBottomUplookingFace(f, y, x);
                    int v1 = GetVertexFromBottomUplookingFace(f, y - 1, x);
                    int v2 = GetVertexFromBottomUplookingFace(f, y, x + 1);
                    
                    geometry.AddFace(v0, v1, v2);
                    
                    int v3 = GetVertexFromBottomUplookingFace(f, y - 1, x + 1);
                    geometry.AddFace(v3, v2, v1);
                }
            }
        }
    }
    
    /// <summary>
    /// 由 Medusa.cs 驅動的更新
    /// </summary>
    public override void UpdateModule(float dt)
    {
        // 內殼通常不包含獨立的 Update 邏輯，材質參數由 Medusa.cs 統一更新
    }
}