using UnityEngine;
using System.Collections.Generic;

public class MedusaOralArmsTube : MonoBehaviour
{
    private Medusa medusa;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    const int armsNum = 4;
    const int armsLength = 35;
    const int armsWidth = 5;

    public void Initialize(Medusa medusa)
    {
        this.medusa = medusa;
        CreateGeometry();
    }

    void CreateGeometry()
    {
        VerletPhysics physics = medusa.physics;
        MedusaVerletBridge bridge = medusa.bridge;
        
        // ---------------------------------------------------------
        // 1. 生成物理結構 (Physics Nodes & Springs)
        // ---------------------------------------------------------
        List<List<List<int>>> arms = new List<List<List<int>>>(); 
        float springStrength = 0.005f;

        for (int i = 0; i < armsNum; i++)
        {
            var arm = new List<List<int>>();
            
            // JS: const azimuth = (i / armsNum) * Math.PI * 2;
            float azimuth = (i / (float)armsNum) * Mathf.PI * 2.0f;
            
            // JS: const offset = new THREE.Vector3(0, 0.05, 0);
            Vector3 offset = new Vector3(0, 0.05f, 0);

            for (int y = 0; y < armsLength; y++)
            {
                var armRow = new List<int>();
                for (int x = 0; x < armsWidth; x++)
                {
                    // JS: offset.x = (Math.random() - 0.5) * 0.1;
                    offset.x = (Random.value - 0.5f) * 0.1f;
                    
                    // JS: const zenith = 0.2 + ((x / (armsWidth - 1) - 0.5) * 2) * (0.05 + (1.0 - y / armsLength) * 0.1);
                    float xRatio = x / (float)(armsWidth - 1);
                    float yRatio = y / (float)armsLength;
                    float zenith = 0.2f + ((xRatio - 0.5f) * 2.0f) * (0.05f + (1.0f - yRatio) * 0.1f);

                    // 創建物理節點
                    // JS: const vertex = physics.addVertex(new THREE.Vector3(), y === 0);
                    // 我們給它一個初始位置 pos (雖然 Bridge 會馬上覆寫它，但給個合理初值是好的)
                    Vector3 initialPos = medusa.transform.position; // 簡單給個原點，反正下一幀就被覆寫了
                    bool isFixed = (y == 0);
                    int nodeId = physics.AddVertex(initialPos, isFixed);
                    
                    // ★★★ 關鍵修正：將節點註冊到 Bridge ★★★
                    // 這一步告訴 Compute Shader：這個點是黏在水母身體上的 (根據 zenith/azimuth)
                    // 如果 isFixed=true (第一排)，CSMain_UpdatePositions 會強制更新它的位置跟隨水母
                    // JS: bridge.registerVertex(medusaId, vertex, zenith, azimuth, true, offset.clone(), 0, y === 0);
                    bridge.RegisterVertex(
                        medusa.medusaId, 
                        nodeId, 
                        zenith, 
                        azimuth, 
                        true,           // isBottom (JS傳的是 true)
                        offset,         // offset
                        0f,             // directionalOffset
                        isFixed         // isFixed (y==0)
                    );

                    armRow.Add(nodeId);
                }
                
                // JS: offset.y -= 0.1 * (1.0 + Math.random() * 0.5);
                offset.y -= 0.1f * (1.0f + Random.value * 0.5f);
                
                arm.Add(armRow);
            }
            arms.Add(arm);

            // Springs (這部分邏輯保持不變，負責觸手內部的物理連結)
            for (int y = 1; y < armsLength; y++)
            {
                for (int x = 0; x < armsWidth; x++)
                {
                    int v0 = arm[y][x];
                    int v1 = arm[y - 1][x];
                    physics.AddSpring(v0, v1, springStrength, 1.0f);
                    if (x > 0) physics.AddSpring(v0, arm[y][x - 1], springStrength, 1.0f);
                    if (x > 1) physics.AddSpring(v0, arm[y][x - 2], springStrength, 1.0f);
                    if (y > 1) physics.AddSpring(v0, arm[y - 2][x], springStrength, 1.0f);
                    if (y > 5 && (y - 3) % 5 == 0) physics.AddSpring(v0, arm[y - 5][x], springStrength * 0.2f, 0.3f + Random.value * 0.2f);
                }
            }
        }

        // ---------------------------------------------------------
        // 2. 生成視覺網格 (Visual Mesh) - 保持上次修正後的正確邏輯
        // ---------------------------------------------------------
        List<Vector3> visualVertices = new List<Vector3>(); 
        List<Vector2> uvs0_Texture = new List<Vector2>(); // UV0
        List<Vector2> uvs1_Data = new List<Vector2>();    // UV1 (ID)
        List<int> triangles = new List<int>();

        Vector3 outerSide = new Vector3(0, 0, 1);
        Vector3 innerSide = new Vector3(0, 0, -1);
        Vector3 rightSide = new Vector3(1, 0, 0);
        Vector3 leftSide = new Vector3(-1, 0, 0);

        int AddArmsVertex(int v0, int v1, int v2, int v3, Vector3 side, float width, float uvx, float uvy)
        {
            int visualIndex = physics.AddVertex(Vector3.zero, true); 
            bridge.RegisterOralArmSegment(v0, v1, v2, v3, side, width, visualIndex);
            
            visualVertices.Add(Vector3.zero); 
            uvs0_Texture.Add(new Vector2(uvx, uvy));      
            uvs1_Data.Add(new Vector2(visualIndex, 0));   
            
            return visualVertices.Count - 1; 
        }

        for (int i = 0; i < armsNum; i++)
        {
            var arm = arms[i];
            List<List<int>> armVertexRows = new List<List<int>>();

            for (int y = 1; y < armsLength; y++)
            {
                float width = (y == armsLength - 1) ? 0.0f : (0.02f + (1.0f - y / (float)armsLength) * 0.02f);
                width *= 2.0f; 

                List<int> armVertexRow = new List<int>(); 
                List<int> backSideRow = new List<int>();  

                for (int x = 0; x < armsWidth - 1; x++)
                {
                    int v0 = arm[y - 1][x]; int v1 = arm[y - 1][x + 1];
                    int v2 = arm[y][x];     int v3 = arm[y][x + 1];
                    float uvY = y * 0.05f;
                    // ★ 新算法：根據 x 的位置計算圓弧角度 (從 -90度 到 +90度)
                    // 讓法線像圓管一樣平滑過渡
                    float t = x / (float)(armsWidth - 2); 
                    float angleRad = Mathf.Lerp(-Mathf.PI * 0.4f, Mathf.PI * 0.4f, t); 
                    
                    // 用圓弧計算 Side Data
                    // x分量 (左右傾斜) = sin(angle)
                    // z分量 (正面強度) = cos(angle)
                    Vector3 smoothSide = new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad));
                    if (x == 0) {
                        armVertexRow.Add(AddArmsVertex(v0, v1, v2, v3, leftSide, width, 0, uvY));
                    }
                    {
                        float uvX = 0.1f + x * 0.1f;
                        armVertexRow.Add(AddArmsVertex(v0, v1, v2, v3, smoothSide, width, uvX, uvY));
                        // 背面：z 軸反轉 (讓背面的法線指向後面)
                        Vector3 backSmoothSide = new Vector3(smoothSide.x, 0, -smoothSide.z);
                        backSideRow.Add(AddArmsVertex(v0, v1, v2, v3, innerSide, width, uvX, uvY));
                    }
                    if (x == armsWidth - 2) {
                        float uvX = 0.2f + x * 0.1f;
                        armVertexRow.Add(AddArmsVertex(v0, v1, v2, v3, rightSide, width, uvX, uvY));
                    }
                }

                backSideRow.Reverse();
                armVertexRow.AddRange(backSideRow);
                armVertexRow.Add(armVertexRow[0]);
                armVertexRows.Add(armVertexRow);
            }

