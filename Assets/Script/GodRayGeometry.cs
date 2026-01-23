using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GodRayGeometry : MonoBehaviour
{
    void Start()
    {
        CreateMesh();
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "GodRay_Procedural";

        // 原作 circleResolution = 2，所以只有 2 個橫向節點 (0 和 1)
        // 加上上下兩排 (Top/Bottom)，總共 4 個頂點
        
        Vector3[] vertices = new Vector3[4];
        Vector2[] uv0 = new Vector2[4]; // 存 zenith, azimuth
        Vector2[] uv1 = new Vector2[4]; // 存 offset, 預留
        int[] triangles = new int[6];

        // --- 頂點數據生成 (還原 godrays.js buildGeometry) ---
        // Top Row (offset = 0)
        // v0
        vertices[0] = Vector3.zero; 
        uv0[0] = new Vector2(1.0f, 0.0f); // x=Zenith(1), y=Azimuth(0)
        uv1[0] = new Vector2(0.0f, 0.0f); // x=Offset(0)

        // v1
        vertices[1] = Vector3.zero;
        uv0[1] = new Vector2(1.0f, Mathf.PI); // x=Zenith(1), y=Azimuth(PI) -> 因為 x/(res-1) * PI
        uv1[1] = new Vector2(0.0f, 0.0f); // x=Offset(0)

        // Bottom Row (offset = 1)
        // v2
        vertices[2] = Vector3.zero;
        uv0[2] = new Vector2(1.0f, 0.0f); // x=Zenith(1), y=Azimuth(0)
        uv1[2] = new Vector2(1.0f, 0.0f); // x=Offset(1) -> 底部要被拉長

        // v3
        vertices[3] = Vector3.zero;
        uv0[3] = new Vector2(1.0f, Mathf.PI); // x=Zenith(1), y=Azimuth(PI)
        uv1[3] = new Vector2(1.0f, 0.0f); // x=Offset(1)

        // --- 三角形索引 ---
        // 順序: v2, v1, v0 (原作 indices.push(v2,v1,v0))
        triangles[0] = 2; triangles[1] = 1; triangles[2] = 0;
        // 順序: v1, v2, v3 (原作 indices.push(v1,v2,v3))
        triangles[3] = 1; triangles[4] = 2; triangles[5] = 3;

        mesh.vertices = vertices;
        mesh.uv = uv0; // UV0 傳遞核心參數
        mesh.uv2 = uv1; // UV2 傳遞 Offset
        mesh.triangles = triangles;
        
        // 設定 Bounds 很大，避免被視錐剔除 (因為我們會在 Shader 裡大幅移動頂點)
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        GetComponent<MeshFilter>().mesh = mesh;
    }
}