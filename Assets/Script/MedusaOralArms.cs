using UnityEngine;
using System.Collections.Generic;

public class MedusaOralArms : MonoBehaviour
{
    [Header("Randomness")]
    public int randomSeed = 0;
    
    [Header("Physics Settings")]
    [Tooltip("★ 抗拉伸硬度：控制管子長度。設高 (0.8~1.0) 就不會被拉長。")]
    [Range(0.1f, 1.0f)]
    public float stretchStiffness = 0.9f; 

    [Tooltip("根部柔軟度 (只影響彎曲)")]
    public float baseBendStiffness = 0.2f; 

    [Tooltip("尾部柔軟度 (只影響彎曲)")]
    public float tipBendStiffness = 0.0001f;
    
    [Header("Ruffle Growth")]
    [Tooltip("皺摺生長起始點")]
    [Range(0f, 1f)]
    public float ruffleStartPct = 0.0f;

    [Tooltip("皺摺結束點")]
    [Range(0f, 1f)]
    public float ruffleEndPct = 0.95f;
    
    [Header("Shape Profiling")]
    [Tooltip("皺摺最寬/最密集的位置 (0.3 = 在 30% 的地方最寬)")]
    [Range(0.1f, 0.9f)]
    public float rufflePeakPos = 0.2f;

    [Tooltip("根部皺摺比例")]
    [Range(0f, 1f)]
    public float rootRuffleScale = 0.01f; 

    [Tooltip("管子變細的起始點")]
    public float tubeThinStart = 0f;

    [Tooltip("變細後的管子比例")]
    public float tubeThinScale = 0.1f;

    [Header("Twist Settings")]
    public float totalTwist = 900.0f;
    
    private Medusa medusa;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    // --- 參數設定 ---
    [Header("Structure Settings")]
    public float armLength = 8.0f; 
    public int physicsNodes = 50; 
    
    [Range(0f, 1f)]
    public float attachmentZenith = 0.2f; // 掛載高度
    public float rootDistributionRadius = 0.4f;

    [Header("Tube Appearance")]
    public float tubeThickness = 0.2f; 

    [Header("Mesh Density")]
    public int visualSegmentsPerNode = 8; 
    
    const int armsNum = 4;        
    const int finsPerArm = 4;     

    [Header("Ruffle Shape")]
    public float ruffleWidth = 2.2f;  
    public float ruffleFoldAmt = 2.5f;  
    public float noiseFreq = .65f;      

    public void Initialize(Medusa medusa)
    {
        this.medusa = medusa;
        CreateGeometry();
    }

