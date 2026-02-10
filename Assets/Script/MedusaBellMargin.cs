using System.Collections.Generic;
using UnityEngine;

public class MedusaBellMargin
{
    private Medusa _medusa;
    
    public List<List<int>> bellMarginRows = new List<List<int>>();
    public int bellMarginWidth;

    public MedusaBellMargin(Medusa medusa)
    {
        _medusa = medusa;
    }

    public void CreateGeometry()
    {
        int subdivisions = _medusa.subdivisions;
        VerletPhysics physics = _medusa.physics;
        MedusaVerletBridge bridge = _medusa.bridge;
        int medusaId = _medusa.medusaId;

        // 引用 Bell Top 的最後一排頂點作為起點
        var vertexRows = _medusa.bell.top.vertexRows;
        
        // ★ 新增：用來暫存物理頂點完整資訊的列表 (為了傳遞給渲染網格用)
        var marginPhysicsInfos = new List<List<MedusaBellGeometry.VertexInfo>>();

        // --- 1. 建立物理結構 (Verlet Geometry) ---
        
        int bellMarginWidth = 5* subdivisions;
        this.bellMarginWidth = bellMarginWidth;
        int bellMarginHeight = 4;

        bellMarginRows.Clear();

        for (int y = 0; y < bellMarginHeight; y++)
        {
            List<int> row = new List<int>();
            // ★ 新增：當前行的資訊列表
            List<MedusaBellGeometry.VertexInfo> infoRow = new List<MedusaBellGeometry.VertexInfo>();

            for (int x = 0; x < bellMarginWidth; x++)
            {
                // 取出 Top 最後一排的頂點作為參考 (Pivot)
                var pivot = vertexRows[vertexRows.Count - 1][x];
                float zenith = pivot.zenith;
                float azimuth = pivot.azimuth;

                // y==0 是固定點 (黏在 Bell Top 上)，y>0 是自由點
                bool isFixed = (y == 0); 
                int vertexId = physics.AddVertex(Vector3.zero, isFixed);

                // 計算偏移
                Vector3 offset = new Vector3(Mathf.Sin(azimuth) * y * 0.06f, y * -0.06f, Mathf.Cos(azimuth) * y * 0.06f);
                offset *= 0.7f;
                
                bridge.RegisterVertex(medusaId, vertexId, zenith, azimuth, false, offset, 0, isFixed);
                
                row.Add(vertexId);

                // ★ 關鍵修正：將計算好的數據存入 VertexInfo 結構
                var vInfo = new MedusaBellGeometry.VertexInfo {
                    id = vertexId,
                    zenith = zenith,
                    azimuth = azimuth,
                    offset = offset
                };
                infoRow.Add(vInfo);

                // Muscle Vertex (僅物理用，不需要存入 infoRow)
                if (y >= 1 && y <= 3)
                {
                    int muscleVertexId = physics.AddVertex(Vector3.zero, true);
                    bridge.RegisterVertex(medusaId, muscleVertexId, zenith, azimuth, false, Vector3.zero, -offset.y, true);
                    physics.AddSpring(vertexId, muscleVertexId, 0.01f / Mathf.Pow(y+1, 3), 0);
                }
            }
            // 閉合圓環
            row.Add(row[0]);
            bellMarginRows.Add(row);
            
            // ★ 閉合 Info 列表 (複製第一個到最後)
            infoRow.Add(infoRow[0]);
            marginPhysicsInfos.Add(infoRow);
        }

        // 建立網格彈簧 (Structural Springs)
        for (int y = 1; y < bellMarginHeight; y++)
        {
            for (int x = 0; x < bellMarginWidth; x++)
            {
                float structuralStrength = 0.02f; 
                float shearStrength = 0.002f;

                int v0 = bellMarginRows[y][x];         // 當前點
                int v1 = bellMarginRows[y - 1][x];     // 上方點
                int v2 = bellMarginRows[y][x + 1];     // 右方點
                int v3 = bellMarginRows[y - 1][x + 1]; // 右上方點

                // 垂直與水平彈簧 (Structural)
                physics.AddSpring(v0, v1, structuralStrength, 1.2f);
                physics.AddSpring(v0, v2, structuralStrength, 1.2f);

                // 交叉彈簧 (Shear) - 增加此部分能大幅減少網面變形
                physics.AddSpring(v0, v3, shearStrength, 1f);
                physics.AddSpring(v1, v2, shearStrength, 1f);
            }
        }

        // --- 2. 建立渲染網格 (Render Geometry) ---
        
        MedusaBellGeometry geometryOutside = _medusa.bell.geometryOutside;
        MedusaBellGeometry geometryInside = _medusa.bell.geometryInside;

        List<List<MedusaBellGeometry.VertexInfo>> marginOuterVertexRows = new List<List<MedusaBellGeometry.VertexInfo>>();
        List<List<MedusaBellGeometry.VertexInfo>> marginInnerVertexRows = new List<List<MedusaBellGeometry.VertexInfo>>();

        Vector3 outerSide = new Vector3(0, 0, 1);
        Vector3 innerSide = new Vector3(0, 0, -1);
        Vector3 downSide = new Vector3(0, 1, 0);

        // 第一層 (接縫處)
        {
            List<MedusaBellGeometry.VertexInfo> outerRow = new List<MedusaBellGeometry.VertexInfo>();
            List<MedusaBellGeometry.VertexInfo> innerRow = new List<MedusaBellGeometry.VertexInfo>();
            
            for (int x = 0; x < bellMarginWidth; x++)
            {
                var outerVertex = vertexRows[vertexRows.Count - 1][x];
                var innerVertex = geometryInside.AddVertexFromParams(outerVertex.zenith, outerVertex.azimuth, innerSide, 0);
                
                outerRow.Add(outerVertex);
                innerRow.Add(innerVertex);
            }
            outerRow.Add(outerRow[0]);
            innerRow.Add(innerRow[0]);
            marginOuterVertexRows.Add(outerRow);
            marginInnerVertexRows.Add(innerRow);
        }

        // 其餘層 (物理驅動)
        List<MedusaBellGeometry.VertexInfo> downRowOutside = new List<MedusaBellGeometry.VertexInfo>();
        List<MedusaBellGeometry.VertexInfo> downRowInside = new List<MedusaBellGeometry.VertexInfo>();
        float marginDepth = 0.025f;

        for (int y = 2; y < bellMarginHeight; y++)
        {
            List<MedusaBellGeometry.VertexInfo> outerRow = new List<MedusaBellGeometry.VertexInfo>();
            List<MedusaBellGeometry.VertexInfo> innerRow = new List<MedusaBellGeometry.VertexInfo>();
            
            for (int x = 0; x < bellMarginWidth; x++)
            {
                // ★ 關鍵修正：從 marginPhysicsInfos 獲取完整的 VertexInfo
                // 這樣 AddVertexFromVertices 才能算出正確的 UV 和 Zenith
                var v0 = marginPhysicsInfos[y - 1][x];
                var v1 = marginPhysicsInfos[y - 1][x + 1];
                var v2 = marginPhysicsInfos[y][x];
                var v3 = marginPhysicsInfos[y][x + 1];

                float thickness = ((float)(y - 1) / (bellMarginHeight - 2)) * marginDepth;

                // 現在這裡傳入的 v0~v3 包含了正確的 zenith/azimuth/offset
                var outerVertex = geometryOutside.AddVertexFromVertices(v0, v1, v2, v3, outerSide, thickness);
                var innerVertex = geometryInside.AddVertexFromVertices(v0, v1, v2, v3, innerSide, thickness);
                
                outerRow.Add(outerVertex);
                innerRow.Add(innerVertex);

                // Cap
                if (y == bellMarginHeight - 1)
                {
                    var downVertexOutside = geometryOutside.AddVertexFromVertices(v0, v1, v2, v3, downSide, thickness);
                    var downVertexInside = geometryInside.AddVertexFromVertices(v0, v1, v2, v3, downSide, thickness);
                    downRowOutside.Add(downVertexOutside);
                    downRowInside.Add(downVertexInside);
                }
            }
            outerRow.Add(outerRow[0]);
            innerRow.Add(innerRow[0]);
            marginOuterVertexRows.Add(outerRow);
            marginInnerVertexRows.Add(innerRow);
        }
        downRowOutside.Add(downRowOutside[0]);
        downRowInside.Add(downRowInside[0]);

        // 構建面 (Faces) - Outside
        var marginVertexRowsOutside = new List<List<MedusaBellGeometry.VertexInfo>>(marginOuterVertexRows);
        marginVertexRowsOutside.Add(downRowOutside);

        for (int y = 1; y < marginVertexRowsOutside.Count; y++)
        {
            for (int x = 0; x < bellMarginWidth; x++)
            {
                var v0 = marginVertexRowsOutside[y - 1][x].ptr;
                var v1 = marginVertexRowsOutside[y - 1][x + 1].ptr;
                var v2 = marginVertexRowsOutside[y][x].ptr;
                var v3 = marginVertexRowsOutside[y][x + 1].ptr;
                
                geometryOutside.AddFace(v2, v1, v0);
                geometryOutside.AddFace(v1, v2, v3);
            }
        }

        // 構建面 (Faces) - Inside
        var marginVertexRowsInside = new List<List<MedusaBellGeometry.VertexInfo>>();
        marginVertexRowsInside.Add(downRowInside);
        for(int i = marginInnerVertexRows.Count - 1; i >= 0; i--)
            marginVertexRowsInside.Add(marginInnerVertexRows[i]);

        for (int y = 1; y < marginVertexRowsInside.Count; y++)
        {
            for (int x = 0; x < bellMarginWidth; x++)
            {
                var v0 = marginVertexRowsInside[y - 1][x].ptr;
                var v1 = marginVertexRowsInside[y - 1][x + 1].ptr;
                var v2 = marginVertexRowsInside[y][x].ptr;
                var v3 = marginVertexRowsInside[y][x + 1].ptr;
                
                geometryInside.AddFace(v2, v1, v0);
                geometryInside.AddFace(v1, v2, v3);
            }
        }
    }
}