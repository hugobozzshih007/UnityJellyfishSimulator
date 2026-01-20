using System.Collections.Generic;
using UnityEngine;

// 對應 src/medusaBellGeometry.js
public class MedusaBellGeometry
{
    // 儲存生成的 Mesh 物件
    public Mesh mesh { get; private set; }
    
    // 緩衝區數據
    private List<Vector3> _positions = new List<Vector3>();
    private List<Vector2> _params_zenith_azimuth = new List<Vector2>(); // 原本的 _uv0
    private List<Vector4> _ids_physics = new List<Vector4>();    // 原本的 _uv1
    private List<Vector2> _tex_coords = new List<Vector2>();    // 原本的 _uv2 (真正的 UV)
    private List<Vector4> _tangents_side = new List<Vector4>(); // Side Data
    private List<int> _indices = new List<int>();

    public bool IsOuterSide { get; private set; }
    private Medusa _medusa;

    // 輔助結構
    public class VertexInfo
    {
        public int ptr; // 頂點索引
        public float zenith;
        public float azimuth;
        public Vector3 offset;
        public int id;
    }

    public MedusaBellGeometry(Medusa medusa, bool isOuterSide)
    {
        _medusa = medusa;
        IsOuterSide = isOuterSide;
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
        mesh.MarkDynamic();
    }

    public void Clear()
    {
        _positions.Clear();
        _params_zenith_azimuth.Clear();
        _ids_physics.Clear();
        _tex_coords.Clear();
        _tangents_side.Clear();
        _indices.Clear();
        mesh.Clear();
    }

    private int _AddVertex(float zenith, float azimuth, int v0, int v1, int v2, int v3, Vector3 side, float width)
    {
        int ptr = _positions.Count;

        // 計算極座標投影 UV (這是畫花紋的關鍵)
        float uvx = Mathf.Sin(azimuth) * zenith;
        float uvy = Mathf.Cos(azimuth) * zenith;

        _positions.Add(Vector3.zero); 
        
        // 1. 幾何參數 (Zenith, Azimuth) -> 稍後存入 UV1
        _params_zenith_azimuth.Add(new Vector2(zenith, azimuth));
        
        // 2. 物理 ID -> 稍後存入 UV2
        _ids_physics.Add(new Vector4(v0, v1, v2, v3));
        
        // 3. 紋理座標 -> 稍後存入 UV0 (★修正點)
        _tex_coords.Add(new Vector2(uvx, uvy));
        
        // 4. Side Data -> 存入 UV3
        _tangents_side.Add(new Vector4(side.x, side.y, side.z, width));

        return ptr;
    }

    private float GetAvgAngle(List<float> angles)
    {
        float x = 0, y = 0;
        foreach (var a in angles)
        {
            x += Mathf.Sin(a);
            y += Mathf.Cos(a);
        }
        return Mathf.Atan2(x, y);
    }

    public VertexInfo AddVertexFromParams(float zenith, float azimuth, Vector3 side = default, float width = 0)
    {
        if (side == default) side = new Vector3(0, 0, 1);
        int ptr = _AddVertex(zenith, azimuth, -1, -1, -1, -1, side, width);
        return new VertexInfo { ptr = ptr, zenith = zenith, azimuth = azimuth, id = -1 };
    }

    public VertexInfo AddVertexFromVertices(VertexInfo v0, VertexInfo v1, VertexInfo v2, VertexInfo v3, Vector3 side, float width)
    {
        float azimuth = GetAvgAngle(new List<float> { v0.azimuth, v1.azimuth, v2.azimuth, v3.azimuth });
        float zenith = (v0.zenith + v1.zenith + v2.zenith + v3.zenith) * 0.25f;
        zenith -= (v0.offset.y + v1.offset.y + v2.offset.y + v3.offset.y) * 0.25f;
        zenith += side.y * width;

        int ptr = _AddVertex(zenith, azimuth, v0.id, v1.id, v2.id, v3.id, side, width);
        return new VertexInfo { ptr = ptr, zenith = zenith, azimuth = azimuth };
    }

    public void AddFace(int v0, int v1, int v2)
    {
        _indices.Add(v0);
        _indices.Add(v1);
        _indices.Add(v2);
    }

    // ★★★ 關鍵修正：重新分配 UV 通道 ★★★
    public void BakeGeometry()
    {
        mesh.SetVertices(_positions);
        
        // Channel 0: 紋理座標 (uvx, uvy) -> 給 MedusaBellPattern 畫花紋
        mesh.SetUVs(0, _tex_coords);
        
        // Channel 1: 幾何參數 (Zenith, Azimuth) -> 給 Vertex Displacement 算位置
        mesh.SetUVs(1, _params_zenith_azimuth); 
        
        // Channel 2: 物理 ID (v0, v1, v2, v3) -> 給 Vertex Displacement 讀 Buffer
        mesh.SetUVs(2, _ids_physics); 
        
        // Channel 3: Side Data -> 給 Vertex Displacement 算厚度
        mesh.SetUVs(3, _tangents_side);
        
        mesh.SetTriangles(_indices, 0);
        mesh.RecalculateBounds();
        //mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 50f);
    }
}