    void CreateGeometry()
    {
        VerletPhysics physics = medusa.physics;
        MedusaVerletBridge bridge = medusa.bridge;
        
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>(); 
        List<Vector2> uvs0 = new List<Vector2>(); 
        List<Vector2> uvs1 = new List<Vector2>(); 
        List<Vector2> uvs2 = new List<Vector2>(); 
        List<int> triangles = new List<int>();

        Random.InitState(randomSeed);

        // 儲存物理 ID 供 Phase 2 使用
        List<List<int>> allArmsPhysicsIds = new List<List<int>>();

        // =================================================================================
        // PHASE 1: PHYSICS GENERATION (Tube Style Anchoring)
        // =================================================================================
        for (int i = 0; i < armsNum; i++)
        {
            float lengthScale = Random.Range(0.75f, 1.5f);
            float currentArmLength = armLength * lengthScale;
            float currentNodeSpacing = currentArmLength / (float)physicsNodes;

            List<int> currentArmIds = new List<int>();
            
            // 計算這條手臂的方位角
            float azimuth = (i / (float)armsNum) * Mathf.PI * 2.0f;
            
            // 初始 Offset (相對於 attachmentZenith/Azimuth 的偏移)
            // 這裡我們讓它從 0 開始，這樣根部就會準確吸在 attachmentZenith 的位置
            // 如果您希望四條分開一點，可以調整 rootDistributionRadius 影響 offset.x/z
            // 但因為 RegisterVertex 已經用了 Azimuth，Offset 主要是用來做 "垂下" 的動作
            Vector3 currentOffset = Vector3.zero;
            
            // 如果想要用 rootDistributionRadius 讓根部稍微擴開，可以加在這裡：
            // currentOffset += new Vector3(Mathf.Sin(azimuth)*0.01f, 0, Mathf.Cos(azimuth)*0.01f);
            // 但通常直接靠 Bridge 的 Azimuth 參數就夠了。

            for (int p = 0; p < physicsNodes; p++)
            {
                // ★ 關鍵回歸：根部 (p=0) 強制固定
                bool isFixed = (p == 0);
                
                float t = p / (float)physicsNodes; 
                // 建立物理點 (初始位置隨便給，反正下一幀 Bridge 會覆寫)
                int id = physics.AddVertex(medusa.transform.position, isFixed, t);
                currentArmIds.Add(id);

                // ★ Tube Style 註冊：所有點都註冊，讓 Bridge 管理它們的相對座標系
                // p=0 時，isFixed=true，Bridge 會把它鎖死在 (Zenith, Azimuth)
                // p>0 時，isFixed=false，Bridge 會用這個 Offset 當作初始參考，或單純提供座標系
                bridge.RegisterVertex(
                    medusa.medusaId, 
                    id, 
                    attachmentZenith,   // 掛載高度
                    azimuth,            // 方位角
                    true,               // isBottom (參考 Tube，通常設 true 表示在傘下)
                    currentOffset,      // 相對偏移 (這很重要，決定了它是一長條)
                    0f,                 // directionalOffset
                    isFixed             // 只有根部固定
                );

                // --- Profile 計算 (軟硬度) ---
                
                float currentTubeThinStart = Mathf.Max(tubeThinStart, 0.12f);
                float currentScale = 1.0f;
                if (t >= currentTubeThinStart) {
                    float transitionLen = 0.15f; 
                    float transitionT = Mathf.Clamp01((t - currentTubeThinStart) / transitionLen);
                    currentScale = Mathf.SmoothStep(1.0f, tubeThinScale, transitionT);
                }
                float thicknessFactor = (Mathf.Abs(1.0f - tubeThinScale) > 0.001f) ? (currentScale - tubeThinScale) / (1.0f - tubeThinScale) : 1.0f;

                float structuralStiffness = stretchStiffness; 
                float bendingStiffness = Mathf.Lerp(tipBendStiffness, baseBendStiffness, thicknessFactor);

                // --- 彈簧連接 ---
                if (p > 0) physics.AddSpring(id, currentArmIds[p-1], structuralStiffness, 1.0f);
                if (p > 1) physics.AddSpring(id, currentArmIds[p-2], bendingStiffness, 1.0f);

                // 準備下一個點的 Offset (向下長)
                currentOffset.y -= currentNodeSpacing;
            }
            allArmsPhysicsIds.Add(currentArmIds);
        }

        // =================================================================================
        // PHASE 2: VISUAL MESH GENERATION (視覺生成 - 保持不變)
        // =================================================================================
        Random.InitState(randomSeed);

        for (int i = 0; i < armsNum; i++)
        {
            float lengthScale = Random.Range(0.9f, 1.2f);
            float currentArmLength = armLength * lengthScale;
            float currentNodeSpacing = currentArmLength / (float)physicsNodes;

            float baseStart = ruffleStartPct;
            if (baseStart < 0.01f) baseStart = 0f;
            float localStartPct = Mathf.Clamp01(baseStart + (baseStart > 0.01f ? Random.Range(-0.01f, 0.02f) : 0f));
            float localEndPct = Mathf.Clamp01(ruffleEndPct + Random.Range(-0.02f, 0.02f));
            float armSeed = i * 13.5f + randomSeed * 7.1f;
            float twistScale = Random.Range(0.8f, 1.2f); 

            List<int> boneIds = allArmsPhysicsIds[i];

            int totalVisualLayers = (physicsNodes - 1) * visualSegmentsPerNode + 1;
            float[] ruffleDistAccumulator = new float[finsPerArm]; 
            Vector3[] prevRufflePos = new Vector3[finsPerArm];

            for (int v = 0; v < totalVisualLayers; v++)
            {
                float progress = v / (float)(totalVisualLayers - 1); 

                float t = v / (float)visualSegmentsPerNode;
                int pIndex = Mathf.FloorToInt(t);
                float lerpT = t - pIndex;
                if (pIndex >= physicsNodes - 1) { pIndex = physicsNodes - 2; lerpT = 1.0f; }
                
                Vector2 bindingData = new Vector2(boneIds[pIndex], lerpT);

                float currentY = -v * (currentNodeSpacing / visualSegmentsPerNode);
                Vector3 centerPos = new Vector3(0, currentY, 0);

                // --- Shape Profile ---
                float currentTubeThinStart = Mathf.Max(tubeThinStart, 0.12f); 
                float tubeScale;
                if (progress < currentTubeThinStart) tubeScale = 1.0f; 
                else {
                    float transitionLen = 0.15f; 
                    float transitionT = Mathf.Clamp01((progress - currentTubeThinStart) / transitionLen);
                    tubeScale = Mathf.SmoothStep(1.0f, tubeThinScale, transitionT);
                }

                float widthProfile;
                if (progress < rufflePeakPos) {
                    float growT = Mathf.SmoothStep(0f, 1f, progress / rufflePeakPos);
                    widthProfile = Mathf.Lerp(rootRuffleScale, 1f, growT);
                } else {
                    float tailT = (progress - rufflePeakPos) / (1.0f - rufflePeakPos);
                    float curveT = Mathf.Pow(tailT, 3.0f); 
                    widthProfile = Mathf.Lerp(1.0f, 0.2f, curveT); 
                }

                float startMask = 1.0f;
                if (localStartPct > 0.001f) startMask = Mathf.SmoothStep(localStartPct, localStartPct + 0.05f, progress);
                float endMask = 1.0f - Mathf.SmoothStep(localEndPct - 0.1f, localEndPct, progress);
                float ruffleIntensity = startMask * endMask * widthProfile;

                // Noise
                float currentTwistRad = progress * totalTwist * twistScale * Mathf.Deg2Rad;
                float primaryWave = Mathf.Sin(v * noiseFreq * 0.5f + armSeed) * 0.5f; 
                float secondaryNoise = (Mathf.PerlinNoise(armSeed, v * noiseFreq * 0.2f) - 0.5f) * 2.0f; 
                float gapMultiplier = Mathf.Max(0.5f + primaryWave + secondaryNoise, 0.6f); 

                for (int f = 0; f < finsPerArm; f++)
                {
                    float baseAngle = (f / (float)finsPerArm) * Mathf.PI * 2.0f;
                    float twistedAngle = baseAngle + currentTwistRad;
                    Vector3 dir = new Vector3(Mathf.Cos(twistedAngle), 0, Mathf.Sin(twistedAngle));

                    // A. Tube
                    float currentTubeR = tubeThickness * tubeScale;
                    Vector3 tubePos = centerPos + dir * currentTubeR;
                    vertices.Add(tubePos);
                    normals.Add(dir);
                    uvs0.Add(new Vector2(0, progress)); 
                    uvs1.Add(bindingData);
                    uvs2.Add(new Vector2(0, -currentY)); 

                    // B. Ruffle
                    float waveNoise = Mathf.PerlinNoise(armSeed + f * 10, v * noiseFreq) - 0.5f;
                    float expansion = ruffleWidth * ruffleIntensity * gapMultiplier * (1.0f + waveNoise);
                    float currentRuffleR = currentTubeR + expansion;

                    float localWobble = Mathf.Sin(v * noiseFreq * 0.5f + f) * 0.2f; 
                    float cosT = Mathf.Cos(localWobble); float sinT = Mathf.Sin(localWobble);
                    Vector3 ruffleDir = dir;
                    floatRxz(ref ruffleDir, cosT, sinT);

                    Vector3 rufflePos = centerPos + ruffleDir * currentRuffleR;
                    vertices.Add(rufflePos);
                    
                    Vector3 distinctNormal = Vector3.Lerp(ruffleDir, Vector3.up, 0.15f).normalized;
                    float normalBlend = Mathf.Clamp01(ruffleIntensity * 5.0f); 
                    Vector3 finalRuffleNormal = Vector3.Slerp(dir, distinctNormal, normalBlend);
                    normals.Add(finalRuffleNormal); 
                    
                    if (v > 0) {
                        float dist = Vector3.Distance(prevRufflePos[f], rufflePos);
                        ruffleDistAccumulator[f] += dist;
                    }
                    prevRufflePos[f] = rufflePos;

                    uvs0.Add(new Vector2(1, progress)); 
                    uvs1.Add(bindingData);
                    uvs2.Add(new Vector2(1, ruffleDistAccumulator[f]));

                    // C. Triangles
                    if (v > 0)
                    {
                        int vertsPerLayer = finsPerArm * 2;
                        int currentBase = vertices.Count - 2;
                        int prevBase = currentBase - vertsPerLayer;
                        int currTube = currentBase; int currRuffle = currentBase + 1;
                        int prevTube = prevBase; int prevRuffle = prevBase + 1;
                        int nextOffset = 2;
                        if (f == finsPerArm - 1) nextOffset = - (finsPerArm - 1) * 2;
                        int currNextTube = currTube + nextOffset;
                        int prevNextTube = prevTube + nextOffset;

                        triangles.Add(prevTube); triangles.Add(prevNextTube); triangles.Add(currTube);
                        triangles.Add(prevNextTube); triangles.Add(currNextTube); triangles.Add(currTube);
                        triangles.Add(prevTube); triangles.Add(prevRuffle); triangles.Add(currTube);
                        triangles.Add(prevRuffle); triangles.Add(currRuffle); triangles.Add(currTube);
                        triangles.Add(prevTube); triangles.Add(currTube); triangles.Add(prevRuffle);
                        triangles.Add(prevRuffle); triangles.Add(currTube); triangles.Add(currRuffle);
                    }
                }
            }
        }
        
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.SetUVs(0, uvs0);
        mesh.SetUVs(1, uvs1);
        mesh.SetUVs(2, uvs2); 
        mesh.SetIndices(triangles.ToArray(), MeshTopology.Triangles, 0);
        mesh.RecalculateTangents();
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        if (meshRenderer == null) {
            GameObject obj = new GameObject("OralArms_Skinned");
            obj.transform.SetParent(medusa.transform, false);
            meshRenderer = obj.AddComponent<MeshRenderer>();
            var mf = obj.AddComponent<MeshFilter>();
            mf.mesh = mesh;
        }
        
        Shader shader = Shader.Find("Shader Graphs/OralArms"); 
        if (shader != null) meshRenderer.material = new Material(shader);
        
        propBlock = new MaterialPropertyBlock();
    }

    void floatRxz(ref Vector3 v, float cosA, float sinA) {
        float x = v.x * cosA - v.z * sinA;
        float z = v.x * sinA + v.z * cosA;
        v.x = x; v.z = z;
    }
    
    void Update()
    {
        if (meshRenderer != null && medusa.physics != null && medusa.physics.vertexBuffer != null)
        {
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetBuffer("_PhysicsBuffer", medusa.physics.vertexBuffer);
            propBlock.SetFloat("_Charge", medusa.charge);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}