            for (int y = 1; y < armVertexRows.Count; y++)
            {
                List<int> prevRow = armVertexRows[y - 1];
                List<int> currRow = armVertexRows[y];
                int loopLength = Mathf.Min(prevRow.Count, currRow.Count);

                for (int k = 0; k < loopLength - 1; k++)
                {
                    int v0 = prevRow[k]; int v1 = prevRow[k + 1];
                    int v2 = currRow[k]; int v3 = currRow[k + 1];
                    triangles.Add(v2); triangles.Add(v1); triangles.Add(v0);
                    triangles.Add(v1); triangles.Add(v2); triangles.Add(v3);
                }
            }
        }

        // 3. 建立 Mesh
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
        mesh.vertices = visualVertices.ToArray();
        mesh.SetUVs(0, uvs0_Texture); 
        mesh.SetUVs(1, uvs1_Data);    
        mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
        
        float totalLength = armsLength * 1.5f; 
        float totalWidth = 25.0f; 
        Vector3 boundsCenter = new Vector3(0, -totalLength / 2.0f, 0);
        Vector3 boundsSize = new Vector3(totalWidth, totalLength, totalWidth);
        mesh.bounds = new Bounds(boundsCenter, boundsSize);

        // 4. 設定 Renderer
        GameObject obj = new GameObject("OralArms");
        obj.transform.SetParent(medusa.transform, false);
        var mf = obj.AddComponent<MeshFilter>();
        meshRenderer = obj.AddComponent<MeshRenderer>();
        mf.mesh = mesh;

        Shader shader = Shader.Find("Shader Graphs/OralArms"); 
        if (shader != null) {
            meshRenderer.material = new Material(shader);
        } else {
            meshRenderer.material = new Material(Shader.Find("Standard")); 
        }

        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (meshRenderer != null && medusa.physics != null && medusa.physics.vertexBuffer != null)
        {
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetBuffer("_PhysicsBuffer", medusa.physics.vertexBuffer);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}