using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// 紫紋海刺型觸手實作 (繼承自 MedusaTentaclesBase)
/// </summary>
public class ChrysaoraTentacles : MedusaTentaclesBase
{
    [Header("Tentacle Settings")]
    public int tentacleNum = 20;
    public int tentacleLength = 20;
    public float tentacleRadius = 0.012f;
    public int tentacleRadialSegments = 6;
    
    [Header("Physics Settings")]
    public float springStrength = 0.1f;
    public float segmentLengthMin = 0.24f;
    public float segmentLengthMax = 0.30f;
    
    private Mesh _mesh;
    private Material _material;
    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _propBlock;

    // 定義與 C# 交互的 Vertex 結構
   [StructLayout(LayoutKind.Sequential)]
    public struct VertexData
    {
        public Vector3 position;
        public Vector3 normal; // 確保 Stride 正確 (28 bytes)
        public float isFixed;
        public float uvY;
    }

    /// <summary>
    /// 初始化模組，由 Medusa.cs 統一呼叫
    /// </summary>
    public override void Initialize(Medusa medusa)
    {
        this.owner = medusa;

        // 安全檢查：觸手依賴裙邊 (Margin) 的物理結點作為根部
        if (owner.bellMargin == null)
        {
            Debug.LogError("ChrysaoraTentacles: 找不到 BellMargin！請確認 Medusa.cs 中的生成順序。");
            return;
        }

        _material = owner.config.tentaclesMaterial;
        _propBlock = new MaterialPropertyBlock();
        CreateGeometry();
    }

    private void CreateGeometry()
    {
        //var bell = owner.bell;
        var physics = owner.physics;
        var bridge = owner.bridge;
        var medusaId = owner.medusaId;
        
        // 透過具名變數直接從裙邊組件抓取數據接口
        var bellMarginRows = owner.bellMargin.GetMarginRows();
        int bellMarginWidth = owner.bellMargin.GetMarginWidth();
        
        if (bellMarginRows == null || bellMarginRows.Count == 0) return;

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
        _mesh.name = "Chrysaora_Tentacles_Mesh";
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.SetVertices(vertices);
        _mesh.SetUVs(0, uv0);
        _mesh.SetUVs(1, uv1);
        _mesh.SetUVs(2, uv2); // ★ 別忘了設定 UV2
        _mesh.SetTriangles(triangles, 0);
        _mesh.RecalculateBounds();
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);

        var shader = Shader.Find("Shader Graphs/Tentacles");
        
        if (_material == null)
        {
            if (shader != null)
                _material = new Material(shader);
            else
                _material = new Material(Shader.Find("Standard"));
        }
        GameObject visualObj = new GameObject("Tentacle_Visual");
        visualObj.transform.SetParent(this.transform, false);
        visualObj.AddComponent<MeshFilter>().mesh = _mesh;
        _meshRenderer = visualObj.AddComponent<MeshRenderer>();
        _meshRenderer.material = _material;
    }

    /// <summary>
    /// 更新材質參數，由 Medusa.cs 的 Update 驅動
    /// </summary>
    public override void UpdateModule(float dt)
    {
        if (_meshRenderer != null && owner.physics.vertexBuffer != null)
        {
            _meshRenderer.GetPropertyBlock(_propBlock);
            
            // 傳遞 Compute Shader 物理緩衝區與 Charge (發光) 參數
            _propBlock.SetBuffer("_VertexData", owner.physics.vertexBuffer);
            _propBlock.SetFloat("_Charge", owner.charge);
            
            _meshRenderer.SetPropertyBlock(_propBlock);
        }
    }
    
    // 輔助函式：從 Bridge 中查找特定頂點的數據
    private MedusaVerletBridge.BridgeData FindBridgeData(int vertexId)
    {
        foreach (var data in owner.bridge.bridgeVertices)
        {
            if (data.vertexId == vertexId) return data;
        }
        Debug.LogError($"找不到 ID {vertexId} 的 BridgeData！");
        return new MedusaVerletBridge.BridgeData();
    }
}