using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public class MedusaTentacles : MonoBehaviour
{
    private Medusa _medusa;
    private Mesh _mesh;
    private Material _material;

    // 定義與 C# 交互的 Vertex 結構
   [StructLayout(LayoutKind.Sequential)]
    public struct VertexData
    {
        public Vector3 position;
        public Vector3 normal; // 確保 Stride 正確 (28 bytes)
        public float isFixed;
        public float uvY;
    }

    public void Initialize(Medusa medusa)
    {
        _medusa = medusa;
        CreatePhysicsAndGeometry();
    }

    // 輔助函式：從 Bridge 中查找特定頂點的數據
    private MedusaVerletBridge.BridgeData FindBridgeData(int vertexId)
    {
        foreach (var data in _medusa.bridge.bridgeVertices)
        {
            if (data.vertexId == vertexId) return data;
        }
        Debug.LogError($"找不到 ID {vertexId} 的 BridgeData！");
        return new MedusaVerletBridge.BridgeData();
    }

    private void CreatePhysicsAndGeometry()
    {
        var bell = _medusa.bell;
        var physics = _medusa.physics;
        var bridge = _medusa.bridge;
        var medusaId = _medusa.medusaId;

        if (bell.margin.bellMarginRows == null || bell.margin.bellMarginRows.Count == 0) return;

        var bellMarginRows = bell.margin.bellMarginRows;
        int bellMarginWidth = bell.margin.bellMarginWidth;

        int tentacleNum = 20;
        int tentacleLength = 30;
        float springStrength = 0.1f; 

        List<List<int>> tentaclesPhysicsIds = new List<List<int>>();

        // --- 1. Physics: 建立觸手物理點與彈簧 ---
        for (int x = 0; x < tentacleNum; x++)
        {
            List<int> tentacleIds = new List<int>();
            int colIndex = Mathf.FloorToInt(x * ((float)bellMarginWidth / tentacleNum));

            // 根部連接 (Roots)
            tentacleIds.Add(bellMarginRows[bellMarginRows.Count - 3][colIndex]); // i-2
            tentacleIds.Add(bellMarginRows[bellMarginRows.Count - 2][colIndex]); // i-1

            // Pivot (裙邊最底端)
            int pivotId = bellMarginRows[bellMarginRows.Count - 1][colIndex];
            tentacleIds.Add(pivotId);

            // 獲取 Pivot 的 BridgeData 以繼承座標系
            var pivotData = FindBridgeData(pivotId);
            
            float zenith = pivotData.zenith;
            float azimuth = pivotData.azimuth;
            Vector3 currentOffset = pivotData.offset; 

            for (int y = 3; y < tentacleLength; y++)
            {
                float segmentLength = 0.24f + Random.value * 0.06f;
                currentOffset.y -= segmentLength;
                
                float progress = (float)y / (float)tentacleLength;
                
                // 建立物理點
                int vertexId = physics.AddVertex(Vector3.zero, false, progress);

                // 註冊到 Bridge
                bridge.RegisterVertex(medusaId, vertexId, zenith, azimuth, false, currentOffset, 0, false);

                // 彈簧連接
                physics.AddSpring(tentacleIds[y - 1], vertexId, springStrength, 1.0f);

                if (y > 1) 
                {
                    physics.AddSpring(tentacleIds[y - 2], vertexId, springStrength, 1.0f);
                }

                tentacleIds.Add(vertexId);
            }
            tentaclesPhysicsIds.Add(tentacleIds);
        }

        // --- 2. Geometry: 建立渲染網格 ---
        List<Vector3> vertices = new List<Vector3>();
        
        // ★ 修改 UV 結構
        List<Vector2> uv0 = new List<Vector2>(); // x:Angle, y:Progress (用於漸層)
        List<Vector2> uv1 = new List<Vector2>(); // ID_A, ID_B
        List<Vector2> uv2 = new List<Vector2>(); // ★ 新增: x:Width (寬度搬到這裡)
        
        List<int> triangles = new List<int>();

        int tentacleRadialSegments = 6;
        float tentacleRadius = 0.015f;

        for (int i = 0; i < tentacleNum; i++)
        {
            List<List<int>> tentacleMeshRowIndices = new List<List<int>>();

            for (int y = 1; y < tentacleLength; y++)
            {
                List<int> row = new List<int>();
                
                float idA = (float)tentaclesPhysicsIds[i][y - 1];
                float idB = (float)tentaclesPhysicsIds[i][y];

                for (int r = 0; r < tentacleRadialSegments; r++)
                {
                    float angle = ((float)r / tentacleRadialSegments) * Mathf.PI * 2.0f;
                    
                    // ★ 計算進度 (0.0 = 根部, 1.0 = 尾端)
                    float progress = (float)y / (tentacleLength - 1);
                    
                    // 計算寬度
                    float width = (y == 1) ? 0f : Mathf.Sqrt(1.0f - progress) * tentacleRadius;

                    vertices.Add(Vector3.zero); 
                    
                    // ★ UV0.y 現在存的是 progress
                    uv0.Add(new Vector2(angle, progress));
                    
                    uv1.Add(new Vector2(idA, idB));
                    
                    // ★ UV2.x 存 width
                    uv2.Add(new Vector2(width, 0));

                    row.Add(vertices.Count - 1);
                }
                tentacleMeshRowIndices.Add(row);
            }

            // 構建三角形索引
            for (int y = 0; y < tentacleMeshRowIndices.Count - 1; y++)
            {
                var currentRow = tentacleMeshRowIndices[y];
                var nextRow = tentacleMeshRowIndices[y + 1];

                for (int x = 0; x < tentacleRadialSegments; x++)
                {
                    int v0 = currentRow[x];
                    int v1 = currentRow[(x + 1) % tentacleRadialSegments];
                    int v2 = nextRow[x];
                    int v3 = nextRow[(x + 1) % tentacleRadialSegments];

                    triangles.Add(v2); 
                    triangles.Add(v1); 
                    triangles.Add(v0);

                    triangles.Add(v1); 
                    triangles.Add(v2); 
                    triangles.Add(v3);
                }
            }
        }

        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uv0);
        _mesh.SetUVs(1, uv1);
        _mesh.SetUVs(2, uv2); // ★ 別忘了設定 UV2
        _mesh.SetTriangles(triangles, 0);
        _mesh.RecalculateBounds();
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);

        var shader = Shader.Find("Shader Graphs/Tentacles");
        
        if (shader != null)
            _material = new Material(shader);
        else
            _material = new Material(Shader.Find("Standard"));

        GameObject obj = new GameObject("Tentacles");
        obj.transform.SetParent(_medusa.transform, false);
        obj.AddComponent<MeshFilter>().mesh = _mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.material = _material;
    }

    void Update()
    {
        if (_material != null && _medusa.physics.vertexBuffer != null)
        {
            _material.SetBuffer("_VertexData", _medusa.physics.vertexBuffer);

            // ★ 新增：傳遞 Charge 讓觸手發光
            if (_medusa != null)
            {
                _material.SetFloat("_Charge", _medusa.charge);
            }
        }
    }
}