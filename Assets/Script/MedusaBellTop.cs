using System.Collections.Generic;
using UnityEngine;

// 對應 src/medusaBellTop.js
public class MedusaBellTop
{
    private Medusa _medusa;
    
    // 公開 vertexRows，因為其他組件 (如 Margin) 需要存取邊緣頂點
    public List<List<MedusaBellGeometry.VertexInfo>> vertexRows = new List<List<MedusaBellGeometry.VertexInfo>>();

    public MedusaBellTop(Medusa medusa)
    {
        _medusa = medusa;
    }

    public void CreateGeometry()
    {
        int subdivisions = _medusa.subdivisions;
        MedusaBellGeometry geometry = _medusa.bell.geometryOutside;

        // ---------------------------------------------------------
        // 1. 定義二十面體 (Icosahedron) 基礎參數
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // Helper: AddVertex (核心修正：正確計算 UV 參數)
        // ---------------------------------------------------------
        MedusaBellGeometry.VertexInfo AddVertex(Vector3 position)
        {
            // [JS 原作邏輯]
            // const width = Math.sqrt(position.x*position.x+position.z*position.z);
            float width = Mathf.Sqrt(position.x * position.x + position.z * position.z);
            
            // [關鍵修正] Zenith 必須是歸一化的弧度 (0~1)
            // atan2(width, y) 算出從頂點向下的角度 (0 ~ PI/2)
            // 除以 (PI * 0.5) 將其映射到 0~1，這直接決定了 UV0 的半徑
            float zenith = Mathf.Atan2(width, position.y) / (Mathf.PI * 0.5f);
            
            // Azimuth 方位角
            float azimuth = Mathf.Atan2(position.x, position.z);

            return geometry.AddVertexFromParams(zenith, azimuth);
        }

        vertexRows.Clear();
        // JS 中的 vertexRows[0] 是空的，我們保持一致以對齊索引
        vertexRows.Add(new List<MedusaBellGeometry.VertexInfo>());

        // ---------------------------------------------------------
        // 2. 生成頂點 (Vertices)
        // ---------------------------------------------------------
        
        // --- Part A: 上半球 (y=1 to subdivisions) ---
        for (int y = 1; y <= subdivisions; y++)
        {
            var vertexRow = new List<MedusaBellGeometry.VertexInfo>();
            float ratio = (float)y / subdivisions;

            for (int f = 0; f < 5; f++)
            {
                // 計算每一層的邊界點
                Vector3 e0 = Vector3.Lerp(icoVertexTop, icoVertexRowTop[f], ratio);
                Vector3 e1 = Vector3.Lerp(icoVertexTop, icoVertexRowTop[f + 1], ratio);
                
                // 在邊界點之間插值生成頂點
                for (int x = 0; x < y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e0, e1, (float)x / y).normalized;
                    vertexRow.Add(AddVertex(pos));
                }
            }
            // 閉合圓周 (複製開頭的點以處理接縫)
            vertexRow.Add(vertexRow[0]);
            vertexRow.Add(vertexRow[1]);
            vertexRows.Add(vertexRow);
        }

        // --- Part B: 下半部延伸/裙邊 (y=1 to subdivisions/2) ---
        for (int y = 1; y <= subdivisions / 2; y++)
        {
            var vertexRow = new List<MedusaBellGeometry.VertexInfo>();
            float ratio = (float)y / subdivisions;

            for (int f = 0; f < 5; f++)
            {
                Vector3 topF = icoVertexRowTop[f];
                Vector3 botF = icoVertexRowBottom[f];
                Vector3 topF1 = icoVertexRowTop[f + 1];
                Vector3 botF1 = icoVertexRowBottom[f + 1];

                // 複雜的插值邏輯 (對應 JS)
                Vector3 e0 = Vector3.Lerp(topF, botF, ratio);
                Vector3 e1 = Vector3.Lerp(topF1, botF, ratio); 
                Vector3 e2 = Vector3.Lerp(topF1, botF1, ratio);

                // Segment 1
                for (int x = 0; x < subdivisions - y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e0, e1, (float)x / (subdivisions - y)).normalized;
                    vertexRow.Add(AddVertex(pos));
                }
                // Segment 2
                for (int x = 0; x < y; x++)
                {
                    Vector3 pos = Vector3.Lerp(e1, e2, (float)x / y).normalized;
                    vertexRow.Add(AddVertex(pos));
                }
            }
            vertexRow.Add(vertexRow[0]);
            vertexRow.Add(vertexRow[1]);
            vertexRows.Add(vertexRow);
        }

        // ---------------------------------------------------------
        // 3. 生成面 (Faces / Indices)
        // ---------------------------------------------------------
        
        // 輔助函數：從 vertexRows 獲取正確的 Vertex ID
        int GetVertexPtr(int row, int index) 
        {
            return vertexRows[row][index].ptr;
        }

        // 對應 JS: getVertexFromTopFace
        int GetVertexFromTopFace(int face, int row, int index)
        {
            return GetVertexPtr(row, face * row + index);
        }

        // 對應 JS: getVertexFromBottomDownlookingFace
        int GetVertexFromBottomDownlookingFace(int face, int row, int index)
        {
            return GetVertexPtr(subdivisions + row, face * subdivisions + index);
        }

        // 對應 JS: getVertexFromBottomUplookingFace
        int GetVertexFromBottomUplookingFace(int face, int row, int index)
        {
            return GetVertexPtr(subdivisions + row, face * subdivisions + (subdivisions - row) + index);
        }

        // --- 處理頂端接縫 (Row 1) ---
        // JS 原作這裡手動縫合了第一圈
        if (vertexRows.Count > 1)
        {
            var row1 = vertexRows[1];
            // 注意：JS 原碼只縫合了前三個面? 這裡照抄邏輯
            // 但為了保險，通常我們會希望縫合完整。
            // 這裡還原 JS 顯式寫出的部分：
            int v0 = row1[0].ptr;
            int v1 = row1[1].ptr;
            int v2 = row1[2].ptr;
            int v3 = row1[3].ptr;
            int v4 = row1[4].ptr;
            
            geometry.AddFace(v0, v1, v2);
            geometry.AddFace(v0, v2, v3);
            geometry.AddFace(v0, v3, v4);
        }

        // --- 生成上半球三角形 ---
        for (int y = 2; y <= subdivisions; y++)
        {
            for (int f = 0; f < 5; f++)
            {
                for (int x = 0; x < y; x++)
                {
                    int v0 = GetVertexFromTopFace(f, y, x);
                    int v1 = GetVertexFromTopFace(f, y - 1, x);
                    int v2 = GetVertexFromTopFace(f, y, x + 1);
                    
                    // JS: addFace(v2, v1, v0) -> 逆時針
                    // Unity 預設順時針剔除，這裡保持 JS 順序 (可能需要 Shader Cull Off)
                    geometry.AddFace(v2, v1, v0);

                    if (x < y - 1)
                    {
                        int v3 = GetVertexFromTopFace(f, y - 1, x + 1);
                        geometry.AddFace(v1, v2, v3);
                    }
                }
            }
        }

        // --- 生成下半部三角形 ---
        for (int y = 1; y <= subdivisions / 2; y++)
        {
            for (int f = 0; f < 5; f++)
            {
                // Part 1 (Downlooking)
                for (int x = 0; x < subdivisions - y; x++)
                {
                    int v0 = GetVertexFromBottomDownlookingFace(f, y, x);
                    int v1 = GetVertexFromBottomDownlookingFace(f, y - 1, x + 1);
                    int v2 = GetVertexFromBottomDownlookingFace(f, y, x + 1);
                    int v3 = GetVertexFromBottomDownlookingFace(f, y - 1, x + 2);
                    
                    geometry.AddFace(v2, v1, v0);
                    geometry.AddFace(v1, v2, v3);
                }
                // Part 2 (Uplooking)
                for (int x = 0; x < y; x++)
                {
                    int v0 = GetVertexFromBottomUplookingFace(f, y, x);
                    int v1 = GetVertexFromBottomUplookingFace(f, y - 1, x);
                    int v2 = GetVertexFromBottomUplookingFace(f, y, x + 1);
                    
                    geometry.AddFace(v2, v1, v0);
                    
                    int v3 = GetVertexFromBottomUplookingFace(f, y - 1, x + 1);
                    geometry.AddFace(v1, v2, v3);
                }
            }
        }
    }
}